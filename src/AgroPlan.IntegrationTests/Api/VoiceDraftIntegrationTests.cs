using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgroPlan.Application.Abstractions;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Planning;
using AgroPlan.Domain.Voice;
using AgroPlan.Domain.Workforce;
using AgroPlan.Infrastructure.Identity;
using AgroPlan.Infrastructure.Messaging;
using AgroPlan.Infrastructure.Persistence;
using AgroPlan.Worker.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgroPlan.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class VoiceDraftIntegrationTests(PostgreSqlFixture postgres)
{
    [Fact]
    public async Task Owner_voice_draft_stays_partial_until_confirm_and_confirmation_is_idempotent()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString, disableBackgroundServices: true);
        using var client = factory.CreateClient();
        var ownerId = $"voice-owner-{Guid.NewGuid():N}";
        Guid farmId; Guid parcelId;
        await using (var seed = factory.Services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (farmId, parcelId) = await TestData.SeedFarmAsync(db, ownerId);
        }
        TestData.SetBearer(client, ownerId);

        using var authorized = await client.PostAsJsonAsync($"/api/farms/{farmId}/voice-drafts/upload-authorizations", new
        {
            parcelId, cropCycleId = (Guid?)null, workTaskId = (Guid?)null,
            language = "DominicanSpanish", contentType = "audio/wav", sizeBytes = 32044,
            consentToExternalProcessing = true, consentNoticeVersion = "voice-consent-v1"
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, authorized.StatusCode);
        var upload = await authorized.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var draftId = upload.GetProperty("draftId").GetGuid();

        using var finalized = await client.PostAsJsonAsync($"/api/farms/{farmId}/voice-drafts",
            new { uploadAuthorizationId = draftId }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, finalized.StatusCode);
        await using (var queued = postgres.CreateDbContext())
        {
            Assert.Equal(1, await queued.VoiceActivityDrafts.CountAsync(row => row.Id == draftId && row.ConsentedToExternalProcessing,
                TestContext.Current.CancellationToken));
            Assert.Equal(1, await queued.OutboxMessages.CountAsync(message => message.AggregateId == draftId &&
                message.EventType == "voice-draft.requested", TestContext.Current.CancellationToken));
            Assert.Empty(await queued.LogbookEntries.Where(row => row.FarmId == farmId).ToArrayAsync(TestContext.Current.CancellationToken));
        }

        var speech = new RecordingSpeechProvider("""
            {"schemaVersion":1,"language":"DominicanSpanish","activityType":"Irrigation","occurredAt":null,"details":"Irrigated the north bed","uncertainFields":["occurredAt"],"confidence":0.89}
            """);
        var consumer = new VoiceDraftConsumer(factory.Services.GetRequiredService<RabbitMqConnection>(),
            factory.Services.GetRequiredService<IServiceScopeFactory>(), factory.Storage, speech,
            NullLogger<VoiceDraftConsumer>.Instance);
        await ProcessPendingAsync(postgres, consumer, draftId);
        Assert.Single(speech.Calls);
        Assert.Equal(SpeechLanguage.DominicanSpanish, speech.Calls[0].Language);
        Assert.Equal(32044, speech.Calls[0].Audio.Length);

        using var draftRead = await client.GetAsync($"/api/farms/{farmId}/voice-drafts/{draftId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, draftRead.StatusCode);
        var draftJson = await draftRead.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("DraftReady", draftJson.GetProperty("status").GetString());
        Assert.Null(draftJson.GetProperty("occurredAt").GetDateTimeOffsetOrNull());
        Assert.Equal(0, await CountLogbookAsync(postgres, farmId));

        var draftVersion = draftJson.GetProperty("version").GetInt64();
        using var edit = await client.PutAsJsonAsync($"/api/farms/{farmId}/voice-drafts/{draftId}", new
        {
            expectedVersion = draftVersion,
            activityType = "Irrigation",
            occurredAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            details = "Irrigated the north bed"
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var edited = await edit.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var currentVersion = edited.GetProperty("version").GetInt64();

        using var confirmation = await client.PostAsJsonAsync($"/api/farms/{farmId}/voice-drafts/{draftId}/confirm",
            new { expectedVersion = currentVersion }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        var confirmed = await confirmation.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var logbookId = confirmed.GetProperty("logbookEntryId").GetGuid();
        Assert.False(confirmed.GetProperty("alreadyConfirmed").GetBoolean());
        Assert.Equal(1, await CountLogbookAsync(postgres, farmId));

        using var replay = await client.PostAsJsonAsync($"/api/farms/{farmId}/voice-drafts/{draftId}/confirm",
            new { expectedVersion = 1 }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayJson = await replay.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(replayJson.GetProperty("alreadyConfirmed").GetBoolean());
        Assert.Equal(logbookId, replayJson.GetProperty("logbookEntryId").GetGuid());
        Assert.Equal(1, await CountLogbookAsync(postgres, farmId));

        using var openApi = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, openApi.StatusCode);
        using var document = JsonDocument.Parse(await openApi.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty(
            "/api/farms/{farmId}/voice-drafts/upload-authorizations", out _));
        using var scalar = await client.GetAsync("/scalar/", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, scalar.StatusCode);
    }

    [Fact]
    public async Task Worker_authorization_is_rechecked_after_enqueue_and_missing_consent_does_not_create_upload()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString, disableBackgroundServices: true);
        using var client = factory.CreateClient();
        var ownerId = $"voice-owner-{Guid.NewGuid():N}";
        var workerId = $"voice-worker-{Guid.NewGuid():N}";
        var replacementWorkerId = $"voice-replacement-{Guid.NewGuid():N}";
        Guid farmId; Guid parcelId; Guid taskId;
        await using (var seed = factory.Services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (farmId, parcelId) = await TestData.SeedFarmAsync(db, ownerId);
            var now = DateTimeOffset.UtcNow;
            foreach (var userId in new[] { workerId, replacementWorkerId })
                db.Users.Add(User(userId));
            db.FarmMemberships.AddRange(new FarmMembership(farmId, workerId, FarmRole.Worker, now),
                new FarmMembership(farmId, replacementWorkerId, FarmRole.Worker, now));
            taskId = Guid.NewGuid();
            db.WorkTasks.Add(new WorkTask(taskId, farmId, workerId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                "Irrigate row", false, null, null, null, null, null));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        TestData.SetBearer(client, workerId);
        using var noConsent = await client.PostAsJsonAsync($"/api/farms/{farmId}/voice-drafts/upload-authorizations", new
        {
            parcelId, cropCycleId = (Guid?)null, workTaskId = taskId, language = "DominicanSpanish",
            contentType = "audio/wav", sizeBytes = 32044, consentToExternalProcessing = false,
            consentNoticeVersion = "voice-consent-v1"
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, noConsent.StatusCode);
        await using (var verifyNoConsent = postgres.CreateDbContext())
            Assert.Empty(await verifyNoConsent.VoiceActivityDrafts.Where(row => row.FarmId == farmId).ToArrayAsync(TestContext.Current.CancellationToken));

        using var uploadResponse = await client.PostAsJsonAsync($"/api/farms/{farmId}/voice-drafts/upload-authorizations", new
        {
            parcelId, cropCycleId = (Guid?)null, workTaskId = taskId, language = "HaitianCreole",
            contentType = "audio/wav", sizeBytes = 32044, consentToExternalProcessing = true,
            consentNoticeVersion = "voice-consent-v1"
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        var upload = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var draftId = upload.GetProperty("draftId").GetGuid();
        using var finalized = await client.PostAsJsonAsync($"/api/farms/{farmId}/voice-drafts",
            new { uploadAuthorizationId = draftId }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, finalized.StatusCode);

        await using (var reassign = postgres.CreateDbContext())
        {
            var task = await reassign.WorkTasks.SingleAsync(row => row.Id == taskId, TestContext.Current.CancellationToken);
            task.Reassign(replacementWorkerId);
            await reassign.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var provider = new RecordingSpeechProvider("{}");
        var consumer = new VoiceDraftConsumer(factory.Services.GetRequiredService<RabbitMqConnection>(),
            factory.Services.GetRequiredService<IServiceScopeFactory>(), factory.Storage, provider,
            NullLogger<VoiceDraftConsumer>.Instance);
        await ProcessPendingAsync(postgres, consumer, draftId);
        Assert.Empty(provider.Calls);
        await using var verify = postgres.CreateDbContext();
        var draft = await verify.VoiceActivityDrafts.SingleAsync(row => row.Id == draftId, TestContext.Current.CancellationToken);
        Assert.Equal(VoiceActivityDraftStatus.Cancelled, draft.Status);
        Assert.Equal("AUTHORIZATION_REVOKED", draft.FailureCode);
    }

    [Fact]
    public async Task Production_hides_voice_openapi_and_scalar_documents()
    {
        await using var factory = new ApiTestFactory("Host=127.0.0.1;Port=5432;Database=not_used;Username=test;Password=test",
            "Production", disableBackgroundServices: true);
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        { AllowAutoRedirect = false });
        using var document = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        using var scalar = await client.GetAsync("/scalar/", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, document.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, scalar.StatusCode);
    }

    [Fact]
    public async Task Confirmed_audio_is_deleted_and_audit_record_remains()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString, disableBackgroundServices: true);
        using var client = factory.CreateClient();
        var ownerId = $"voice-cleanup-owner-{Guid.NewGuid():N}";
        TestData.SetBearer(client, ownerId);
        Guid farmId; Guid parcelId;
        await using (var seed = factory.Services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (farmId, parcelId) = await TestData.SeedFarmAsync(db, ownerId);
        }

        using var authorization = await client.PostAsJsonAsync($"/api/farms/{farmId}/voice-drafts/upload-authorizations", new
        {
            parcelId, cropCycleId = (Guid?)null, workTaskId = (Guid?)null, language = "DominicanSpanish",
            contentType = "audio/wav", sizeBytes = 32044, consentToExternalProcessing = true,
            consentNoticeVersion = "voice-consent-v1"
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, authorization.StatusCode);
        var authorized = await authorization.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var draftId = authorized.GetProperty("draftId").GetGuid();
        using var finalize = await client.PostAsJsonAsync($"/api/farms/{farmId}/voice-drafts",
            new { uploadAuthorizationId = draftId }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, finalize.StatusCode);

        var consumer = new VoiceDraftConsumer(factory.Services.GetRequiredService<RabbitMqConnection>(),
            factory.Services.GetRequiredService<IServiceScopeFactory>(), factory.Storage,
            new RecordingSpeechProvider("""{"schemaVersion":1,"language":"DominicanSpanish","activityType":"Irrigation","occurredAt":"2026-11-10T10:00:00Z","details":"Irrigated","uncertainFields":[],"confidence":0.9}"""),
            NullLogger<VoiceDraftConsumer>.Instance);
        await ProcessPendingAsync(postgres, consumer, draftId);
        using var read = await client.GetAsync($"/api/farms/{farmId}/voice-drafts/{draftId}", TestContext.Current.CancellationToken);
        var draft = await read.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        using var confirm = await client.PostAsJsonAsync($"/api/farms/{farmId}/voice-drafts/{draftId}/confirm",
            new { expectedVersion = draft.GetProperty("version").GetInt64() }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var cleanup = new ExpiredVoiceAudioCleanupService(factory.Services.GetRequiredService<IServiceScopeFactory>(),
            factory.Storage, NullLogger<ExpiredVoiceAudioCleanupService>.Instance);
        Assert.True(await cleanup.ProcessDueAsync(TestContext.Current.CancellationToken) >= 1);
        await using var verify = postgres.CreateDbContext();
        var saved = await verify.VoiceActivityDrafts.SingleAsync(item => item.Id == draftId, TestContext.Current.CancellationToken);
        Assert.Equal(VoiceActivityDraftStatus.Confirmed, saved.Status);
        Assert.NotNull(saved.AudioDeletedAt);
        Assert.Null(saved.AudioObjectKey);
        Assert.Contains(factory.Storage.DeletedObjectKeys, key => key.Contains(draftId.ToString("D"), StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, await verify.LogbookEntries.CountAsync(item => item.FarmId == farmId, TestContext.Current.CancellationToken));
    }

    private static async Task ProcessPendingAsync(PostgreSqlFixture postgres, VoiceDraftConsumer consumer, Guid draftId)
    {
        await using var db = postgres.CreateDbContext();
        var message = await db.OutboxMessages.SingleAsync(item => item.AggregateId == draftId &&
            item.EventType == "voice-draft.requested", TestContext.Current.CancellationToken);
        await consumer.ProcessMessageAsync(message.Id, JsonSerializer.SerializeToUtf8Bytes(
            JsonSerializer.Deserialize<VoiceDraftRequested>(message.PayloadJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)), TestContext.Current.CancellationToken);
    }

    private static async Task<int> CountLogbookAsync(PostgreSqlFixture postgres, Guid farmId)
    {
        await using var db = postgres.CreateDbContext();
        return await db.LogbookEntries.CountAsync(row => row.FarmId == farmId, TestContext.Current.CancellationToken);
    }

    private static ApplicationUser User(string userId) => new()
    {
        Id = userId,
        UserName = userId,
        NormalizedUserName = userId.ToUpperInvariant(),
        Email = $"{userId}@example.test",
        NormalizedEmail = $"{userId}@EXAMPLE.TEST",
        EmailConfirmed = true
    };

    private sealed class RecordingSpeechProvider(string payload) : ISpeechTranscriptionProvider
    {
        public List<SpeechTranscriptionRequest> Calls { get; } = [];

        public Task<string> TranscribeAsync(SpeechTranscriptionRequest request, CancellationToken cancellationToken)
        {
            Calls.Add(request);
            return Task.FromResult(payload);
        }
    }
}

[CollectionDefinition("VoiceDraftOpenApi", DisableParallelization = true)]
public sealed class VoiceDraftOpenApiCollection { }

[Collection("VoiceDraftOpenApi")]
public sealed class VoiceDraftOpenApiTests
{
    [Fact]
    public async Task Development_openapi_and_scalar_include_voice_routes()
    {
        await using var factory = new ApiTestFactory("Host=127.0.0.1;Port=5432;Database=not_used;Username=test;Password=test",
            "Development", disableBackgroundServices: true);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty(
            "/api/farms/{farmId}/voice-drafts/upload-authorizations", out _));
        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty(
            "/api/farms/{farmId}/voice-drafts/{draftId}/confirm", out _));
        using var scalar = await client.GetAsync("/scalar/", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, scalar.StatusCode);
    }
}

internal static class VoiceJsonExtensions
{
    public static DateTimeOffset? GetDateTimeOffsetOrNull(this JsonElement element) =>
        element.ValueKind == JsonValueKind.Null ? null : element.GetDateTimeOffset();
}
