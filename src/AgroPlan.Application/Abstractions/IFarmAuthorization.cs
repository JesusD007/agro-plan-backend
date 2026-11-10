using AgroPlan.Domain.Farms;

namespace AgroPlan.Application.Abstractions;

public interface IFarmAuthorization
{
    Task<FarmRole?> GetActiveRoleAsync(Guid farmId, string userId, CancellationToken cancellationToken);
}
