using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgroPlan.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class AdvisoriesTests(PostgreSqlFixture postgres)
{
    [Fact]
    public async Task Weather_and_price_routes_require_farm_membership_and_return_explicit_unavailable()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var ownerId = $"owner-{Guid.NewGuid():N}";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlan.Infrastructure.Persistence.AgroPlanDbContext>();
            var (farmId, _) = await TestData.SeedFarmAsync(db, ownerId);
            TestData.SetBearer(client, ownerId);

            using var weatherResponse = await client.GetAsync(
                $"/api/farms/{farmId}/weather-advisories?geographicArea=Hato%20Mayor");
            Assert.Equal(HttpStatusCode.OK, weatherResponse.StatusCode);
            using var weather = JsonDocument.Parse(await weatherResponse.Content.ReadAsStringAsync());
            Assert.Equal("Unavailable", weather.RootElement.GetProperty("status").GetString());
            Assert.Equal("weather_source_not_configured", weather.RootElement.GetProperty("unavailableReason").GetString());
            Assert.False(weather.RootElement.TryGetProperty("hazardKind", out var hazard) && hazard.ValueKind != JsonValueKind.Null);

            using var invalidCoordinates = await client.GetAsync(
                $"/api/farms/{farmId}/weather-advisories?geographicArea=Hato%20Mayor&latitude=18.7");
            Assert.Equal(HttpStatusCode.BadRequest, invalidCoordinates.StatusCode);
        }

        using var crossFarm = await client.GetAsync(
            $"/api/farms/{Guid.NewGuid()}/weather-advisories?geographicArea=Hato%20Mayor");
        Assert.Equal(HttpStatusCode.Forbidden, crossFarm.StatusCode);
    }

}
