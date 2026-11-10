namespace AgroPlan.Application.Abstractions;

public sealed record ClimateRequest(
    string? LocationDescription,
    double? Latitude,
    double? Longitude,
    DateOnly? RequestedPlantingDate,
    DateTimeOffset RequestedAt);

public sealed record ClimateContext(
    string Status,
    string? ProviderName,
    string? SourceName,
    DateTimeOffset? ObservedAt,
    DateTimeOffset? ForecastIssuedAt,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    string? GeographicCoverage,
    string? Freshness,
    string? UnavailableReason,
    IReadOnlyList<string> MissingInputs,
    IReadOnlyList<string> SourceIds);

public interface IClimateProvider
{
    Task<ClimateContext> GetAsync(ClimateRequest request, CancellationToken cancellationToken);
}
