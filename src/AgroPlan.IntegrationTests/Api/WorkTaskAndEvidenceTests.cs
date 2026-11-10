using System.Net;
using System.Net.Http.Json;
using AgroPlan.Domain.Farms;
using AgroPlan.Infrastructure.Identity;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgroPlan.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class WorkTaskAndEvidenceTests(PostgreSqlFixture database) : IAsyncLifetime
{
    private readonly string _ownerId = $"task-owner-{Guid.NewGuid():N}";
    private readonly string _workerId = $"task-worker-{Guid.NewGuid():N}";
    private readonly string _otherWorkerId = $"task-other-{Guid.NewGuid():N}";
    private readonly string _adminId = $"task-admin-{Guid.NewGuid():N}";
    private readonly Guid _farmId = Guid.NewGuid();
    private HttpClient _client = null!;
    private ApiTestFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        database.EnsureAvailable();
        await using var db = database.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        db.Farms.Add(new Farm(_farmId, "Task test farm", "Test", now));
        foreach (var id in new[] { _ownerId, _workerId, _otherWorkerId, _adminId })
            db.Users.Add(User(id));
        db.FarmMemberships.AddRange(
            new FarmMembership(_farmId, _ownerId, FarmRole.Owner, now),
            new FarmMembership(_farmId, _workerId, FarmRole.Worker, now),
            new FarmMembership(_farmId, _otherWorkerId, FarmRole.Worker, now),
            new FarmMembership(_farmId, _adminId, FarmRole.Administrator, now));
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
    public async Task Assigned_worker_only_sees_own_task_and_verified_task_confirms_wage_without_payment()
    {
        database.EnsureAvailable();
        TestData.SetBearer(_client, _ownerId);
        var created = await _client.PostAsJsonAsync($"/api/farms/{_farmId}/work-tasks", new
        {
            assignedWorkerUserId = _workerId,
            dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            description = "Prune the north row",
            photoRequired = true,
            wageAmount = 125m,
            wageUnit = "day",
            parcelId = (Guid?)null,
            cropCycleId = (Guid?)null,
            operationKind = (string?)null
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var taskJson = await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var taskId = taskJson.GetProperty("id").GetGuid();

        TestData.SetBearer(_client, _workerId);
        var workerTasks = await _client.GetFromJsonAsync<System.Text.Json.JsonElement[]>($"/api/farms/{_farmId}/work-tasks");
        Assert.NotNull(workerTasks);
        Assert.Single(workerTasks);
        TestData.SetBearer(_client, _otherWorkerId);
        var otherTasks = await _client.GetFromJsonAsync<System.Text.Json.JsonElement[]>($"/api/farms/{_farmId}/work-tasks");
        Assert.NotNull(otherTasks);
        Assert.Empty(otherTasks);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync($"/api/farms/{_farmId}/work-tasks/{taskId}/start", null)).StatusCode);

        TestData.SetBearer(_client, _workerId);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/api/farms/{_farmId}/work-tasks/{taskId}/start", null)).StatusCode);
        var firstOperationId = Guid.NewGuid();
        var submissionResponse = await _client.PostAsJsonAsync($"/api/farms/{_farmId}/work-tasks/{taskId}/evidence/uploads", new
        {
            clientOperationId = firstOperationId, contentType = "image/png", sizeBytes = 512L,
            consentLocation = false, location = (string?)null, consentCapturedAt = false, capturedAt = (DateTimeOffset?)null
        });
        Assert.Equal(HttpStatusCode.Created, submissionResponse.StatusCode);
        var uploadJson = await submissionResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var submissionId = uploadJson.GetProperty("submissionId").GetGuid();
        var retryUpload = await _client.PostAsJsonAsync($"/api/farms/{_farmId}/work-tasks/{taskId}/evidence/uploads", new
        {
            clientOperationId = firstOperationId, contentType = "image/png", sizeBytes = 512L,
            consentLocation = false, location = (string?)null, consentCapturedAt = false, capturedAt = (DateTimeOffset?)null
        });
        Assert.Equal(HttpStatusCode.OK, retryUpload.StatusCode);
        var retryJson = await retryUpload.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(submissionId, retryJson.GetProperty("submissionId").GetGuid());
        TestData.SetBearer(_client, _otherWorkerId);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync($"/api/farms/{_farmId}/work-tasks/{taskId}/evidence/{submissionId}/finalize", null)).StatusCode);
        TestData.SetBearer(_client, _workerId);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/api/farms/{_farmId}/work-tasks/{taskId}/evidence/{submissionId}/finalize", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/api/farms/{_farmId}/work-tasks/{taskId}/evidence/submit", null)).StatusCode);

        TestData.SetBearer(_client, _adminId);
        var correction = await _client.PostAsJsonAsync($"/api/farms/{_farmId}/work-tasks/{taskId}/review", new { action = 3, reason = "Please include the full row." });
        Assert.Equal(HttpStatusCode.OK, correction.StatusCode);
        await using (var beforeApprovalDb = database.CreateDbContext())
            Assert.Empty(await beforeApprovalDb.VerifiedWageEntries.Where(row => row.WorkTaskId == taskId).ToArrayAsync());

        TestData.SetBearer(_client, _workerId);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsync(
            $"/api/farms/{_farmId}/work-tasks/{taskId}/evidence/submit", null)).StatusCode);
        var correctionUpload = await _client.PostAsJsonAsync($"/api/farms/{_farmId}/work-tasks/{taskId}/evidence/uploads", new
        {
            clientOperationId = Guid.NewGuid(), contentType = "image/png", sizeBytes = 512L,
            consentLocation = true, location = "North row", consentCapturedAt = true, capturedAt = DateTimeOffset.UtcNow
        });
        Assert.Equal(HttpStatusCode.Created, correctionUpload.StatusCode);
        var correctionJson = await correctionUpload.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(2, correctionJson.GetProperty("revision").GetInt32());
        var correctionSubmissionId = correctionJson.GetProperty("submissionId").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/api/farms/{_farmId}/work-tasks/{taskId}/evidence/{correctionSubmissionId}/finalize", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/api/farms/{_farmId}/work-tasks/{taskId}/evidence/submit", null)).StatusCode);

        TestData.SetBearer(_client, _adminId);
        var review = await _client.PostAsJsonAsync($"/api/farms/{_farmId}/work-tasks/{taskId}/review", new { action = 1, reason = (string?)null });
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        var history = await _client.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/farms/{_farmId}/work-tasks/{taskId}/history");
        Assert.Equal("Verified", history.GetProperty("status").GetString());
        Assert.Equal(2, history.GetProperty("evidence").GetArrayLength());
        Assert.Equal(submissionId, history.GetProperty("evidence")[1].GetProperty("previousSubmissionId").GetGuid());
        Assert.Equal("North row", history.GetProperty("evidence")[1].GetProperty("location").GetString());
        Assert.Equal(2, history.GetProperty("decisions").GetArrayLength());
        Assert.Equal(125m, history.GetProperty("verifiedWage").GetProperty("amount").GetDecimal());
        await using var verifyDb = database.CreateDbContext();
        Assert.Equal(1, await verifyDb.VerifiedWageEntries.CountAsync(row => row.FarmId == _farmId && row.WorkTaskId == taskId));
        Assert.Equal(1, await verifyDb.OutboxMessages.CountAsync(message => message.AggregateId == taskId && message.EventType == "work-task.verified"));
    }

    [Fact]
    public async Task Required_photo_blocks_submission_and_invalid_uploads_are_rejected()
    {
        database.EnsureAvailable();
        TestData.SetBearer(_client, _ownerId);
        var response = await _client.PostAsJsonAsync($"/api/farms/{_farmId}/work-tasks", new
        {
            assignedWorkerUserId = _workerId,
            dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            description = "Inspect the field", photoRequired = true,
            wageAmount = (decimal?)null, wageUnit = (string?)null,
            parcelId = (Guid?)null, cropCycleId = (Guid?)null, operationKind = (string?)null
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var task = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var taskId = task.GetProperty("id").GetGuid();
        TestData.SetBearer(_client, _workerId);
        await _client.PostAsync($"/api/farms/{_farmId}/work-tasks/{taskId}/start", null);
        var badUpload = await _client.PostAsJsonAsync($"/api/farms/{_farmId}/work-tasks/{taskId}/evidence/uploads", new
        {
            clientOperationId = Guid.NewGuid(), contentType = "text/plain", sizeBytes = 32L,
            consentLocation = false, location = "Should not persist", consentCapturedAt = false, capturedAt = (DateTimeOffset?)null
        });
        Assert.Equal(HttpStatusCode.BadRequest, badUpload.StatusCode);
        var submit = await _client.PostAsync($"/api/farms/{_farmId}/work-tasks/{taskId}/evidence/submit", null);
        Assert.Equal(HttpStatusCode.BadRequest, submit.StatusCode);
    }

    [Fact]
    public async Task Rejected_task_does_not_create_a_verified_wage_entry()
    {
        database.EnsureAvailable();
        TestData.SetBearer(_client, _ownerId);
        var response = await _client.PostAsJsonAsync($"/api/farms/{_farmId}/work-tasks", new
        {
            assignedWorkerUserId = _workerId,
            dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            description = "Clear the irrigation channel", photoRequired = false,
            wageAmount = 80m, wageUnit = "hour",
            parcelId = (Guid?)null, cropCycleId = (Guid?)null, operationKind = (string?)null
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var task = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var taskId = task.GetProperty("id").GetGuid();
        TestData.SetBearer(_client, _workerId);
        await _client.PostAsync($"/api/farms/{_farmId}/work-tasks/{taskId}/start", null);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/api/farms/{_farmId}/work-tasks/{taskId}/evidence/submit", null)).StatusCode);
        TestData.SetBearer(_client, _adminId);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync($"/api/farms/{_farmId}/work-tasks/{taskId}/review",
            new { action = 2, reason = "Work does not match the assigned task." })).StatusCode);
        await using var db = database.CreateDbContext();
        Assert.Empty(await db.VerifiedWageEntries.Where(row => row.FarmId == _farmId && row.WorkTaskId == taskId).ToArrayAsync());
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
