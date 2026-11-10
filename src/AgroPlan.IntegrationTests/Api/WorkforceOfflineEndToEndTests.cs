using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Workforce;
using AgroPlan.Infrastructure.Identity;
using AgroPlan.Infrastructure.Messaging;
using AgroPlan.Infrastructure.Messaging.Events;
using AgroPlan.Infrastructure.Persistence;
using AgroPlan.Worker.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgroPlan.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class WorkforceOfflineEndToEndTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task Invited_worker_completes_offline_evidence_lifecycle_and_sees_only_verified_progress()
    {
        var ct = TestContext.Current.CancellationToken;
        database.EnsureAvailable();
        var ownerId = $"e2e-owner-{Guid.NewGuid():N}";
        var workerId = $"e2e-worker-{Guid.NewGuid():N}";
        var peerId = $"e2e-peer-{Guid.NewGuid():N}";
        var email = $"{workerId}@example.test";
        Guid farmId;

        await using var factory = new ApiTestFactory(database.ConnectionString, disableBackgroundServices: true);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (farmId, _) = await TestData.SeedFarmAsync(db, ownerId, withParcel: false);
        }

        using var owner = factory.CreateClient();
        TestData.SetBearer(owner, ownerId);
        using var invitation = await owner.PostAsJsonAsync($"/api/farms/{farmId}/workers/invitations", new { email }, ct);
        Assert.Equal(HttpStatusCode.Created, invitation.StatusCode);
        var token = System.Text.RegularExpressions.Regex.Match(factory.Email.Messages.Single().TextBody,
            @"token=([^\s]+)").Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(token));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            db.Users.Add(new ApplicationUser
            {
                Id = workerId, UserName = email, NormalizedUserName = email.ToUpperInvariant(), Email = email,
                NormalizedEmail = email.ToUpperInvariant(), EmailConfirmed = true
            });
            db.Users.Add(new ApplicationUser
            {
                Id = peerId, UserName = $"{peerId}@example.test", NormalizedUserName = $"{peerId}@example.test".ToUpperInvariant(),
                Email = $"{peerId}@example.test", NormalizedEmail = $"{peerId}@example.test".ToUpperInvariant(), EmailConfirmed = true
            });
            await db.SaveChangesAsync(ct);
        }
        using var worker = factory.CreateClient();
        TestData.SetBearer(worker, workerId);
        using var accepted = await worker.PostAsJsonAsync("/api/worker-invitations/accept", new { token }, ct);
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);

        TestData.SetBearer(owner, ownerId);
        using var created = await owner.PostAsJsonAsync($"/api/farms/{farmId}/work-tasks", new
        {
            assignedWorkerUserId = workerId, dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            description = "Inspect and photograph the north row", photoRequired = true,
            wageAmount = 850m, wageUnit = "DOP/day", parcelId = (Guid?)null, cropCycleId = (Guid?)null,
            operationKind = (string?)null, pointsForVerification = 10, completeEvidenceBonusPoints = 5
        }, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var taskJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync(ct));
        var taskId = taskJson.RootElement.GetProperty("id").GetGuid();

        TestData.SetBearer(worker, workerId);
        using var started = await worker.PostAsync($"/api/farms/{farmId}/work-tasks/{taskId}/start", null, ct);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        var operationId = Guid.NewGuid();
        using var uploaded = await worker.PostAsJsonAsync($"/api/farms/{farmId}/work-tasks/{taskId}/evidence/uploads", new
        {
            clientOperationId = Guid.NewGuid(), contentType = "image/png", sizeBytes = 512L,
            consentLocation = false, location = (string?)null, consentCapturedAt = false,
            capturedAt = (DateTimeOffset?)null
        }, ct);
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        using var uploadJson = JsonDocument.Parse(await uploaded.Content.ReadAsStringAsync(ct));
        var submissionId = uploadJson.RootElement.GetProperty("submissionId").GetGuid();

        var syncRequest = new
        {
            operations = new[]
            {
                new { operationId, taskId, kind = "evidence.finalize", baseVersion = 1,
                    payload = new { }, submissionId }
            }
        };
        using var synced = await worker.PostAsJsonAsync($"/api/farms/{farmId}/work-sync", syncRequest, ct);
        Assert.Equal(HttpStatusCode.OK, synced.StatusCode);
        using var syncJson = JsonDocument.Parse(await synced.Content.ReadAsStringAsync(ct));
        Assert.Equal("Applied", syncJson.RootElement.GetProperty("operations")[0].GetProperty("status").GetString());
        using var replayed = await worker.PostAsJsonAsync($"/api/farms/{farmId}/work-sync", syncRequest, ct);
        using var replayJson = JsonDocument.Parse(await replayed.Content.ReadAsStringAsync(ct));
        Assert.Equal("Replayed", replayJson.RootElement.GetProperty("operations")[0].GetProperty("status").GetString());
        using var submitted = await worker.PostAsync($"/api/farms/{farmId}/work-tasks/{taskId}/evidence/submit", null, ct);
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);

        TestData.SetBearer(owner, ownerId);
        using var correction = await owner.PostAsJsonAsync($"/api/farms/{farmId}/work-tasks/{taskId}/review",
            new { action = 3, reason = "Retake the north row image." }, ct);
        Assert.Equal(HttpStatusCode.OK, correction.StatusCode);
        TestData.SetBearer(worker, workerId);
        using var correctionUpload = await worker.PostAsJsonAsync($"/api/farms/{farmId}/work-tasks/{taskId}/evidence/uploads", new
        {
            clientOperationId = Guid.NewGuid(), contentType = "image/png", sizeBytes = 512L,
            consentLocation = false, location = (string?)null, consentCapturedAt = false,
            capturedAt = (DateTimeOffset?)null
        }, ct);
        Assert.Equal(HttpStatusCode.Created, correctionUpload.StatusCode);
        using var correctionJson = JsonDocument.Parse(await correctionUpload.Content.ReadAsStringAsync(ct));
        var correctionId = correctionJson.RootElement.GetProperty("submissionId").GetGuid();
        using var finalized = await worker.PostAsync($"/api/farms/{farmId}/work-tasks/{taskId}/evidence/{correctionId}/finalize", null, ct);
        Assert.Equal(HttpStatusCode.OK, finalized.StatusCode);
        using var resubmitted = await worker.PostAsync($"/api/farms/{farmId}/work-tasks/{taskId}/evidence/submit", null, ct);
        Assert.Equal(HttpStatusCode.OK, resubmitted.StatusCode);

        TestData.SetBearer(owner, ownerId);
        using var verified = await owner.PostAsJsonAsync($"/api/farms/{farmId}/work-tasks/{taskId}/review",
            new { action = 1, reason = (string?)null }, ct);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        using var historyResponse = await owner.GetAsync($"/api/farms/{farmId}/work-tasks/{taskId}/history", ct);
        using var history = JsonDocument.Parse(await historyResponse.Content.ReadAsStringAsync(ct));
        Assert.Equal("Verified", history.RootElement.GetProperty("status").GetString());
        Assert.Equal(2, history.RootElement.GetProperty("evidence").GetArrayLength());
        Assert.Equal(submissionId, history.RootElement.GetProperty("evidence")[1].GetProperty("previousSubmissionId").GetGuid());
        Assert.Equal(850m, history.RootElement.GetProperty("verifiedWage").GetProperty("amount").GetDecimal());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            var outbox = await db.OutboxMessages.SingleAsync(x => x.AggregateId == taskId && x.EventType == "work-task.verified", ct);
            var verifiedEvent = JsonSerializer.Deserialize<WorkTaskVerified>(outbox.PayloadJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.NotNull(verifiedEvent);
            using var consumer = new VerifiedWorkRewardsConsumer(
                new RabbitMqConnection(new ConfigurationBuilder().Build(), NullLogger<RabbitMqConnection>.Instance),
                factory.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<VerifiedWorkRewardsConsumer>.Instance);
            await consumer.ProcessMessageAsync(outbox.Id, JsonSerializer.SerializeToUtf8Bytes(verifiedEvent), ct);
        }

        TestData.SetBearer(worker, workerId);
        using var ownProgress = await worker.GetAsync($"/api/farms/{farmId}/worker-progress/{workerId}", ct);
        Assert.Equal(HttpStatusCode.OK, ownProgress.StatusCode);
        using var ownProgressJson = JsonDocument.Parse(await ownProgress.Content.ReadAsStringAsync(ct));
        Assert.Equal(15, ownProgressJson.RootElement.GetProperty("points").GetInt32());
        using var ownHistory = await worker.GetAsync($"/api/farms/{farmId}/work-tasks/{taskId}/history", ct);
        using var ownHistoryJson = JsonDocument.Parse(await ownHistory.Content.ReadAsStringAsync(ct));
        Assert.Equal(850m, ownHistoryJson.RootElement.GetProperty("verifiedWage").GetProperty("amount").GetDecimal());
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            db.FarmMemberships.Add(new FarmMembership(farmId, peerId, FarmRole.Worker, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(ct);
        }
        using var peer = factory.CreateClient();
        TestData.SetBearer(peer, peerId);
        using var peerHistory = await peer.GetAsync($"/api/farms/{farmId}/work-tasks/{taskId}/history", ct);
        Assert.Equal(HttpStatusCode.NotFound, peerHistory.StatusCode);
        await using var verifyDb = database.CreateDbContext();
        Assert.Equal(1, await verifyDb.SyncOperationReceipts.CountAsync(x => x.FarmId == farmId && x.ClientOperationId == operationId, ct));
        Assert.Equal(1, await verifyDb.VerifiedWageEntries.CountAsync(x => x.FarmId == farmId && x.WorkTaskId == taskId, ct));
        Assert.Equal(1, await verifyDb.VerifiedWorkRewards.CountAsync(x => x.FarmId == farmId && x.WorkTaskId == taskId && x.Kind == VerifiedWorkRewardKind.Points, ct));
    }
}
