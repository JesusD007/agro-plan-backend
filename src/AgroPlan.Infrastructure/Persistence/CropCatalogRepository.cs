using AgroPlan.Application.Crops;
using AgroPlan.Domain.Crops;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Infrastructure.Persistence;

public sealed class CropCatalogRepository(AgroPlanDbContext dbContext) : ICropCatalogRepository
{
    public async Task<IReadOnlyList<CropCatalogEntry>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.CropCatalogEntries.AsNoTracking()
            .Include(crop => crop.Parameters)
            .OrderBy(crop => crop.DisplayName)
            .ToListAsync(cancellationToken);
}
