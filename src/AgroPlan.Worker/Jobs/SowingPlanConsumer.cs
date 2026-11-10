using System.Text.Json;
using AgroPlan.Domain.Planning;
using AgroPlan.Infrastructure.Messaging;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client.Events;

namespace AgroPlan.Worker.Jobs;

public sealed class SowingPlanConsumer(
    RabbitMqConnection connection,
    IServiceScopeFactory scopeFactory,
    ILogger<SowingPlanConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!connection.IsConfigured)
            {
                logger.LogWarning("RABBITMQ_URL is not configured; the proposal consumer is idle.");
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
                continue;
            }

            try
            {
                var brokerConnection = await connection.GetAsync(stoppingToken);
                await using var channel = await brokerConnection.CreateChannelAsync(cancellationToken: stoppingToken);
                await RabbitMqPublisher.EnsureQueueAsync(channel, stoppingToken);
                await channel.BasicQosAsync(0, 1, global: false, cancellationToken: stoppingToken);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, delivery) =>
                {
                    // RabbitMQ.Client 7.x exposes delivery memory that is valid only while
                    // this handler is running, so copy before any asynchronous work.
                    var body = delivery.Body.ToArray();
                    try
                    {
                        await HandleAsync(body, stoppingToken);
                        await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        logger.LogError("Proposal delivery processing failed ({ExceptionType}); the broker will redeliver it.", exception.GetType().Name);
                        await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
                    }
                };

                await channel.BasicConsumeAsync(
                    queue: RabbitMqPublisher.QueueName,
                    autoAck: false,
                    consumerTag: string.Empty,
                    noLocal: false,
                    exclusive: false,
                    arguments: null,
                    consumer: consumer,
                    cancellationToken: stoppingToken);
                logger.LogInformation("Sowing-plan consumer is listening on {QueueName}.", RabbitMqPublisher.QueueName);
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError("RabbitMQ consumer connection failed ({ExceptionType}); it will retry.", exception.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task HandleAsync(byte[] body, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<SowingPlanRequested>(body);
        if (message is null || message.JobId == Guid.Empty || message.Version != 1)
            throw new InvalidOperationException("The proposal queue message is invalid.");

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        var proposal = await dbContext.SowingProposals.SingleOrDefaultAsync(
            item => item.Id == message.JobId,
            cancellationToken);
        if (proposal is null)
        {
            // A missing aggregate is terminal: acknowledging avoids an infinite retry loop.
            logger.LogWarning("Proposal job {JobId} no longer exists; acknowledging the stale delivery.", message.JobId);
            return;
        }

        if (proposal.Status != SowingProposalStatus.Pending) return;

        // The following plan adds the Gemini adapter. Until it is registered, terminate
        // visibly instead of inventing guidance or leaving a job in pending forever.
        proposal.MarkFailed(
            "PROPOSAL_PROVIDER_NOT_CONFIGURED",
            "Proposal generation is not configured yet. No agronomic guidance was generated.",
            DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed record SowingPlanRequested(Guid JobId, int Version);
}
