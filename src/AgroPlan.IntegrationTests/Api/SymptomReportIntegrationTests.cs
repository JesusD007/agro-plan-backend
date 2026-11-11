using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgroPlan.Domain.Symptoms;
using AgroPlan.Domain.Farms;
using AgroPlan.Infrastructure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgroPlan.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class SymptomReportIntegrationTests(PostgreSqlFixture postgres)
{
    [Fact]
    public async Task Active_farm_member_can_request_private_symptom_photo_upload_authorization()
    {
        var ct = TestContext.Current.CancellationToken;
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var ownerId = $"symptom-owner-{Guid.NewGuid():N}";
        Guid farmId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlan.Infrastructure.Persistence.AgroPlanDbContext>();
            (farmId, _) = await TestData.SeedFarmAsync(db, ownerId);
        }
        TestData.SetBearer(client, ownerId);

        using var response = await client.PostAsJsonAsync($"/api/farms/{farmId}/symptom-reports/upload-authorizations", new
        {
            cropCycleId = (Guid?)null,
            contentType = "image/png",
            sizeBytes = 512L,
            externalProcessingConsent = true,
            consentNoticeVersion = "symptom-ai-v1"
        }, ct);

        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Expected OK; got {(int)response.StatusCode} {response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        Assert.True(document.RootElement.TryGetProperty("photoId", out _));
        Assert.True(document.RootElement.TryGetProperty("uploadUrl", out _));
        Assert.False(document.RootElement.TryGetProperty("publicUrl", out _));
    }

    [Fact]
    public async Task Finalized_private_photo_report_stays_pending_and_never_exposes_storage_keys()
    {
        var ct = TestContext.Current.CancellationToken;
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var ownerId = $"symptom-finalize-{Guid.NewGuid():N}";
        var otherOwnerId = $"symptom-other-owner-{Guid.NewGuid():N}";
        Guid farmId;
        Guid otherFarmId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlan.Infrastructure.Persistence.AgroPlanDbContext>();
            (farmId, _) = await TestData.SeedFarmAsync(db, ownerId);
            (otherFarmId, _) = await TestData.SeedFarmAsync(db, otherOwnerId);
            db.FarmMemberships.Add(new FarmMembership(otherFarmId, ownerId, FarmRole.Administrator, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(ct);
        }
        TestData.SetBearer(client, ownerId);
        using var upload = await client.PostAsJsonAsync($"/api/farms/{farmId}/symptom-reports/upload-authorizations", new
        {
            cropCycleId = (Guid?)null,
            contentType = "image/png",
            sizeBytes = 512L,
            externalProcessingConsent = true,
            consentNoticeVersion = "symptom-ai-v1"
        }, ct);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        using var uploadJson = JsonDocument.Parse(await upload.Content.ReadAsStringAsync(ct));
        var reportId = uploadJson.RootElement.GetProperty("reportId").GetGuid();
        using var crossFarm = await client.GetAsync($"/api/farms/{otherFarmId}/symptom-reports/{reportId}", ct);
        Assert.Equal(HttpStatusCode.NotFound, crossFarm.StatusCode);
        using var finalize = await client.PostAsJsonAsync($"/api/farms/{farmId}/symptom-reports", new { reportId }, ct);
        Assert.Equal(HttpStatusCode.Accepted, finalize.StatusCode);
        using var get = await client.GetAsync($"/api/farms/{farmId}/symptom-reports/{reportId}", ct);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var response = await get.Content.ReadAsStringAsync(ct);
        using var result = JsonDocument.Parse(response);
        Assert.Equal("Pending", result.RootElement.GetProperty("status").GetString());
        Assert.True(result.RootElement.GetProperty("photos")[0].GetProperty("consentedToAi").GetBoolean());
        Assert.DoesNotContain("objectKey", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("publicUrl", response, StringComparison.OrdinalIgnoreCase);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlan.Infrastructure.Persistence.AgroPlanDbContext>();
            Assert.False(await db.OutboxMessages.AnyAsync(x => x.AggregateId == reportId &&
                x.EventType == "symptom-observation.requested", ct));
        }
    }

    [Fact]
    public async Task Revoked_farm_member_cannot_finalize_symptom_report_or_emit_outbox()
    {
        var ct = TestContext.Current.CancellationToken;
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var ownerId = $"symptom-revoked-owner-{Guid.NewGuid():N}";
        var workerId = $"symptom-revoked-worker-{Guid.NewGuid():N}";
        Guid farmId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlan.Infrastructure.Persistence.AgroPlanDbContext>();
            (farmId, _) = await TestData.SeedFarmAsync(db, ownerId, workerId);
            db.FarmMemberships.Single(x => x.FarmId == farmId && x.UserId == workerId).SetRole(FarmRole.Worker);
            await db.SaveChangesAsync(ct);
        }

        TestData.SetBearer(client, workerId);
        using var upload = await client.PostAsJsonAsync($"/api/farms/{farmId}/symptom-reports/upload-authorizations", new
        {
            cropCycleId = (Guid?)null,
            contentType = "image/png",
            sizeBytes = 512L,
            externalProcessingConsent = true,
            consentNoticeVersion = "symptom-ai-v1"
        }, ct);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        using var uploadJson = JsonDocument.Parse(await upload.Content.ReadAsStringAsync(ct));
        var reportId = uploadJson.RootElement.GetProperty("reportId").GetGuid();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlan.Infrastructure.Persistence.AgroPlanDbContext>();
            db.FarmMemberships.Single(x => x.FarmId == farmId && x.UserId == workerId).Revoke();
            await db.SaveChangesAsync(ct);
        }

        using var finalize = await client.PostAsJsonAsync($"/api/farms/{farmId}/symptom-reports", new { reportId }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, finalize.StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlan.Infrastructure.Persistence.AgroPlanDbContext>();
            var report = await db.Set<SymptomReport>().SingleAsync(x => x.Id == reportId, ct);
            Assert.Equal(SymptomReportStatus.Uploading, report.Status);
            Assert.False(await db.OutboxMessages.AnyAsync(x => x.AggregateId == reportId &&
                x.EventType == "symptom-observation.requested", ct));
        }
    }

    [Fact]
    public async Task Only_currently_verified_and_assigned_agronomist_can_review_and_read_private_photos()
    {
        var ct = TestContext.Current.CancellationToken;
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var ownerClient = factory.CreateClient();
        var ownerId = $"symptom-owner-review-{Guid.NewGuid():N}";
        var otherOwnerId = $"symptom-other-owner-review-{Guid.NewGuid():N}";
        var agronomistId = $"symptom-agronomist-{Guid.NewGuid():N}";
        Guid farmId;
        Guid otherFarmId;
        Guid reportId;
        Guid photoId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlan.Infrastructure.Persistence.AgroPlanDbContext>();
            (farmId, _) = await TestData.SeedFarmAsync(db, ownerId);
            (otherFarmId, _) = await TestData.SeedFarmAsync(db, otherOwnerId);
            db.FarmMemberships.Add(new FarmMembership(otherFarmId, ownerId, FarmRole.Administrator, DateTimeOffset.UtcNow));
            db.Users.Add(new ApplicationUser { Id = agronomistId, UserName = agronomistId,
                NormalizedUserName = agronomistId.ToUpperInvariant(), Email = $"{agronomistId}@example.test",
                NormalizedEmail = $"{agronomistId}@EXAMPLE.TEST", EmailConfirmed = true });
            db.Set<TrustedAgronomistVerification>().Add(new TrustedAgronomistVerification(
                agronomistId, ownerId, "credential-ref", DateTimeOffset.UtcNow));
            var report = new SymptomReport(Guid.NewGuid(), farmId, ownerId, null, null, DateTimeOffset.UtcNow);
            var photo = new SymptomReportPhoto(Guid.NewGuid(), "private/staging", "image/png", 512,
                ownerId, false, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(10));
            photo.Finalize("private/final", 512, DateTimeOffset.UtcNow);
            report.AddPhoto(photo);
            report.Submit(DateTimeOffset.UtcNow);
            reportId = report.Id;
            photoId = photo.Id;
            db.Add(report);
            await db.SaveChangesAsync(ct);
        }

        TestData.SetBearer(ownerClient, ownerId);
        using var grant = await ownerClient.PostAsJsonAsync($"/api/farms/{farmId}/symptom-reports/agronomist-authorizations",
            new { userId = agronomistId }, ct);
        Assert.True(grant.StatusCode == HttpStatusCode.NoContent,
            $"Expected agronomist grant; got {(int)grant.StatusCode}: {await grant.Content.ReadAsStringAsync(ct)}");
        using var wrongFarmGrant = await ownerClient.PostAsJsonAsync($"/api/farms/{otherFarmId}/symptom-reports/agronomist-authorizations",
            new { userId = agronomistId }, ct);
        Assert.Equal(HttpStatusCode.NoContent, wrongFarmGrant.StatusCode);
        using var agronomistClient = factory.CreateClient();
        TestData.SetBearer(agronomistClient, agronomistId);
        using var review = await agronomistClient.PostAsJsonAsync($"/api/farms/{farmId}/symptom-reports/{reportId}/review",
            new { action = SymptomReviewAction.MoreEvidenceRequested, requestedEvidence = "Take another photo.", diagnosis = (string?)null, treatment = (string?)null }, ct);
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        using var photoResponse = await agronomistClient.GetAsync($"/api/farms/{farmId}/symptom-reports/{reportId}/photos/{photoId}", ct);
        Assert.Equal(HttpStatusCode.OK, photoResponse.StatusCode);
        using var wrongFarmPhoto = await agronomistClient.GetAsync($"/api/farms/{otherFarmId}/symptom-reports/{reportId}/photos/{photoId}", ct);
        Assert.Equal(HttpStatusCode.NotFound, wrongFarmPhoto.StatusCode);

        using var revoke = await ownerClient.DeleteAsync($"/api/farms/{farmId}/symptom-reports/agronomist-authorizations/{agronomistId}", ct);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        using var forbidden = await agronomistClient.PostAsJsonAsync($"/api/farms/{farmId}/symptom-reports/{reportId}/review",
            new { action = SymptomReviewAction.Confirmed, requestedEvidence = (string?)null, diagnosis = "x", treatment = "y" }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlan.Infrastructure.Persistence.AgroPlanDbContext>();
            db.Set<TrustedAgronomistVerification>().Single(x => x.UserId == agronomistId).Revoke(ownerId, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(ct);
        }
        using var wrongIdentity = await ownerClient.PostAsJsonAsync($"/api/farms/{otherFarmId}/symptom-reports/agronomist-authorizations",
            new { userId = agronomistId }, ct);
        Assert.Equal(HttpStatusCode.Conflict, wrongIdentity.StatusCode);
        using var revokedVerification = await agronomistClient.PostAsJsonAsync($"/api/farms/{otherFarmId}/symptom-reports/{Guid.NewGuid()}/review",
            new { action = SymptomReviewAction.Confirmed, requestedEvidence = (string?)null, diagnosis = "x", treatment = "y" }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, revokedVerification.StatusCode);
    }
}

