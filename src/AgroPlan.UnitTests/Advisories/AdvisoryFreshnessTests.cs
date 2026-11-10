using AgroPlan.Application.Abstractions;
using AgroPlan.Infrastructure.Climate;
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

    private static WeatherAdvisoryResult Weather(string area, DateTimeOffset from, DateTimeOffset to) => new(
        WeatherAdvisoryStatus.Available, "heavy_rain", area, "official-provider", "INDOMET bulletin",
        Now.AddHours(-2), Now.AddHours(-1), from, to, null, null);
}
