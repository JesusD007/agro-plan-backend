using System.Text.Json;
using AgroPlan.Application.Abstractions;
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

public sealed class TaskCalendarSyncConsumer(RabbitMqConnection connection, IServiceScopeFactory scopes,
    IExternalWorkCalendar calendar, ILogger<TaskCalendarSyncConsumer> logger) : BackgroundService
{
    private const int MaximumAttempts = 5;
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
                await RabbitMqPublisher.EnsureTaskCalendarQueueAsync(channel, stoppingToken);
                await channel.BasicQosAsync(0, 1, global: false, cancellationToken: stoppingToken);
                var retryTask = RunRetryLoopAsync(stoppingToken);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, delivery) =>
                {
                    try
                    {
                        if (!Guid.TryParse(delivery.BasicProperties.MessageId, out var outboxId))
                            throw new InvalidOperationException("Calendar sync message is missing its outbox id.");
                        await ProcessMessageAsync(outboxId, delivery.Body.ToArray(), stoppingToken);
                        await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception exception)
                    {
                        logger.LogError("Calendar operation failed before a durable outcome was recorded ({ExceptionType}); broker will redeliver.", exception.GetType().Name);
                        await channel.BasicNackAsync(delivery.DeliveryTag, false, true, stoppingToken);
                    }
                };
                await channel.BasicConsumeAsync(RabbitMqPublisher.TaskCalendarQueueName, false, consumerTag: "",
                    noLocal: false, exclusive: false, arguments: null, consumer: consumer, cancellationToken: stoppingToken);
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
                await retryTask;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogError("Calendar sync consumer failed ({ExceptionType}); retrying.", exception.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    public async Task ProcessMessageAsync(Guid outboxId, byte[] body, CancellationToken cancellationToken = default)
    {
        var request = JsonSerializer.Deserialize<TaskCalendarSyncRequested>(body, JsonOptions);
        if (request is null || outboxId == Guid.Empty || request.FarmId == Guid.Empty || request.TaskId == Guid.Empty ||
            request.RevisionId == Guid.Empty || request.TaskVersion <= 0 || request.DueDate == default || request.RequestedAt == default)
            throw new InvalidOperationException("Task calendar sync event is invalid.");
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        var persisted = await db.OutboxMessages.AsNoTracking().SingleOrDefaultAsync(x => x.Id == outboxId &&
            x.AggregateId == request.TaskId && x.EventType == "task-calendar.sync", cancellationToken);
        if (persisted is null || JsonSerializer.Deserialize<TaskCalendarSyncRequested>(persisted.PayloadJson, JsonOptions) != request)
            throw new InvalidOperationException("Calendar sync event does not match a persisted outbox operation.");
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT \"Id\" FROM agro.calendar_delivery_statuses WHERE \"OutboxEventId\" = {outboxId} FOR UPDATE",
            cancellationToken);
        var status = await db.CalendarDeliveryStatuses.SingleOrDefaultAsync(x => x.OutboxEventId == outboxId, cancellationToken);
        if (status is null)
        {
            status = new CalendarDeliveryStatus(Guid.NewGuid(), outboxId, request.FarmId, request.TaskId,
                request.RevisionId, request.TaskVersion, now);
            db.CalendarDeliveryStatuses.Add(status);
        }
        if (!status.CanAttempt(now)) { await transaction.CommitAsync(cancellationToken); return; }
        var task = await db.WorkTasks.AsNoTracking().SingleOrDefaultAsync(x => x.FarmId == request.FarmId &&
            x.Id == request.TaskId, cancellationToken);
        var revision = await db.TaskScheduleRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.FarmId == request.FarmId &&
            x.Id == request.RevisionId && x.WorkTaskId == request.TaskId, cancellationToken);
        if (task is null || revision is null || task.Version != request.TaskVersion ||
            task.DueDate != request.DueDate || revision.Status != TaskScheduleRevisionStatus.Confirmed)
        {
            status.StartAttempt(now);
            status.MarkFailed("SCHEDULE_VERSION_STALE", retryable: false, now, TimeSpan.Zero, MaximumAttempts);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }
        status.StartAttempt(now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        ExternalCalendarDeliveryResult outcome;
        try
        {
            outcome = await calendar.UpsertAsync(new ExternalWorkCalendarEntry(request.FarmId, request.TaskId,
                request.RevisionId, task.AssignedWorkerUserId, request.DueDate, request.TaskVersion, outboxId.ToString("N")),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            outcome = new ExternalCalendarDeliveryResult(false, true, exception.GetType().Name);
        }
        if (outcome.Succeeded)
            status.MarkSucceeded(DateTimeOffset.UtcNow);
        else
        {
            var retryDelay = TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2, status.AttemptCount)));
            status.MarkFailed(outcome.ErrorCode ?? "CALENDAR_DELIVERY_FAILED", outcome.Retryable,
                DateTimeOffset.UtcNow, retryDelay, MaximumAttempts);
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ProcessDueRetriesAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        var now = DateTimeOffset.UtcNow;
        var pending = await db.CalendarDeliveryStatuses.AsNoTracking()
            .Where(status => status.State == CalendarDeliveryState.Retryable && status.NextAttemptAt <= now)
            .OrderBy(status => status.NextAttemptAt).Take(50)
            .Join(db.OutboxMessages.AsNoTracking(), status => status.OutboxEventId, message => message.Id,
                (status, message) => new { message.Id, message.PayloadJson }).ToListAsync(cancellationToken);
        foreach (var item in pending)
        {
            try { await ProcessMessageAsync(item.Id, JsonSerializer.SerializeToUtf8Bytes(
                JsonSerializer.Deserialize<TaskCalendarSyncRequested>(item.PayloadJson, JsonOptions)!, JsonOptions), cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                logger.LogWarning("One calendar retry remains pending after an error ({ExceptionType}); other retries continue.", exception.GetType().Name);
            }
        }
    }

    private async Task RunRetryLoopAsync(CancellationToken stoppingToken)
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
                logger.LogWarning("Calendar retry polling failed ({ExceptionType}); retry loop continues.", exception.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }
}
