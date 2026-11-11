namespace AgroPlan.Application.Abstractions;

/// <summary>Neutral boundary for optional prose generation over a deterministic summary projection.</summary>
public interface IWeeklySummaryNarrativeProvider
{
    Task<WeeklySummaryNarrativeDraft> GenerateAsync(WeeklySummaryNarrativeInput input, CancellationToken cancellationToken);
}

public enum WeeklySummaryNarrativeFailureKind
{
    NotConfigured = 1,
    TemporarilyUnavailable = 2
}

public sealed class WeeklySummaryNarrativeProviderException(WeeklySummaryNarrativeFailureKind failureKind)
    : Exception("Weekly summary narrative provider is unavailable.")
{
    public WeeklySummaryNarrativeFailureKind FailureKind { get; } = failureKind;
}

public sealed record WeeklySummaryNarrativeInput(
    DateOnly WeekStartDate,
    DateOnly WeekEndDate,
    string TimeZoneId,
    IReadOnlyList<WeeklySummaryNarrativeSource> Sources,
    IReadOnlyList<WeeklySummaryNarrativeFact> DeterministicFacts,
    IReadOnlyList<string> MissingData);

public sealed record WeeklySummaryNarrativeSource(
    string Alias,
    string Kind,
    DateOnly LocalDate,
    decimal? Amount,
    string? CurrencyCode,
    decimal? Quantity,
    string? Unit);

public sealed record WeeklySummaryNarrativeFact(string Kind, decimal Value, string? Unit, string? CurrencyCode);

public sealed record WeeklySummaryNarrativeDraft(string Text, IReadOnlyList<string> SourceAliases);

public sealed record WeeklySummaryNarrativeRequested(Guid EventId, Guid FarmId, Guid SummaryId,
    string RequestedByUserId, int Version);
