using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Workforce;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Api.Authorization;

public sealed class AssignedWorkerAuthorizationService(AgroPlanDbContext db)
{
    public Task<FarmRole?> GetActiveRoleAsync(Guid farmId, string userId, CancellationToken cancellationToken) =>
        db.FarmMemberships.AsNoTracking()
            .Where(member => member.FarmId == farmId && member.UserId == userId && member.IsActive)
            .Select(member => (FarmRole?)member.Role)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> IsAssignedWorkerAsync(Guid farmId, Guid taskId, string userId, CancellationToken cancellationToken) =>
        db.WorkTasks.AsNoTracking().AnyAsync(task => task.FarmId == farmId && task.Id == taskId &&
            task.AssignedWorkerUserId == userId && db.FarmMemberships.Any(member => member.FarmId == farmId &&
                member.UserId == userId && member.IsActive && member.Role == FarmRole.Worker), cancellationToken);
}
