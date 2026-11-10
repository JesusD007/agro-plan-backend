using System.Text.Json;
using AgroPlan.Application.Planning;
using AgroPlan.Domain.Planning;
using AgroPlan.Infrastructure.Messaging;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client.Events;

namespace AgroPlan.Worker.Jobs;

public sealed class PlantingFrameConsumer(RabbitMqConnection connection, IServiceScopeFactory scopeFactory,
    ILogger<PlantingFrameConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!connection.IsConfigured) { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); continue; }
            try
            {
                var broker = await connection.GetAsync(stoppingToken);
                await using var channel = await broker.CreateChannelAsync(cancellationToken: stoppingToken);
                await RabbitMqPublisher.EnsurePlantingFramesQueueAsync(channel, stoppingToken);
                await channel.BasicQosAsync(0, 1, false, stoppingToken);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, delivery) =>
                {
                    try { await HandleAsync(delivery.Body.ToArray(), stoppingToken); await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken); }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception ex) { logger.LogError("Planting frame job failed ({ExceptionType}); broker will redeliver.", ex.GetType().Name); await channel.BasicNackAsync(delivery.DeliveryTag, false, true, stoppingToken); }
                };
                await channel.BasicConsumeAsync(queue: RabbitMqPublisher.PlantingFramesQueueName, autoAck: false,
                    consumerTag: string.Empty, noLocal: false, exclusive: false, arguments: null,
                    consumer: consumer, cancellationToken: stoppingToken);
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError("Planting frame consumer connection failed ({ExceptionType}).", ex.GetType().Name); await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
        }
    }

    private async Task HandleAsync(byte[] body, CancellationToken ct)
    {
        var message = JsonSerializer.Deserialize<FrameRequested>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (message is null || message.JobId == Guid.Empty || message.Version != 1) throw new InvalidOperationException("Invalid planting frame event.");
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        var proposal = await db.PlantingFrameProposals.SingleOrDefaultAsync(item => item.Id == message.JobId, ct);
        if (proposal is null || proposal.Status != PlantingFrameProposalStatus.Pending) return;
        var cycle = await db.CropCycles.AsNoTracking().Where(item => item.Id == proposal.CropCycleId && item.FarmId == proposal.FarmId)
            .Include(item => item.CropCatalogEntry).ThenInclude(crop => crop.Frameworks).SingleOrDefaultAsync(ct);
        if (cycle is null) { proposal.MarkFailed("CROP_CYCLE_NOT_FOUND", "The crop cycle is no longer available.", DateTimeOffset.UtcNow); await db.SaveChangesAsync(ct); return; }

        // Parcel currently stores gross hectares only; usable area and aisle geometry are absent.
        // Never substitute gross parcel area or model output for validated usable geometry.
        var calculation = new PlantingFrameCalculator().Calculate(null, null, cycle.CropCatalogEntry.Frameworks, DateTimeOffset.UtcNow, cycle.CropCatalogEntry.Region);
        var result = JsonSerializer.Serialize(new
        {
            status = "draft", options = calculation.Options, missingInputs = calculation.MissingInputs,
            sources = calculation.Options.Select(option => new { option.SourceName, option.SourceUrl, option.Region, option.ValidFrom, option.ValidTo }),
            explanation = "No hay datos de capacidad validados suficientes; no se calculó ni estimó una capacidad.",
            confirmationAllowed = false, ruleVersion = "crop-framework-v1"
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        proposal.MarkCompleted(result, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    private sealed record FrameRequested(Guid JobId, int Version);
}
