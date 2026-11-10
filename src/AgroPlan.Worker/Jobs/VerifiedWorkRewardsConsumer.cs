using System.Text.Json;
using AgroPlan.Domain.Workforce;
using AgroPlan.Infrastructure.Messaging;
using AgroPlan.Infrastructure.Messaging.Events;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client.Events;

namespace AgroPlan.Worker.Jobs;

public sealed class VerifiedWorkRewardsConsumer(
    RabbitMqConnection connection,
    IServiceScopeFactory scopeFactory,
    ILogger<VerifiedWorkRewardsConsumer> logger) : BackgroundService
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
                await RabbitMqPublisher.EnsureVerifiedWorkQueueAsync(channel, stoppingToken);
                await channel.BasicQosAsync(0, 1, global: false, cancellationToken: stoppingToken);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, delivery) =>
                {
                    try
                    {
                        if (!Guid.TryParse(delivery.BasicProperties.MessageId, out var outboxId))
                            throw new InvalidOperationException("Verified-work message is missing its outbox id.");
                        await ProcessMessageAsync(outboxId, delivery.Body.ToArray(), stoppingToken);
                        await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception exception)
                    {
                        logger.LogError("Verified-work reward delivery failed ({ExceptionType}); broker will redeliver.", exception.GetType().Name);
                        await channel.BasicNackAsync(delivery.DeliveryTag, false, true, stoppingToken);
                    }
                };
                await channel.BasicConsumeAsync(RabbitMqPublisher.VerifiedWorkQueueName, false, consumerTag: "", noLocal: false,
                    exclusive: false, arguments: null, consumer: consumer, cancellationToken: stoppingToken);
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogError("Verified-work consumer failed ({ExceptionType}); retrying.", exception.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    public async Task ProcessMessageAsync(Guid outboxId, byte[] body, CancellationToken cancellationToken = default)
    {
        if (outboxId == Guid.Empty) throw new ArgumentException("A persisted outbox id is required.", nameof(outboxId));
        var message = JsonSerializer.Deserialize<WorkTaskVerified>(body, JsonOptions);
        if (message is null || message.FarmId == Guid.Empty || message.TaskId == Guid.Empty ||
            string.IsNullOrWhiteSpace(message.WorkerUserId) || message.VerifiedAt == default)
            throw new InvalidOperationException("Verified-work message is invalid.");

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var persistedOutbox = await db.OutboxMessages.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == outboxId && item.AggregateId == message.TaskId &&
                item.EventType == "work-task.verified", cancellationToken);
        var persistedEvent = persistedOutbox is null ? null : JsonSerializer.Deserialize<WorkTaskVerified>(persistedOutbox.PayloadJson, JsonOptions);
        if (persistedEvent != message)
            throw new InvalidOperationException("Verified-work event does not match a persisted outbox record.");

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT \"Id\" FROM agro.work_tasks WHERE \"FarmId\" = {message.FarmId} AND \"Id\" = {message.TaskId} FOR UPDATE",
            cancellationToken);
        var task = await db.WorkTasks.SingleOrDefaultAsync(item => item.FarmId == message.FarmId &&
            item.Id == message.TaskId && item.AssignedWorkerUserId == message.WorkerUserId &&
            item.Status == WorkTaskStatus.Verified && item.VerifiedAt == message.VerifiedAt, cancellationToken);
        if (task is null) throw new InvalidOperationException("Verified-work event has no matching persisted verified task.");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT \"FarmId\" FROM agro.farm_memberships WHERE \"FarmId\" = {message.FarmId} AND \"UserId\" = {message.WorkerUserId} FOR UPDATE",
            cancellationToken);

        var alreadyAwarded = await db.VerifiedWorkRewards.AnyAsync(item => item.FarmId == message.FarmId &&
            item.WorkTaskId == message.TaskId && item.Kind == VerifiedWorkRewardKind.Points, cancellationToken);
        if (alreadyAwarded) { await transaction.CommitAsync(cancellationToken); return; }

        var settings = await db.FarmWorkerSettings.SingleOrDefaultAsync(item => item.FarmId == message.FarmId, cancellationToken)
            ?? new FarmWorkerSettings(message.FarmId);
        var hasRequiredEvidence = !task.PhotoRequired || await db.TaskEvidenceSubmissions.AnyAsync(item =>
            item.FarmId == message.FarmId && item.WorkTaskId == message.TaskId && item.FinalizedAt != null, cancellationToken);
        var points = task.PointsForVerification + (hasRequiredEvidence ? task.CompleteEvidenceBonusPoints : 0);
        var now = DateTimeOffset.UtcNow;
        db.VerifiedWorkRewards.Add(new VerifiedWorkReward(Guid.NewGuid(), message.FarmId, message.WorkerUserId,
            VerifiedWorkRewardKind.Points, "verified-task", points, settings.RuleVersion, now, message.TaskId));

        var priorPoints = await db.VerifiedWorkRewards.AsNoTracking().Where(item => item.FarmId == message.FarmId &&
            item.WorkerUserId == message.WorkerUserId && item.Kind == VerifiedWorkRewardKind.Points).SumAsync(item => item.Points, cancellationToken);
        var badgeRule = $"verified-points-{settings.BadgeThresholdPoints}";
        if (priorPoints + points >= settings.BadgeThresholdPoints &&
            !await db.VerifiedWorkRewards.AnyAsync(item => item.FarmId == message.FarmId &&
                item.WorkerUserId == message.WorkerUserId && item.Kind == VerifiedWorkRewardKind.Badge &&
                item.RuleKey == badgeRule, cancellationToken))
            db.VerifiedWorkRewards.Add(new VerifiedWorkReward(Guid.NewGuid(), message.FarmId, message.WorkerUserId,
                VerifiedWorkRewardKind.Badge, badgeRule, 0, settings.RuleVersion, now, message.TaskId,
                description: "Verified work points milestone."));

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
