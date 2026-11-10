using AgroPlan.Application.Abstractions;
using AgroPlan.Infrastructure.Climate;
using AgroPlan.Infrastructure.Prices;
using Xunit;

namespace AgroPlan.UnitTests.Advisories;

public sealed class AdvisoryFreshnessTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Unconfigured_weather_provider_returns_unavailable_without_no_risk_claim()
    {
        var result = await new UnavailableWeatherAdvisoryProvider().GetAsync(
            new(Guid.NewGuid(), "Hato Mayor", null, null, Now), CancellationToken.None);

        Assert.Equal(WeatherAdvisoryStatus.Unavailable, result.Status);
        Assert.Equal("weather_source_not_configured", result.UnavailableReason);
        Assert.Null(result.HazardKind);
        Assert.Null(result.ProviderId);
    }

    [Fact]
    public void Weather_result_is_current_only_with_provenance_matching_geography_and_validity()
    {
        var current = WeatherAdvisoryFreshness.Evaluate(Weather("Hato Mayor", Now.AddHours(-1), Now.AddHours(1)),
            "Hato Mayor", Now);
        var wrongArea = WeatherAdvisoryFreshness.Evaluate(Weather("El Seibo", Now.AddHours(-1), Now.AddHours(1)),
            "Hato Mayor", Now);
        var expired = WeatherAdvisoryFreshness.Evaluate(Weather("Hato Mayor", Now.AddHours(-2), Now.AddMinutes(-1)),
            "Hato Mayor", Now);
        var missingProvenance = WeatherAdvisoryFreshness.Evaluate(Weather("Hato Mayor", Now.AddHours(-1), Now.AddHours(1)) with
            { SourceName = null }, "Hato Mayor", Now);

        Assert.Equal(WeatherAdvisoryStatus.Available, current.Status);
        Assert.Equal("current", current.Freshness);
        Assert.Equal(WeatherAdvisoryStatus.GeographicMismatch, wrongArea.Status);
        Assert.Equal(WeatherAdvisoryStatus.Stale, expired.Status);
        Assert.Equal(WeatherAdvisoryStatus.Unavailable, missingProvenance.Status);
        Assert.Equal("required_provenance_missing", missingProvenance.UnavailableReason);
    }

    [Fact]
    public async Task Unconfigured_price_provider_returns_unavailable_and_no_numeric_amount()
    {
        var result = await new UnavailableReferencePriceProvider().GetAsync(
            new(Guid.NewGuid(), "rice", null, "wholesale", "Hato Mayor", Now), CancellationToken.None);

        Assert.Equal(ReferencePriceStatus.Unavailable, result.Status);
        Assert.Equal("reference_price_source_not_configured", result.UnavailableReason);
        Assert.Null(result.Amount);
        Assert.Null(result.CurrencyCode);
    }

    [Fact]
    public void Price_result_requires_provenance_and_expiry_to_be_current()
    {
        var valid = new ReferencePriceResult(ReferencePriceStatus.Available, 1350m, "rice", "Juma",
            "wholesale", "Hato Mayor", "quintal", "DOP", "official-feed", "MIDA market bulletin",
            Now.AddHours(-1), Now.AddHours(2), null, null);
        var current = ReferencePriceFreshness.Evaluate(valid, Now);
        var stale = ReferencePriceFreshness.Evaluate(valid with { ValidUntil = Now.AddSeconds(-1) }, Now);
        var incomplete = ReferencePriceFreshness.Evaluate(valid with { Unit = null }, Now);

        Assert.Equal(ReferencePriceStatus.Available, current.Status);
        Assert.Equal("current", current.Freshness);
        Assert.Equal("DOP", current.CurrencyCode);
        Assert.Equal(ReferencePriceStatus.Stale, stale.Status);
        Assert.Equal(ReferencePriceStatus.Unavailable, incomplete.Status);
        Assert.Null(incomplete.Amount);
    }

    private static WeatherAdvisoryResult Weather(string area, DateTimeOffset from, DateTimeOffset to) => new(
        WeatherAdvisoryStatus.Available, "heavy_rain", area, "official-provider", "INDOMET bulletin",
        Now.AddHours(-2), Now.AddHours(-1), from, to, null, null);
}
