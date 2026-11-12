using System.Security.Claims;
using AgroPlan.Api.Authorization;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Api.Controllers;

/// <summary>Discover only the caller's active memberships, including worker farms.</summary>
[ApiController]
[Authorize]
public sealed class AccountAccessController(AgroPlanDbContext db) : ControllerBase
{
    [HttpGet("api/farms/memberships")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var userId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();
        var memberships = await db.FarmMemberships.AsNoTracking()
            .Where(m => m.UserId == userId && m.IsActive)
            .Include(m => m.Farm).OrderBy(m => m.Farm.Name).ToArrayAsync(ct);
        return Ok(memberships.Select(m => new {
            m.FarmId, farmName = m.Farm.Name, m.Farm.Location,
            role = m.Role.ToString(), workerEntitlement = m.Farm.WorkerEntitlement.ToString(), m.UserId
        }));
    }

    [HttpGet("api/farms/{farmId:guid}/members")]
    [Authorize(Policy = FarmMembershipRequirement.PolicyName)]
    public async Task<IActionResult> Members(Guid farmId, CancellationToken ct)
    {
        var memberships = await db.FarmMemberships.AsNoTracking().Where(m => m.FarmId == farmId && m.IsActive)
            .ToArrayAsync(ct);
        var ids = memberships.Select(m => m.UserId).ToArray();
        var emails = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email, ct);
        return Ok(memberships.Select(m => new { m.UserId, email = emails.GetValueOrDefault(m.UserId), role = m.Role.ToString(), m.IsActive }));
    }
}
