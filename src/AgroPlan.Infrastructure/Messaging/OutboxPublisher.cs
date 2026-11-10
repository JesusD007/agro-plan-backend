using AgroPlan.Domain.Planning;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgroPlan.Infrastructure.Messaging;

public sealed class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    RabbitMqConnection connection,
    RabbitMqPublisher rabbitPublisher,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private const int BatchSize = 20;
    private const int MaxAttempts = 3;
    private static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!connection.IsConfigured)
            {
                logger.LogWarning("RABBITMQ_URL is not configured; outbox messages remain durably pending.");
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
                continue;
            }

            try
            {
                var messages = await ClaimBatchAsync(stoppingToken);
                if (messages.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    continue;
                }

                foreach (var message in messages)
                {
                    stoppingToken.ThrowIfCancellationRequested();
                    await PublishOneAsync(message, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError("Outbox polling failed ({ExceptionType}); pending messages remain in PostgreSQL.", exception.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task<IReadOnlyList<OutboxMessage>> ClaimBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var messages = await dbContext.OutboxMessages
            .FromSqlInterpolated($"SELECT * FROM infra.outbox_messages WHERE \"PublishedAt\" IS NULL AND \"DeadLetteredAt\" IS NULL AND \"AvailableAt\" <= {now} AND (\"LockedUntil\" IS NULL OR \"LockedUntil\" <= {now}) ORDER BY \"OccurredAt\" LIMIT {BatchSize} FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken);

        foreach (var message in messages) message.Claim(now, ClaimLease);
        if (messages.Count > 0) await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return messages;
    }

    private async Task PublishOneAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await rabbitPublisher.PublishAsync(message, cancellationToken);
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            var storedMessage = await dbContext.OutboxMessages.SingleOrDefaultAsync(item => item.Id == message.Id, cancellationToken);
            if (storedMessage is null) return;
            storedMessage.MarkPublished(DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await RecordFailureAsync(message, exception, cancellationToken);
        }
    }

    private async Task RecordFailureAsync(OutboxMessage message, Exception exception, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var retrySeconds = Math.Pow(2, Math.Max(0, message.AttemptCount - 1));
        var retryDelay = TimeSpan.FromSeconds(retrySeconds) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000));
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        var storedMessage = await dbContext.OutboxMessages.SingleOrDefaultAsync(item => item.Id == message.Id, cancellationToken);
        if (storedMessage is null) return;
        var deadLettered = storedMessage.MarkDeliveryFailed(exception.GetType().Name, now, retryDelay, MaxAttempts);
        if (deadLettered && storedMessage.EventType == "sowing-plan.requested")
        {
            var proposal = await dbContext.SowingProposals.SingleOrDefaultAsync(item => item.Id == storedMessage.AggregateId, cancellationToken);
            proposal?.MarkFailed("BROKER_DELIVERY_FAILED", "The asynchronous queue could not accept this request.", now);
        }
        if (deadLettered && storedMessage.EventType == "planting-frame.requested")
        {
            var proposal = await dbContext.PlantingFrameProposals.SingleOrDefaultAsync(item => item.Id == storedMessage.AggregateId, cancellationToken);
            proposal?.MarkFailed("BROKER_DELIVERY_FAILED", "The asynchronous queue could not accept this request.", now);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        if (deadLettered)
            logger.LogError("Outbox message {OutboxId} exhausted {AttemptCount} delivery attempts ({ExceptionType}).", message.Id, message.AttemptCount, exception.GetType().Name);
        else
            logger.LogWarning("Outbox message {OutboxId} publish failed on attempt {AttemptCount}; a bounded retry is scheduled ({ExceptionType}).", message.Id, message.AttemptCount, exception.GetType().Name);
    }
}
