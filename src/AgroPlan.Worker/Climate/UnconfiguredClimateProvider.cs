using AgroPlan.Application.Abstractions;

namespace AgroPlan.Worker.Climate;

public sealed class UnconfiguredClimateProvider : IClimateProvider
{
    public Task<ClimateContext> GetAsync(ClimateRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ClimateContext(
            Status: "unavailable",
            ProviderName: null,
            SourceName: null,
            ObservedAt: null,
            ForecastIssuedAt: null,
            ValidFrom: null,
            ValidTo: null,
            GeographicCoverage: null,
            Freshness: null,
            UnavailableReason: "No climate provider has been selected and validated for production.",
            MissingInputs: ["climate.provider", "climate.coordinates"],
            SourceIds: []));
    }
}
