using System.Text.Json;
using AgroPlan.Application.Abstractions;
using AgroPlan.Application.Reports;
using AgroPlan.Domain.Planning;
using AgroPlan.Domain.Reports;
using AgroPlan.Infrastructure.Messaging;
using AgroPlan.Infrastructure.Persistence;
using AgroPlan.Worker.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgroPlan.IntegrationTests.Worker;

[Collection(PostgreSqlCollection.Name)]
public sealed class WeeklySummaryConsumerTests(PostgreSqlFixture postgres)
{
    [Fact]
    public async Task Consumer_sends_only_bounded_alias_projection_and_maps_valid_aliases_after_call()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString, disableBackgroundServices: true);
        var owner = $"weekly-summary-worker-{Guid.NewGuid():N}";
        var sourceId = Guid.NewGuid();
        var summaryId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        Guid farmId;
        var anchor = new DateOnly(2026, 11, 9);
        var projection = new WeeklySummaryProjection(Guid.Empty, anchor, anchor, anchor.AddDays(6), now, now.AddDays(7),
            "America/Santo_Domingo", null,
            [new WeeklySummarySource(sourceId, "expense", anchor, null, 25m, "DOP", null, null)], [],
            [new CostCurrencyTotal("DOP", 25m, [sourceId])], [], [], []);
        await using (var seed = factory.Services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (farmId, _) = await TestData.SeedFarmAsync(db, owner, withParcel: false);
            projection = projection with { FarmId = farmId };
            var summary = new WeeklySummary(summaryId, farmId, owner, anchor, anchor, anchor.AddDays(6), null,
                projection.TimeZoneId, JsonSerializer.Serialize(projection, new JsonSerializerOptions(JsonSerializerDefaults.Web)), now);
            summary.MarkNarrativePending();
            var request = new WeeklySummaryNarrativeRequested(eventId, farmId, summaryId, owner, summary.Version);
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            db.Set<WeeklySummary>().Add(summary);
            db.OutboxMessages.Add(new OutboxMessage(eventId, summaryId, "weekly-summary.narrative.requested",
                JsonSerializer.Serialize(request, json), now));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var provider = new CapturingNarrativeProvider((input, _) => Task.FromResult(
            new WeeklySummaryNarrativeDraft("The farm recorded an expense.", [Assert.Single(input.Sources).Alias])));
        var consumer = CreateConsumer(factory, provider);
        var body = JsonSerializer.SerializeToUtf8Bytes(new WeeklySummaryNarrativeRequested(eventId, farmId, summaryId, owner, 2),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        await consumer.ProcessMessageAsync(eventId, body, TestContext.Current.CancellationToken);

        Assert.NotNull(provider.LastInput);
        var serializedInput = JsonSerializer.Serialize(provider.LastInput, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain(farmId.ToString(), serializedInput, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(owner, serializedInput, StringComparison.Ordinal);
        Assert.DoesNotContain(sourceId.ToString(), serializedInput, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(sourceId.ToString(), provider.LastInput!.Sources[0].Alias);
        await using var verify = factory.Services.CreateAsyncScope();
        var stored = await verify.ServiceProvider.GetRequiredService<AgroPlanDbContext>().Set<WeeklySummary>()
            .SingleAsync(x => x.Id == summaryId, TestContext.Current.CancellationToken);
        Assert.Equal(WeeklySummaryNarrativeStatus.Available, stored.NarrativeStatus);
        Assert.Equal("The farm recorded an expense.", stored.NarrativeText);
        Assert.Equal([sourceId], stored.NarrativeSourceIds);
    }

    [Fact]
    public async Task Consumer_rejects_unknown_aliases_and_numeric_claims()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString, disableBackgroundServices: true);
        var owner = $"weekly-summary-invalid-ai-{Guid.NewGuid():N}";
        var fixture = await SeedPendingSummaryAsync(factory, owner, 1);
        var provider = new CapturingNarrativeProvider((_, _) => Task.FromResult(
            new WeeklySummaryNarrativeDraft("The harvest was 100 units.", ["unknown_alias"])));
        var consumer = CreateConsumer(factory, provider);

        await consumer.ProcessMessageAsync(fixture.EventId, fixture.Body, TestContext.Current.CancellationToken);

        await using var verify = factory.Services.CreateAsyncScope();
        var stored = await verify.ServiceProvider.GetRequiredService<AgroPlanDbContext>().Set<WeeklySummary>()
            .SingleAsync(x => x.Id == fixture.SummaryId, TestContext.Current.CancellationToken);
        Assert.Equal(WeeklySummaryNarrativeStatus.Unavailable, stored.NarrativeStatus);
        Assert.Null(stored.NarrativeText);
        Assert.Empty(stored.NarrativeSourceIds);
    }

    [Fact]
    public async Task Requester_membership_revoked_during_provider_call_discards_the_narrative()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString, disableBackgroundServices: true);
        var owner = $"weekly-summary-revoke-owner-{Guid.NewGuid():N}";
        var fixture = await SeedPendingSummaryAsync(factory, owner, 1, requestedByAdmin: true);
        var provider = new CapturingNarrativeProvider(async (input, ct) =>
        {
            await using var revoke = factory.Services.CreateAsyncScope();
            var db = revoke.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (await db.FarmMemberships.SingleAsync(x => x.UserId == fixture.RequestedByUserId, ct)).Revoke();
            await db.SaveChangesAsync(ct);
            return new WeeklySummaryNarrativeDraft("The farm recorded an expense.", [Assert.Single(input.Sources).Alias]);
        });
        var consumer = CreateConsumer(factory, provider);

        await consumer.ProcessMessageAsync(fixture.EventId, fixture.Body, TestContext.Current.CancellationToken);

        await using var verify = factory.Services.CreateAsyncScope();
        var stored = await verify.ServiceProvider.GetRequiredService<AgroPlanDbContext>().Set<WeeklySummary>()
            .SingleAsync(x => x.Id == fixture.SummaryId, TestContext.Current.CancellationToken);
        Assert.Equal(WeeklySummaryNarrativeStatus.Unavailable, stored.NarrativeStatus);
        Assert.Null(stored.NarrativeText);
        Assert.Empty(stored.NarrativeSourceIds);
    }

    [Fact]
    public async Task Oversized_provider_input_is_unavailable_without_invoking_provider()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString, disableBackgroundServices: true);
        var owner = $"weekly-summary-oversized-{Guid.NewGuid():N}";
        var fixture = await SeedPendingSummaryAsync(factory, owner, 500, new string('u', 100));
        var provider = new CapturingNarrativeProvider((_, _) => throw new InvalidOperationException("Provider must not be invoked."));
        var consumer = CreateConsumer(factory, provider);

        await consumer.ProcessMessageAsync(fixture.EventId, fixture.Body, TestContext.Current.CancellationToken);

        Assert.Null(provider.LastInput);
        await using var verify = factory.Services.CreateAsyncScope();
        var stored = await verify.ServiceProvider.GetRequiredService<AgroPlanDbContext>().Set<WeeklySummary>()
            .SingleAsync(x => x.Id == fixture.SummaryId, TestContext.Current.CancellationToken);
        Assert.Equal(WeeklySummaryNarrativeStatus.Unavailable, stored.NarrativeStatus);
    }

    [Fact]
    public async Task Oversized_provider_output_is_unavailable_and_does_not_replace_deterministic_projection()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString, disableBackgroundServices: true);
        var owner = $"weekly-summary-output-limit-{Guid.NewGuid():N}";
        var fixture = await SeedPendingSummaryAsync(factory, owner, 1);
        var provider = new CapturingNarrativeProvider((input, _) => Task.FromResult(
            new WeeklySummaryNarrativeDraft(new string('x', 16_384), [Assert.Single(input.Sources).Alias])));
        var consumer = CreateConsumer(factory, provider);

        await consumer.ProcessMessageAsync(fixture.EventId, fixture.Body, TestContext.Current.CancellationToken);

        await using var verify = factory.Services.CreateAsyncScope();
        var stored = await verify.ServiceProvider.GetRequiredService<AgroPlanDbContext>().Set<WeeklySummary>()
            .SingleAsync(x => x.Id == fixture.SummaryId, TestContext.Current.CancellationToken);
        Assert.Equal(WeeklySummaryNarrativeStatus.Unavailable, stored.NarrativeStatus);
        Assert.Null(stored.NarrativeText);
        Assert.Single(JsonSerializer.Deserialize<WeeklySummaryProjection>(stored.ProjectionJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Sources);
    }

    [Fact]
    public async Task Provider_failure_logs_only_exception_type_and_never_provider_payload_or_record_identifiers()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString, disableBackgroundServices: true);
        var owner = $"weekly-summary-private-{Guid.NewGuid():N}";
        var fixture = await SeedPendingSummaryAsync(factory, owner, 1);
        var marker = $"private-provider-payload-{Guid.NewGuid():N}";
        var provider = new CapturingNarrativeProvider((_, _) => throw new InvalidOperationException(marker));
        var logger = new CapturingLogger<WeeklySummaryConsumer>();
        var consumer = new WeeklySummaryConsumer(factory.Services.GetRequiredService<RabbitMqConnection>(),
            factory.Services.GetRequiredService<IServiceScopeFactory>(), provider, logger);

        await consumer.ProcessMessageAsync(fixture.EventId, fixture.Body, TestContext.Current.CancellationToken);

        var logs = string.Join("\n", logger.Messages);
        Assert.Contains(nameof(InvalidOperationException), logs, StringComparison.Ordinal);
        Assert.DoesNotContain(marker, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(owner, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.FarmId.ToString(), logs, StringComparison.OrdinalIgnoreCase);
    }

    private static WeeklySummaryConsumer CreateConsumer(ApiTestFactory factory, IWeeklySummaryNarrativeProvider provider) =>
        new(factory.Services.GetRequiredService<RabbitMqConnection>(), factory.Services.GetRequiredService<IServiceScopeFactory>(),
            provider, NullLogger<WeeklySummaryConsumer>.Instance);

    private static async Task<PendingSummaryFixture> SeedPendingSummaryAsync(ApiTestFactory factory, string owner, int sourceCount,
        string? unit = null, bool requestedByAdmin = false)
    {
        var now = DateTimeOffset.UtcNow;
        var anchor = new DateOnly(2026, 11, 9);
        Guid farmId;
        var summaryId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var requester = requestedByAdmin ? $"{owner}-admin" : owner;
        var sources = Enumerable.Range(0, sourceCount).Select(_ => new WeeklySummarySource(Guid.NewGuid(), "expense", anchor,
            null, 1m, "DOP", null, unit)).ToArray();
        var projection = new WeeklySummaryProjection(Guid.Empty, anchor, anchor, anchor.AddDays(6), now, now.AddDays(7),
            "America/Santo_Domingo", null, sources, [], [], [], [], []);
        await using (var seed = factory.Services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (farmId, _) = requestedByAdmin
                ? await TestData.SeedFarmAsync(db, owner, requester, withParcel: false)
                : await TestData.SeedFarmAsync(db, owner, withParcel: false);
            projection = projection with { FarmId = farmId };
            var summary = new WeeklySummary(summaryId, farmId, requester, anchor, anchor, anchor.AddDays(6), null,
                projection.TimeZoneId, JsonSerializer.Serialize(projection, new JsonSerializerOptions(JsonSerializerDefaults.Web)), now);
            summary.MarkNarrativePending();
            var request = new WeeklySummaryNarrativeRequested(eventId, farmId, summaryId, requester, summary.Version);
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            db.Set<WeeklySummary>().Add(summary);
            db.OutboxMessages.Add(new OutboxMessage(eventId, summaryId, "weekly-summary.narrative.requested", JsonSerializer.Serialize(request, json), now));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        return new PendingSummaryFixture(summaryId, eventId, requester, farmId,
            JsonSerializer.SerializeToUtf8Bytes(new WeeklySummaryNarrativeRequested(eventId, farmId, summaryId, requester, 2),
                new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    private sealed record PendingSummaryFixture(Guid SummaryId, Guid EventId, string RequestedByUserId, Guid FarmId, byte[] Body);

    private sealed class CapturingNarrativeProvider(Func<WeeklySummaryNarrativeInput, CancellationToken, Task<WeeklySummaryNarrativeDraft>> generate)
        : IWeeklySummaryNarrativeProvider
    {
        public WeeklySummaryNarrativeInput? LastInput { get; private set; }
        public Task<WeeklySummaryNarrativeDraft> GenerateAsync(WeeklySummaryNarrativeInput input, CancellationToken cancellationToken)
        {
            LastInput = input;
            return generate(input, cancellationToken);
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
