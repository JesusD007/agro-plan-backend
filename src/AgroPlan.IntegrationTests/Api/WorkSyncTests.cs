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
        var changedPayload = await _client.PostAsJsonAsync($"/api/farms/{_farmId}/work-sync", new
        {
            operations = new[]
            {
                new { operationId, taskId = _taskId, kind = "evidence.finalize", baseVersion = 1,
                    payload = new { }, submissionId = Guid.NewGuid() }
            }
        });
        Assert.Equal(HttpStatusCode.OK, changedPayload.StatusCode);
        var changedResult = await changedPayload.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Conflict", changedResult.GetProperty("operations")[0].GetProperty("status").GetString());
        await using var db = database.CreateDbContext();
        Assert.Equal(1, await db.WorkTasks.CountAsync(task => task.Id == _taskId && task.Status == WorkTaskStatus.InProgress));
        Assert.Equal(1, await db.SyncOperationReceipts.CountAsync(receipt => receipt.FarmId == _farmId && receipt.ClientOperationId == operationId));
    }

    [Fact]
    public async Task Evidence_finalize_is_idempotent_and_a_failed_batch_item_does_not_stop_other_operations()
    {
        database.EnsureAvailable();
        var taskId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();
        var uploadOperationId = Guid.NewGuid();
        var workerOperationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await using (var db = database.CreateDbContext())
        {
            var task = new WorkTask(taskId, _farmId, _workerId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                "Upload irrigation photo", false, null, null, null, null);
            task.Start();
            db.WorkTasks.Add(task);
            db.TaskEvidenceSubmissions.Add(new TaskEvidenceSubmission(submissionId, _farmId, taskId, _workerId,
                1, null, uploadOperationId, $"farms/{_farmId:D}/work-tasks/{taskId:D}/evidence/{submissionId:D}/staging",
                "image/png", 512, false, null, false, null, now, now.AddMinutes(10)));
            await db.SaveChangesAsync();
        }

        TestData.SetBearer(_client, _workerId);
        var invalidId = Guid.NewGuid();
        var request = new
        {
            operations = new object[]
            {
                new { operationId = invalidId, taskId = Guid.NewGuid(), kind = "task.start", baseVersion = 1, payload = new { } },
                new { operationId = workerOperationId, taskId, kind = "evidence.finalize", baseVersion = 1, payload = new { }, submissionId }
            }
        };
        var first = await _client.PostAsJsonAsync($"/api/farms/{_farmId}/work-sync", request);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstResult = await first.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Rejected", firstResult.GetProperty("operations")[0].GetProperty("status").GetString());
        Assert.Equal("Applied", firstResult.GetProperty("operations")[1].GetProperty("status").GetString());

        var replay = await _client.PostAsJsonAsync($"/api/farms/{_farmId}/work-sync", new
        {
            operations = new[]
            {
                new { operationId = workerOperationId, taskId, kind = "evidence.finalize", baseVersion = 1, payload = new { }, submissionId }
            }
        });
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayResult = await replay.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Replayed", replayResult.GetProperty("operations")[0].GetProperty("status").GetString());
        Assert.Equal(submissionId, replayResult.GetProperty("operations")[0].GetProperty("submissionId").GetGuid());
        await using var verifyDb = database.CreateDbContext();
        Assert.Equal(1, await verifyDb.TaskEvidenceSubmissions.CountAsync(row => row.Id == submissionId && row.FinalizedAt != null));
        Assert.Equal(1, await verifyDb.SyncOperationReceipts.CountAsync(row => row.ClientOperationId == workerOperationId));
    }

    [Fact]
    public async Task Stale_task_update_preserves_client_and_server_versions_until_admin_resolution()
    {
        database.EnsureAvailable();
        TestData.SetBearer(_client, _workerId);
        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        var currentOperationId = Guid.NewGuid();
        var current = await _client.PostAsJsonAsync($"/api/farms/{_farmId}/work-sync", new
        {
            operations = new[]
            {
                new { operationId = currentOperationId, taskId = _taskId, kind = "task.update", baseVersion = 1,
                    payload = new { description = "Server version", dueDate } }
            }
        });
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        var currentResult = await current.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(currentResult.GetProperty("operations")[0].GetProperty("status").GetString() == "Applied",
            currentResult.GetProperty("operations")[0].GetProperty("message").GetString());
        Assert.Equal(2, currentResult.GetProperty("operations")[0].GetProperty("taskVersion").GetInt32());

        var conflictOperationId = Guid.NewGuid();
        var stale = await _client.PostAsJsonAsync($"/api/farms/{_farmId}/work-sync", new
        {
            operations = new[]
            {
                new { operationId = conflictOperationId, taskId = _taskId, kind = "task.update", baseVersion = 1,
                    payload = new { description = "Client version", dueDate } }
            }
        });
        Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
        var staleResult = await stale.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Conflict", staleResult.GetProperty("operations")[0].GetProperty("status").GetString());
        var conflictId = staleResult.GetProperty("operations")[0].GetProperty("conflictId").GetGuid();
        await using (var db = database.CreateDbContext())
            Assert.Equal("Server version", (await db.WorkTasks.SingleAsync(task => task.Id == _taskId)).Description);

        TestData.SetBearer(_client, _workerId);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsJsonAsync(
            $"/api/farms/{_farmId}/work-sync/conflicts/{conflictId}/resolve", new { resolution = "client" })).StatusCode);
        TestData.SetBearer(_client, _ownerId);
        var resolved = await _client.PostAsJsonAsync($"/api/farms/{_farmId}/work-sync/conflicts/{conflictId}/resolve",
            new { resolution = "client" });
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        await using var verifyDb = database.CreateDbContext();
        Assert.Equal("Client version", (await verifyDb.WorkTasks.SingleAsync(task => task.Id == _taskId)).Description);
        var storedConflict = await verifyDb.TaskSyncConflicts.SingleAsync(conflict => conflict.Id == conflictId);
        Assert.Equal(1, storedConflict.BaseVersion);
        Assert.Equal(2, storedConflict.ServerVersion);
        Assert.Contains("Client version", storedConflict.ClientValuesJson);
        Assert.Contains("Server version", storedConflict.ServerValuesJson);
        var resolution = await verifyDb.TaskSyncConflictResolutions.SingleAsync(item => item.ConflictId == conflictId);
        Assert.Equal(_ownerId, resolution.DecidedByUserId);
        Assert.Equal("Client", resolution.Resolution);
        Assert.Equal(3, resolution.ResultTaskVersion);
    }

    [Fact]
    public async Task Conflict_resolution_is_rejected_if_the_task_changed_after_conflict_capture()
    {
        database.EnsureAvailable();
        TestData.SetBearer(_client, _workerId);
        await _client.PostAsJsonAsync($"/api/farms/{_farmId}/work-sync", new
        {
            operations = new[]
            {
                new { operationId = Guid.NewGuid(), taskId = _taskId, kind = "task.update", baseVersion = 1,
                    payload = new { description = "First server change", dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)) } }
            }
        });
        var stale = await _client.PostAsJsonAsync($"/api/farms/{_farmId}/work-sync", new
        {
            operations = new[]
            {
                new { operationId = Guid.NewGuid(), taskId = _taskId, kind = "task.update", baseVersion = 1,
                    payload = new { description = "Offline attempt", dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)) } }
            }
        });
        var staleResult = await stale.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var conflictId = staleResult.GetProperty("operations")[0].GetProperty("conflictId").GetGuid();

        await using (var db = database.CreateDbContext())
        {
            var task = await db.WorkTasks.SingleAsync(item => item.Id == _taskId);
            task.Update("Later server change", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4)));
            await db.SaveChangesAsync();
        }

        TestData.SetBearer(_client, _ownerId);
        var resolution = await _client.PostAsJsonAsync($"/api/farms/{_farmId}/work-sync/conflicts/{conflictId}/resolve",
            new { resolution = "client" });
        Assert.Equal(HttpStatusCode.Conflict, resolution.StatusCode);
        await using var verifyDb = database.CreateDbContext();
        Assert.Equal("Later server change", (await verifyDb.WorkTasks.SingleAsync(item => item.Id == _taskId)).Description);
        Assert.Empty(await verifyDb.TaskSyncConflictResolutions.Where(item => item.ConflictId == conflictId).ToArrayAsync());
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
