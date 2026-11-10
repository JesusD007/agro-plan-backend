using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgroPlan.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class AdvisoriesTests(PostgreSqlFixture postgres)
{
    [Fact]
    public async Task Weather_route_requires_farm_membership_and_returns_explicit_unavailable()
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

            using var priceResponse = await client.GetAsync(
                $"/api/farms/{farmId}/reference-prices?cropId=rice&marketChannel=wholesale&region=Hato%20Mayor");
            Assert.Equal(HttpStatusCode.OK, priceResponse.StatusCode);
            using var price = JsonDocument.Parse(await priceResponse.Content.ReadAsStringAsync());
            Assert.Equal("Unavailable", price.RootElement.GetProperty("status").GetString());
            Assert.Equal("reference_price_source_not_configured", price.RootElement.GetProperty("unavailableReason").GetString());
            Assert.Equal(JsonValueKind.Null, price.RootElement.GetProperty("amount").ValueKind);

            using var invalidCoordinates = await client.GetAsync(
                $"/api/farms/{farmId}/weather-advisories?geographicArea=Hato%20Mayor&latitude=18.7");
            Assert.Equal(HttpStatusCode.BadRequest, invalidCoordinates.StatusCode);
        }

        using var crossFarm = await client.GetAsync(
            $"/api/farms/{Guid.NewGuid()}/weather-advisories?geographicArea=Hato%20Mayor");
        Assert.Equal(HttpStatusCode.Forbidden, crossFarm.StatusCode);
    }

    [Fact]
    public async Task Openapi_lists_typed_advisory_routes_and_production_keeps_docs_disabled()
    {
        await using var developmentFactory = new ApiTestFactory(
            "Host=127.0.0.1;Port=5432;Database=not_used;Username=test;Password=test", disableBackgroundServices: true);
        using var developmentClient = developmentFactory.CreateClient();
        using var documentResponse = await developmentClient.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, documentResponse.StatusCode);
        using var document = JsonDocument.Parse(await documentResponse.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/farms/{farmId}/weather-advisories", out _));
        Assert.True(paths.TryGetProperty("/api/farms/{farmId}/reference-prices", out _));

        await using var productionFactory = new ApiTestFactory(
            "Host=127.0.0.1;Port=5432;Database=not_used;Username=test;Password=test", "Production", disableBackgroundServices: true);
        using var productionClient = productionFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.NotFound, (await productionClient.GetAsync("/openapi/v1.json")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await productionClient.GetAsync("/scalar")).StatusCode);
    }

}
