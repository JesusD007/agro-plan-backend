using System.Text.Json;
using AgroPlan.Application.Abstractions;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Symptoms;
using AgroPlan.Infrastructure.Messaging;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client.Events;

namespace AgroPlan.Worker.Jobs;

public sealed class SymptomObservationConsumer(RabbitMqConnection connection, IServiceScopeFactory scopeFactory,
    IFileStorage storage, ISymptomObservationProvider provider, ILogger<SymptomObservationConsumer> logger) : BackgroundService
{
    public const string QueueName = "agroplan.symptom-observations.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!connection.IsConfigured) { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); continue; }
            try
            {
                var broker = await connection.GetAsync(stoppingToken);
                await using var channel = await broker.CreateChannelAsync(cancellationToken: stoppingToken);
                await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);
                await channel.BasicQosAsync(0, 1, false, stoppingToken);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, delivery) =>
                {
                    try
                    {
                        if (!Guid.TryParse(delivery.BasicProperties.MessageId, out var outboxId))
                            throw new InvalidOperationException("Symptom delivery has no outbox id.");
                        await ProcessMessageAsync(outboxId, delivery.Body.ToArray(), stoppingToken);
                        await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception exception)
                    {
                        logger.LogWarning("Symptom observation failed ({ExceptionType}); broker will retry.", exception.GetType().Name);
                        await channel.BasicNackAsync(delivery.DeliveryTag, false, true, stoppingToken);
                    }
                };
                await channel.BasicConsumeAsync(QueueName, false, consumerTag: string.Empty, noLocal: false, exclusive: false,
                    arguments: null, consumer: consumer, cancellationToken: stoppingToken);
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogWarning("Symptom observation consumer failed ({ExceptionType}); retrying.", exception.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    public async Task ProcessMessageAsync(Guid outboxId, byte[] body, CancellationToken ct = default)
    {
        var request = JsonSerializer.Deserialize<SymptomObservationRequested>(body, JsonOptions);
        if (request is null || outboxId == Guid.Empty || request.EventId != outboxId || request.FarmId == Guid.Empty || request.ReportId == Guid.Empty)
            throw new InvalidOperationException("Symptom observation event is invalid.");

        List<(Guid Id, string ObjectKey, string ContentType)> photos;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            var persisted = await db.OutboxMessages.AsNoTracking().SingleOrDefaultAsync(x => x.Id == outboxId &&
                x.AggregateId == request.ReportId && x.EventType == "symptom-observation.requested", ct);
            if (persisted is null || JsonSerializer.Deserialize<SymptomObservationRequested>(persisted.PayloadJson, JsonOptions) != request)
                throw new InvalidOperationException("Symptom observation does not match a persisted outbox event.");
            var report = await db.Set<SymptomReport>().Include(x => x.Photos).SingleOrDefaultAsync(x =>
                x.FarmId == request.FarmId && x.Id == request.ReportId && x.Version == request.Version && x.Status == SymptomReportStatus.Pending, ct);
            if (report is null || !await HasActiveSubmitterAsync(db, report, ct)) return;
            photos = report.Photos.Where(p => p.ConsentGranted && !p.ConsentRevokedAt.HasValue && p.FinalizedAt.HasValue &&
                p.DeletedAt == null && p.ObjectKey != null).Select(p => (p.Id, p.ObjectKey!, p.ContentType)).ToList();
            if (photos.Count == 0) return;
        }

        var results = new List<SymptomObservationResult>();
        foreach (var photo in photos)
        {
            // Recheck membership, report state and this photo's consent immediately before private retrieval.
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
                if (!await IsAuthorizedAsync(db, request, photo.Id, ct)) return;
            }
            var bytes = await storage.ReadObjectAsync(photo.ObjectKey, SymptomPhotoInspector.MaximumPhotoBytes, ct);
            if (bytes is null || !SymptomPhotoInspector.TryInspect(bytes, photo.ContentType, out _)) continue;

            // Consent can be withdrawn while storage is read; make a fresh DB check before calling the provider.
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
                if (!await IsAuthorizedAsync(db, request, photo.Id, ct)) return;
            }
            try { results.Add(await provider.ObserveAsync(new SymptomObservationRequest(bytes, null, null), ct)); }
            catch (SymptomObservationProviderException exception) when (exception.FailureKind == SymptomObservationFailureKind.NotConfigured)
            { return; }
        }
        if (results.Count == 0) return;

        // Output is untrusted. Bound it and re-check access/consent before persisting anything.
        var visible = string.Join(" ", results.Select(x => x.VisibleSigns).Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        var uncertainty = string.Join(" ", results.Select(x => x.Uncertainty).Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        var evidence = results.SelectMany(x => x.RequestedEvidence ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Take(20).ToArray();
        if (visible.Length > 4000 || uncertainty.Length > 2000 || evidence.Any(x => x.Length > 200)) return;

        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            var report = await db.Set<SymptomReport>().Include(x => x.Photos).SingleOrDefaultAsync(x =>
                x.FarmId == request.FarmId && x.Id == request.ReportId && x.Version == request.Version && x.Status == SymptomReportStatus.Pending, ct);
            if (report is null || !await HasActiveSubmitterAsync(db, report, ct) ||
                !photos.All(photo => report.Photos.Any(p => p.Id == photo.Id && p.ConsentGranted && !p.ConsentRevokedAt.HasValue))) return;
            if (!report.TryStartObservation(DateTimeOffset.UtcNow)) return;
            report.CompleteObservation(visible, uncertainty, results.All(x => x.ImageSufficient), evidence, DateTimeOffset.UtcNow);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { /* A newer report state wins; discard stale model output. */ }
        }
    }

    private static Task<bool> HasActiveSubmitterAsync(AgroPlanDbContext db, SymptomReport report, CancellationToken ct) =>
        db.FarmMemberships.AsNoTracking().AnyAsync(x => x.FarmId == report.FarmId && x.UserId == report.SubmittedByUserId && x.IsActive, ct);

    private static async Task<bool> IsAuthorizedAsync(AgroPlanDbContext db, SymptomObservationRequested request, Guid photoId, CancellationToken ct)
    {
        var report = await db.Set<SymptomReport>().AsNoTracking().Include(x => x.Photos).SingleOrDefaultAsync(x =>
            x.FarmId == request.FarmId && x.Id == request.ReportId && x.Version == request.Version && x.Status == SymptomReportStatus.Pending, ct);
        return report is not null && await HasActiveSubmitterAsync(db, report, ct) && report.Photos.Any(p => p.Id == photoId &&
            p.ConsentGranted && !p.ConsentRevokedAt.HasValue && p.FinalizedAt.HasValue && p.DeletedAt == null);
    }

    private sealed record SymptomObservationRequested(Guid EventId, Guid FarmId, Guid ReportId, int Version);
}
