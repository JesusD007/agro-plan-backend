namespace AgroPlan.Application.Abstractions;

public sealed record ValidatedAgronomicInput(
    string Key,
    decimal NumericValue,
    string Unit,
    string SourceName,
    string? SourceUrl,
    string? Region,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo);

public sealed record SowingGenerationInput(
    string CropCode,
    string CropName,
    string CropValidationStatus,
    string? CropRegion,
    DateTimeOffset? CropValidFrom,
    DateTimeOffset? CropValidTo,
    DateOnly? DesiredPlantingDate,
    decimal ParcelAreaHectares,
    string? LocationDescription,
    IReadOnlyList<ValidatedAgronomicInput> ValidatedParameters,
    ClimateContext Climate);

public sealed record AiProposalDraft(
    string Summary,
    IReadOnlyList<string> SourceIds,
    IReadOnlyList<string> MissingInputs,
    string Model,
    string PromptVersion,
    string SchemaVersion,
    DateTimeOffset GeneratedAt,
    string Provider);

public enum AiProposalProviderFailureKind
{
    NotConfigured,
    TemporarilyUnavailable,
    GenerationFailed
}

public class AiProposalProviderException(
    AiProposalProviderFailureKind failureKind,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public AiProposalProviderFailureKind FailureKind { get; } = failureKind;
}

public interface IAiProposalProvider
{
    Task<AiProposalDraft> GenerateDraftAsync(
        SowingGenerationInput input,
        CancellationToken cancellationToken);
}
