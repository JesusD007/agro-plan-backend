using System.Text.Json.Serialization;

namespace AgroPlan.Application.Abstractions;

public sealed record WeatherAdvisoryRequest(
    Guid FarmId,
    string GeographicArea,
    double? Latitude,
    double? Longitude,
    DateTimeOffset RequestedAt);

[JsonConverter(typeof(JsonStringEnumConverter<WeatherAdvisoryStatus>))]
public enum WeatherAdvisoryStatus
{
    Unavailable,
    Available,
    Stale,
    GeographicMismatch
}

/// <summary>
/// Weather hazard data with provenance and bounded validity. An unavailable result never means
/// that conditions are safe or that no hazard exists.
/// </summary>
public sealed record WeatherAdvisoryResult(
    WeatherAdvisoryStatus Status,
    string? HazardKind,
    string? GeographicCoverage,
    string? ProviderId,
    string? SourceName,
    DateTimeOffset? IssuedAt,
    DateTimeOffset? ObservedAt,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    string? Freshness,
    string? UnavailableReason)
{
    public static WeatherAdvisoryResult Unavailable(string reason) => new(
        WeatherAdvisoryStatus.Unavailable, null, null, null, null, null, null, null, null,
        "unavailable", reason);
}

public interface IWeatherAdvisoryProvider
{
    Task<WeatherAdvisoryResult> GetAsync(WeatherAdvisoryRequest request, CancellationToken cancellationToken);
}

public static class WeatherAdvisoryFreshness
{
    public static WeatherAdvisoryResult Evaluate(
        WeatherAdvisoryResult result,
        string requestedGeographicArea,
        DateTimeOffset now)
    {
        if (result.Status != WeatherAdvisoryStatus.Available)
            return result;

        if (string.IsNullOrWhiteSpace(result.HazardKind)
            || string.IsNullOrWhiteSpace(result.GeographicCoverage)
            || string.IsNullOrWhiteSpace(result.ProviderId)
            || string.IsNullOrWhiteSpace(result.SourceName)
            || result.IssuedAt is null
            || result.ValidFrom is null
            || result.ValidTo is null)
        {
            return WeatherAdvisoryResult.Unavailable("required_provenance_missing");
        }

        if (!string.Equals(result.GeographicCoverage, requestedGeographicArea, StringComparison.OrdinalIgnoreCase))
            return result with { Status = WeatherAdvisoryStatus.GeographicMismatch, Freshness = "unavailable",
                UnavailableReason = "geographic_coverage_mismatch" };

        if (result.ValidTo <= now || result.ValidFrom > now)
            return result with { Status = WeatherAdvisoryStatus.Stale, Freshness = "stale",
                UnavailableReason = "outside_validity_window" };

        return result with { Freshness = "current", UnavailableReason = null };
    }
}
