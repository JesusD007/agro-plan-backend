using AgroPlan.Application.Abstractions;

namespace AgroPlan.Infrastructure.Climate;

public sealed class UnavailableWeatherAdvisoryProvider : IWeatherAdvisoryProvider
{
    public Task<WeatherAdvisoryResult> GetAsync(WeatherAdvisoryRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(WeatherAdvisoryResult.Unavailable("weather_source_not_configured"));
    }
}
