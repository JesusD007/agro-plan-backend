using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AgroPlan.Api.Authorization;
using AgroPlan.Application.AdvisoryAccess;
using AgroPlan.Application.Abstractions;
using AgroPlan.Domain.AdvisoryAccess;
using AgroPlan.Domain.Farms;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Api.Controllers;

[ApiController]
[Route("api/farms/{farmId:guid}/advisor-access")]
[Authorize(Policy = FarmMembershipRequirement.PolicyName)]
public sealed class AdvisorAccessController(AgroPlanDbContext db, IFarmAuthorization farmAuthorization,
    AdvisorGrantService grants) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdvisorGrantResponse>>> List(Guid farmId, CancellationToken ct)
    {
        var actor = CurrentUserId();
        if (actor is null) return Unauthorized();
        if (await farmAuthorization.GetActiveRoleAsync(farmId, actor, ct) != FarmRole.Owner) return Forbid();
        var rows = await db.AdvisorAccessGrants.AsNoTracking().Where(x => x.FarmId == farmId)
            .OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return Ok(rows.Select(ToResponse).ToArray());
    }

    [HttpPost]
    public async Task<ActionResult<AdvisorGrantResponse>> Create(Guid farmId, CreateAdvisorGrantRequest request, CancellationToken ct)
    {
        var actor = CurrentUserId();
        if (actor is null) return Unauthorized();
        if (await farmAuthorization.GetActiveRoleAsync(farmId, actor, ct) != FarmRole.Owner) return Forbid();
        if (request.AdvisorUserId == actor) return BadRequest(new { message = "The producer cannot grant advisor access to themself." });
        if (!await db.Users.AsNoTracking().AnyAsync(x => x.Id == request.AdvisorUserId, ct)) return NotFound(new { message = "Advisor account was not found." });
        if (request.Scopes is null || request.Scopes.Length == 0 || request.Scopes.Any(x => !Enum.IsDefined(x)) || request.Scopes.Distinct().Count() != request.Scopes.Length)
            return BadRequest(new { message = "Select one or more valid read-only report scopes." });
        AdvisorAccessGrant grant;
        try
        {
            var now = DateTimeOffset.UtcNow;
            grant = grants.Create(farmId, request.AdvisorUserId, request.Scopes, actor, request.Purpose, request.StartsAt, request.ExpiresAt, now);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        db.AdvisorAccessGrants.Add(grant);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), new { farmId }, ToResponse(grant));
    }

    [HttpDelete("{grantId:guid}")]
    public async Task<IActionResult> Revoke(Guid farmId, Guid grantId, CancellationToken ct)
    {
        var actor = CurrentUserId();
        if (actor is null) return Unauthorized();
        if (await farmAuthorization.GetActiveRoleAsync(farmId, actor, ct) != FarmRole.Owner) return Forbid();
        var grant = await db.AdvisorAccessGrants.SingleOrDefaultAsync(x => x.FarmId == farmId && x.Id == grantId, ct);
        if (grant is null) return NotFound();
        if (grant.RevokedAt.HasValue) return NoContent();
        grant.Revoke(actor, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private string? CurrentUserId() => User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
    private static AdvisorGrantResponse ToResponse(AdvisorAccessGrant x) => new(x.Id, x.FarmId, x.AdvisorUserId,
        x.Scopes, x.GrantedByUserId, x.Purpose, x.StartsAt, x.ExpiresAt, x.CreatedAt, x.RevokedAt, x.RevokedByUserId, x.LastUsedAt);
}

public sealed record CreateAdvisorGrantRequest([param: Required, StringLength(450, MinimumLength = 1)] string AdvisorUserId,
    [param: Required] AdvisorReportScope[] Scopes, [param: Required, StringLength(500, MinimumLength = 1)] string Purpose,
    [param: Required] DateTimeOffset StartsAt, [param: Required] DateTimeOffset ExpiresAt);
public sealed record AdvisorGrantResponse(Guid Id, Guid FarmId, string AdvisorUserId, IReadOnlyCollection<AdvisorReportScope> Scopes,
    string GrantedByUserId, string Purpose, DateTimeOffset StartsAt, DateTimeOffset ExpiresAt, DateTimeOffset CreatedAt,
    DateTimeOffset? RevokedAt, string? RevokedByUserId, DateTimeOffset? LastUsedAt);
