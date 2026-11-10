using System.Net;
using System.Net.Http.Json;
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

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await using var verifyDb = database.CreateDbContext();
        Assert.Empty(await verifyDb.Set<WorkTask>().Where(task => task.Id == taskId).ToArrayAsync(ct));
    }
}
