using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgroPlan.Application.Workforce;
using AgroPlan.Domain.Crops;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Logbook;
using AgroPlan.Domain.Planning;
using AgroPlan.Domain.Workforce;
using AgroPlan.Infrastructure.Identity;
using AgroPlan.Infrastructure.Messaging;
using AgroPlan.Infrastructure.Messaging.Events;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using AgroPlan.Worker.Jobs;

namespace AgroPlan.IntegrationTests.Worker;

[Collection(PostgreSqlCollection.Name)]
public sealed class ConfirmedRecordTaskImpactTests(PostgreSqlFixture postgres)
{
    [Fact]
    public async Task Consumer_creates_one_unapplied_source_backed_proposal_and_ignores_ambiguous_replays()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString, disableBackgroundServices: true);
        var ownerId = $"owner-{Guid.NewGuid():N}";
        var workerId = $"worker-{Guid.NewGuid():N}";
        Guid farmId; Guid parcelId; Guid cycleId; Guid taskId;
        await using (var seed = factory.Services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (farmId, parcelId) = await TestData.SeedFarmAsync(db, ownerId);
            var crop = await db.CropCatalogEntries.SingleAsync(x => x.Code == "YUCA", TestContext.Current.CancellationToken);
            var now = DateTimeOffset.UtcNow;
            cycleId = Guid.NewGuid(); taskId = Guid.NewGuid();
            db.Users.Add(new ApplicationUser { Id = workerId, UserName = workerId, NormalizedUserName = workerId.ToUpperInvariant(),
                Email = $"{workerId}@example.test", NormalizedEmail = $"{workerId}@EXAMPLE.TEST", EmailConfirmed = true });
            db.FarmMemberships.Add(new FarmMembership(farmId, workerId, FarmRole.Worker, now));
            db.CropCycles.Add(new CropCycle(cycleId, farmId, parcelId, crop, null, null, null, now));
            db.WorkTasks.Add(new WorkTask(taskId, farmId, workerId, new DateOnly(2026, 11, 20), "Irrigate", false,
                null, null, parcelId, cycleId, "irrigation"));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var occurrence = DateTimeOffset.UtcNow.AddDays(-2);
        var sourceId = Guid.NewGuid(); var eventId = Guid.NewGuid();
        var evt = new FarmRecordConfirmed(eventId, farmId, parcelId, cycleId, "irrigation", sourceId, 1,
            DateOnly.FromDateTime(occurrence.UtcDateTime));
        var webJson = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        await using (var seedEvent = factory.Services.CreateAsyncScope())
        {
            var db = seedEvent.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            db.LogbookEntries.Add(new LogbookEntry(sourceId, farmId, parcelId, cycleId, LogbookActivityType.Irrigation,
                occurrence, "Irrigated north plot", ownerId, DateTimeOffset.UtcNow));
            db.OutboxMessages.Add(new OutboxMessage(eventId, sourceId, "farm-record.confirmed", JsonSerializer.Serialize(evt, webJson), DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var consumer = new ConfirmedRecordTaskImpactConsumer(factory.Services.GetRequiredService<RabbitMqConnection>(),
            factory.Services.GetRequiredService<IServiceScopeFactory>(), new WorkTaskImpactProjector(),
            NullLogger<ConfirmedRecordTaskImpactConsumer>.Instance);
        var body = JsonSerializer.SerializeToUtf8Bytes(evt, webJson);
        await consumer.ProcessMessageAsync(eventId, body, TestContext.Current.CancellationToken);
        await consumer.ProcessMessageAsync(eventId, body, TestContext.Current.CancellationToken);
        await using (var verify = factory.Services.CreateAsyncScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            var proposal = await db.TaskScheduleRevisions.SingleAsync(x => x.SourceEventId == eventId, TestContext.Current.CancellationToken);
            Assert.Equal(TaskScheduleRevisionStatus.Proposed, proposal.Status);
            Assert.Equal(new DateOnly(2026, 11, 20), proposal.PreviousDate);
            Assert.Equal(DateOnly.FromDateTime(occurrence.UtcDateTime), proposal.ProposedDate);
            Assert.Equal(1, await db.TaskScheduleRevisions.CountAsync(x => x.SourceEventId == eventId, TestContext.Current.CancellationToken));
            Assert.Equal(new DateOnly(2026, 11, 20), (await db.WorkTasks.SingleAsync(x => x.Id == taskId, TestContext.Current.CancellationToken)).DueDate);

            var ambiguousTask = new WorkTask(Guid.NewGuid(), farmId, workerId, new DateOnly(2026, 11, 21), "Irrigate", false,
                null, null, parcelId, cycleId, "irrigation");
            db.WorkTasks.Add(ambiguousTask);
            var ambiguousSourceId = Guid.NewGuid(); var ambiguousEventId = Guid.NewGuid();
            var ambiguousTime = occurrence.AddHours(1);
            var ambiguousEvent = new FarmRecordConfirmed(ambiguousEventId, farmId, parcelId, cycleId, "irrigation",
                ambiguousSourceId, 1, DateOnly.FromDateTime(ambiguousTime.UtcDateTime));
            db.LogbookEntries.Add(new LogbookEntry(ambiguousSourceId, farmId, parcelId, cycleId,
                LogbookActivityType.Irrigation, ambiguousTime, "Second irrigation record", ownerId, DateTimeOffset.UtcNow));
            db.OutboxMessages.Add(new OutboxMessage(ambiguousEventId, ambiguousSourceId, "farm-record.confirmed",
                JsonSerializer.Serialize(ambiguousEvent, webJson), DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await consumer.ProcessMessageAsync(ambiguousEventId, JsonSerializer.SerializeToUtf8Bytes(ambiguousEvent, webJson),
                TestContext.Current.CancellationToken);
            Assert.False(await db.TaskScheduleRevisions.AnyAsync(x => x.SourceEventId == ambiguousEventId, TestContext.Current.CancellationToken));
        }
    }

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
