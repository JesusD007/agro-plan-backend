using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgroPlan.Domain.Operations;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgroPlan.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class IrrigationEntriesTests(PostgreSqlFixture postgres)
{
    [Fact]
    public async Task Append_list_correction_and_cross_farm_scope_keep_irrigation_immutable()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var owner = $"owner-{Guid.NewGuid():N}";
        Guid farmId;
        Guid parcelId;
        Guid foreignParcelId;
        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (farmId, parcelId) = await TestData.SeedFarmAsync(seedDb, owner);
            (_, foreignParcelId) = await TestData.SeedFarmAsync(seedDb, $"owner-{Guid.NewGuid():N}");
        }
        TestData.SetBearer(client, owner);
        var occurredAt = DateTimeOffset.UtcNow.AddDays(-1);
        using var invalidReference = await client.PostAsJsonAsync($"/api/farms/{farmId}/irrigation-entries",
            new { parcelId = foreignParcelId, method = IrrigationMethod.Drip, waterSource = "well", occurredAt });
        Assert.Equal(HttpStatusCode.BadRequest, invalidReference.StatusCode);
        using var append = await client.PostAsJsonAsync($"/api/farms/{farmId}/irrigation-entries",
            new { parcelId, method = IrrigationMethod.Drip, waterSource = "well", occurredAt });
        Assert.Equal(HttpStatusCode.Created, append.StatusCode);
        using var appendJson = JsonDocument.Parse(await append.Content.ReadAsStringAsync());
        var originalId = appendJson.RootElement.GetProperty("id").GetGuid();
        using var correction = await client.PostAsJsonAsync($"/api/farms/{farmId}/irrigation-entries/{originalId}/corrections",
            new { reason = "Source was canal", method = IrrigationMethod.Flood, waterSource = "community canal", occurredAt });
        Assert.Equal(HttpStatusCode.Created, correction.StatusCode);
        using var correctionJson = JsonDocument.Parse(await correction.Content.ReadAsStringAsync());
        Assert.Equal(originalId, correctionJson.RootElement.GetProperty("correctsEntryId").GetGuid());
        using var list = await client.GetAsync($"/api/farms/{farmId}/irrigation-entries");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var crossFarm = await client.GetAsync($"/api/farms/{Guid.NewGuid()}/irrigation-entries");
        Assert.Equal(HttpStatusCode.Forbidden, crossFarm.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        var rows = await db.IrrigationEntries.Where(x => x.FarmId == farmId).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal("well", Assert.Single(rows, x => x.Id == originalId).WaterSource);
        var original = await db.IrrigationEntries.SingleAsync(x => x.Id == originalId);
        db.IrrigationEntries.Remove(original);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
