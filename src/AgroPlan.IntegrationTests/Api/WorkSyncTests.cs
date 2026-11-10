using System.Net;
using System.Net.Http.Json;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Workforce;
using AgroPlan.Infrastructure.Identity;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgroPlan.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class WorkSyncTests(PostgreSqlFixture database) : IAsyncLifetime
{
    private readonly string _ownerId = $"sync-owner-{Guid.NewGuid():N}";
    private readonly string _workerId = $"sync-worker-{Guid.NewGuid():N}";
    private readonly Guid _farmId = Guid.NewGuid();
    private readonly Guid _taskId = Guid.NewGuid();
    private HttpClient _client = null!;
    private ApiTestFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        database.EnsureAvailable();
        await using var db = database.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        db.Farms.Add(new Farm(_farmId, "Sync test farm", "Test", now));
        db.Users.AddRange(User(_ownerId), User(_workerId));
        db.FarmMemberships.AddRange(new FarmMembership(_farmId, _ownerId, FarmRole.Owner, now),
            new FarmMembership(_farmId, _workerId, FarmRole.Worker, now));
        db.WorkTasks.Add(new WorkTask(_taskId, _farmId, _workerId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            "Inspect irrigation", false, null, null, null, null));
        await db.SaveChangesAsync();
        _factory = new ApiTestFactory(database.ConnectionString, disableBackgroundServices: true);
        _client = _factory.CreateClient();
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Sync_applies_an_operation_once_and_returns_the_stored_result_on_replay()
    {
        database.EnsureAvailable();
        TestData.SetBearer(_client, _workerId);
        var operationId = Guid.NewGuid();
        var request = new
        {
            operations = new[]
            {
                new { operationId, taskId = _taskId, kind = "task.start", baseVersion = 1, payload = new { } }
            }
        };

        var first = await _client.PostAsJsonAsync($"/api/farms/{_farmId}/work-sync", request);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstResult = await first.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Applied", firstResult.GetProperty("operations")[0].GetProperty("status").GetString());

        var replay = await _client.PostAsJsonAsync($"/api/farms/{_farmId}/work-sync", request);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayResult = await replay.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Replayed", replayResult.GetProperty("operations")[0].GetProperty("status").GetString());
        await using var db = database.CreateDbContext();
        Assert.Equal(1, await db.WorkTasks.CountAsync(task => task.Id == _taskId && task.Status == WorkTaskStatus.InProgress));
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
