using System.Net;
using System.Net.Http.Json;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Workforce;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgroPlan.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class TaskScheduleControllerTests(PostgreSqlFixture postgres)
{
    [Fact]
    public async Task Administrator_reschedule_appends_revision_and_calendar_outbox_event()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString, disableBackgroundServices: true);
        using var client = factory.CreateClient();
        var ownerId = $"owner-{Guid.NewGuid():N}";
        var workerId = $"worker-{Guid.NewGuid():N}";
        Guid farmId; Guid taskId;
        await using (var seed = factory.Services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            var seeded = await TestData.SeedFarmAsync(db, ownerId);
            farmId = seeded.FarmId;
            db.Users.Add(new AgroPlan.Infrastructure.Identity.ApplicationUser
            {
                Id = workerId, UserName = $"{workerId}@example.test", NormalizedUserName = $"{workerId}@EXAMPLE.TEST",
                Email = $"{workerId}@example.test", NormalizedEmail = $"{workerId}@EXAMPLE.TEST", EmailConfirmed = true
            });
            db.FarmMemberships.Add(new FarmMembership(farmId, workerId, FarmRole.Worker, DateTimeOffset.UtcNow));
            taskId = Guid.NewGuid();
            db.WorkTasks.Add(new WorkTask(taskId, farmId, workerId, new DateOnly(2026, 11, 20), "Irrigate parcel", false,
                null, null, null, null));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        TestData.SetBearer(client, ownerId);

        using var response = await client.PostAsJsonAsync($"/api/farms/{farmId}/task-schedule/tasks/{taskId}/revisions",
            new { proposedDate = new DateOnly(2026, 11, 21), explanation = "Administrator reschedule" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var verify = factory.Services.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        var task = await verifyDb.WorkTasks.FindAsync([taskId], TestContext.Current.CancellationToken);
        Assert.Equal(new DateOnly(2026, 11, 21), task!.DueDate);
        Assert.True(await verifyDb.OutboxMessages.AnyAsync(x => x.EventType == "task-schedule.changed" && x.AggregateId == taskId,
            TestContext.Current.CancellationToken));
        Assert.True(await verifyDb.OutboxMessages.AnyAsync(x => x.EventType == "task-calendar.sync" && x.AggregateId == taskId,
            TestContext.Current.CancellationToken));
    }
}
