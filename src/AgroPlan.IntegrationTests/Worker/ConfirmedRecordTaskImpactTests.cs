using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgroPlan.Domain.Logbook;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgroPlan.IntegrationTests.Worker;

[Collection(PostgreSqlCollection.Name)]
public sealed class ConfirmedRecordTaskImpactTests(PostgreSqlFixture postgres)
{
    [Fact]
    public async Task Confirmed_record_and_correction_commit_with_immutable_outbox_events()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString, disableBackgroundServices: true);
        using var client = factory.CreateClient();
        var ownerId = $"owner-{Guid.NewGuid():N}";
        Guid farmId; Guid parcelId;
        await using (var seed = factory.Services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (farmId, parcelId) = await TestData.SeedFarmAsync(db, ownerId);
        }
        TestData.SetBearer(client, ownerId);
        var occurredAt = DateTimeOffset.UtcNow.AddDays(-1);
        using var created = await client.PostAsJsonAsync($"/api/farms/{farmId}/logbook",
            new { parcelId, cropCycleId = (Guid?)null, activityType = LogbookActivityType.Irrigation, occurredAt, details = "Watered north bed" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var sourceId = createdJson.RootElement.GetProperty("id").GetGuid();
        using var corrected = await client.PostAsJsonAsync($"/api/farms/{farmId}/logbook/{sourceId}/corrections",
            new { reason = "Corrected operation detail", occurredAt, details = "Watered east bed" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, corrected.StatusCode);
        using var correctionJson = JsonDocument.Parse(await corrected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var correctionId = correctionJson.RootElement.GetProperty("id").GetGuid();

        await using var verify = factory.Services.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        var events = await verifyDb.OutboxMessages.Where(x => x.EventType == "farm-record.confirmed" &&
            (x.AggregateId == sourceId || x.AggregateId == correctionId)).OrderBy(x => x.AggregateId).ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, events.Length);
        foreach (var message in events)
        {
            using var payload = JsonDocument.Parse(message.PayloadJson);
            Assert.Equal(message.Id, payload.RootElement.GetProperty("eventId").GetGuid());
            Assert.Equal(farmId, payload.RootElement.GetProperty("farmId").GetGuid());
            Assert.Equal(parcelId, payload.RootElement.GetProperty("parcelId").GetGuid());
            Assert.Equal("irrigation", payload.RootElement.GetProperty("operationKind").GetString());
            Assert.Equal(message.AggregateId == sourceId ? 1 : 2, payload.RootElement.GetProperty("sourceRecordVersion").GetInt32());
        }
        Assert.Contains(events, x => x.AggregateId == sourceId);
        Assert.Contains(events, x => x.AggregateId == correctionId);
    }
}
