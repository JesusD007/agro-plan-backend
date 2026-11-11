using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgroPlan.Application.Abstractions;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Reports;
using AgroPlan.Infrastructure.Messaging;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client.Events;

namespace AgroPlan.Worker.Jobs;

/// <summary>Optional narrative worker. It is only hosted when a provider is explicitly registered.</summary>
public sealed class WeeklySummaryConsumer(RabbitMqConnection connection, IServiceScopeFactory scopeFactory,
    IWeeklySummaryNarrativeProvider provider, ILogger<WeeklySummaryConsumer> logger) : BackgroundService
{
    public const string QueueName = "agroplan.weekly-summary-narratives.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int MaximumInputBytes = 64 * 1024;
    private const int MaximumOutputBytes = 16 * 1024;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!connection.IsConfigured) { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); continue; }
            try
            {
                var broker = await connection.GetAsync(stoppingToken);
                await using var channel = await broker.CreateChannelAsync(cancellationToken: stoppingToken);
                await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false,
                    arguments: null, cancellationToken: stoppingToken);
                await channel.BasicQosAsync(0, 1, global: false, cancellationToken: stoppingToken);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, delivery) =>
                {
                    try
                    {
                        if (!Guid.TryParse(delivery.BasicProperties.MessageId, out var outboxId))
                            throw new InvalidOperationException("Weekly summary delivery has no persisted outbox id.");
                        await ProcessMessageAsync(outboxId, delivery.Body.ToArray(), stoppingToken);
                        await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception ex)
                    {
                        logger.LogWarning("Weekly summary narrative job failed ({ExceptionType}); broker will redeliver.", ex.GetType().Name);
                        await channel.BasicNackAsync(delivery.DeliveryTag, false, true, stoppingToken);
                    }
                };
                await channel.BasicConsumeAsync(QueueName, false, consumerTag: string.Empty, noLocal: false,
                    exclusive: false, arguments: null, consumer: consumer, cancellationToken: stoppingToken);
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError("Weekly summary consumer failed ({ExceptionType}); retrying.", ex.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    public async Task ProcessMessageAsync(Guid outboxId, byte[] body, CancellationToken cancellationToken = default)
    {
        var request = JsonSerializer.Deserialize<WeeklySummaryNarrativeRequested>(body, JsonOptions);
        if (request is null || outboxId == Guid.Empty || request.EventId != outboxId || request.FarmId == Guid.Empty ||
            request.SummaryId == Guid.Empty || string.IsNullOrWhiteSpace(request.RequestedByUserId) || request.Version < 2)
            throw new InvalidOperationException("Weekly summary event is invalid.");

        WeeklySummaryNarrativeInput input;
        Dictionary<string, Guid> aliases;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            var persisted = await db.OutboxMessages.AsNoTracking().SingleOrDefaultAsync(x => x.Id == outboxId &&
                x.AggregateId == request.SummaryId && x.EventType == "weekly-summary.narrative.requested", cancellationToken);
            if (persisted is null || JsonSerializer.Deserialize<WeeklySummaryNarrativeRequested>(persisted.PayloadJson, JsonOptions) != request)
                throw new InvalidOperationException("Weekly summary event does not match a persisted outbox record.");

            var summary = await db.Set<WeeklySummary>().AsNoTracking().SingleOrDefaultAsync(x =>
                x.FarmId == request.FarmId && x.Id == request.SummaryId && x.Version == request.Version &&
                x.NarrativeStatus == WeeklySummaryNarrativeStatus.Pending, cancellationToken);
            if (summary is null) return;
            if (!await HasCurrentRequesterAuthorizationAsync(db, request.FarmId, request.RequestedByUserId, cancellationToken))
            {
                await FinalizeAsync(request, null, WeeklySummaryNarrativeStatus.Unavailable, [], cancellationToken);
                return;
            }
            var projection = JsonSerializer.Deserialize<AgroPlan.Application.Reports.WeeklySummaryProjection>(summary.ProjectionJson, JsonOptions);
            if (projection is null) return;
            aliases = projection.Sources.ToDictionary(_ => $"src_{Guid.NewGuid():N}", source => source.Id, StringComparer.Ordinal);
            var narrativeSources = projection.Sources.Select(source => new WeeklySummaryNarrativeSource(
                aliases.Single(pair => pair.Value == source.Id).Key, source.Kind, source.LocalDate,
                source.Amount, source.CurrencyCode, source.Quantity, source.Unit)).ToArray();
            var facts = projection.CostTotals.Select(total => new WeeklySummaryNarrativeFact("cost_total", total.Amount,
                    null, total.CurrencyCode))
                .Concat(projection.HarvestTotals.Select(total => new WeeklySummaryNarrativeFact("harvest_total", total.Quantity,
                    total.Unit, null))).ToArray();
            input = new WeeklySummaryNarrativeInput(projection.WeekStartDate, projection.WeekEndDate,
                projection.TimeZoneId, narrativeSources, facts, projection.MissingData.ToArray());
        }

        var inputBytes = JsonSerializer.SerializeToUtf8Bytes(input, JsonOptions);
        if (inputBytes.Length > MaximumInputBytes)
        {
            await FinalizeAsync(request, null, WeeklySummaryNarrativeStatus.Unavailable, [], cancellationToken);
            return;
        }

        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            if (!await HasCurrentRequesterAuthorizationAsync(db, request.FarmId, request.RequestedByUserId, cancellationToken))
            {
                await FinalizeAsync(request, null, WeeklySummaryNarrativeStatus.Unavailable, [], cancellationToken);
                return;
            }
        }

        WeeklySummaryNarrativeDraft draft;
        try { draft = await provider.GenerateAsync(input, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (WeeklySummaryNarrativeProviderException exception) when (exception.FailureKind == WeeklySummaryNarrativeFailureKind.NotConfigured)
        {
            await FinalizeAsync(request, null, WeeklySummaryNarrativeStatus.Unavailable, [], cancellationToken);
            return;
        }
        catch (Exception ex)
        {
            logger.LogWarning("Weekly summary provider failed ({ExceptionType}).", ex.GetType().Name);
            await FinalizeAsync(request, null, WeeklySummaryNarrativeStatus.Failed, [], cancellationToken);
            return;
        }

        var outputBytes = JsonSerializer.SerializeToUtf8Bytes(draft, JsonOptions);
        if (outputBytes.Length > MaximumOutputBytes || string.IsNullOrWhiteSpace(draft.Text) || draft.Text.Length > 16_384 ||
            Regex.IsMatch(draft.Text, @"\d|[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}") ||
            draft.SourceAliases is null || draft.SourceAliases.Any(alias => !aliases.ContainsKey(alias)) ||
            draft.SourceAliases.Distinct(StringComparer.Ordinal).Count() != draft.SourceAliases.Count)
        {
            await FinalizeAsync(request, null, WeeklySummaryNarrativeStatus.Unavailable, [], cancellationToken);
            return;
        }
        var sourceIds = draft.SourceAliases.Select(alias => aliases[alias]).ToArray();
        await FinalizeAsync(request, draft.Text.Trim(), WeeklySummaryNarrativeStatus.Available, sourceIds, cancellationToken);
    }

    private static async Task<bool> HasCurrentRequesterAuthorizationAsync(AgroPlanDbContext db, Guid farmId,
        string userId, CancellationToken ct) => await db.FarmMemberships.AsNoTracking().AnyAsync(x => x.FarmId == farmId &&
            x.UserId == userId && x.IsActive && (x.Role == FarmRole.Owner || x.Role == FarmRole.Administrator), ct);

    private async Task FinalizeAsync(WeeklySummaryNarrativeRequested request, string? text,
        WeeklySummaryNarrativeStatus status, IReadOnlyCollection<Guid> sourceIds, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        var summary = await db.Set<WeeklySummary>().SingleOrDefaultAsync(x => x.FarmId == request.FarmId &&
            x.Id == request.SummaryId && x.Version == request.Version && x.NarrativeStatus == WeeklySummaryNarrativeStatus.Pending, ct);
        if (summary is null) return;
        if (!await HasCurrentRequesterAuthorizationAsync(db, request.FarmId, request.RequestedByUserId, ct))
        {
            summary.SetNarrative(null, WeeklySummaryNarrativeStatus.Unavailable);
            await db.SaveChangesAsync(ct);
            return;
        }
        summary.SetNarrative(text, status, sourceIds);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { /* A newer summary state wins; stale prose is discarded. */ }
    }
}
