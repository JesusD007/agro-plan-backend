using System.Text.Json;
using AgroPlan.Application.Workforce;
using AgroPlan.Domain.Workforce;
using AgroPlan.Infrastructure.Messaging;
using AgroPlan.Infrastructure.Messaging.Events;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AgroPlan.Worker.Jobs;

public sealed class ConfirmedRecordTaskImpactConsumer(RabbitMqConnection connection, IServiceScopeFactory scopes,
    WorkTaskImpactProjector projector, ILogger<ConfirmedRecordTaskImpactConsumer> logger) : BackgroundService
{
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
                await RabbitMqPublisher.EnsureConfirmedRecordsQueueAsync(channel, stoppingToken);
                await channel.BasicQosAsync(0, 1, global: false, cancellationToken: stoppingToken);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, delivery) =>
                {
                    try
                    {
                        if (!Guid.TryParse(delivery.BasicProperties.MessageId, out var outboxId))
                            throw new InvalidOperationException("Confirmed-record event has no outbox id.");
                        await ProcessMessageAsync(outboxId, delivery.Body.ToArray(), stoppingToken);
                        await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception exception)
                    {
                        logger.LogError("Confirmed-record task projection failed ({ExceptionType}); broker will redeliver.", exception.GetType().Name);
                        await channel.BasicNackAsync(delivery.DeliveryTag, false, true, stoppingToken);
                    }
                };
                await channel.BasicConsumeAsync(RabbitMqPublisher.ConfirmedRecordsQueueName, false, consumerTag: "",
                    noLocal: false, exclusive: false, arguments: null, consumer: consumer, cancellationToken: stoppingToken);
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogError("Confirmed-record task impact consumer failed ({ExceptionType}); retrying.", exception.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    public async Task ProcessMessageAsync(Guid outboxId, byte[] body, CancellationToken cancellationToken = default)
    {
        var record = JsonSerializer.Deserialize<FarmRecordConfirmed>(body, JsonOptions);
        if (record is null || outboxId == Guid.Empty || record.EventId != outboxId || record.FarmId == Guid.Empty ||
            record.ParcelId == Guid.Empty || record.SourceRecordId == Guid.Empty || record.SourceRecordVersion <= 0 ||
            record.OccurredOn == default || string.IsNullOrWhiteSpace(record.OperationKind))
            throw new InvalidOperationException("Confirmed-record event is invalid.");
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var persisted = await db.OutboxMessages.AsNoTracking().SingleOrDefaultAsync(item => item.Id == outboxId &&
            item.AggregateId == record.SourceRecordId && item.EventType == "farm-record.confirmed", cancellationToken);
        if (persisted is null || JsonSerializer.Deserialize<FarmRecordConfirmed>(persisted.PayloadJson, JsonOptions) != record)
            throw new InvalidOperationException("Confirmed-record event does not match a persisted outbox message.");
        if (await db.TaskScheduleRevisions.AnyAsync(item => item.FarmId == record.FarmId &&
                item.SourceEventId == outboxId, cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }
        if (!await SourceExistsAsync(db, record, cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }
        var candidates = await db.WorkTasks.Where(task => task.FarmId == record.FarmId && task.ParcelId == record.ParcelId &&
            task.CropCycleId == record.CropCycleId && task.OperationKind == record.OperationKind &&
            (task.Status == WorkTaskStatus.Pending || task.Status == WorkTaskStatus.InProgress)).ToListAsync(cancellationToken);
        var impacts = projector.Project(new ConfirmedFarmRecord(record.EventId, record.FarmId, record.ParcelId,
            record.CropCycleId, record.OperationKind, record.SourceRecordId, record.SourceRecordVersion, record.OccurredOn), candidates);
        // An ambiguous record-to-task match is intentionally ignored until a human can disambiguate it.
        if (impacts.Count == 1)
        {
            var impact = impacts[0];
            db.TaskScheduleRevisions.Add(new TaskScheduleRevision(Guid.NewGuid(), record.FarmId, impact.Task.Id,
                record.EventId, record.SourceRecordId, record.SourceRecordVersion, impact.Task.Version,
                impact.Task.DueDate, impact.ProposedDate, impact.Explanation, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<bool> SourceExistsAsync(AgroPlanDbContext db, FarmRecordConfirmed record, CancellationToken ct)
    {
        var logbook = await db.LogbookEntries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == record.SourceRecordId &&
            x.FarmId == record.FarmId && x.ParcelId == record.ParcelId && x.CropCycleId == record.CropCycleId, ct);
        if (logbook is not null && logbook.ActivityType.ToString().Equals(record.OperationKind, StringComparison.OrdinalIgnoreCase) &&
            DateOnly.FromDateTime(logbook.OccurredAt.UtcDateTime) == record.OccurredOn) return true;
        var irrigation = await db.IrrigationEntries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == record.SourceRecordId &&
            x.FarmId == record.FarmId && x.ParcelId == record.ParcelId && x.CropCycleId == record.CropCycleId, ct);
        if (irrigation is not null && record.OperationKind == "irrigation" && DateOnly.FromDateTime(irrigation.OccurredAt.UtcDateTime) == record.OccurredOn) return true;
        var application = await db.InputApplicationEntries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == record.SourceRecordId &&
            x.FarmId == record.FarmId && x.ParcelId == record.ParcelId && x.CropCycleId == record.CropCycleId, ct);
        if (application is not null && record.OperationKind == "input-application" && DateOnly.FromDateTime(application.AppliedAt.UtcDateTime) == record.OccurredOn) return true;
        var harvest = await db.HarvestEntries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == record.SourceRecordId &&
            x.FarmId == record.FarmId && x.ParcelId == record.ParcelId && x.CropCycleId == record.CropCycleId, ct);
        return harvest is not null && record.OperationKind == "harvest" && DateOnly.FromDateTime(harvest.HarvestedAt.UtcDateTime) == record.OccurredOn;
    }
}
