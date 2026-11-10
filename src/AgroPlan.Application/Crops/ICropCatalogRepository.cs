using AgroPlan.Domain.Crops;

namespace AgroPlan.Application.Crops;

public interface ICropCatalogRepository
{
    Task<IReadOnlyList<CropCatalogEntry>> ListAsync(CancellationToken cancellationToken);
}
