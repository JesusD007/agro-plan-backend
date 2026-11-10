using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using AgroPlan.Api.Authorization;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Planning;
using AgroPlan.Domain.Workforce;
using AgroPlan.Infrastructure.Messaging.Events;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/farms/{farmId:guid}/work-tasks/{taskId:guid}")]
public sealed class TaskReviewsController(AgroPlanDbContext db, AssignedWorkerAuthorizationService authorization) : ControllerBase
{
    [HttpPost("review")]
    public async Task<IActionResult> Review(Guid farmId, Guid taskId, ReviewTaskRequest request, CancellationToken ct)
    {
        var reviewerId = CurrentUserId();
        if (reviewerId is null) return Unauthorized();
        var role = await authorization.GetActiveRoleAsync(farmId, reviewerId, ct);
        if (role is not (FarmRole.Owner or FarmRole.Administrator)) return Forbid();
        if (!ModelState.IsValid || !Enum.IsDefined(request.Action)) return ValidationProblem(ModelState);
        if (request.Action != TaskReviewAction.Verified && string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { message = "A reason is required when rejecting or requesting correction." });

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM agro.work_tasks WHERE \"FarmId\" = {farmId} AND \"Id\" = {taskId} FOR UPDATE", ct);
        var task = await db.WorkTasks.SingleOrDefaultAsync(item => item.FarmId == farmId && item.Id == taskId, ct);
        if (task is null) return NotFound();
        if (task.Status != WorkTaskStatus.Submitted) return Conflict(new { message = "Only submitted work can be reviewed." });
        var latestSubmission = await db.TaskEvidenceSubmissions.Where(item => item.FarmId == farmId &&
            item.WorkTaskId == taskId && item.FinalizedAt != null).OrderByDescending(item => item.Revision).FirstOrDefaultAsync(ct);
        if (task.PhotoRequired && latestSubmission is null)
            return Conflict(new { message = "Required finalized evidence is missing." });

        var now = DateTimeOffset.UtcNow;
        try
        {
            switch (request.Action)
            {
                case TaskReviewAction.Verified:
                    task.Verify(now);
                    break;
                case TaskReviewAction.Rejected:
                    task.Reject();
                    break;
                case TaskReviewAction.CorrectionRequested:
                    task.RequestCorrection();
                    break;
                default:
                    return BadRequest(new { message = "Unsupported review action." });
            }

            var decision = new TaskReviewDecision(Guid.NewGuid(), farmId, taskId, latestSubmission?.Id,
                reviewerId, request.Action, request.Action == TaskReviewAction.Verified ? null : request.Reason, now);
            db.TaskReviewDecisions.Add(decision);
            if (request.Action == TaskReviewAction.Verified)
            {
                if (task.WageAmount.HasValue && task.WageUnit is not null)
                    db.VerifiedWageEntries.Add(new VerifiedWageEntry(Guid.NewGuid(), farmId, task.Id,
                        task.AssignedWorkerUserId, task.WageAmount.Value, task.WageUnit, now));
                db.OutboxMessages.Add(new OutboxMessage(Guid.NewGuid(), task.Id, "work-task.verified",
                    JsonSerializer.Serialize(new WorkTaskVerified(farmId, task.Id, task.AssignedWorkerUserId,
                        task.WageAmount, task.WageUnit, now)), now));
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Ok(new { task.Id, Status = task.Status.ToString(), DecisionId = decision.Id, DecidedAt = now });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpGet("history")]
    public async Task<IActionResult> History(Guid farmId, Guid taskId, CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        var role = await authorization.GetActiveRoleAsync(farmId, userId, ct);
        if (role is null) return Forbid();
        var task = await db.WorkTasks.AsNoTracking().SingleOrDefaultAsync(item => item.FarmId == farmId && item.Id == taskId, ct);
        if (task is null) return NotFound();
        if (role == FarmRole.Worker && task.AssignedWorkerUserId != userId) return NotFound();
        if (role is not (FarmRole.Owner or FarmRole.Administrator or FarmRole.Worker)) return Forbid();
        var decisions = await db.TaskReviewDecisions.AsNoTracking().Where(item => item.FarmId == farmId && item.WorkTaskId == taskId)
            .OrderBy(item => item.DecidedAt).Select(item => new { item.Id, Action = item.Action.ToString(), item.Reason, item.DecidedAt }).ToArrayAsync(ct);
        var evidence = await db.TaskEvidenceSubmissions.AsNoTracking().Where(item => item.FarmId == farmId && item.WorkTaskId == taskId)
            .OrderBy(item => item.Revision).Select(item => new { item.Id, item.Revision, item.PreviousSubmissionId,
                item.ContentType, item.SizeBytes, item.Location, item.CapturedAt, item.FinalizedAt }).ToArrayAsync(ct);
        var wage = await db.VerifiedWageEntries.AsNoTracking().Where(item => item.FarmId == farmId && item.WorkTaskId == taskId)
            .Select(item => new { item.Amount, item.Unit, item.VerifiedAt }).SingleOrDefaultAsync(ct);
        return Ok(new { task.Id, Status = task.Status.ToString(), Evidence = evidence, Decisions = decisions, VerifiedWage = wage });
    }

    private string? CurrentUserId() => User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
}

public sealed record ReviewTaskRequest(TaskReviewAction Action, [StringLength(1000)] string? Reason);
