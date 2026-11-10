using System.Security.Claims;
using AgroPlan.Domain.AdvisoryAccess;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Api.Authorization;

public sealed class AdvisorReportAuthorizationHandler(AgroPlanDbContext db) : AuthorizationHandler<AdvisorReportRequirement, AdvisorReportResource>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        AdvisorReportRequirement requirement, AdvisorReportResource resource)
    {
        var userId = context.User.FindFirstValue("sub") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId) || resource.FarmId == Guid.Empty || resource.Scope != requirement.Scope) return;

        var now = DateTimeOffset.UtcNow;
        var grants = await db.AdvisorAccessGrants
            .Where(x => x.FarmId == resource.FarmId && x.AdvisorUserId == userId && x.RevokedAt == null
                && x.StartsAt <= now && x.ExpiresAt > now)
            .ToListAsync();
        var grant = grants.FirstOrDefault(x => x.Scopes.Contains(requirement.Scope));
        if (grant is null) return;

        // Persist use metadata only after the live grant has been reloaded from PostgreSQL.
        var row = await db.AdvisorAccessGrants.SingleAsync(x => x.Id == grant.Id);
        row.RecordUse(now);
        await db.SaveChangesAsync();
        context.Succeed(requirement);
    }
}
