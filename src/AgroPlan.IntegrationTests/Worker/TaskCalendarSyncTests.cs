using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgroPlan.Application.Abstractions;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Workforce;
using AgroPlan.Infrastructure.Messaging;
using AgroPlan.Infrastructure.Messaging.Events;
using AgroPlan.Infrastructure.Persistence;
using AgroPlan.Worker.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgroPlan.IntegrationTests.Worker;

[Collection(PostgreSqlCollection.Name)]
public sealed class TaskCalendarSyncTests(PostgreSqlFixture postgres)
{
    [Fact]
    public async Task Delivery_failures_are_retryable_per_operation_and_never_revert_saved_tasks()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString, disableBackgroundServices: true);
        using var client = factory.CreateClient();
        var ownerId = $"owner-{Guid.NewGuid():N}";
        var workerId = $"worker-{Guid.NewGuid():N}";
        Guid farmId; Guid firstTaskId; Guid secondTaskId;
        await using (var seed = factory.Services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            farmId = (await TestData.SeedFarmAsync(db, ownerId)).FarmId;
            db.Users.Add(new AgroPlan.Infrastructure.Identity.ApplicationUser
            {
                Id = workerId, UserName = $"{workerId}@example.test", NormalizedUserName = $"{workerId}@EXAMPLE.TEST",
                Email = $"{workerId}@example.test", NormalizedEmail = $"{workerId}@EXAMPLE.TEST", EmailConfirmed = true
            });
            db.FarmMemberships.Add(new FarmMembership(farmId, workerId, FarmRole.Worker, DateTimeOffset.UtcNow));
            firstTaskId = Guid.NewGuid(); secondTaskId = Guid.NewGuid();
            db.WorkTasks.AddRange(
                new WorkTask(firstTaskId, farmId, workerId, new DateOnly(2026, 11, 20), "First", false, null, null, null, null),
                new WorkTask(secondTaskId, farmId, workerId, new DateOnly(2026, 11, 20), "Second", false, null, null, null, null));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        TestData.SetBearer(client, ownerId);
        var rescheduleDate = new DateOnly(2026, 11, 21);
        using var firstResponse = await client.PostAsJsonAsync($"/api/farms/{farmId}/task-schedule/tasks/{firstTaskId}/revisions",
            new { proposedDate = rescheduleDate, explanation = "First schedule change" }, TestContext.Current.CancellationToken);
        using var secondResponse = await client.PostAsJsonAsync($"/api/farms/{farmId}/task-schedule/tasks/{secondTaskId}/revisions",
            new { proposedDate = rescheduleDate, explanation = "Independent schedule change" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        var deliveries = await ReadCalendarEvents(factory, firstTaskId, secondTaskId);
        var provider = new SequencedCalendar(false, true, true);
        var consumer = new TaskCalendarSyncConsumer(factory.Services.GetRequiredService<RabbitMqConnection>(),
            factory.Services.GetRequiredService<IServiceScopeFactory>(), provider, NullLogger<TaskCalendarSyncConsumer>.Instance);

        await consumer.ProcessMessageAsync(deliveries[firstTaskId].Id,
            JsonSerializer.SerializeToUtf8Bytes(JsonSerializer.Deserialize<TaskCalendarSyncRequested>(deliveries[firstTaskId].PayloadJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!, new JsonSerializerOptions(JsonSerializerDefaults.Web)), TestContext.Current.CancellationToken);
        await consumer.ProcessMessageAsync(deliveries[secondTaskId].Id,
            JsonSerializer.SerializeToUtf8Bytes(JsonSerializer.Deserialize<TaskCalendarSyncRequested>(deliveries[secondTaskId].PayloadJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!, new JsonSerializerOptions(JsonSerializerDefaults.Web)), TestContext.Current.CancellationToken);
        await consumer.ProcessMessageAsync(deliveries[secondTaskId].Id,
            JsonSerializer.SerializeToUtf8Bytes(JsonSerializer.Deserialize<TaskCalendarSyncRequested>(deliveries[secondTaskId].PayloadJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!, new JsonSerializerOptions(JsonSerializerDefaults.Web)), TestContext.Current.CancellationToken);

        using var pendingResponse = await client.GetAsync($"/api/farms/{farmId}/task-schedule/tasks/{firstTaskId}/calendar-delivery",
            TestContext.Current.CancellationToken);
        using var pendingJson = JsonDocument.Parse(await pendingResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("retryable", pendingJson.RootElement[0].GetProperty("state").GetString());

        await using (var retryScope = factory.Services.CreateAsyncScope())
        {
            var retryDb = retryScope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            await retryDb.CalendarDeliveryStatuses.Where(x => x.WorkTaskId == firstTaskId)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.NextAttemptAt, DateTimeOffset.UtcNow.AddSeconds(-1)),
                    TestContext.Current.CancellationToken);
        }
        await consumer.ProcessDueRetriesAsync(TestContext.Current.CancellationToken);

        await using var verify = factory.Services.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        var firstTask = await verifyDb.WorkTasks.SingleAsync(x => x.Id == firstTaskId, TestContext.Current.CancellationToken);
        var firstStatus = await verifyDb.CalendarDeliveryStatuses.SingleAsync(x => x.WorkTaskId == firstTaskId, TestContext.Current.CancellationToken);
        var secondStatus = await verifyDb.CalendarDeliveryStatuses.SingleAsync(x => x.WorkTaskId == secondTaskId, TestContext.Current.CancellationToken);
        Assert.Equal(rescheduleDate, firstTask.DueDate);
        Assert.Equal(CalendarDeliveryState.Succeeded, firstStatus.State);
        Assert.Equal(CalendarDeliveryState.Succeeded, secondStatus.State);
        Assert.Equal(1, secondStatus.AttemptCount);
        Assert.Equal(2, firstStatus.AttemptCount);
        Assert.Equal(3, provider.Calls);

        using var statusResponse = await client.GetAsync($"/api/farms/{farmId}/task-schedule/tasks/{firstTaskId}/calendar-delivery",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        using var statusJson = JsonDocument.Parse(await statusResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("succeeded", statusJson.RootElement[0].GetProperty("state").GetString());
    }

    private static async Task<Dictionary<Guid, AgroPlan.Domain.Planning.OutboxMessage>> ReadCalendarEvents(ApiTestFactory factory,
        Guid firstTaskId, Guid secondTaskId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        return await db.OutboxMessages.Where(x => x.EventType == "task-calendar.sync" &&
                (x.AggregateId == firstTaskId || x.AggregateId == secondTaskId))
            .ToDictionaryAsync(x => x.AggregateId, TestContext.Current.CancellationToken);
    }

    private sealed class SequencedCalendar(params bool[] outcomes) : IExternalWorkCalendar
    {
        private int _index;
        public int Calls => _index;
        public Task<ExternalCalendarDeliveryResult> UpsertAsync(ExternalWorkCalendarEntry entry, CancellationToken cancellationToken)
        {
            var succeeded = outcomes[_index++];
            return Task.FromResult(succeeded
                ? new ExternalCalendarDeliveryResult(true, false, null)
                : new ExternalCalendarDeliveryResult(false, true, "PROVIDER_UNAVAILABLE"));
        }
    }
}
