using System.Text.Json;
using AgroPlan.Application.Abstractions;
using AgroPlan.Application.Calendars;
using AgroPlan.Domain.Calendars;
using AgroPlan.Infrastructure.Messaging;
using AgroPlan.Infrastructure.Messaging.Events;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client.Events;

namespace AgroPlan.Worker.Jobs;

public sealed class ScheduleChangeProposalConsumer(RabbitMqConnection connection, IServiceScopeFactory scopeFactory,
    IAiProposalProvider provider, ILogger<ScheduleChangeProposalConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(5);
    private const int MaxProcessingAttempts = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!connection.IsConfigured) { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); continue; }
            try
            {
                var broker = await connection.GetAsync(stoppingToken);
                await using var channel = await broker.CreateChannelAsync(cancellationToken: stoppingToken);
                await RabbitMqPublisher.EnsureScheduleChangesQueueAsync(channel, stoppingToken);
                await channel.BasicQosAsync(0, 1, global: false, cancellationToken: stoppingToken);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, delivery) =>
                {
                    try { await HandleAsync(delivery.Body.ToArray(), stoppingToken); await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken); }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (ProcessingLeaseActiveException)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                        await channel.BasicNackAsync(delivery.DeliveryTag, false, true, stoppingToken);
                    }
                    catch (Exception ex) { logger.LogError("Schedule proposal delivery failed ({ExceptionType}); broker will redeliver.", ex.GetType().Name); await channel.BasicNackAsync(delivery.DeliveryTag, false, true, stoppingToken); }
                };
                await channel.BasicConsumeAsync(RabbitMqPublisher.ScheduleChangesQueueName, false, consumerTag: "", noLocal: false,
                    exclusive: false, arguments: null, consumer: consumer, cancellationToken: stoppingToken);
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError("Schedule proposal consumer failed ({ExceptionType}); retrying.", ex.GetType().Name); await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
        }
    }

    private async Task HandleAsync(byte[] body, CancellationToken token)
    {
        var message = JsonSerializer.Deserialize<ScheduleChangeRequested>(body, JsonOptions);
        if (message is null || message.ProposalId == Guid.Empty || message.Version < 1) throw new InvalidOperationException("Schedule proposal message is invalid.");
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        var now = DateTimeOffset.UtcNow;
        var expiredLease = now - ProcessingLease;
        await db.ScheduleChangeProposals.Where(x => x.Id == message.ProposalId && x.Version == message.Version &&
                x.Status == ScheduleChangeProposalStatus.Processing && x.ProcessingStartedAt <= expiredLease && x.ExpiresAt <= now)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, ScheduleChangeProposalStatus.Stale)
                .SetProperty(x => x.FailureCode, "PROPOSAL_EXPIRED")
                .SetProperty(x => x.ProcessingStartedAt, (DateTimeOffset?)null)
                .SetProperty(x => x.StatusVersion, x => x.StatusVersion + 1), token);
        await db.ScheduleChangeProposals.Where(x => x.Id == message.ProposalId && x.Version == message.Version &&
                x.Status == ScheduleChangeProposalStatus.Processing && x.ProcessingStartedAt <= expiredLease &&
                x.ProcessingAttempts >= MaxProcessingAttempts && x.ExpiresAt > now)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, ScheduleChangeProposalStatus.Failed)
                .SetProperty(x => x.FailureCode, "PROCESSING_ATTEMPTS_EXHAUSTED")
                .SetProperty(x => x.ProcessingStartedAt, (DateTimeOffset?)null)
                .SetProperty(x => x.StatusVersion, x => x.StatusVersion + 1), token);
        var claimed = await db.ScheduleChangeProposals.Where(x => x.Id == message.ProposalId && x.Version == message.Version &&
                x.ExpiresAt > now && x.ProcessingAttempts < MaxProcessingAttempts &&
                (x.Status == ScheduleChangeProposalStatus.Pending ||
                 (x.Status == ScheduleChangeProposalStatus.Processing && x.ProcessingStartedAt <= expiredLease)))
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, ScheduleChangeProposalStatus.Processing)
                .SetProperty(x => x.ProcessingStartedAt, now)
                .SetProperty(x => x.ProcessingAttempts, x => x.ProcessingAttempts + 1)
                .SetProperty(x => x.StatusVersion, x => x.StatusVersion + 1), token);
        if (claimed == 0)
        {
            var current = await db.ScheduleChangeProposals.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == message.ProposalId && x.Version == message.Version, token);
            if (current?.Status == ScheduleChangeProposalStatus.Processing && current.ProcessingStartedAt > expiredLease)
                throw new ProcessingLeaseActiveException();
            return;
        }
        var proposal = await db.ScheduleChangeProposals.SingleAsync(x => x.Id == message.ProposalId, token);
        try
        {
            var cycle = await db.CropCycles.AsNoTracking().Include(x => x.CropCatalogEntry)
                .SingleOrDefaultAsync(x => x.FarmId == proposal.FarmId && x.Id == proposal.CropCycleId, token);
            var templates = cycle is null ? new List<CropCalendarTemplate>() : await db.CropCalendarTemplates.AsNoTracking()
                .Where(x => x.CropCatalogEntryId == cycle.CropCatalogEntryId && x.Status == CropCalendarTemplateStatus.Validated &&
                    x.EffectiveFrom <= now && (!x.EffectiveUntil.HasValue || x.EffectiveUntil >= now)).ToListAsync(token);
            if (cycle is not null) templates = templates.Where(x => x.IsUsableAt(now, cycle.CropCatalogEntryId, cycle.CropCatalogEntry.Region, null)).ToList();
            if (templates.Count != 1 || $"{templates[0].Version}:{templates[0].SourceVersion}:{templates[0].EffectiveFrom:O}:{templates[0].EffectiveUntil:O}" != proposal.EvidenceVersion)
            {
                proposal.Fail("EVIDENCE_STALE");
                await db.SaveChangesAsync(token);
                return;
            }
            var input = new CalendarExplanationInput(proposal.Version, proposal.EvidenceVersion, proposal.Deltas, proposal.EvidenceIds);
            var draft = await provider.ExplainCalendarChangeAsync(input, token);
            CalendarExplanationDraftValidator.Validate(input, draft);
            proposal.SetExplanation(draft, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            proposal.Fail(ex.GetType().Name);
            await db.SaveChangesAsync(CancellationToken.None);
            logger.LogWarning("Schedule explanation failed safely ({ExceptionType}).", ex.GetType().Name);
        }
    }

    private sealed class ProcessingLeaseActiveException : Exception { }
}
