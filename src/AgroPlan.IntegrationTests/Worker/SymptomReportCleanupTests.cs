using AgroPlan.Domain.Symptoms;
using AgroPlan.Application.Abstractions;
using AgroPlan.Domain.Planning;
using AgroPlan.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using AgroPlan.Infrastructure.Persistence;
using AgroPlan.Worker.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgroPlan.IntegrationTests.Worker;

[Collection(PostgreSqlCollection.Name)]
public sealed class SymptomReportCleanupTests(PostgreSqlFixture postgres)
{
    [Fact]
    public async Task Observation_consumer_sends_only_consented_photo_bytes_and_persists_observations()
    {
        var ct = TestContext.Current.CancellationToken;
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        var ownerId = $"symptom-observer-{Guid.NewGuid():N}";
        Guid farmId;
        Guid reportId;
        Guid photoId;
        Guid eventId = Guid.NewGuid();
        int version;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (farmId, _) = await TestData.SeedFarmAsync(db, ownerId);
            var report = new SymptomReport(Guid.NewGuid(), farmId, ownerId, null, "leaf marks", DateTimeOffset.UtcNow);
            photoId = Guid.NewGuid();
            var privateKey = $"private/{Guid.NewGuid():N}/final";
            var photo = new SymptomReportPhoto(photoId, $"private/{Guid.NewGuid():N}/staging", "image/png", 512,
                ownerId, true, "symptom-ai-v1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(10));
            photo.Finalize(privateKey, 512, DateTimeOffset.UtcNow);
            report.AddPhoto(photo);
            report.Submit(DateTimeOffset.UtcNow);
            reportId = report.Id;
            photoId = photo.Id;
            version = report.Version;
            var payload = System.Text.Json.JsonSerializer.Serialize(new { eventId, farmId, reportId, version },
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            db.Add(report);
            db.OutboxMessages.Add(new OutboxMessage(eventId, reportId, "symptom-observation.requested", payload, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(ct);
        }

        var provider = new RecordingObservationProvider();
        var connection = new RabbitMqConnection(factory.Services.GetRequiredService<IConfiguration>(),
            NullLogger<RabbitMqConnection>.Instance);
        var consumer = new SymptomObservationConsumer(connection, factory.Services.GetRequiredService<IServiceScopeFactory>(),
            factory.Storage, provider, NullLogger<SymptomObservationConsumer>.Instance);
        var body = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { eventId, farmId, reportId, version },
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        await consumer.ProcessMessageAsync(eventId, body, ct);

        Assert.Equal(1, provider.CallCount);
        Assert.Equal(512, provider.LastPhotoBytes);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            var report = await db.Set<SymptomReport>().Include(x => x.Photos).SingleAsync(x => x.FarmId == farmId && x.Id == reportId, ct);
            Assert.Equal(SymptomReportStatus.Pending, report.Status);
            Assert.Equal("Small pale spots are visible.", report.VisibleSigns);
            Assert.Null(report.HumanDiagnosis);
            Assert.Null(report.HumanTreatment);
            Assert.Equal(photoId, Assert.Single(report.Photos).Id);
        }
    }

    [Fact]
    public async Task Rejected_report_deletes_private_photo_and_retains_report_and_review_history()
    {
        var ct = TestContext.Current.CancellationToken;
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        var ownerId = $"symptom-cleanup-{Guid.NewGuid():N}";
        Guid farmId;
        Guid reportId;
        string objectKey = $"farms/{Guid.NewGuid():D}/symptom-reports/{Guid.NewGuid():D}/final";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (farmId, _) = await TestData.SeedFarmAsync(db, ownerId);
            var report = new SymptomReport(Guid.NewGuid(), farmId, ownerId, null, null, DateTimeOffset.UtcNow);
            var photo = new SymptomReportPhoto(Guid.NewGuid(), objectKey + "/staging", "image/png", 512,
                ownerId, false, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(10));
            photo.Finalize(objectKey, 512, DateTimeOffset.UtcNow);
            report.AddPhoto(photo);
            report.Submit(DateTimeOffset.UtcNow);
            report.AddDecision(new SymptomReportDecision(Guid.NewGuid(), SymptomReviewAction.Rejected, ownerId, DateTimeOffset.UtcNow));
            reportId = report.Id;
            db.Add(report);
            await db.SaveChangesAsync(ct);
        }

        var cleanup = new SymptomPhotoCleanupService(factory.Services.GetRequiredService<IServiceScopeFactory>(),
            factory.Storage, Microsoft.Extensions.Logging.Abstractions.NullLogger<SymptomPhotoCleanupService>.Instance);
        Assert.Equal(1, await cleanup.ProcessDueDeletionsAsync(ct));
        Assert.Contains(objectKey, factory.Storage.DeletedObjectKeys);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            var report = await db.Set<SymptomReport>().Include(x => x.Photos).Include(x => x.Decisions)
                .SingleAsync(x => x.FarmId == farmId && x.Id == reportId, ct);
            Assert.Equal(SymptomReportStatus.Rejected, report.Status);
            Assert.Single(report.Decisions);
            Assert.NotNull(Assert.Single(report.Photos).DeletedAt);
        }
    }

    private sealed class RecordingObservationProvider : ISymptomObservationProvider
    {
        public int CallCount { get; private set; }
        public int LastPhotoBytes { get; private set; }
        public Task<SymptomObservationResult> ObserveAsync(SymptomObservationRequest request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastPhotoBytes = request.PhotoBytes.Length;
            return Task.FromResult(new SymptomObservationResult("Small pale spots are visible.", "Image quality limits confidence.", false,
                ["Take a closer photo of the lower leaves."]));
        }
    }
}
