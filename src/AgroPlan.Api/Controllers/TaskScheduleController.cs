using System.Security.Claims;
using System.Text.Json;
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
[Route("api/farms/{farmId:guid}/task-schedule")]
public sealed class TaskScheduleController(AgroPlanDbContext db) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpPost("tasks/{taskId:guid}/revisions")]
    public async Task<ActionResult<TaskScheduleRevisionResponse>> Reschedule(Guid farmId, Guid taskId,
        RescheduleTaskRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();
        var role = await db.FarmMemberships.AsNoTracking().Where(x => x.FarmId == farmId && x.UserId == userId && x.IsActive)
            .Select(x => (FarmRole?)x.Role).SingleOrDefaultAsync(ct);
        if (role is not (FarmRole.Owner or FarmRole.Administrator)) return Forbid();
        if (request.ProposedDate == default || string.IsNullOrWhiteSpace(request.Explanation) || request.Explanation.Trim().Length > 1000)
            return BadRequest(new { message = "A valid proposed date and explanation are required." });
        var task = await db.WorkTasks.SingleOrDefaultAsync(x => x.FarmId == farmId && x.Id == taskId, ct);
        if (task is null) return NotFound();
        var previousDate = task.DueDate;
        if (previousDate == request.ProposedDate) return BadRequest(new { message = "The proposed date must change the assignment." });
        var now = DateTimeOffset.UtcNow;
        var revision = new TaskScheduleRevision(Guid.NewGuid(), farmId, taskId, null, null, 0,
            task.Version, previousDate, request.ProposedDate, request.Explanation, now);
        try
        {
            revision.Confirm(userId, now, task.Version);
            task.Reschedule(request.ProposedDate);
        }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        db.TaskScheduleRevisions.Add(revision);
        var notificationId = Guid.NewGuid();
        var notification = new TaskScheduleChanged(farmId, taskId, revision.Id, task.AssignedWorkerUserId,
            task.DueDate, task.Version, now);
        db.OutboxMessages.Add(new OutboxMessage(notificationId, taskId, "task-schedule.changed",
            JsonSerializer.Serialize(notification, JsonOptions), now));
        var calendarId = Guid.NewGuid();
        var calendar = new TaskCalendarSyncRequested(farmId, taskId, revision.Id, task.Version, task.DueDate, now);
        db.OutboxMessages.Add(new OutboxMessage(calendarId, taskId, "task-calendar.sync",
            JsonSerializer.Serialize(calendar, JsonOptions), now));
        await db.SaveChangesAsync(ct);
        var response = new TaskScheduleRevisionResponse(revision.Id, taskId, previousDate, request.ProposedDate,
            revision.Explanation, revision.Status.ToString(), null, 0, now, revision.ConfirmedByUserId, revision.ConfirmedAt);
        return CreatedAtAction(nameof(Proposals), new { farmId }, response);
    }

    [HttpPost("revisions/{revisionId:guid}/confirm")]
    public async Task<ActionResult<TaskScheduleRevisionResponse>> Confirm(Guid farmId, Guid revisionId, CancellationToken ct)
    {
        var userId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();
        var role = await db.FarmMemberships.AsNoTracking().Where(x => x.FarmId == farmId && x.UserId == userId && x.IsActive)
            .Select(x => (FarmRole?)x.Role).SingleOrDefaultAsync(ct);
        if (role is not (FarmRole.Owner or FarmRole.Administrator)) return Forbid();
        var revision = await db.TaskScheduleRevisions.SingleOrDefaultAsync(x => x.FarmId == farmId && x.Id == revisionId, ct);
        if (revision is null) return NotFound();
        var task = await db.WorkTasks.SingleOrDefaultAsync(x => x.FarmId == farmId && x.Id == revision.WorkTaskId, ct);
        if (task is null) return NotFound();
        if (revision.SourceEventId.HasValue && !await SourceIsCurrentAsync(revision, ct))
        {
            revision.MarkStale();
            await db.SaveChangesAsync(ct);
            return Conflict(new { message = "The source record is no longer current; this proposal is stale." });
        }
        var now = DateTimeOffset.UtcNow;
        try
        {
            revision.Confirm(userId, now, task.Version);
            task.Reschedule(revision.ProposedDate);
        }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
        var changed = new TaskScheduleChanged(farmId, task.Id, revision.Id, task.AssignedWorkerUserId,
            task.DueDate, task.Version, now);
        var calendar = new TaskCalendarSyncRequested(farmId, task.Id, revision.Id, task.Version, task.DueDate, now);
        db.OutboxMessages.Add(new OutboxMessage(Guid.NewGuid(), task.Id, "task-schedule.changed", JsonSerializer.Serialize(changed, JsonOptions), now));
        db.OutboxMessages.Add(new OutboxMessage(Guid.NewGuid(), task.Id, "task-calendar.sync", JsonSerializer.Serialize(calendar, JsonOptions), now));
        await db.SaveChangesAsync(ct);
        return Ok(new TaskScheduleRevisionResponse(revision.Id, task.Id, revision.PreviousDate, revision.ProposedDate,
            revision.Explanation, revision.Status.ToString(), revision.SourceRecordId, revision.SourceRecordVersion,
            revision.ProposedAt, revision.ConfirmedByUserId, revision.ConfirmedAt));
    }

    private async Task<bool> SourceIsCurrentAsync(TaskScheduleRevision revision, CancellationToken ct)
    {
        var outbox = await db.OutboxMessages.AsNoTracking().SingleOrDefaultAsync(x => x.Id == revision.SourceEventId &&
            x.EventType == "farm-record.confirmed", ct);
        var source = outbox is null ? null : JsonSerializer.Deserialize<FarmRecordConfirmed>(outbox.PayloadJson, JsonOptions);
        if (source is null || source.FarmId != farmIdOf(revision) || source.SourceRecordId != revision.SourceRecordId ||
            source.SourceRecordVersion != revision.SourceRecordVersion) return false;
        return await db.LogbookEntries.AsNoTracking().AnyAsync(x => x.Id == source.SourceRecordId && x.FarmId == source.FarmId &&
                   x.ParcelId == source.ParcelId && x.CropCycleId == source.CropCycleId, ct) ||
               await db.IrrigationEntries.AsNoTracking().AnyAsync(x => x.Id == source.SourceRecordId && x.FarmId == source.FarmId &&
                   x.ParcelId == source.ParcelId && x.CropCycleId == source.CropCycleId, ct) ||
               await db.InputApplicationEntries.AsNoTracking().AnyAsync(x => x.Id == source.SourceRecordId && x.FarmId == source.FarmId &&
                   x.ParcelId == source.ParcelId && x.CropCycleId == source.CropCycleId, ct) ||
               await db.HarvestEntries.AsNoTracking().AnyAsync(x => x.Id == source.SourceRecordId && x.FarmId == source.FarmId &&
                   x.ParcelId == source.ParcelId && x.CropCycleId == source.CropCycleId, ct);
    }

    private static Guid farmIdOf(TaskScheduleRevision revision) => revision.FarmId;

    [HttpGet("proposals")]
    public async Task<ActionResult<IReadOnlyList<TaskScheduleRevisionResponse>>> Proposals(Guid farmId, CancellationToken ct)
    {
        var userId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();
        var role = await db.FarmMemberships.AsNoTracking().Where(x => x.FarmId == farmId && x.UserId == userId && x.IsActive)
            .Select(x => (FarmRole?)x.Role).SingleOrDefaultAsync(ct);
        if (role is null) return Forbid();
        var revisions = db.TaskScheduleRevisions.AsNoTracking().Where(x => x.FarmId == farmId);
        if (role == FarmRole.Worker)
            revisions = revisions.Where(x => db.WorkTasks.Any(task => task.FarmId == farmId && task.Id == x.WorkTaskId && task.AssignedWorkerUserId == userId));
        else if (role is not (FarmRole.Owner or FarmRole.Administrator)) return Forbid();
        var rows = await revisions.OrderByDescending(x => x.ProposedAt).Take(500).ToListAsync(ct);
        return Ok(rows.Select(x => new TaskScheduleRevisionResponse(x.Id, x.WorkTaskId, x.PreviousDate,
            x.ProposedDate, x.Explanation, x.Status.ToString(), x.SourceRecordId, x.SourceRecordVersion,
            x.ProposedAt, x.ConfirmedByUserId, x.ConfirmedAt)).ToArray());
    }
}

public sealed record RescheduleTaskRequest(DateOnly ProposedDate, string Explanation);
public sealed record TaskScheduleRevisionResponse(Guid Id, Guid WorkTaskId, DateOnly PreviousDate,
    DateOnly ProposedDate, string Explanation, string Status, Guid? SourceRecordId, int SourceRecordVersion,
    DateTimeOffset ProposedAt, string? ConfirmedByUserId, DateTimeOffset? ConfirmedAt);
