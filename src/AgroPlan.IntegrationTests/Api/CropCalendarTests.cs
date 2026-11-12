using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgroPlan.Domain.Calendars;
using AgroPlan.Domain.Crops;
using AgroPlan.Domain.Logbook;
using AgroPlan.Domain.Operations;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgroPlan.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class CropCalendarTests(PostgreSqlFixture postgres)
{
    [Fact]
    public async Task Calendar_returns_sourced_estimate_from_validated_template_and_confirmed_operations()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var owner = $"calendar-{Guid.NewGuid():N}";
        Guid farmId; Guid parcelId; Guid cycleId;
        var now = DateTimeOffset.UtcNow;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (farmId, parcelId) = await TestData.SeedFarmAsync(db, owner);
            var crop = await db.CropCatalogEntries.SingleAsync(item => item.Code == "ARROZ", cancellationToken);
            cycleId = Guid.NewGuid();
            db.CropCycles.Add(new CropCycle(cycleId, farmId, parcelId, crop, null, null, null, now));
            db.LogbookEntries.Add(new LogbookEntry(Guid.NewGuid(), farmId, parcelId, cycleId,
                LogbookActivityType.Sowing, now.AddDays(-40), "Confirmed sowing", owner, now.AddDays(-40)));
            db.IrrigationEntries.Add(new IrrigationEntry(Guid.NewGuid(), farmId, parcelId, cycleId,
                IrrigationMethod.Drip, "Test source", now.AddDays(-20), owner, now.AddDays(-20)));
            db.InputApplicationEntries.Add(new InputApplicationEntry(Guid.NewGuid(), farmId, parcelId, cycleId,
                "unverified-product", "unverified-formulation", "ARROZ", "field-use", now.AddDays(-2),
                InputApplicationMethod.Manual, 1m, "L", owner, now.AddDays(-2)));
            db.CropCalendarTemplates.Add(new CropCalendarTemplate(Guid.NewGuid(), crop.Id, "v1", "MIDA approved dataset",
                "2026.2", CropCalendarTemplateStatus.Validated, null, null, now.AddDays(-30), now.AddDays(30),
                30, 45, [new CropCalendarTask("check-water", "growth", "Check irrigation", "Use the sourced schedule.",
                    20, 1, CropCalendarOperationKind.Irrigation)]));
            await db.SaveChangesAsync(cancellationToken);
        }
        TestData.SetBearer(client, owner);

        using var response = await client.GetAsync($"/api/farms/{farmId}/crop-cycles/{cycleId}/calendar", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var body = document.RootElement;
        Assert.Equal("available", body.GetProperty("status").GetString());
        var window = body.GetProperty("estimatedHarvestWindow");
        Assert.Equal(DateOnly.FromDateTime(now.AddDays(-40).UtcDateTime).AddDays(30).ToString("yyyy-MM-dd"),
            window.GetProperty("startsOn").GetString());
        Assert.Equal("MIDA approved dataset", window.GetProperty("source").GetString());
        Assert.Equal("2026.2", window.GetProperty("sourceVersion").GetString());
        Assert.True(body.GetProperty("actualHarvestRequiresValidatedPhi").GetBoolean());
        Assert.Equal("completed", body.GetProperty("tasks")[0].GetProperty("status").GetString());

        using var harvestResponse = await client.PostAsJsonAsync($"/api/farms/{farmId}/harvest-records", new
        {
            parcelId,
            cropCycleId = cycleId,
            harvestedAt = DateTimeOffset.UtcNow,
            quantity = 1m,
            unit = "kg"
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, harvestResponse.StatusCode);
    }

    [Fact]
    public async Task Calendar_returns_unavailable_with_missing_evidence_and_hides_foreign_cycle()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var ownerA = $"calendar-a-{Guid.NewGuid():N}";
        var ownerB = $"calendar-b-{Guid.NewGuid():N}";
        Guid farmA; Guid farmB; Guid parcelA; Guid cycleB;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (farmA, parcelA) = await TestData.SeedFarmAsync(db, ownerA);
            (farmB, var parcelB) = await TestData.SeedFarmAsync(db, ownerB);
            var crop = await db.CropCatalogEntries.SingleAsync(item => item.Code == "ARROZ", cancellationToken);
            cycleB = Guid.NewGuid();
            db.CropCycles.Add(new CropCycle(cycleB, farmB, parcelB, crop, null, null, null, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(cancellationToken);
        }
        TestData.SetBearer(client, ownerA);

        using var missingResponse = await client.GetAsync($"/api/farms/{farmA}/crop-cycles/{Guid.NewGuid()}/calendar", cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        using var foreignResponse = await client.GetAsync($"/api/farms/{farmA}/crop-cycles/{cycleB}/calendar", cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, foreignResponse.StatusCode);

        // A real farm cycle with no validated template or sowing evidence returns explicit gaps.
        Guid cycleA;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            cycleA = Guid.NewGuid();
            // Shared catalog codes may have templates from earlier tests. This fixture
            // specifically requires a crop with no validated calendar evidence.
            var crop = new CropCatalogEntry(Guid.NewGuid(), $"GAP-{Guid.NewGuid():N}", "Missing evidence fixture",
                "Integration fixture", CropCatalogValidationStatus.IdentityOnly, null, null, null);
            db.CropCatalogEntries.Add(crop);
            db.CropCycles.Add(new CropCycle(cycleA, farmA, parcelA, crop, null, null, null, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(cancellationToken);
        }
        using var unavailableResponse = await client.GetAsync($"/api/farms/{farmA}/crop-cycles/{cycleA}/calendar", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, unavailableResponse.StatusCode);
        using var unavailable = JsonDocument.Parse(await unavailableResponse.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal("unavailable", unavailable.RootElement.GetProperty("status").GetString());
        Assert.Null(unavailable.RootElement.GetProperty("estimatedHarvestWindow").GetString());
        Assert.Contains(unavailable.RootElement.GetProperty("missingInputs").EnumerateArray(),
            item => item.GetString() == "validated_crop_calendar_rule");
        Assert.True(unavailable.RootElement.GetProperty("actualHarvestRequiresValidatedPhi").GetBoolean());
    }
}
