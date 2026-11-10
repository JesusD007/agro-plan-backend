using System.Security.Claims;
using AgroPlan.Domain.Farms;
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

public sealed record TaskScheduleRevisionResponse(Guid Id, Guid WorkTaskId, DateOnly PreviousDate,
    DateOnly ProposedDate, string Explanation, string Status, Guid SourceRecordId, int SourceRecordVersion,
    DateTimeOffset ProposedAt, string? ConfirmedByUserId, DateTimeOffset? ConfirmedAt);
