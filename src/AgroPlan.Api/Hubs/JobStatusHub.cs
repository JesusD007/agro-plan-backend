using System.Security.Claims;
using AgroPlan.Application.Abstractions;
using AgroPlan.Domain.Farms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AgroPlan.Api.Hubs;

[Authorize]
public sealed class JobStatusHub(IFarmAuthorization farmAuthorization) : Hub
{
    public static string FarmUserGroup(Guid farmId, string userId) => $"farm:{farmId:N}:user:{userId}";

    public async Task JoinFarm(Guid farmId)
    {
        var userId = Context.User?.FindFirstValue("sub") ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) throw new HubException("Authentication is required.");
        var role = await farmAuthorization.GetActiveRoleAsync(farmId, userId, Context.ConnectionAborted);
        if (role is not (FarmRole.Owner or FarmRole.Administrator))
            throw new HubException("Active farm membership is required.");
        await Groups.AddToGroupAsync(Context.ConnectionId, FarmUserGroup(farmId, userId), Context.ConnectionAborted);
    }
}
