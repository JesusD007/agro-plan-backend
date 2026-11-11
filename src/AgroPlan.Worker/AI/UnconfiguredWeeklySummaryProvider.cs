using AgroPlan.Application.Abstractions;

namespace AgroPlan.Worker.AI;

/// <summary>Development/test-only implementation that makes no network request.</summary>
public sealed class UnconfiguredWeeklySummaryProvider : IWeeklySummaryNarrativeProvider
{
    public Task<WeeklySummaryNarrativeDraft> GenerateAsync(WeeklySummaryNarrativeInput input, CancellationToken cancellationToken) =>
        Task.FromException<WeeklySummaryNarrativeDraft>(
            new WeeklySummaryNarrativeProviderException(WeeklySummaryNarrativeFailureKind.NotConfigured));
}
