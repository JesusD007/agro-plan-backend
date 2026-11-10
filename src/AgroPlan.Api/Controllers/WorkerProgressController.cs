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
[Route("api/farms/{farmId:guid}/worker-progress")]
public sealed class WorkerProgressController(AgroPlanDbContext db, AssignedWorkerAuthorizationService authorization) : ControllerBase
{
    [HttpGet("{workerUserId}")]
    public async Task<IActionResult> Get(Guid farmId, string workerUserId, CancellationToken ct)
    {
        var requesterId = CurrentUserId();
        if (requesterId is null) return Unauthorized();
        var role = await authorization.GetActiveRoleAsync(farmId, requesterId, ct);
        if (role is null) return Forbid();
        if (role == FarmRole.Worker && requesterId != workerUserId) return NotFound();
        if (role is not (FarmRole.Owner or FarmRole.Administrator or FarmRole.Worker)) return Forbid();
        var workerExists = await db.FarmMemberships.AsNoTracking().AnyAsync(member => member.FarmId == farmId &&
            member.UserId == workerUserId && member.Role == FarmRole.Worker && member.IsActive, ct);
        if (!workerExists) return NotFound();

        var settings = await db.FarmWorkerSettings.AsNoTracking().SingleOrDefaultAsync(item => item.FarmId == farmId, ct)
            ?? new FarmWorkerSettings(farmId);
        var points = await db.VerifiedWorkRewards.AsNoTracking().Where(item => item.FarmId == farmId &&
            item.WorkerUserId == workerUserId && item.Kind == VerifiedWorkRewardKind.Points).SumAsync(item => item.Points, ct);
        var rewards = await db.VerifiedWorkRewards.AsNoTracking().Where(item => item.FarmId == farmId && item.WorkerUserId == workerUserId)
            .OrderByDescending(item => item.AwardedAt).Select(item => new WorkerRewardItem(item.Id, item.Kind.ToString(),
                item.RuleKey, item.Points, item.Description, item.AwardedAt)).ToArrayAsync(ct);
        var verifiedAt = await db.WorkTasks.AsNoTracking().Where(item => item.FarmId == farmId &&
            item.AssignedWorkerUserId == workerUserId && item.Status == WorkTaskStatus.Verified && item.VerifiedAt != null)
            .Select(item => item.VerifiedAt!.Value).ToArrayAsync(ct);
        var teamPoints = await db.VerifiedWorkRewards.AsNoTracking().Where(item => item.FarmId == farmId &&
            item.Kind == VerifiedWorkRewardKind.Points).SumAsync(item => item.Points, ct);
        var history = await db.WorkTasks.AsNoTracking().Where(item => item.FarmId == farmId &&
            item.AssignedWorkerUserId == workerUserId && item.Status == WorkTaskStatus.Verified)
            .OrderByDescending(item => item.VerifiedAt).Select(item => new { item.Id, item.Description, item.VerifiedAt, item.WageAmount, item.WageUnit })
            .ToArrayAsync(ct);
        return Ok(new
        {
            WorkerUserId = workerUserId,
            Points = points,
            StreakAvailable = settings.TimeZoneId is not null,
            StreakDays = settings.GetCurrentStreak(verifiedAt, DateTimeOffset.UtcNow),
            RankingsEnabled = settings.RankingsEnabled,
            TeamGoal = new { settings.TeamGoalTargetPoints, Points = teamPoints, Complete = teamPoints >= settings.TeamGoalTargetPoints },
            History = history,
            Rewards = rewards
        });
    }

    [HttpGet("board")]
    public async Task<IActionResult> Board(Guid farmId, CancellationToken ct)
    {
        var requesterId = CurrentUserId();
        if (requesterId is null) return Unauthorized();
        var role = await authorization.GetActiveRoleAsync(farmId, requesterId, ct);
        if (role is not (FarmRole.Owner or FarmRole.Administrator)) return Forbid();
        var settings = await db.FarmWorkerSettings.AsNoTracking().SingleOrDefaultAsync(item => item.FarmId == farmId, ct);
        if (settings?.RankingsEnabled != true) return Forbid();
        var board = await db.VerifiedWorkRewards.AsNoTracking().Where(item => item.FarmId == farmId && item.Kind == VerifiedWorkRewardKind.Points)
            .GroupBy(item => item.WorkerUserId).Select(group => new { WorkerUserId = group.Key, Points = group.Sum(item => item.Points) })
            .OrderByDescending(item => item.Points).ThenBy(item => item.WorkerUserId).ToArrayAsync(ct);
        return Ok(board);
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings(Guid farmId, UpdateWorkerSettingsRequest request, CancellationToken ct)
    {
        var requesterId = CurrentUserId();
        if (requesterId is null) return Unauthorized();
        var role = await authorization.GetActiveRoleAsync(farmId, requesterId, ct);
        if (role is not (FarmRole.Owner or FarmRole.Administrator)) return Forbid();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (request.TimeZoneId is not null)
        {
            try { _ = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZoneId); }
            catch (TimeZoneNotFoundException) { return BadRequest(new { message = "The farm time zone is not recognized." }); }
            catch (InvalidTimeZoneException) { return BadRequest(new { message = "The farm time zone is invalid." }); }
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM agro.farms WHERE \"Id\" = {farmId} FOR UPDATE", ct);
        var settings = await db.FarmWorkerSettings.SingleOrDefaultAsync(item => item.FarmId == farmId, ct);
        if (settings is null)
        {
            settings = new FarmWorkerSettings(farmId);
            db.FarmWorkerSettings.Add(settings);
        }
        settings.SetRankingsEnabled(request.RankingsEnabled);
        if (request.TimeZoneId is not null) settings.SetTimeZone(request.TimeZoneId);
        settings.ConfigureGoals(request.BadgeThresholdPoints, request.TeamGoalTargetPoints);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(new { settings.FarmId, settings.RankingsEnabled, settings.TimeZoneId,
            settings.BadgeThresholdPoints, settings.TeamGoalTargetPoints, settings.RuleVersion });
    }

    private string? CurrentUserId() => User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
}

public sealed record UpdateWorkerSettingsRequest(
    bool RankingsEnabled,
    [StringLength(100)] string? TimeZoneId,
    [Range(1, 1_000_000)] int BadgeThresholdPoints,
    [Range(1, 10_000_000)] int TeamGoalTargetPoints);

public sealed record WorkerRewardItem(Guid Id, string Kind, string RuleKey, int Points, string? Description, DateTimeOffset AwardedAt);
