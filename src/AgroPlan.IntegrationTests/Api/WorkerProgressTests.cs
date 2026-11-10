using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Planning;
using AgroPlan.Domain.Workforce;
using AgroPlan.Infrastructure.Messaging;
using AgroPlan.Infrastructure.Messaging.Events;
using AgroPlan.Infrastructure.Identity;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using AgroPlan.Worker.Jobs;
using Xunit;

namespace AgroPlan.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class WorkerProgressTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task Only_persisted_verified_event_awards_once_with_objective_evidence_bonus()
    {
        database.EnsureAvailable();
        var ct = TestContext.Current.CancellationToken;
        var ownerId = $"ledger-owner-{Guid.NewGuid():N}";
        var adminId = $"ledger-admin-{Guid.NewGuid():N}";
        var workerId = $"ledger-worker-{Guid.NewGuid():N}";
        Guid farmId;
        var taskId = Guid.NewGuid();
        var outboxId = Guid.NewGuid();
        var verifiedAt = DateTimeOffset.UtcNow;
        WorkTask task;
        WorkTaskVerified evt;
        await using (var db = database.CreateDbContext())
        {
            (farmId, _) = await TestData.SeedFarmAsync(db, ownerId, adminId, withParcel: false);
            db.Users.Add(User(workerId));
            db.FarmMemberships.Add(new FarmMembership(farmId, workerId, FarmRole.Worker, DateTimeOffset.UtcNow));
            var settings = new FarmWorkerSettings(farmId);
            settings.ConfigureGoals(15, 100);
            db.FarmWorkerSettings.Add(settings);
            task = new WorkTask(taskId, farmId, workerId, DateOnly.FromDateTime(DateTime.UtcNow), "Pick tomatoes",
                true, null, null, null, null, pointsForVerification: 10, completeEvidenceBonusPoints: 5);
            task.Start();
            task.Submit(verifiedAt.AddMinutes(-1));
            task.Verify(verifiedAt);
            db.WorkTasks.Add(task);
            evt = new WorkTaskVerified(farmId, taskId, workerId, null, null, verifiedAt);
            db.OutboxMessages.Add(new OutboxMessage(outboxId, taskId, "work-task.verified", JsonSerializer.Serialize(evt), verifiedAt));
            db.TaskEvidenceSubmissions.Add(new TaskEvidenceSubmission(Guid.NewGuid(), farmId, taskId, workerId, 1,
                null, Guid.NewGuid(), "staging/test", "image/png", 512, false, null, false, null,
                verifiedAt.AddMinutes(-3), verifiedAt.AddMinutes(5)));
            await db.SaveChangesAsync(ct);
            var evidence = await db.TaskEvidenceSubmissions.SingleAsync(item => item.WorkTaskId == taskId, ct);
            evidence.Finalize("farms/test/evidence.png", 512, verifiedAt.AddMinutes(-2));
            await db.SaveChangesAsync(ct);
        }

        await using var factory = new ApiTestFactory(database.ConnectionString);
        var connection = new RabbitMqConnection(new ConfigurationBuilder().Build(), NullLogger<RabbitMqConnection>.Instance);
        using var consumer = new VerifiedWorkRewardsConsumer(connection,
            factory.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<VerifiedWorkRewardsConsumer>.Instance);
        await consumer.ProcessMessageAsync(outboxId, JsonSerializer.SerializeToUtf8Bytes(evt), ct);
        await consumer.ProcessMessageAsync(outboxId, JsonSerializer.SerializeToUtf8Bytes(evt), ct);
        await using (var db = database.CreateDbContext())
        {
            var awards = await db.VerifiedWorkRewards.Where(item => item.FarmId == farmId && item.WorkTaskId == taskId).ToArrayAsync(ct);
            Assert.Single(awards, item => item.Kind == VerifiedWorkRewardKind.Points && item.Points == 15);
            Assert.Single(awards, item => item.Kind == VerifiedWorkRewardKind.Badge);
        }

        var unverifiedTask = new WorkTask(Guid.NewGuid(), farmId, workerId, DateOnly.FromDateTime(DateTime.UtcNow),
            "Unverified work", false, null, null, null, null);
        var unverifiedEvent = new WorkTaskVerified(farmId, unverifiedTask.Id, workerId, null, null, verifiedAt);
        var unverifiedOutboxId = Guid.NewGuid();
        await using (var db = database.CreateDbContext())
        {
            db.WorkTasks.Add(unverifiedTask);
            db.OutboxMessages.Add(new OutboxMessage(unverifiedOutboxId, unverifiedTask.Id, "work-task.verified",
                JsonSerializer.Serialize(unverifiedEvent), verifiedAt));
            await db.SaveChangesAsync(ct);
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.ProcessMessageAsync(unverifiedOutboxId,
            JsonSerializer.SerializeToUtf8Bytes(unverifiedEvent), ct));
    }

    [Fact]
    public async Task Progress_history_is_private_and_rankings_are_disabled_by_default()
    {
        database.EnsureAvailable();
        var ct = TestContext.Current.CancellationToken;
        var ownerId = $"progress-owner-{Guid.NewGuid():N}";
        var adminId = $"progress-admin-{Guid.NewGuid():N}";
        var workerId = $"progress-worker-{Guid.NewGuid():N}";
        var peerId = $"progress-peer-{Guid.NewGuid():N}";
        Guid farmId;
        await using (var db = database.CreateDbContext())
        {
            (farmId, _) = await TestData.SeedFarmAsync(db, ownerId, adminId, withParcel: false);
            foreach (var userId in new[] { workerId, peerId })
            {
                db.Users.Add(User(userId));
                db.FarmMemberships.Add(new FarmMembership(farmId, userId, FarmRole.Worker, DateTimeOffset.UtcNow));
            }
            db.FarmWorkerSettings.Add(new FarmWorkerSettings(farmId));
            var verifiedTask = new WorkTask(Guid.NewGuid(), farmId, workerId, DateOnly.FromDateTime(DateTime.UtcNow),
                "Complete harvest", false, null, null, null, null);
            verifiedTask.Start();
            verifiedTask.Submit(DateTimeOffset.UtcNow.AddMinutes(-1));
            verifiedTask.Verify(DateTimeOffset.UtcNow);
            db.WorkTasks.Add(verifiedTask);
            db.VerifiedWorkRewards.Add(new VerifiedWorkReward(Guid.NewGuid(), farmId, workerId,
                VerifiedWorkRewardKind.Points, "verified-task", 25, 1, DateTimeOffset.UtcNow, verifiedTask.Id));
            await db.SaveChangesAsync(ct);
        }

        await using var factory = new ApiTestFactory(database.ConnectionString);
        using var worker = factory.CreateClient();
        TestData.SetBearer(worker, workerId);
        using var own = await worker.GetAsync($"/api/farms/{farmId}/worker-progress/{workerId}", ct);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        using var ownJson = await own.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>(cancellationToken: ct);
        Assert.Equal(25, ownJson!.RootElement.GetProperty("points").GetInt32());
        Assert.Single(ownJson.RootElement.GetProperty("history").EnumerateArray());
        using var peer = await worker.GetAsync($"/api/farms/{farmId}/worker-progress/{peerId}", ct);
        Assert.Equal(HttpStatusCode.NotFound, peer.StatusCode);
        using var disabledBoard = await worker.GetAsync($"/api/farms/{farmId}/worker-progress/board", ct);
        Assert.Equal(HttpStatusCode.Forbidden, disabledBoard.StatusCode);

        using var admin = factory.CreateClient();
        TestData.SetBearer(admin, adminId);
        using var adminProgress = await admin.GetAsync($"/api/farms/{farmId}/worker-progress/{workerId}", ct);
        Assert.Equal(HttpStatusCode.OK, adminProgress.StatusCode);
        using var disabledBoardAsAdmin = await admin.GetAsync($"/api/farms/{farmId}/worker-progress/board", ct);
        Assert.Equal(HttpStatusCode.Forbidden, disabledBoardAsAdmin.StatusCode);
        using var workerSettingsChange = await worker.PutAsJsonAsync($"/api/farms/{farmId}/worker-progress/settings",
            new { rankingsEnabled = true, timeZoneId = (string?)null, badgeThresholdPoints = 50, teamGoalTargetPoints = 500 }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, workerSettingsChange.StatusCode);
        using var enable = await admin.PutAsJsonAsync($"/api/farms/{farmId}/worker-progress/settings",
            new { rankingsEnabled = true, timeZoneId = "America/Santo_Domingo", badgeThresholdPoints = 50, teamGoalTargetPoints = 500 }, ct);
        Assert.Equal(HttpStatusCode.OK, enable.StatusCode);
        using var board = await admin.GetAsync($"/api/farms/{farmId}/worker-progress/board", ct);
        Assert.Equal(HttpStatusCode.OK, board.StatusCode);
    }

    [Fact]
    public async Task Manual_reward_is_audited_and_does_not_publish_payment_event()
    {
        database.EnsureAvailable();
        var ct = TestContext.Current.CancellationToken;
        var ownerId = $"reward-owner-{Guid.NewGuid():N}";
        var adminId = $"reward-admin-{Guid.NewGuid():N}";
        var workerId = $"reward-worker-{Guid.NewGuid():N}";
        Guid farmId;
        await using (var db = database.CreateDbContext())
        {
            (farmId, _) = await TestData.SeedFarmAsync(db, ownerId, adminId, withParcel: false);
            db.Users.Add(User(workerId));
            db.FarmMemberships.Add(new FarmMembership(farmId, workerId, FarmRole.Worker, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(ct);
        }

        await using var factory = new ApiTestFactory(database.ConnectionString);
        using var admin = factory.CreateClient();
        TestData.SetBearer(admin, adminId);
        int outboxBefore;
        await using (var scope = factory.Services.CreateAsyncScope())
            outboxBefore = await scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>().OutboxMessages.CountAsync(ct);

        using var create = await admin.PostAsJsonAsync($"/api/farms/{farmId}/worker-rewards/{workerId}",
            new { description = "Reliable harvest support" }, ct);
        Assert.True(create.StatusCode == HttpStatusCode.Created,
            $"Expected Created, got {(int)create.StatusCode}: {await create.Content.ReadAsStringAsync(ct)}");
        using var worker = factory.CreateClient();
        TestData.SetBearer(worker, workerId);
        using var ownRewards = await worker.GetAsync($"/api/farms/{farmId}/worker-rewards/{workerId}", ct);
        Assert.Equal(HttpStatusCode.OK, ownRewards.StatusCode);
        using var peer = factory.CreateClient();
        var peerId = $"unrelated-{Guid.NewGuid():N}";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            db.Users.Add(User(peerId));
            await db.SaveChangesAsync(ct);
        }
        TestData.SetBearer(peer, peerId);
        Assert.Equal(HttpStatusCode.Forbidden, (await peer.GetAsync($"/api/farms/{farmId}/worker-rewards/{workerId}", ct)).StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            var reward = await db.VerifiedWorkRewards.SingleAsync(item => item.FarmId == farmId &&
                item.WorkerUserId == workerId && item.Kind == VerifiedWorkRewardKind.Manual, ct);
            Assert.Equal(adminId, reward.GrantedByUserId);
            Assert.Equal("Reliable harvest support", reward.Description);
            Assert.Equal(outboxBefore, await db.OutboxMessages.CountAsync(ct));
        }
    }

    private static ApplicationUser User(string id) => new()
    {
        Id = id,
        UserName = $"{id}@example.test",
        NormalizedUserName = $"{id}@EXAMPLE.TEST",
        Email = $"{id}@example.test",
        NormalizedEmail = $"{id}@EXAMPLE.TEST",
        EmailConfirmed = true
    };
}
