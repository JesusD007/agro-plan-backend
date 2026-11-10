using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AgroPlan.Api.Authorization;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Workforce;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/farms/{farmId:guid}/worker-rewards")]
public sealed class WorkerRewardsController(AgroPlanDbContext db, AssignedWorkerAuthorizationService authorization) : ControllerBase
{
    [HttpGet("{workerUserId}")]
    public async Task<IActionResult> List(Guid farmId, string workerUserId, CancellationToken ct)
    {
        var requesterId = CurrentUserId();
        if (requesterId is null) return Unauthorized();
        var role = await authorization.GetActiveRoleAsync(farmId, requesterId, ct);
        if (role is null) return Forbid();
        if (role == FarmRole.Worker && requesterId != workerUserId) return NotFound();
        if (role is not (FarmRole.Owner or FarmRole.Administrator or FarmRole.Worker)) return Forbid();
        if (!await IsActiveWorker(farmId, workerUserId, ct)) return NotFound();
        var rewards = await db.VerifiedWorkRewards.AsNoTracking().Where(item => item.FarmId == farmId && item.WorkerUserId == workerUserId)
            .OrderByDescending(item => item.AwardedAt).Select(item => new WorkerRewardItem(item.Id, item.Kind.ToString(),
                item.RuleKey, item.Points, item.Description, item.AwardedAt)).ToArrayAsync(ct);
        return Ok(rewards);
    }

    [HttpPost("{workerUserId}")]
    public async Task<IActionResult> Grant(Guid farmId, string workerUserId, GrantWorkerRewardRequest request, CancellationToken ct)
    {
        var administratorId = CurrentUserId();
        if (administratorId is null) return Unauthorized();
        var role = await authorization.GetActiveRoleAsync(farmId, administratorId, ct);
        if (role is not (FarmRole.Owner or FarmRole.Administrator)) return Forbid();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (!await IsActiveWorker(farmId, workerUserId, ct)) return NotFound();
        var reward = new VerifiedWorkReward(Guid.NewGuid(), farmId, workerUserId, VerifiedWorkRewardKind.Manual,
            "manual", 0, 1, DateTimeOffset.UtcNow, grantedByUserId: administratorId, description: request.Description);
        db.VerifiedWorkRewards.Add(reward);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), new { farmId, workerUserId },
            new WorkerRewardItem(reward.Id, reward.Kind.ToString(), reward.RuleKey, reward.Points, reward.Description, reward.AwardedAt));
    }

    private Task<bool> IsActiveWorker(Guid farmId, string workerUserId, CancellationToken ct) =>
        db.FarmMemberships.AsNoTracking().AnyAsync(member => member.FarmId == farmId && member.UserId == workerUserId &&
            member.Role == FarmRole.Worker && member.IsActive, ct);

    private string? CurrentUserId() => User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
}

public sealed record GrantWorkerRewardRequest([Required, StringLength(1000)] string Description);