[Collection(PostgreSqlCollection.Name)]
public sealed class SymptomReportOpenApiTests
{
    private const string UnusedConnectionString = "Host=127.0.0.1;Port=5432;Database=not_used;Username=test;Password=test";

    [Fact]
    public async Task Development_exposes_symptom_report_routes_in_openapi_and_scalar()
    {
        await using var factory = new ApiTestFactory(UnusedConnectionString, disableBackgroundServices: true);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var paths = document.RootElement.GetProperty("paths");
        foreach (var expected in new[]
                 {
                     "/api/farms/{farmId}/symptom-reports/upload-authorizations",
                     "/api/farms/{farmId}/symptom-reports",
                     "/api/farms/{farmId}/symptom-reports/{reportId}",
                     "/api/farms/{farmId}/symptom-reports/{reportId}/photos/{photoId}",
                     "/api/farms/{farmId}/symptom-reports/{reportId}/review",
                     "/api/farms/{farmId}/symptom-reports/agronomist-authorizations"
                 })
            Assert.True(paths.TryGetProperty(expected, out _), $"OpenAPI is missing {expected}.");
        using var scalar = await client.GetAsync("/scalar/", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, scalar.StatusCode);
    }

    [Fact]
    public async Task Production_hides_symptom_report_documentation()
    {
        await using var factory = new ApiTestFactory(UnusedConnectionString, "Production", disableBackgroundServices: true);
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        { AllowAutoRedirect = false });
        using var document = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        using var scalar = await client.GetAsync("/scalar/", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, document.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, scalar.StatusCode);
    }
}
