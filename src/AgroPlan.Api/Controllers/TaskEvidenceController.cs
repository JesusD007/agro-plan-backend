using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AgroPlan.Api.Authorization;
using AgroPlan.Application.Abstractions;
using AgroPlan.Application.Workforce;
using AgroPlan.Domain.Workforce;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/farms/{farmId:guid}/work-tasks/{taskId:guid}/evidence")]
public sealed class TaskEvidenceController(AgroPlanDbContext db, IFileStorage storage,
    AssignedWorkerAuthorizationService authorization, TaskEvidenceFinalizationService finalization) : ControllerBase
{
    private static readonly HashSet<string> AllowedTypes = ["image/jpeg", "image/png", "image/webp"];

    [HttpPost("uploads")]
    public async Task<ActionResult<TaskEvidenceUploadResponse>> CreateUpload(Guid farmId, Guid taskId,
        CreateTaskEvidenceUploadRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        if (!await authorization.IsAssignedWorkerAsync(farmId, taskId, userId, ct)) return NotFound();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (!AllowedTypes.Contains(request.ContentType) || request.SizeBytes is <= 0 or > 10 * 1024 * 1024)
            return BadRequest(new { message = "Only JPEG, PNG or WebP evidence up to 10 MiB is accepted." });

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM agro.work_tasks WHERE \"FarmId\" = {farmId} AND \"Id\" = {taskId} FOR UPDATE", ct);
        var task = await db.WorkTasks.SingleOrDefaultAsync(row => row.FarmId == farmId && row.Id == taskId, ct);
        if (task is null) return NotFound();
        if (task.AssignedWorkerUserId != userId || !await db.FarmMemberships.AnyAsync(member => member.FarmId == farmId &&
            member.UserId == userId && member.IsActive && member.Role == AgroPlan.Domain.Farms.FarmRole.Worker, ct))
            return NotFound();
        if (task.Status is not (WorkTaskStatus.InProgress or WorkTaskStatus.CorrectionRequested))
            return Conflict(new { message = "The task must be in progress or awaiting correction before evidence can be uploaded." });

        var existing = await db.TaskEvidenceSubmissions.SingleOrDefaultAsync(row => row.WorkTaskId == taskId &&
            row.ClientOperationId == request.ClientOperationId, ct);
        if (existing is not null)
        {
            if (existing.WorkerUserId != userId || existing.ExpectedSizeBytes != request.SizeBytes || existing.ContentType != request.ContentType)
                return Conflict(new { message = "The client operation id was already used with different upload data." });
            if (existing.FinalizedAt.HasValue) return Conflict(new { message = "This operation already has finalized evidence." });
            if (DateTimeOffset.UtcNow > existing.ExpiresAt) return Conflict(new { message = "This upload authorization expired; retry with a new client operation id." });
            var retryAuthorization = storage.CreateTaskEvidenceUploadAuthorization(farmId, taskId, existing.Id,
                existing.ContentType, existing.ExpiresAt);
            return Ok(new TaskEvidenceUploadResponse(existing.Id, existing.Revision, existing.UploadObjectKey,
                retryAuthorization.UploadUrl, existing.ExpiresAt, existing.ContentType, existing.ExpectedSizeBytes));
        }

        var previous = await db.TaskEvidenceSubmissions.Where(row => row.WorkTaskId == taskId)
            .OrderByDescending(row => row.Revision).FirstOrDefaultAsync(ct);
        var revision = (previous?.Revision ?? 0) + 1;
        var now = DateTimeOffset.UtcNow;
        var submissionId = Guid.NewGuid();
        var expiresAt = now.AddMinutes(10);
        UploadAuthorization upload;
        try
        {
            upload = storage.CreateTaskEvidenceUploadAuthorization(farmId, taskId, submissionId, request.ContentType, expiresAt);
            var submission = new TaskEvidenceSubmission(submissionId, farmId, taskId, userId, revision,
                previous?.Id, request.ClientOperationId, upload.ObjectKey, request.ContentType, request.SizeBytes,
                request.ConsentLocation, request.Location, request.ConsentCapturedAt, request.CapturedAt, now, expiresAt);
            db.TaskEvidenceSubmissions.Add(submission);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return CreatedAtAction(nameof(CreateUpload), new { farmId, taskId }, new TaskEvidenceUploadResponse(
                submission.Id, revision, upload.ObjectKey, upload.UploadUrl, expiresAt, request.ContentType, request.SizeBytes));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("{submissionId:guid}/finalize")]
    public async Task<IActionResult> Finalize(Guid farmId, Guid taskId, Guid submissionId, CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var submission = await finalization.FinalizeAsync(farmId, taskId, submissionId, userId, ct);
            return submission is null ? NotFound() : Ok(new TaskEvidenceResponse(submission.Id, submission.Revision,
                submission.PreviousSubmissionId, submission.ContentType, submission.SizeBytes, submission.Location,
                submission.CapturedAt, submission.FinalizedAt));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("submit")]
    public async Task<IActionResult> Submit(Guid farmId, Guid taskId, CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        if (!await authorization.IsAssignedWorkerAsync(farmId, taskId, userId, ct)) return NotFound();
        var task = await db.WorkTasks.SingleOrDefaultAsync(row => row.FarmId == farmId && row.Id == taskId, ct);
        if (task is null) return NotFound();
        var latestEvidence = await db.TaskEvidenceSubmissions.Where(row => row.FarmId == farmId && row.WorkTaskId == taskId &&
            row.WorkerUserId == userId && row.FinalizedAt != null).OrderByDescending(row => row.Revision).FirstOrDefaultAsync(ct);
        if (task.PhotoRequired && latestEvidence is null)
            return BadRequest(new { message = "A finalized photo is required for this task." });
        if (task.Status == WorkTaskStatus.CorrectionRequested)
        {
            var correctedSubmissionId = await db.TaskReviewDecisions.Where(row => row.FarmId == farmId &&
                row.WorkTaskId == taskId && row.Action == TaskReviewAction.CorrectionRequested)
                .OrderByDescending(row => row.DecidedAt).Select(row => row.SubmissionId).FirstOrDefaultAsync(ct);
            if (correctedSubmissionId.HasValue)
            {
                var correctedRevision = await db.TaskEvidenceSubmissions.Where(row => row.FarmId == farmId &&
                    row.WorkTaskId == taskId && row.Id == correctedSubmissionId.Value)
                    .Select(row => (int?)row.Revision).SingleOrDefaultAsync(ct);
                if (correctedRevision.HasValue && (latestEvidence is null || latestEvidence.Revision <= correctedRevision.Value))
                    return BadRequest(new { message = "A new evidence revision is required to resubmit this correction." });
            }
        }
        try { task.Submit(DateTimeOffset.UtcNow); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
        await db.SaveChangesAsync(ct);
        return Ok(new { task.Id, Status = task.Status.ToString() });
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TaskEvidenceResponse>>> History(Guid farmId, Guid taskId, CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        var role = await authorization.GetActiveRoleAsync(farmId, userId, ct);
        if (role is null) return Forbid();
        if (role == AgroPlan.Domain.Farms.FarmRole.Worker &&
            !await authorization.IsAssignedWorkerAsync(farmId, taskId, userId, ct)) return NotFound();
        if (role is not (AgroPlan.Domain.Farms.FarmRole.Owner or AgroPlan.Domain.Farms.FarmRole.Administrator or AgroPlan.Domain.Farms.FarmRole.Worker))
            return Forbid();
        var submissions = await db.TaskEvidenceSubmissions.AsNoTracking().Where(row => row.FarmId == farmId && row.WorkTaskId == taskId)
            .OrderBy(row => row.Revision).Select(row => new TaskEvidenceResponse(row.Id, row.Revision,
                row.PreviousSubmissionId, row.ContentType, row.SizeBytes, row.Location, row.CapturedAt, row.FinalizedAt)).ToArrayAsync(ct);
        return Ok(submissions);
    }

    private string? CurrentUserId() => User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
}

public sealed record CreateTaskEvidenceUploadRequest(
    Guid ClientOperationId,
    [Required, StringLength(64)] string ContentType,
    [Range(1, 10485760)] long SizeBytes,
    bool ConsentLocation,
    [StringLength(256)] string? Location,
    bool ConsentCapturedAt,
    DateTimeOffset? CapturedAt);

public sealed record TaskEvidenceUploadResponse(Guid SubmissionId, int Revision, string ObjectKey,
    string UploadUrl, DateTimeOffset ExpiresAt, string ContentType, long ExpectedSizeBytes);

public sealed record TaskEvidenceResponse(Guid SubmissionId, int Revision, Guid? PreviousSubmissionId,
    string ContentType, long? SizeBytes, string? Location, DateTimeOffset? CapturedAt, DateTimeOffset? FinalizedAt);
