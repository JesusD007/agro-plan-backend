using System.Security.Claims;
using AgroPlan.Domain.Farms;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Api.Authorization;

public sealed class FarmMembershipRequirement : IAuthorizationRequirement
{
    public const string PolicyName = "ActiveFarmMembership";
}

public sealed class FarmMembershipAuthorizationHandler(AgroPlanDbContext dbContext)
    : AuthorizationHandler<FarmMembershipRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, FarmMembershipRequirement requirement)
    {
        var httpContext = context.Resource switch
        {
            HttpContext direct => direct,
            AuthorizationFilterContext filterContext => filterContext.HttpContext,
            _ => null
        };
        var userId = context.User.FindFirstValue("sub") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var farmIdValue = httpContext?.Request.RouteValues["farmId"]?.ToString();
        if (httpContext is null || string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(farmIdValue, out var farmId)) return;

        var isActiveMember = await dbContext.FarmMemberships.AsNoTracking().AnyAsync(
            membership => membership.FarmId == farmId && membership.UserId == userId && membership.IsActive &&
                (membership.Role == FarmRole.Owner || membership.Role == FarmRole.Administrator),
            httpContext.RequestAborted);
        if (isActiveMember) context.Succeed(requirement);
    }
}
