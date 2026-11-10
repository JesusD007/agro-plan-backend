using System.Text.Json.Serialization;

namespace AgroPlan.Application.Abstractions;

public sealed record ReferencePriceRequest(
    Guid FarmId,
    string CropId,
    string? Variety,
    string MarketChannel,
    string Region,
    DateTimeOffset RequestedAt);

[JsonConverter(typeof(JsonStringEnumConverter<ReferencePriceStatus>))]
public enum ReferencePriceStatus
{
    Unavailable,
    Available,
    Stale
}

/// <summary>A market observation, not a sale record, farm-gate estimate or guaranteed quote.</summary>
public sealed record ReferencePriceResult(
    ReferencePriceStatus Status,
    decimal? Amount,
    string? CropId,
    string? Variety,
    string? MarketChannel,
    string? Region,
    string? Unit,
    string? CurrencyCode,
    string? ProviderId,
    string? SourceName,
    DateTimeOffset? ObservedAt,
    DateTimeOffset? ValidUntil,
    string? Freshness,
    string? UnavailableReason)
{
    public static ReferencePriceResult Unavailable(string reason) => new(
        ReferencePriceStatus.Unavailable, null, null, null, null, null, null, null, null, null,
        null, null, "unavailable", reason);
}

public interface IReferencePriceProvider
{
    Task<ReferencePriceResult> GetAsync(ReferencePriceRequest request, CancellationToken cancellationToken);
}

public static class ReferencePriceFreshness
{
    public static ReferencePriceResult Evaluate(ReferencePriceResult result, DateTimeOffset now)
    {
        if (result.Status != ReferencePriceStatus.Available)
            return result;

        if (result.Amount is null
            || string.IsNullOrWhiteSpace(result.CropId)
            || string.IsNullOrWhiteSpace(result.MarketChannel)
            || string.IsNullOrWhiteSpace(result.Region)
            || string.IsNullOrWhiteSpace(result.Unit)
            || string.IsNullOrWhiteSpace(result.CurrencyCode)
            || string.IsNullOrWhiteSpace(result.ProviderId)
            || string.IsNullOrWhiteSpace(result.SourceName)
            || result.ObservedAt is null
            || result.ValidUntil is null)
        {
            return ReferencePriceResult.Unavailable("required_price_provenance_missing");
        }

        if (result.ValidUntil <= now)
            return result with { Status = ReferencePriceStatus.Stale, Freshness = "stale",
                UnavailableReason = "outside_validity_window" };

        return result with { Freshness = "current", UnavailableReason = null };
    }
}
