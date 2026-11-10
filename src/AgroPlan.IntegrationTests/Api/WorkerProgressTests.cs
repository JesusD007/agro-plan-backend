using System.Net;
using System.Net.Http.Json;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Workforce;
using AgroPlan.Infrastructure.Identity;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgroPlan.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class WorkerProgressTests(PostgreSqlFixture database)
{
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
            db.VerifiedWorkRewards.Add(new VerifiedWorkReward(Guid.NewGuid(), farmId, workerId,
                VerifiedWorkRewardKind.Points, "verified-task", 25, 1, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(ct);
        }

        await using var factory = new ApiTestFactory(database.ConnectionString);
        using var worker = factory.CreateClient();
        TestData.SetBearer(worker, workerId);
        using var own = await worker.GetAsync($"/api/farms/{farmId}/worker-progress/{workerId}", ct);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        using var ownJson = await own.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>(cancellationToken: ct);
        Assert.Equal(25, ownJson!.RootElement.GetProperty("points").GetInt32());
        using var peer = await worker.GetAsync($"/api/farms/{farmId}/worker-progress/{peerId}", ct);
        Assert.Equal(HttpStatusCode.NotFound, peer.StatusCode);
        using var disabledBoard = await worker.GetAsync($"/api/farms/{farmId}/worker-progress/board", ct);
        Assert.Equal(HttpStatusCode.Forbidden, disabledBoard.StatusCode);

        using var admin = factory.CreateClient();
        TestData.SetBearer(admin, adminId);
        using var adminProgress = await admin.GetAsync($"/api/farms/{farmId}/worker-progress/{workerId}", ct);
        Assert.Equal(HttpStatusCode.OK, adminProgress.StatusCode);
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
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
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
