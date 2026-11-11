using System.Text.Json;
using AgroPlan.Application.Abstractions;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Logbook;
using AgroPlan.Domain.Voice;
using AgroPlan.Domain.Workforce;
using AgroPlan.Infrastructure.Messaging;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AgroPlan.Worker.Jobs;

public sealed class VoiceDraftConsumer(RabbitMqConnection connection, IServiceScopeFactory scopeFactory,
    IFileStorage storage, ISpeechTranscriptionProvider provider, ILogger<VoiceDraftConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!connection.IsConfigured)
            {
                logger.LogWarning("RABBITMQ_URL is not configured; voice transcription jobs remain durably pending.");
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
                continue;
            }

            try
            {
                var broker = await connection.GetAsync(stoppingToken);
                await using var channel = await broker.CreateChannelAsync(cancellationToken: stoppingToken);
                await RabbitMqPublisher.EnsureVoiceDraftsQueueAsync(channel, stoppingToken);
                await channel.BasicQosAsync(0, 1, global: false, cancellationToken: stoppingToken);
                var retryLoop = RetryPendingAsync(stoppingToken);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, delivery) =>
                {
                    try
                    {
                        if (!Guid.TryParse(delivery.BasicProperties.MessageId, out var outboxId))
                            throw new InvalidOperationException("Voice delivery is missing its outbox id.");
                        await ProcessMessageAsync(outboxId, delivery.Body.ToArray(), stoppingToken);
                        await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception exception)
                    {
                        logger.LogError("Voice job failed before a durable state was recorded ({ExceptionType}); broker will redeliver.", exception.GetType().Name);
                        await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
                    }
                };
                await channel.BasicConsumeAsync(RabbitMqPublisher.VoiceDraftsQueueName, autoAck: false,
                    consumerTag: string.Empty, noLocal: false, exclusive: false, arguments: null,
                    consumer: consumer, cancellationToken: stoppingToken);
                logger.LogInformation("Voice draft consumer is listening on {QueueName}.", RabbitMqPublisher.VoiceDraftsQueueName);
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
                await retryLoop;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogError("Voice consumer failed ({ExceptionType}); it will retry.", exception.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    public async Task ProcessMessageAsync(Guid outboxId, byte[] body, CancellationToken cancellationToken = default)
    {
        var requested = JsonSerializer.Deserialize<VoiceDraftRequested>(body, JsonOptions);
        if (requested is null || outboxId == Guid.Empty || requested.EventId != outboxId ||
            requested.FarmId == Guid.Empty || requested.DraftId == Guid.Empty || requested.DraftVersion <= 0)
            throw new InvalidOperationException("Voice transcription event is invalid.");

        VoiceActivityDraftStatus claimedStatus;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            var persisted = await db.OutboxMessages.AsNoTracking().SingleOrDefaultAsync(item =>
                item.Id == outboxId && item.AggregateId == requested.DraftId && item.EventType == "voice-draft.requested", cancellationToken);
            if (persisted is null || JsonSerializer.Deserialize<VoiceDraftRequested>(persisted.PayloadJson, JsonOptions) != requested)
                throw new InvalidOperationException("Voice event does not match a persisted outbox operation.");

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM agro.voice_activity_drafts WHERE \"FarmId\" = {requested.FarmId} AND \"Id\" = {requested.DraftId} FOR UPDATE", cancellationToken);
            var draft = await db.VoiceActivityDrafts.SingleOrDefaultAsync(item => item.FarmId == requested.FarmId &&
                item.Id == requested.DraftId, cancellationToken);
            if (draft is null) { await transaction.CommitAsync(cancellationToken); return; }
            if (draft.Status is VoiceActivityDraftStatus.DraftReady or VoiceActivityDraftStatus.Unavailable or
                VoiceActivityDraftStatus.Confirmed or VoiceActivityDraftStatus.Cancelled)
            { await transaction.CommitAsync(cancellationToken); return; }

            if (!await HasCurrentAuthorization(db, draft, cancellationToken) ||
                !draft.ConsentedToExternalProcessing || draft.ConsentRevokedAt.HasValue)
            {
                if (draft.ConsentRevokedAt.HasValue || !draft.ConsentedToExternalProcessing)
                    draft.RevokeConsent(draft.SubmittedByUserId, DateTimeOffset.UtcNow);
                else draft.CancelForAuthorizationRevocation(DateTimeOffset.UtcNow);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            var now = DateTimeOffset.UtcNow;
            if (draft.Status == VoiceActivityDraftStatus.Processing && draft.UpdatedAt >= now.AddMinutes(-15))
            { await transaction.CommitAsync(cancellationToken); return; }
            if (!draft.TryStartProcessing(now, now.AddMinutes(-15)))
            {
                if (draft.Status == VoiceActivityDraftStatus.Pending && draft.ProcessingAttemptCount >= 3)
                    draft.MarkUnavailable("SPEECH_TEMPORARILY_UNAVAILABLE", now);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return;
            }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            claimedStatus = draft.Status;
        }

        if (claimedStatus != VoiceActivityDraftStatus.Processing) return;
        var authorized = await LoadAuthorizedDraftAsync(requested.FarmId, requested.DraftId, cancellationToken);
        if (authorized is null) return;
        var draftSnapshot = authorized;
        byte[]? audio;
        try
        {
            audio = await storage.ReadObjectAsync(draftSnapshot.AudioObjectKey!, 10 * 1024 * 1024, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogWarning("Voice media retrieval failed ({ExceptionType}).", exception.GetType().Name);
            await MarkUnavailableAsync(requested.FarmId, requested.DraftId, "VOICE_MEDIA_UNAVAILABLE", cancellationToken);
            return;
        }

        if (audio is null || audio.LongLength != draftSnapshot.ExpectedSizeBytes ||
            !PcmWaveAudioInspector.TryGetDurationSeconds(audio, out var duration) || duration != draftSnapshot.AudioDurationSeconds)
        {
            await MarkUnavailableAsync(requested.FarmId, requested.DraftId, "VOICE_MEDIA_INVALID", cancellationToken);
            return;
        }

        var beforeProvider = await LoadAuthorizedDraftAsync(requested.FarmId, requested.DraftId, cancellationToken);
        if (beforeProvider is null) return;
        string payload;
        try
        {
            payload = await provider.TranscribeAsync(new SpeechTranscriptionRequest(audio, beforeProvider.Language), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (SpeechTranscriptionProviderException exception) when (exception.FailureKind == SpeechTranscriptionFailureKind.TemporarilyUnavailable)
        {
            await ScheduleRetryAsync(requested.FarmId, requested.DraftId, cancellationToken);
            return;
        }
        catch (SpeechTranscriptionProviderException exception) when (exception.FailureKind == SpeechTranscriptionFailureKind.NotConfigured)
        {
            await MarkUnavailableAsync(requested.FarmId, requested.DraftId, "SPEECH_PROVIDER_NOT_CONFIGURED", cancellationToken);
            return;
        }
        catch (Exception exception)
        {
            logger.LogWarning("Speech provider failed ({ExceptionType}).", exception.GetType().Name);
            await MarkUnavailableAsync(requested.FarmId, requested.DraftId, "SPEECH_PROVIDER_FAILED", cancellationToken);
            return;
        }

        if (!SpeechTranscriptionResultValidator.TryValidate(payload, beforeProvider.Language, out var result) || result is null)
        {
            await MarkUnavailableAsync(requested.FarmId, requested.DraftId, "SPEECH_RESPONSE_INVALID", cancellationToken);
            return;
        }

        await using var publishScope = scopeFactory.CreateAsyncScope();
        var publishDb = publishScope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        await using var publishTransaction = await publishDb.Database.BeginTransactionAsync(cancellationToken);
        await publishDb.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT \"Id\" FROM agro.voice_activity_drafts WHERE \"FarmId\" = {requested.FarmId} AND \"Id\" = {requested.DraftId} FOR UPDATE", cancellationToken);
        var current = await publishDb.VoiceActivityDrafts.SingleOrDefaultAsync(item => item.FarmId == requested.FarmId &&
            item.Id == requested.DraftId, cancellationToken);
        if (current is null || current.Status != VoiceActivityDraftStatus.Processing) { await publishTransaction.CommitAsync(cancellationToken); return; }
        if (!await HasCurrentAuthorization(publishDb, current, cancellationToken) ||
            current.ConsentRevokedAt.HasValue || !current.ConsentedToExternalProcessing)
        {
            if (current.ConsentRevokedAt.HasValue || !current.ConsentedToExternalProcessing)
                current.RevokeConsent(current.SubmittedByUserId, DateTimeOffset.UtcNow);
            else current.CancelForAuthorizationRevocation(DateTimeOffset.UtcNow);
            await publishDb.SaveChangesAsync(cancellationToken);
            await publishTransaction.CommitAsync(cancellationToken);
            return;
        }
        current.MarkDraftReady(result.ActivityType, result.OccurredAt, result.Details,
            result.UncertainFields, result.Confidence, DateTimeOffset.UtcNow);
        await publishDb.SaveChangesAsync(cancellationToken);
        await publishTransaction.CommitAsync(cancellationToken);
    }

    public async Task ProcessDueRetriesAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        var now = DateTimeOffset.UtcNow;
        var due = await (from draft in db.VoiceActivityDrafts.AsNoTracking()
                         join message in db.OutboxMessages.AsNoTracking() on draft.Id equals message.AggregateId
                         where draft.Status == VoiceActivityDraftStatus.Pending && draft.RetryAt <= now &&
                               message.EventType == "voice-draft.requested"
                         orderby draft.RetryAt
                         select new { message.Id, message.PayloadJson }).Take(50).ToArrayAsync(cancellationToken);
        foreach (var item in due)
        {
            try { await ProcessMessageAsync(item.Id, JsonSerializer.SerializeToUtf8Bytes(
                    JsonSerializer.Deserialize<VoiceDraftRequested>(item.PayloadJson, JsonOptions)!, JsonOptions), cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                logger.LogWarning("A voice retry remains pending after an error ({ExceptionType}).", exception.GetType().Name);
            }
        }
    }

    private async Task<VoiceActivityDraft?> LoadAuthorizedDraftAsync(Guid farmId, Guid draftId, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        var draft = await db.VoiceActivityDrafts.AsNoTracking().SingleOrDefaultAsync(item =>
            item.FarmId == farmId && item.Id == draftId, ct);
        if (draft is null) return null;
        if (!await HasCurrentAuthorization(db, draft, ct) || draft.ConsentRevokedAt.HasValue || !draft.ConsentedToExternalProcessing)
        {
            await CancelJobAsync(farmId, draftId, ct);
            return null;
        }
        return draft;
    }

    private static async Task<bool> HasCurrentAuthorization(AgroPlanDbContext db, VoiceActivityDraft draft, CancellationToken ct)
    {
        var role = await db.FarmMemberships.AsNoTracking().Where(member => member.FarmId == draft.FarmId &&
                member.UserId == draft.SubmittedByUserId && member.IsActive)
            .Select(member => (FarmRole?)member.Role).SingleOrDefaultAsync(ct);
        if (role is FarmRole.Owner or FarmRole.Administrator) return true;
        if (role != FarmRole.Worker || !draft.WorkTaskId.HasValue) return false;
        return await db.WorkTasks.AsNoTracking().AnyAsync(task => task.FarmId == draft.FarmId &&
            task.Id == draft.WorkTaskId.Value && task.AssignedWorkerUserId == draft.SubmittedByUserId &&
            (task.Status == WorkTaskStatus.Pending || task.Status == WorkTaskStatus.InProgress ||
             task.Status == WorkTaskStatus.CorrectionRequested), ct);
    }

    private async Task MarkUnavailableAsync(Guid farmId, Guid draftId, string code, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT \"Id\" FROM agro.voice_activity_drafts WHERE \"FarmId\" = {farmId} AND \"Id\" = {draftId} FOR UPDATE", ct);
        var draft = await db.VoiceActivityDrafts.SingleOrDefaultAsync(item => item.FarmId == farmId && item.Id == draftId, ct);
        if (draft is not null && draft.Status == VoiceActivityDraftStatus.Processing)
        {
            draft.MarkUnavailable(code, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }

    private async Task ScheduleRetryAsync(Guid farmId, Guid draftId, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT \"Id\" FROM agro.voice_activity_drafts WHERE \"FarmId\" = {farmId} AND \"Id\" = {draftId} FOR UPDATE", ct);
        var draft = await db.VoiceActivityDrafts.SingleOrDefaultAsync(item => item.FarmId == farmId && item.Id == draftId, ct);
        if (draft is not null && draft.Status == VoiceActivityDraftStatus.Processing)
        {
            var now = DateTimeOffset.UtcNow;
            var delay = TimeSpan.FromSeconds(5 * Math.Pow(2, Math.Max(0, draft.ProcessingAttemptCount - 1)));
            draft.ScheduleTransientRetry(now.Add(delay), now);
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }

    private async Task CancelJobAsync(Guid farmId, Guid draftId, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        var draft = await db.VoiceActivityDrafts.SingleOrDefaultAsync(item => item.FarmId == farmId && item.Id == draftId, ct);
        if (draft is null) return;
        if (draft.ConsentRevokedAt.HasValue || !draft.ConsentedToExternalProcessing)
            draft.RevokeConsent(draft.SubmittedByUserId, DateTimeOffset.UtcNow);
        else draft.CancelForAuthorizationRevocation(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    private async Task RetryPendingAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueRetriesAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogWarning("Voice retry polling failed ({ExceptionType}); retry loop continues.", exception.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }
}
