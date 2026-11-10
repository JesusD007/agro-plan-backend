using AgroPlan.Application.Abstractions;

namespace AgroPlan.Infrastructure.Routing;

/// <summary>Explicitly disables route generation until a geographic provider has been selected and approved.</summary>
public sealed class UnconfiguredWorkRouteProvider : IWorkRouteProvider
{
    public Task<WorkRouteProposal> GenerateAsync(WorkRouteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new WorkRouteProposal(
            Available: false,
            ProviderName: null,
            OrderedTaskIds: [],
            GeometryGeoJson: null,
            UnavailableReason: "Route generation is unavailable until a geographic provider is selected and approved."));
    }
}
