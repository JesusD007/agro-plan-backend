using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgroPlan.Infrastructure.Persistence;
using Xunit;
using Microsoft.Extensions.DependencyInjection;

namespace AgroPlan.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class CropCycleAndPlanningTests(PostgreSqlFixture postgres)
{
    [Fact]
    public async Task Catalog_keeps_only_approved_crop_identities_and_rejects_cross_farm_parcels()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var ownerA = $"owner-{Guid.NewGuid():N}";
        var ownerB = $"owner-{Guid.NewGuid():N}";
        Guid farmA;
        Guid parcelA;
        Guid parcelB;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (farmA, parcelA) = await TestData.SeedFarmAsync(db, ownerA);
            (_, parcelB) = await TestData.SeedFarmAsync(db, ownerB);
        }
        TestData.SetBearer(client, ownerA);

        using var catalogResponse = await client.GetAsync($"/api/farms/{farmA}/crop-cycles/catalog");
        Assert.Equal(HttpStatusCode.OK, catalogResponse.StatusCode);
        using var catalog = JsonDocument.Parse(await catalogResponse.Content.ReadAsStringAsync());
        var crops = catalog.RootElement.EnumerateArray().ToArray();
        Assert.Equal(["ARROZ", "BATATA", "HABICHUELA", "PLATANO", "YUCA"],
            crops.Select(crop => crop.GetProperty("code").GetString()).OrderBy(code => code, StringComparer.Ordinal));
        Assert.All(crops, crop =>
        {
            Assert.Equal("IdentityOnly", crop.GetProperty("validationStatus").GetString());
            Assert.Empty(crop.GetProperty("validatedParameters").EnumerateArray());
            Assert.NotEmpty(crop.GetProperty("missingParameters").EnumerateArray());
        });

        using var foreignParcelResponse = await client.PostAsJsonAsync($"/api/farms/{farmA}/crop-cycles", new
        {
            parcelId = parcelB,
            cropCode = "ARROZ",
            desiredPlantingDate = (DateOnly?)null,
            variety = (string?)null,
            notes = (string?)null
        });
        Assert.Equal(HttpStatusCode.BadRequest, foreignParcelResponse.StatusCode);

        using var validCycleResponse = await client.PostAsJsonAsync($"/api/farms/{farmA}/crop-cycles", new
        {
            parcelId = parcelA,
            cropCode = "ARROZ",
            desiredPlantingDate = (DateOnly?)null,
            variety = (string?)null,
            notes = "test cycle"
        });
        Assert.Equal(HttpStatusCode.Created, validCycleResponse.StatusCode);
        using var cycle = JsonDocument.Parse(await validCycleResponse.Content.ReadAsStringAsync());
        Assert.Equal("ARROZ", cycle.RootElement.GetProperty("crop").GetProperty("code").GetString());
        Assert.Empty(cycle.RootElement.GetProperty("crop").GetProperty("validatedParameters").EnumerateArray());
    }
}
