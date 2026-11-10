using AgroPlan.Application.Abstractions;

namespace AgroPlan.Infrastructure.Prices;

public sealed class UnavailableReferencePriceProvider : IReferencePriceProvider
{
    public Task<ReferencePriceResult> GetAsync(ReferencePriceRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ReferencePriceResult.Unavailable("reference_price_source_not_configured"));
    }
}
