using AgroPlan.Application.Abstractions;

namespace AgroPlan.Worker.AI;

/// <summary>Development/test-only implementation that makes no network request.</summary>
public sealed class UnconfiguredSymptomObservationProvider : ISymptomObservationProvider
{
    public Task<SymptomObservationResult> ObserveAsync(SymptomObservationRequest request, CancellationToken cancellationToken) =>
        Task.FromException<SymptomObservationResult>(
            new SymptomObservationProviderException(SymptomObservationFailureKind.NotConfigured,
                "Symptom observation is not configured."));
}
