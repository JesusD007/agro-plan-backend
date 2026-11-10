using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgroPlan.Domain.Crops;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Workforce;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgroPlan.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class WorkRoutesTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task Route_generation_is_unavailable_until_provider_is_approved()
    {
        database.EnsureAvailable();
        var ct = TestContext.Current.CancellationToken;
        var adminId = $"route-admin-{Guid.NewGuid():N}";
        var ownerId = $"route-owner-{Guid.NewGuid():N}";
        var workerId = $"route-worker-{Guid.NewGuid():N}";
        Guid farmId;
        var taskId = Guid.NewGuid();
        await using (var db = database.CreateDbContext())
        {
            (farmId, _) = await TestData.SeedFarmAsync(db, ownerId, adminId, withParcel: false);
            db.Users.Add(new AgroPlan.Infrastructure.Identity.ApplicationUser
            {
                Id = workerId,
                UserName = $"{workerId}@example.test",
                NormalizedUserName = $"{workerId}@EXAMPLE.TEST",
                Email = $"{workerId}@example.test",
                NormalizedEmail = $"{workerId}@EXAMPLE.TEST",
                EmailConfirmed = true
            });
            db.FarmMemberships.Add(new FarmMembership(farmId, workerId, FarmRole.Worker, DateTimeOffset.UtcNow));
            db.WorkTasks.Add(new WorkTask(taskId, farmId, workerId, DateOnly.FromDateTime(DateTime.UtcNow),
                "Inspect crop row", false, null, null, null, null));
            await db.SaveChangesAsync(ct);
        }

        await using var factory = new ApiTestFactory(database.ConnectionString);
        using var admin = factory.CreateClient();
        TestData.SetBearer(admin, adminId);
        using var response = await admin.PostAsJsonAsync($"/api/farms/{farmId}/work-routes/generate",
            new { workerUserId = workerId, taskIds = new[] { taskId } }, ct);

        Assert.True(response.StatusCode == HttpStatusCode.ServiceUnavailable,
            $"Expected ServiceUnavailable, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
        await using var verifyDb = database.CreateDbContext();
        Assert.Single(await verifyDb.WorkTasks.Where(task => task.Id == taskId).ToArrayAsync(ct));
        Assert.Empty(await verifyDb.WorkRoutes.Where(route => route.FarmId == farmId).ToArrayAsync(ct));
    }

    [Fact]
    public async Task Worker_downloads_only_confirmed_current_route_and_admin_must_confirm_replacement()
    {
        database.EnsureAvailable();
        var ct = TestContext.Current.CancellationToken;
        var ownerId = $"route-owner-{Guid.NewGuid():N}";
        var adminId = $"route-admin-{Guid.NewGuid():N}";
        var workerId = $"route-worker-{Guid.NewGuid():N}";
        var peerId = $"route-peer-{Guid.NewGuid():N}";
        var routeId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        Guid farmId;
        var now = DateTimeOffset.UtcNow;
        var task = new WorkTask(taskId, Guid.NewGuid(), workerId, DateOnly.FromDateTime(DateTime.UtcNow),
            "Inspect crop row", false, null, null, null, null);
        WorkRoute routeV1;
        await using (var db = database.CreateDbContext())
        {
            (farmId, _) = await TestData.SeedFarmAsync(db, ownerId, adminId, withParcel: false);
            task = new WorkTask(taskId, farmId, workerId, DateOnly.FromDateTime(DateTime.UtcNow),
                "Inspect crop row", false, null, null, null, null);
            foreach (var userId in new[] { workerId, peerId })
            {
                db.Users.Add(new AgroPlan.Infrastructure.Identity.ApplicationUser
                {
                    Id = userId,
                    UserName = $"{userId}@example.test",
                    NormalizedUserName = $"{userId}@EXAMPLE.TEST",
                    Email = $"{userId}@example.test",
                    NormalizedEmail = $"{userId}@EXAMPLE.TEST",
                    EmailConfirmed = true
                });
                db.FarmMemberships.Add(new FarmMembership(farmId, userId, FarmRole.Worker, now));
            }
            routeV1 = Route(routeId, farmId, workerId, 1, task, now);
            routeV1.Confirm(adminId, now);
            db.WorkTasks.Add(task);
            db.WorkRoutes.Add(routeV1);
            await db.SaveChangesAsync(ct);
        }

        await using var factory = new ApiTestFactory(database.ConnectionString);
        using var worker = factory.CreateClient();
        TestData.SetBearer(worker, workerId);
        using var download = await worker.GetAsync($"/api/farms/{farmId}/work-routes/{routeId}/current", ct);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        using (var snapshot = await download.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct))
        {
            Assert.Equal(1, snapshot!.RootElement.GetProperty("version").GetInt32());
            Assert.Equal(taskId, snapshot.RootElement.GetProperty("orderedTaskIds")[0].GetGuid());
            Assert.True(snapshot.RootElement.TryGetProperty("geometryGeoJson", out _));
            Assert.False(snapshot.RootElement.TryGetProperty("mapTiles", out _));
            Assert.False(snapshot.RootElement.TryGetProperty("turnByTurn", out _));
        }
        using var peer = factory.CreateClient();
        TestData.SetBearer(peer, peerId);
        using var otherWorker = await peer.GetAsync($"/api/farms/{farmId}/work-routes/{routeId}/current", ct);
        Assert.Equal(HttpStatusCode.NotFound, otherWorker.StatusCode);

        await using (var db = database.CreateDbContext())
        {
            var currentTask = await db.WorkTasks.SingleAsync(item => item.FarmId == farmId && item.Id == taskId, ct);
            currentTask.Update("Inspect crop row north", currentTask.DueDate.AddDays(1));
            await db.SaveChangesAsync(ct);
        }
        using var stale = await worker.GetAsync($"/api/farms/{farmId}/work-routes/{routeId}/current", ct);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await using (var db = database.CreateDbContext())
        {
            var staleRoute = await db.WorkRoutes.SingleAsync(route => route.FarmId == farmId && route.RouteId == routeId && route.Version == 1, ct);
            Assert.Equal(WorkRouteStatus.Stale, staleRoute.Status);
            var changedTask = await db.WorkTasks.SingleAsync(item => item.FarmId == farmId && item.Id == taskId, ct);
            db.WorkRoutes.Add(Route(routeId, farmId, workerId, 2, changedTask, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(ct);
        }

        using var workerConfirm = await worker.PostAsync($"/api/farms/{farmId}/work-routes/{routeId}/versions/2/confirm", null, ct);
        Assert.Equal(HttpStatusCode.Forbidden, workerConfirm.StatusCode);
        using var admin = factory.CreateClient();
        TestData.SetBearer(admin, adminId);
        using var confirm = await admin.PostAsync($"/api/farms/{farmId}/work-routes/{routeId}/versions/2/confirm", null, ct);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        using var replacement = await worker.GetAsync($"/api/farms/{farmId}/work-routes/{routeId}/current", ct);
        Assert.Equal(HttpStatusCode.OK, replacement.StatusCode);
        using var replacementJson = await replacement.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
        Assert.Equal(2, replacementJson!.RootElement.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task Parcel_change_marks_confirmed_route_stale()
    {
        database.EnsureAvailable();
        var ct = TestContext.Current.CancellationToken;
        var ownerId = $"route-parcel-owner-{Guid.NewGuid():N}";
        var workerId = $"route-parcel-worker-{Guid.NewGuid():N}";
        var routeId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var cycleId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        Guid farmId;
        Guid parcelId;
        await using (var db = database.CreateDbContext())
        {
            (farmId, parcelId) = await TestData.SeedFarmAsync(db, ownerId);
            db.Users.Add(new AgroPlan.Infrastructure.Identity.ApplicationUser
            {
                Id = workerId,
                UserName = $"{workerId}@example.test",
                NormalizedUserName = $"{workerId}@EXAMPLE.TEST",
                Email = $"{workerId}@example.test",
                NormalizedEmail = $"{workerId}@EXAMPLE.TEST",
                EmailConfirmed = true
            });
            db.FarmMemberships.Add(new FarmMembership(farmId, workerId, FarmRole.Worker, now));
            var crop = await db.CropCatalogEntries.SingleAsync(item => item.Code == "YUCA", ct);
            db.CropCycles.Add(new CropCycle(cycleId, farmId, parcelId, crop, null, null, null, now));
            var task = new WorkTask(taskId, farmId, workerId, DateOnly.FromDateTime(DateTime.UtcNow),
                "Inspect parcel", false, null, null, parcelId, cycleId, "inspection");
            var parcel = await db.Parcels.SingleAsync(item => item.FarmId == farmId && item.Id == parcelId, ct);
            var route = new WorkRoute(Guid.NewGuid(), routeId, farmId, workerId, 1, [taskId],
                [new WorkRouteTaskSource(taskId, task.Version, parcelId, parcel.UpdatedAt)],
                "{\"type\":\"LineString\",\"coordinates\":[[-69.9,18.5],[-69.8,18.6]]}", now);
            route.Confirm(ownerId, now);
            db.WorkTasks.Add(task);
            db.WorkRoutes.Add(route);
            await db.SaveChangesAsync(ct);
        }

        await using var factory = new ApiTestFactory(database.ConnectionString);
        using var workerClient = factory.CreateClient();
        TestData.SetBearer(workerClient, workerId);
        await using (var db = database.CreateDbContext())
        {
            var parcel = await db.Parcels.SingleAsync(item => item.FarmId == farmId && item.Id == parcelId, ct);
            parcel.Update("Parcel A revised", "Test location revised", parcel.AreaHectares, now.AddMinutes(1));
            await db.SaveChangesAsync(ct);
        }

        using var response = await workerClient.GetAsync($"/api/farms/{farmId}/work-routes/{routeId}/current", ct);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var verifyDb = database.CreateDbContext();
        var routeAfterParcelChange = await verifyDb.WorkRoutes.SingleAsync(route => route.FarmId == farmId && route.RouteId == routeId, ct);
        Assert.Equal(WorkRouteStatus.Stale, routeAfterParcelChange.Status);
    }

    private static WorkRoute Route(Guid routeId, Guid farmId, string workerId, int version, WorkTask task, DateTimeOffset now) =>
        new(Guid.NewGuid(), routeId, farmId, workerId, version, [task.Id],
            [new WorkRouteTaskSource(task.Id, task.Version, task.ParcelId, null)],
            "{\"type\":\"LineString\",\"coordinates\":[[-69.9,18.5],[-69.8,18.6]]}", now);
}
