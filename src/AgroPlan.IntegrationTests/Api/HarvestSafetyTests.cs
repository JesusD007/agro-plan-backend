using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgroPlan.Domain.Operations;
using AgroPlan.Domain.Safety;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgroPlan.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class HarvestSafetyTests(PostgreSqlFixture postgres)
{
    [Fact]
    public async Task Missing_phi_blocks_harvest_and_persists_no_row()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var owner = $"owner-{Guid.NewGuid():N}";
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        var (farmId, parcelId) = await TestData.SeedFarmAsync(db, owner);
        TestData.SetBearer(client, owner);
        var cycle = await CreateCycle(client, farmId, parcelId);
        await AddApplication(db, farmId, parcelId, cycle, DateTimeOffset.UtcNow.AddDays(-1));

        using var response = await client.PostAsJsonAsync($"/api/farms/{farmId}/harvest-records", new
        {
            parcelId, cropCycleId = cycle, harvestedAt = DateTimeOffset.UtcNow,
            quantity = 100m, unit = "kg"
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("blocked", json.RootElement.GetProperty("status").GetString());
        Assert.NotEmpty(json.RootElement.GetProperty("missingEvidenceReasons").EnumerateArray());
        Assert.Equal(0, await db.HarvestEntries.CountAsync(x => x.CropCycleId == cycle, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Exact_verified_phi_allows_registration_and_cross_farm_is_denied()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var owner = $"owner-{Guid.NewGuid():N}";
        Guid farmId, parcelId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (farmId, parcelId) = await TestData.SeedFarmAsync(db, owner);
        }
        TestData.SetBearer(client, owner);
        var cycle = await CreateCycle(client, farmId, parcelId);
        var appliedAt = DateTimeOffset.UtcNow.AddDays(-10);
        var registration = $"REG-{Guid.NewGuid():N}";
        Guid applicationId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            applicationId = await AddApplication(db, farmId, parcelId, cycle, appliedAt, registration);
            db.ValidatedProductLabels.Add(new(Guid.NewGuid(), registration, "FORM-EC", "ARROZ", "fungicide-use", "DO",
                "MIDA approved product label", "label-v3", appliedAt.AddDays(-30), DateTimeOffset.UtcNow.AddDays(30),
                5, null, 12, true));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var harvestedAt = DateTimeOffset.UtcNow.AddDays(-1);
        using var response = await client.PostAsJsonAsync($"/api/farms/{farmId}/harvest-records", new
        {
            parcelId, cropCycleId = cycle, harvestedAt, quantity = 100m, unit = "kg"
        }, TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Harvest registration response: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains(applicationId.ToString(), json.RootElement.GetProperty("assessmentEvidenceJson").GetString());
        using var crossFarm = await client.GetAsync($"/api/farms/{Guid.NewGuid()}/harvest-records", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, crossFarm.StatusCode);
    }

    [Fact]
    public async Task Verified_records_are_immutable_and_corrections_are_linked()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var owner = $"owner-{Guid.NewGuid():N}";
        Guid farmId, parcelId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (farmId, parcelId) = await TestData.SeedFarmAsync(db, owner);
        }
        TestData.SetBearer(client, owner);
        var cycle = await CreateCycle(client, farmId, parcelId);
        var appliedAt = DateTimeOffset.UtcNow.AddDays(-10);
        var registration = $"REG-{Guid.NewGuid():N}";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            await AddApplication(db, farmId, parcelId, cycle, appliedAt, registration);
            db.ValidatedProductLabels.Add(new(Guid.NewGuid(), registration, "FORM-EC", "ARROZ", "fungicide-use", "DO",
                "MIDA approved product label", "label-v3", appliedAt.AddDays(-30), DateTimeOffset.UtcNow.AddDays(30),
                5, null, 12, true));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var harvestedAt = DateTimeOffset.UtcNow;
        using var append = await client.PostAsJsonAsync($"/api/farms/{farmId}/harvest-records", new
        {
            parcelId, cropCycleId = cycle, harvestedAt, quantity = 100m, unit = "kg"
        }, TestContext.Current.CancellationToken);
        Assert.True(append.StatusCode == HttpStatusCode.Created,
            $"Harvest registration response: {(int)append.StatusCode} {await append.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");
        using var appended = JsonDocument.Parse(await append.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var originalId = appended.RootElement.GetProperty("id").GetGuid();
        using var correction = await client.PostAsJsonAsync($"/api/farms/{farmId}/harvest-records/{originalId}/corrections", new
        {
            reason = "Corrected measured amount", harvestedAt, quantity = 98m, unit = "kg"
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, correction.StatusCode);
        using var corrected = JsonDocument.Parse(await correction.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(originalId, corrected.RootElement.GetProperty("correctsEntryId").GetGuid());
        await using var checkScope = factory.Services.CreateAsyncScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        var original = await checkDb.HarvestEntries.SingleAsync(x => x.Id == originalId, TestContext.Current.CancellationToken);
        checkDb.HarvestEntries.Remove(original);
        await Assert.ThrowsAsync<DbUpdateException>(() => checkDb.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    private static async Task<Guid> CreateCycle(HttpClient client, Guid farmId, Guid parcelId)
    {
        using var response = await client.PostAsJsonAsync($"/api/farms/{farmId}/crop-cycles", new
        {
            parcelId, cropCode = "ARROZ", desiredPlantingDate = (DateOnly?)null, variety = (string?)null, notes = "PHI test"
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return json.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> AddApplication(AgroPlanDbContext db, Guid farmId, Guid parcelId, Guid cycle,
        DateTimeOffset appliedAt, string registration = "REG-42")
    {
        var entry = new InputApplicationEntry(Guid.NewGuid(), farmId, parcelId, cycle, registration, "FORM-EC", "ARROZ",
            "fungicide-use", appliedAt, InputApplicationMethod.Manual, 1m, "L/ha", "producer", DateTimeOffset.UtcNow);
        db.InputApplicationEntries.Add(entry);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return entry.Id;
    }
}
