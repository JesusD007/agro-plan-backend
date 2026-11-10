using System.Text.Json;
using AgroPlan.Application.Abstractions;
using AgroPlan.Domain.Crops;
using AgroPlan.Worker.AI;
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
    IClimateProvider climateProvider,
    IGeminiProposalProvider geminiProvider,
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
        try
        {
            var snapshot = JsonSerializer.Deserialize<ProposalSnapshot>(proposal.InputSnapshotJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (snapshot is null) throw new InvalidOperationException("The proposal input snapshot is invalid.");

            var cycle = await dbContext.CropCycles.AsNoTracking()
                .Where(item => item.FarmId == proposal.FarmId && item.Id == proposal.CropCycleId)
                .Include(item => item.CropCatalogEntry)
                .ThenInclude(crop => crop.Parameters)
                .SingleOrDefaultAsync(cancellationToken);
            if (cycle is null)
            {
                proposal.MarkFailed("CROP_CYCLE_NOT_FOUND", "The crop cycle is no longer available.", DateTimeOffset.UtcNow);
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            var now = DateTimeOffset.UtcNow;
            var climate = await climateProvider.GetAsync(new ClimateRequest(
                snapshot.LocationDescription, null, null, snapshot.DesiredPlantingDate, now), cancellationToken);
            var parameters = cycle.CropCatalogEntry.Parameters
                .Where(parameter => parameter.IsUsableAt(now))
                .Select(parameter => new ValidatedAgronomicInput(
                    parameter.Key, parameter.NumericValue!.Value, parameter.Unit!, parameter.SourceName!,
                    parameter.SourceUrl, parameter.Region, parameter.ValidFrom, parameter.ValidTo))
                .ToArray();
            var input = new SowingGenerationInput(
                cycle.CropCatalogEntry.Code,
                cycle.CropCatalogEntry.DisplayName,
                cycle.CropCatalogEntry.ValidationStatus.ToString(),
                cycle.CropCatalogEntry.Region,
                cycle.CropCatalogEntry.ValidFrom,
                cycle.CropCatalogEntry.ValidTo,
                snapshot.DesiredPlantingDate,
                snapshot.ParcelAreaHectares,
                snapshot.LocationDescription,
                parameters,
                climate);
            var missingInputs = snapshot.MissingInputs
                .Concat(climate.MissingInputs)
                .Concat(parameters.Length == 0 ? ["validated.crop.parameters"] : [])
                .Concat(cycle.CropCatalogEntry.ValidationStatus != CropCatalogValidationStatus.Validated ? ["validated.crop.catalog"] : [])
                .Concat(string.IsNullOrWhiteSpace(snapshot.LocationDescription) ? ["parcel.location"] : [])
                .Concat(["parcel.geometry"])
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var climateValidated = climate.Status == "available" &&
                                   !string.IsNullOrWhiteSpace(climate.SourceName) &&
                                   climate.ForecastIssuedAt.HasValue &&
                                   !string.IsNullOrWhiteSpace(climate.GeographicCoverage);
            if (!climateValidated && !missingInputs.Contains("climate.forecast", StringComparer.Ordinal))
                missingInputs = [.. missingInputs, "climate.forecast"];

            if (missingInputs.Length > 0)
            {
                var safeResult = JsonSerializer.Serialize(new
                {
                    status = "insufficient_data",
                    summary = "No agronomic recommendation was generated because required validated inputs are missing.",
                    sourceIds = Array.Empty<string>(),
                    missingInputs,
                    sources = Array.Empty<object>(),
                    climate,
                    model = (string?)null,
                    promptVersion = (string?)null,
                    schemaVersion = (string?)null,
                    generatedAt = now,
                    modelProvenance = new { provider = "deterministic", model = (string?)null, promptVersion = (string?)null, schemaVersion = (string?)null },
                    inputSnapshot = proposal.InputSnapshotJson,
                    confirmationAllowed = false
                }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                proposal.MarkCompleted(safeResult, DateTimeOffset.UtcNow);
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            var draft = await geminiProvider.GenerateDraftAsync(input, cancellationToken);
            var resultMissingInputs = missingInputs.Concat(draft.MissingInputs).Distinct(StringComparer.Ordinal).ToArray();
            var sourceIds = draft.SourceIds.Distinct(StringComparer.Ordinal).ToArray();
            var result = JsonSerializer.Serialize(new
            {
                status = "draft",
                draft.Summary,
                sourceIds,
                missingInputs = resultMissingInputs,
                sources = parameters.Select(parameter => new { parameter.Key, parameter.SourceName, parameter.SourceUrl, parameter.ValidFrom, parameter.ValidTo }),
                climate,
                model = draft.Model,
                promptVersion = draft.PromptVersion,
                schemaVersion = draft.SchemaVersion,
                modelProvenance = new { provider = "google-ai-studio", model = draft.Model, promptVersion = draft.PromptVersion, schemaVersion = draft.SchemaVersion },
                generatedAt = draft.GeneratedAt,
                inputSnapshot = proposal.InputSnapshotJson,
                confirmationAllowed = false
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            proposal.MarkCompleted(result, DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (GeminiConfigurationException)
        {
            proposal.MarkFailed("GEMINI_NOT_CONFIGURED", "Draft generation is not configured; no agronomic guidance was generated.", DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (GeminiCircuitOpenException)
        {
            proposal.MarkFailed("GEMINI_TEMPORARILY_UNAVAILABLE", "Draft generation is temporarily unavailable.", DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (GeminiProviderException)
        {
            proposal.MarkFailed("GEMINI_GENERATION_FAILED", "A safe draft could not be generated; no agronomic guidance was saved.", DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed record SowingPlanRequested(Guid JobId, int Version);
    private sealed record ProposalSnapshot(
        DateOnly? DesiredPlantingDate,
        decimal ParcelAreaHectares,
        string? LocationDescription,
        IReadOnlyList<string> MissingInputs);
}
