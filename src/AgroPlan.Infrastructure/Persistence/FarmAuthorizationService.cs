using AgroPlan.Application.Abstractions;
using AgroPlan.Domain.Farms;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Infrastructure.Persistence;

public sealed class FarmAuthorizationService(AgroPlanDbContext dbContext) : IFarmAuthorization
{
    public Task<FarmRole?> GetActiveRoleAsync(Guid farmId, string userId, CancellationToken cancellationToken) =>
        dbContext.FarmMemberships.AsNoTracking()
            .Where(membership => membership.FarmId == farmId && membership.UserId == userId && membership.IsActive)
            .Select(membership => (FarmRole?)membership.Role)
            .SingleOrDefaultAsync(cancellationToken);
}
