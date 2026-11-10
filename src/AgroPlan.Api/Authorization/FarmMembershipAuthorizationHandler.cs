using System.Security.Claims;
using AgroPlan.Application.Abstractions;
using AgroPlan.Domain.Farms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AgroPlan.Api.Authorization;

public sealed class FarmMembershipRequirement : IAuthorizationRequirement
{
    public const string PolicyName = "ActiveFarmMembership";
}

public sealed class FarmMembershipAuthorizationHandler(IFarmAuthorization farmAuthorization)
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

        var role = await farmAuthorization.GetActiveRoleAsync(farmId, userId, httpContext.RequestAborted);
        if (role is FarmRole.Owner or FarmRole.Administrator) context.Succeed(requirement);
    }
}
