namespace AgroPlan.Application.Abstractions;

/// <summary>Provider-neutral, minimum-context observation request. It deliberately carries no farm, user, report or database identifiers.</summary>
public sealed record SymptomObservationRequest(byte[] PhotoBytes, string? CropName, string? GrowthStage);

/// <summary>Model output is limited to visible signs and evidence needs; diagnosis and treatment are not representable.</summary>
public sealed record SymptomObservationResult(string VisibleSigns, string Uncertainty, bool ImageSufficient,
    IReadOnlyList<string> RequestedEvidence);

public enum SymptomObservationFailureKind
{
    NotConfigured = 1,
    TemporarilyUnavailable = 2,
    Failed = 3
}

public sealed class SymptomObservationProviderException(SymptomObservationFailureKind failureKind, string message)
    : Exception(message)
{
    public SymptomObservationFailureKind FailureKind { get; } = failureKind;
}

public interface ISymptomObservationProvider
{
    Task<SymptomObservationResult> ObserveAsync(SymptomObservationRequest request, CancellationToken cancellationToken);
}
