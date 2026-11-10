using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AgroPlan.Application.Abstractions;
using AgroPlan.Domain.Crops;
using AgroPlan.Domain.Calendars;
using AgroPlan.Domain.Logbook;
using AgroPlan.Infrastructure.Messaging;
using AgroPlan.Infrastructure.Persistence;
using AgroPlan.Worker.Climate;
using AgroPlan.Worker.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Xunit;

namespace AgroPlan.IntegrationTests.Worker;

[Collection(PostgreSqlRabbitCollection.Name)]
public sealed class OutboxAndConsumerTests(PostgreSqlRabbitFixture services)
{
    [Fact]
    public async Task Schedule_change_is_durable_explained_once_and_requires_producer_confirmation()
    {
        services.EnsureAvailable();
        await using var factory = new ApiTestFactory(services.ConnectionString, rabbitMqUrl: services.RabbitMqUrl);
        using var client = factory.CreateClient();
        var ownerId = $"calendar-owner-{Guid.NewGuid():N}";
        Guid farmId; Guid cycleId; Guid correctedSowingId; Guid templateId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            var (farm, parcelId) = await TestData.SeedFarmAsync(db, ownerId); farmId = farm;
            var crop = await db.CropCatalogEntries.SingleAsync(item => item.Code == "PLATANO");
            var cycle = new CropCycle(Guid.NewGuid(), farmId, parcelId, crop, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)), null, null, DateTimeOffset.UtcNow);
            db.CropCycles.Add(cycle); cycleId = cycle.Id;
            var now = DateTimeOffset.UtcNow;
            db.CropCalendarTemplates.Add(new CropCalendarTemplate(templateId, crop.Id, "calendar-test-v1", "approved fixture", "source-v1",
                CropCalendarTemplateStatus.Validated, null, null, now.AddDays(-1), now.AddDays(7), null, null,
                [new CropCalendarTask("irrigation-check", "growth", "Check irrigation", "Follow current sourced schedule.", 12, 1, CropCalendarOperationKind.Irrigation)]));
            var originalSowingId = Guid.NewGuid();
            db.LogbookEntries.Add(new LogbookEntry(originalSowingId, farmId, parcelId, cycleId, LogbookActivityType.Sowing,
                now.AddDays(-10), "Original sowing date", ownerId, now.AddDays(-10)));
            correctedSowingId = Guid.NewGuid();
            db.LogbookEntries.Add(new LogbookEntry(correctedSowingId, farmId, parcelId, cycleId, LogbookActivityType.Sowing,
                now.AddDays(-8), "Corrected sowing date", ownerId, now.AddDays(-8), originalSowingId, "Producer corrected planting date"));
            await db.SaveChangesAsync();
        }
        TestData.SetBearer(client, ownerId);
        using var create = await client.PostAsync($"/api/farms/{farmId}/crop-cycles/{cycleId}/schedule-change-proposals", null);
        Assert.Equal(HttpStatusCode.Accepted, create.StatusCode);
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var proposalId = created.RootElement.GetProperty("id").GetGuid();
        var evidenceIds = created.RootElement.GetProperty("evidenceIds").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains(correctedSowingId.ToString("N"), evidenceIds);
        using var premature = await client.PostAsJsonAsync($"/api/farms/{farmId}/crop-cycles/{cycleId}/schedule-change-proposals/{proposalId}/confirm", new { version = 1 });
        Assert.Equal(HttpStatusCode.Conflict, premature.StatusCode);

        string eventJson;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            eventJson = await db.OutboxMessages.Where(x => x.AggregateId == proposalId).Select(x => x.PayloadJson).SingleAsync();
            await db.ScheduleChangeProposals.Where(x => x.Id == proposalId).ExecuteUpdateAsync(update => update
                .SetProperty(x => x.Status, ScheduleChangeProposalStatus.Processing)
                .SetProperty(x => x.ProcessingStartedAt, DateTimeOffset.UtcNow.AddMinutes(-6))
                .SetProperty(x => x.ProcessingAttempts, 1));
        }
        await PublishScheduleDuplicateAsync(services.RabbitMqUrl, eventJson);
        await PublishScheduleDuplicateAsync(services.RabbitMqUrl, eventJson);
        var provider = new CountingAiProvider();
        using var worker = CreateWorkerHost(services.ConnectionString, services.RabbitMqUrl, provider);
        await worker.StartAsync();
        try
        {
            var until = DateTimeOffset.UtcNow.AddSeconds(30);
            ScheduleChangeProposal? stored = null;
            while (DateTimeOffset.UtcNow < until)
            {
                await using var scope = factory.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
                stored = await db.ScheduleChangeProposals.AsNoTracking().SingleAsync(x => x.Id == proposalId);
                if (stored.Status == ScheduleChangeProposalStatus.Explained) break;
                await Task.Delay(150);
            }
            Assert.NotNull(stored);
            Assert.Equal(ScheduleChangeProposalStatus.Explained, stored.Status);
            Assert.Equal(1, provider.CalendarCalls);
            using var confirm = await client.PostAsJsonAsync($"/api/farms/{farmId}/crop-cycles/{cycleId}/schedule-change-proposals/{proposalId}/confirm", new { version = 1 });
            Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
            await using var finalScope = factory.Services.CreateAsyncScope();
            var finalDb = finalScope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            var schedule = await finalDb.CalendarScheduleItems.SingleAsync(x => x.FarmId == farmId && x.CropCycleId == cycleId && x.TaskKey == "irrigation-check");
            Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4)), schedule.DueDate);
            Assert.Equal(proposalId, schedule.LastProposalId);
        }
        finally { await worker.StopAsync(); }
    }

    [Fact]
    public async Task Outbox_publishes_jobs_and_workers_persist_safe_missing_data_without_calling_ai()
    {
        services.EnsureAvailable();
        await using var factory = new ApiTestFactory(services.ConnectionString, rabbitMqUrl: services.RabbitMqUrl);
        using var client = factory.CreateClient();
        var ownerId = $"worker-owner-{Guid.NewGuid():N}";
        Guid farmId;
        Guid cropCycleId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            var (farm, parcelId) = await TestData.SeedFarmAsync(db, ownerId);
            farmId = farm;
            var crop = await db.CropCatalogEntries.SingleAsync(item => item.Code == "PLATANO");
            var cycle = new CropCycle(Guid.NewGuid(), farmId, parcelId, crop, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), null, null, DateTimeOffset.UtcNow);
            db.CropCycles.Add(cycle);
            await db.SaveChangesAsync();
            cropCycleId = cycle.Id;
        }

        TestData.SetBearer(client, ownerId);
        using var sowingResponse = await client.PostAsJsonAsync(
            $"/api/farms/{farmId}/planning-jobs/sowing-plans", new { cropCycleId });
        Assert.Equal(HttpStatusCode.Accepted, sowingResponse.StatusCode);
        using var sowingJson = JsonDocument.Parse(await sowingResponse.Content.ReadAsStringAsync());
        var sowingJobId = sowingJson.RootElement.GetProperty("id").GetGuid();

        using var frameResponse = await client.PostAsJsonAsync(
            $"/api/farms/{farmId}/planning-jobs/planting-frames", new { cropCycleId });
        Assert.Equal(HttpStatusCode.Accepted, frameResponse.StatusCode);
        using var frameJson = JsonDocument.Parse(await frameResponse.Content.ReadAsStringAsync());
        var frameJobId = frameJson.RootElement.GetProperty("id").GetGuid();

        var (sowingPayload, sowingEventType, initialStatusVersion) = await WaitForPublishedSowingOutboxAsync(
            factory, sowingJobId, TimeSpan.FromSeconds(30));
        await PublishDuplicateSowingEventAsync(services.RabbitMqUrl, sowingEventType, sowingPayload, 1);
        await PublishDuplicateSowingEventAsync(services.RabbitMqUrl, sowingEventType, sowingPayload, 2);

        var aiProvider = new CountingAiProvider();
        using var worker = CreateWorkerHost(services.ConnectionString, services.RabbitMqUrl, aiProvider);
        await worker.StartAsync();
        try
        {
            var (sowing, frame) = await WaitForCompletionAsync(factory, farmId, sowingJobId, frameJobId, TimeSpan.FromSeconds(30));
            Assert.Equal("insufficient_data", sowing.GetProperty("status").GetString());
            Assert.Equal("draft", frame.GetProperty("status").GetString());
            Assert.Equal(0, aiProvider.Calls);

            Assert.False(sowing.GetProperty("confirmationAllowed").GetBoolean());
            var sowingMissing = sowing.GetProperty("missingInputs").EnumerateArray()
                .Select(item => item.GetString()).ToArray();
            Assert.Contains("parcel.geometry", sowingMissing);
            Assert.Contains("validated.crop.parameters", sowingMissing);
            Assert.Empty(sowing.GetProperty("sourceIds").EnumerateArray());

            Assert.Empty(frame.GetProperty("options").EnumerateArray());
            Assert.False(frame.GetProperty("confirmationAllowed").GetBoolean());
            var frameMissing = frame.GetProperty("missingInputs").EnumerateArray()
                .Select(item => item.GetString()).ToArray();
            Assert.Contains("parcel.usableAreaSquareMeters", frameMissing);
            Assert.Contains("parcel.aisleGeometry", frameMissing);
            Assert.Contains("crop.validatedFrameRules", frameMissing);

            using var confirmSowing = await client.PostAsync($"/api/farms/{farmId}/planning-jobs/{sowingJobId}/confirm", content: null);
            Assert.Equal(HttpStatusCode.Conflict, confirmSowing.StatusCode);
            using var confirmFrame = await client.PostAsJsonAsync(
                $"/api/farms/{farmId}/planning-jobs/planting-frames/{frameJobId}/confirm", new { frameworkId = Guid.NewGuid() });
            Assert.Equal(HttpStatusCode.Conflict, confirmFrame.StatusCode);

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
                var outbox = await db.OutboxMessages.Where(item => item.AggregateId == sowingJobId || item.AggregateId == frameJobId).ToListAsync();
                Assert.Equal(2, outbox.Count);
                Assert.All(outbox, message => Assert.NotNull(message.PublishedAt));
                var finalStatusVersion = await db.SowingProposals.Where(item => item.Id == sowingJobId)
                    .Select(item => item.StatusVersion).SingleAsync();
                Assert.Equal(initialStatusVersion + 2, finalStatusVersion);
            }
        }
        finally
        {
            await worker.StopAsync();
        }
    }

    private static IHost CreateWorkerHost(string connectionString, string rabbitMqUrl, CountingAiProvider aiProvider) =>
        Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AgroPlan"] = connectionString,
                ["RABBITMQ_URL"] = rabbitMqUrl
            }))
            .ConfigureServices((_, services) =>
            {
                services.AddDbContext<AgroPlanDbContext>(options => options.UseNpgsql(connectionString));
                services.AddSingleton<RabbitMqConnection>();
                services.AddSingleton<IClimateProvider, UnconfiguredClimateProvider>();
                services.AddSingleton<IAiProposalProvider>(aiProvider);
                // Two independent consumers force duplicate broker deliveries to race on the DB claim.
                services.AddSingleton<IHostedService, SowingPlanConsumer>();
                services.AddSingleton<IHostedService, SowingPlanConsumer>();
                services.AddHostedService<PlantingFrameConsumer>();
                services.AddHostedService<ScheduleChangeProposalConsumer>();
            })
            .ConfigureLogging((_, logging) => logging.SetMinimumLevel(LogLevel.Warning))
            .Build();

    private static async Task<(string Payload, string EventType, long StatusVersion)> WaitForPublishedSowingOutboxAsync(
        ApiTestFactory factory, Guid sowingJobId, TimeSpan timeout)
    {
        var until = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < until)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            var message = await db.OutboxMessages.AsNoTracking()
                .SingleOrDefaultAsync(item => item.AggregateId == sowingJobId, TestContext.Current.CancellationToken);
            if (message?.PublishedAt is not null)
            {
                var statusVersion = await db.SowingProposals.AsNoTracking().Where(item => item.Id == sowingJobId)
                    .Select(item => item.StatusVersion).SingleAsync(TestContext.Current.CancellationToken);
                return (message.PayloadJson, message.EventType, statusVersion);
            }
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
        throw new TimeoutException("Sowing outbox message was not published before the duplicate-delivery setup timeout.");
    }

    private static async Task PublishDuplicateSowingEventAsync(
        string rabbitMqUrl, string eventType, string payload, int duplicateNumber)
    {
        var factory = new ConnectionFactory { Uri = new Uri(rabbitMqUrl, UriKind.Absolute) };
        await using var connection = await factory.CreateConnectionAsync(TestContext.Current.CancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken);
        await RabbitMqPublisher.EnsureQueueAsync(channel, TestContext.Current.CancellationToken);
        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: RabbitMqPublisher.QueueName,
            mandatory: true,
            basicProperties: new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                Type = eventType,
                MessageId = $"duplicate-{duplicateNumber}-{Guid.NewGuid():N}"
            },
            body: Encoding.UTF8.GetBytes(payload),
            cancellationToken: TestContext.Current.CancellationToken);
    }

    private static async Task<(JsonElement Sowing, JsonElement Frame)> WaitForCompletionAsync(
        ApiTestFactory factory,
        Guid farmId,
        Guid sowingJobId,
        Guid frameJobId,
        TimeSpan timeout)
    {
        var until = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < until)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            var sowing = await db.SowingProposals.AsNoTracking().SingleOrDefaultAsync(item => item.Id == sowingJobId && item.FarmId == farmId);
            var frame = await db.PlantingFrameProposals.AsNoTracking().SingleOrDefaultAsync(item => item.Id == frameJobId && item.FarmId == farmId);
            if (sowing?.ResultJson is not null && frame?.ResultJson is not null)
            {
                using var sowingDocument = JsonDocument.Parse(sowing.ResultJson);
                using var frameDocument = JsonDocument.Parse(frame.ResultJson);
                return (sowingDocument.RootElement.Clone(), frameDocument.RootElement.Clone());
            }
            await Task.Delay(200);
        }
        throw new TimeoutException("Sowing and planting-frame jobs did not reach a terminal result before the integration timeout.");
    }

    private sealed class CountingAiProvider : IAiProposalProvider
    {
        private int _calls;
        private int _calendarCalls;
        public int Calls => Volatile.Read(ref _calls);
        public int CalendarCalls => Volatile.Read(ref _calendarCalls);

        public Task<AiProposalDraft> GenerateDraftAsync(SowingGenerationInput input, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(new AiProposalDraft("test draft", [], [], "test-model", "test-prompt", "test-schema", DateTimeOffset.UtcNow, "test-provider"));
        }

        public Task<CalendarExplanationDraft> ExplainCalendarChangeAsync(CalendarExplanationInput input, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calendarCalls);
            return Task.FromResult(new CalendarExplanationDraft("La propuesta reorganiza la tarea según la guía aprobada; revísela y confirme si está de acuerdo.",
                input.AllowedEvidenceIds.Take(1).ToArray(), "fake", "test-model", "test-prompt", "test-schema", DateTimeOffset.UtcNow));
        }
    }

    private static async Task PublishScheduleDuplicateAsync(string rabbitMqUrl, string payload)
    {
        var factory = new ConnectionFactory { Uri = new Uri(rabbitMqUrl, UriKind.Absolute) };
        await using var connection = await factory.CreateConnectionAsync(TestContext.Current.CancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken);
        await RabbitMqPublisher.EnsureScheduleChangesQueueAsync(channel, TestContext.Current.CancellationToken);
        await channel.BasicPublishAsync(exchange: string.Empty, routingKey: RabbitMqPublisher.ScheduleChangesQueueName, mandatory: true,
            basicProperties: new BasicProperties { ContentType = "application/json", DeliveryMode = DeliveryModes.Persistent, MessageId = Guid.NewGuid().ToString("N") },
            body: Encoding.UTF8.GetBytes(payload), cancellationToken: TestContext.Current.CancellationToken);
    }
}
