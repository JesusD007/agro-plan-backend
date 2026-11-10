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
public sealed class ExpenseEntriesTests(PostgreSqlFixture postgres)
{
    [Fact]
    public async Task Append_list_correction_and_cross_farm_authorization_preserve_immutable_history()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var ownerId = $"owner-{Guid.NewGuid():N}";
        var (farmId, parcelId) = await SeedFarmAsync(factory, ownerId);
        TestData.SetBearer(client, ownerId);
        var occurredAt = DateTimeOffset.UtcNow.AddDays(-1);

        using var appendResponse = await client.PostAsJsonAsync($"/api/farms/{farmId}/expenses", new
        {
            parcelId, cropCycleId = (Guid?)null, category = OperationCostCategory.Material,
            occurredAt, description = "Fertilizer", amount = 150.25m, currencyCode = "DOP",
            quantity = 10m, unit = "kg"
        });
        Assert.Equal(HttpStatusCode.Created, appendResponse.StatusCode);
        using var appendJson = JsonDocument.Parse(await appendResponse.Content.ReadAsStringAsync());
        var originalId = appendJson.RootElement.GetProperty("id").GetGuid();

        using var correctionResponse = await client.PostAsJsonAsync(
            $"/api/farms/{farmId}/expenses/{originalId}/corrections", new
            {
                reason = "Corrected receipt amount", occurredAt, description = "Fertilizer",
                amount = 175m, currencyCode = "DOP", quantity = 10m, unit = "kg"
            });
        Assert.Equal(HttpStatusCode.Created, correctionResponse.StatusCode);
        using var correctionJson = JsonDocument.Parse(await correctionResponse.Content.ReadAsStringAsync());
        var correctionId = correctionJson.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(originalId, correctionJson.RootElement.GetProperty("correctsEntryId").GetGuid());

        using var listResponse = await client.GetAsync($"/api/farms/{farmId}/expenses");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using (var listJson = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync()))
            Assert.Equal(2, listJson.RootElement.GetArrayLength());

        using var crossFarmResponse = await client.GetAsync($"/api/farms/{Guid.NewGuid()}/expenses");
        Assert.Equal(HttpStatusCode.Forbidden, crossFarmResponse.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            var rows = await db.ExpenseEntries.Where(item => item.FarmId == farmId).ToListAsync();
            var original = Assert.Single(rows, item => item.Id == originalId);
            var correction = Assert.Single(rows, item => item.Id == correctionId);
            Assert.Equal(150.25m, original.Amount);
            Assert.Equal("DOP", original.CurrencyCode);
            Assert.Equal(10m, original.Quantity);
            Assert.Equal("kg", original.Unit);
            Assert.Equal(175m, correction.Amount);
            Assert.Equal(original.Id, correction.CorrectsEntryId);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            var original = await db.ExpenseEntries.SingleAsync(item => item.Id == originalId);
            db.Entry(original).Property(item => item.Description).CurrentValue = "mutated";
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            var original = await db.ExpenseEntries.SingleAsync(item => item.Id == originalId);
            db.ExpenseEntries.Remove(original);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            Assert.Equal(2, await db.ExpenseEntries.CountAsync(item => item.FarmId == farmId));
            Assert.Equal("Fertilizer", (await db.ExpenseEntries.SingleAsync(item => item.Id == originalId)).Description);
        }
    }

    [Fact]
    public async Task Append_rejects_missing_currency_and_unpaired_quantity_unit()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var ownerId = $"owner-{Guid.NewGuid():N}";
        var (farmId, parcelId) = await SeedFarmAsync(factory, ownerId);
        TestData.SetBearer(client, ownerId);
        var request = new
        {
            parcelId, category = OperationCostCategory.Expense, occurredAt = DateTimeOffset.UtcNow,
            description = "Seeds", amount = 20m, currencyCode = " ", quantity = (decimal?)null, unit = "kg"
        };
        using var response = await client.PostAsJsonAsync($"/api/farms/{farmId}/expenses", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<(Guid FarmId, Guid ParcelId)> SeedFarmAsync(ApiTestFactory factory, string ownerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        return await TestData.SeedFarmAsync(db, ownerId);
    }
}
