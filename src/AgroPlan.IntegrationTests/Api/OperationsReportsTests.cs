using System.Net;
using System.Text.Json;
using AgroPlan.Domain.AdvisoryAccess;
using AgroPlan.Domain.Crops;
using AgroPlan.Domain.Logbook;
using AgroPlan.Domain.Operations;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgroPlan.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class OperationsReportsTests(PostgreSqlFixture postgres)
{
    [Fact]
    public async Task Date_filtered_owner_and_advisor_csv_keep_superseded_expense_inactive()
    {
        var ct = TestContext.Current.CancellationToken;
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var ownerClient = factory.CreateClient();
        using var advisorClient = factory.CreateClient();
        var owner = $"reports-correction-owner-{Guid.NewGuid():N}";
        var advisor = $"reports-correction-advisor-{Guid.NewGuid():N}";
        Guid farmId; Guid parcelId; Guid cycleId; Guid originalId;
        var now = DateTimeOffset.UtcNow;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (farmId, parcelId) = await TestData.SeedFarmAsync(db, owner);
            var crop = await db.CropCatalogEntries.SingleAsync(x => x.Code == "ARROZ", ct);
            cycleId = Guid.NewGuid();
            db.CropCycles.Add(new CropCycle(cycleId, farmId, parcelId, crop, new DateOnly(2026, 1, 2), null, null, now));
            originalId = Guid.NewGuid();
            db.ExpenseEntries.Add(new ExpenseEntry(originalId, farmId, parcelId, cycleId, OperationCostCategory.Material,
                now.AddDays(-5), "Original expense", 10m, "DOP", null, null, owner, now.AddDays(-5)));
            db.ExpenseEntries.Add(new ExpenseEntry(Guid.NewGuid(), farmId, parcelId, cycleId, OperationCostCategory.Material,
                now.AddDays(-1), "Corrected expense", 12m, "DOP", null, null, owner, now.AddDays(-1), originalId, "Invoice correction"));
            db.Users.Add(new AgroPlan.Infrastructure.Identity.ApplicationUser
            {
                Id = advisor,
                UserName = $"{advisor}@example.test",
                NormalizedUserName = $"{advisor}@EXAMPLE.TEST",
                Email = $"{advisor}@example.test",
                NormalizedEmail = $"{advisor}@EXAMPLE.TEST",
                EmailConfirmed = true
            });
            db.AdvisorAccessGrants.Add(new AdvisorAccessGrant(Guid.NewGuid(), farmId, advisor,
                [AdvisorReportScope.Costs], owner, "Cost review", now.AddDays(-1), now.AddDays(14), now));
            await db.SaveChangesAsync(ct);
        }
        TestData.SetBearer(ownerClient, owner);
        TestData.SetBearer(advisorClient, advisor);
        var from = Uri.EscapeDataString(now.AddDays(-10).ToString("O"));
        var to = Uri.EscapeDataString(now.AddDays(-2).ToString("O"));

        using var ownerCsv = await ownerClient.GetAsync($"/api/farms/{farmId}/reports/costs.csv?cropCycleId={cycleId}&from={from}&to={to}", ct);
        Assert.Equal(HttpStatusCode.OK, ownerCsv.StatusCode);
        var ownerText = await ownerCsv.Content.ReadAsStringAsync(ct);
        var ownerRow = Assert.Single(ownerText.Split('\n').Where(line => line.Contains(originalId.ToString(), StringComparison.Ordinal)));
        Assert.Contains("\"false\"", ownerRow);

        using var advisorCsv = await advisorClient.GetAsync($"/api/farms/{farmId}/advisor-reports/costs.csv?cropCycleId={cycleId}&from={from}&to={to}", ct);
        Assert.Equal(HttpStatusCode.OK, advisorCsv.StatusCode);
        var advisorText = await advisorCsv.Content.ReadAsStringAsync(ct);
        var advisorRow = Assert.Single(advisorText.Split('\n').Where(line => line.Contains(originalId.ToString(), StringComparison.Ordinal)));
        Assert.Contains("\"false\"", advisorRow);
    }

    [Fact]
    public async Task Costs_and_production_are_farm_scoped_traceable_and_keep_units_and_currency()
    {
        var ct = TestContext.Current.CancellationToken;
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var owner = $"reports-{Guid.NewGuid():N}";
        Guid farmId; Guid parcelId; Guid cycleId; Guid expenseId; Guid harvestId;
        var now = DateTimeOffset.UtcNow;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (farmId, parcelId) = await TestData.SeedFarmAsync(db, owner);
            var crop = await db.CropCatalogEntries.SingleAsync(x => x.Code == "ARROZ", ct);
            cycleId = Guid.NewGuid();
            db.CropCycles.Add(new CropCycle(cycleId, farmId, parcelId, crop, new DateOnly(2026, 1, 2), null, null, now));
            db.LogbookEntries.Add(new LogbookEntry(Guid.NewGuid(), farmId, parcelId, cycleId, LogbookActivityType.Sowing,
                now.AddDays(-30), "Confirmed planting", owner, now.AddDays(-30)));
            expenseId = Guid.NewGuid();
            db.ExpenseEntries.Add(new ExpenseEntry(expenseId, farmId, parcelId, cycleId, OperationCostCategory.Material,
                now.AddDays(-1), "=SUM(A1:A2)", 12.5m, "DOP", 3m, "kg", owner, now.AddDays(-1)));
            harvestId = Guid.NewGuid();
            db.HarvestEntries.Add(new HarvestEntry(harvestId, farmId, parcelId, cycleId, now, 8m, "kg", owner, now, "{\"status\":\"eligible\"}"));
            await db.SaveChangesAsync(ct);
        }
        TestData.SetBearer(client, owner);

        using var costsResponse = await client.GetAsync($"/api/farms/{farmId}/reports/costs?cropCycleId={cycleId}", ct);
        Assert.Equal(HttpStatusCode.OK, costsResponse.StatusCode);
        using var costsJson = JsonDocument.Parse(await costsResponse.Content.ReadAsStringAsync(ct));
        var costs = costsJson.RootElement;
        Assert.Equal(expenseId, costs.GetProperty("currencyTotals")[0].GetProperty("sourceEntryIds")[0].GetGuid());
        Assert.Equal("DOP", costs.GetProperty("currencyTotals")[0].GetProperty("currencyCode").GetString());
        Assert.Equal(12.5m, costs.GetProperty("currencyTotals")[0].GetProperty("amount").GetDecimal());
        Assert.Equal("not_computable", costs.GetProperty("costPerQuintal").GetProperty("status").GetString());

        using var productionResponse = await client.GetAsync($"/api/farms/{farmId}/reports/production", ct);
        Assert.Equal(HttpStatusCode.OK, productionResponse.StatusCode);
        using var productionJson = JsonDocument.Parse(await productionResponse.Content.ReadAsStringAsync(ct));
        var production = Assert.Single(productionJson.RootElement.GetProperty("cropCycles").EnumerateArray());
        Assert.Equal(cycleId, production.GetProperty("cropCycleId").GetGuid());
        Assert.Equal("ARROZ", production.GetProperty("cropCode").GetString());
        Assert.Equal(1.5m, production.GetProperty("areaHectares").GetDecimal());
        Assert.Equal(harvestId, production.GetProperty("harvestedQuantities")[0].GetProperty("sourceHarvestIds")[0].GetGuid());

        using var foreignResponse = await client.GetAsync($"/api/farms/{Guid.NewGuid()}/reports/production", ct);
        Assert.Equal(HttpStatusCode.Forbidden, foreignResponse.StatusCode);
    }

    [Fact]
    public async Task Csv_exports_include_provenance_and_escape_formula_cells()
    {
        var ct = TestContext.Current.CancellationToken;
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var owner = $"reports-csv-{Guid.NewGuid():N}";
        Guid farmId; Guid parcelId; Guid cycleId;
        var now = DateTimeOffset.UtcNow;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (farmId, parcelId) = await TestData.SeedFarmAsync(db, owner);
            var crop = await db.CropCatalogEntries.SingleAsync(x => x.Code == "ARROZ", ct);
            cycleId = Guid.NewGuid();
            db.CropCycles.Add(new CropCycle(cycleId, farmId, parcelId, crop, new DateOnly(2026, 1, 2), null, null, now));
            db.LogbookEntries.Add(new LogbookEntry(Guid.NewGuid(), farmId, parcelId, cycleId, LogbookActivityType.Sowing,
                now.AddDays(-30), "Confirmed planting", owner, now.AddDays(-30)));
            db.ExpenseEntries.Add(new ExpenseEntry(Guid.NewGuid(), farmId, parcelId, cycleId, OperationCostCategory.Material,
                now, "=HYPERLINK(\"https://bad.example\")", 9m, "DOP", 1m, "kg", owner, now));
            db.HarvestEntries.Add(new HarvestEntry(Guid.NewGuid(), farmId, parcelId, cycleId, now, 6m, "sacks", owner, now, "{}"));
            await db.SaveChangesAsync(ct);
        }
        TestData.SetBearer(client, owner);

        using var costs = await client.GetAsync($"/api/farms/{farmId}/reports/costs.csv?cropCycleId={cycleId}", ct);
        Assert.Equal(HttpStatusCode.OK, costs.StatusCode);
        var csv = await costs.Content.ReadAsStringAsync(ct);
        Assert.Contains("'", csv);
        Assert.Contains("HYPERLINK", csv);
        Assert.Contains("DOP", csv);

        using var production = await client.GetAsync($"/api/farms/{farmId}/reports/production.csv", ct);
        Assert.Equal(HttpStatusCode.OK, production.StatusCode);
        var productionCsv = await production.Content.ReadAsStringAsync(ct);
        Assert.Contains("crop_code", productionCsv);
        Assert.Contains("area_hectares", productionCsv);
        Assert.Contains("planting_date", productionCsv);
        Assert.Contains("sacks", productionCsv);
    }
}
