using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgroPlan.Domain.AdvisoryAccess;
using AgroPlan.Domain.Crops;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Operations;
using AgroPlan.Domain.Reports;
using AgroPlan.Domain.Workforce;
using AgroPlan.Infrastructure.Identity;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgroPlan.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class WeeklySummaryTests(PostgreSqlFixture postgres)
{
    [Fact]
    public async Task Owner_can_request_a_farm_local_weekly_summary()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var owner = $"weekly-summary-{Guid.NewGuid():N}";
        Guid farmId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (farmId, _) = await TestData.SeedFarmAsync(db, owner);
            var settings = new FarmWorkerSettings(farmId);
            settings.SetTimeZone("America/Santo_Domingo");
            db.FarmWorkerSettings.Add(settings);
            await db.SaveChangesAsync(cancellationToken);
        }
        TestData.SetBearer(client, owner);

        using var response = await client.PostAsJsonAsync($"/api/farms/{farmId}/weekly-summaries", new
        {
            weekAnchorDate = new DateOnly(2026, 11, 9),
            cropCycleId = (Guid?)null
        }, cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        Assert.True(document.RootElement.TryGetProperty("id", out _));
        Assert.Equal("Unavailable", document.RootElement.GetProperty("narrativeStatus").GetString());
        var summaryId = document.RootElement.GetProperty("id").GetGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            Assert.Empty(await db.OutboxMessages.Where(x => x.AggregateId == summaryId &&
                x.EventType == "weekly-summary.narrative.requested").ToListAsync(cancellationToken));
        }
    }

    [Fact]
    public async Task Worker_cannot_request_a_summary_and_missing_farm_time_zone_is_explicit()
    {
        var ct = TestContext.Current.CancellationToken;
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var workerClient = factory.CreateClient();
        using var ownerClient = factory.CreateClient();
        var owner = $"weekly-summary-owner-{Guid.NewGuid():N}";
        var worker = $"weekly-summary-worker-{Guid.NewGuid():N}";
        Guid farmId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (farmId, _) = await TestData.SeedFarmAsync(db, owner, worker);
            var member = await db.FarmMemberships.SingleAsync(x => x.FarmId == farmId && x.UserId == worker, ct);
            member.SetRole(FarmRole.Worker);
            await db.SaveChangesAsync(ct);
        }
        TestData.SetBearer(workerClient, worker);
        TestData.SetBearer(ownerClient, owner);
        var payload = new { weekAnchorDate = new DateOnly(2026, 11, 9), cropCycleId = (Guid?)null };

        using var forbidden = await workerClient.PostAsJsonAsync($"/api/farms/{farmId}/weekly-summaries", payload, ct);
        using var unavailable = await ownerClient.PostAsJsonAsync($"/api/farms/{farmId}/weekly-summaries", payload, ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unavailable.StatusCode);
        Assert.Contains("farm_time_zone_not_configured", await unavailable.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Summary_rate_limit_is_partitioned_per_user_and_farm()
    {
        var ct = TestContext.Current.CancellationToken;
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var ownerClient = factory.CreateClient();
        using var adminClient = factory.CreateClient();
        var owner = $"weekly-summary-limit-owner-{Guid.NewGuid():N}";
        var admin = $"weekly-summary-limit-admin-{Guid.NewGuid():N}";
        Guid farmId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (farmId, _) = await TestData.SeedFarmAsync(db, owner, admin, withParcel: false);
        }
        TestData.SetBearer(ownerClient, owner);
        TestData.SetBearer(adminClient, admin);
        var url = $"/api/farms/{farmId}/weekly-summaries";
        var payload = new { weekAnchorDate = new DateOnly(2026, 11, 9), cropCycleId = (Guid?)null };
        for (var index = 0; index < 20; index++)
        {
            using var response = await ownerClient.PostAsJsonAsync(url, payload, ct);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        }
        using var limited = await ownerClient.PostAsJsonAsync(url, payload, ct);
        using var otherUser = await adminClient.PostAsJsonAsync(url, payload, ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, otherUser.StatusCode);
    }

    [Fact]
    public async Task Owner_can_share_summary_with_current_cost_grant_and_revocation_denies_the_next_read()
    {
        var ct = TestContext.Current.CancellationToken;
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var ownerClient = factory.CreateClient();
        using var advisorClient = factory.CreateClient();
        var owner = $"weekly-summary-share-owner-{Guid.NewGuid():N}";
        var advisor = $"weekly-summary-advisor-{Guid.NewGuid():N}";
        Guid farmId; Guid parcelId; Guid expenseId; Guid grantId;
        var now = DateTimeOffset.UtcNow;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (farmId, parcelId) = await TestData.SeedFarmAsync(db, owner);
            db.Users.Add(new ApplicationUser
            {
                Id = advisor, UserName = $"{advisor}@example.test", NormalizedUserName = $"{advisor}@EXAMPLE.TEST",
                Email = $"{advisor}@example.test", NormalizedEmail = $"{advisor}@EXAMPLE.TEST", EmailConfirmed = true
            });
            var settings = new FarmWorkerSettings(farmId);
            settings.SetTimeZone("America/Santo_Domingo");
            db.FarmWorkerSettings.Add(settings);
            expenseId = Guid.NewGuid();
            db.ExpenseEntries.Add(new ExpenseEntry(expenseId, farmId, parcelId, null, OperationCostCategory.Material,
                new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero), "Cost", 25m, "DOP", null, null,
                owner, now));
            grantId = Guid.NewGuid();
            db.AdvisorAccessGrants.Add(new AdvisorAccessGrant(grantId, farmId, advisor, [AdvisorReportScope.Costs], owner,
                "Review costs", now.AddMinutes(-1), now.AddDays(10), now));
            await db.SaveChangesAsync(ct);
        }
        TestData.SetBearer(ownerClient, owner);
        TestData.SetBearer(advisorClient, advisor);
        using var created = await ownerClient.PostAsJsonAsync($"/api/farms/{farmId}/weekly-summaries",
            new { weekAnchorDate = new DateOnly(2026, 11, 9), cropCycleId = (Guid?)null }, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync(ct));
        var summaryId = createdJson.RootElement.GetProperty("id").GetGuid();
        using var advisorCreate = await advisorClient.PostAsJsonAsync($"/api/farms/{farmId}/weekly-summaries",
            new { weekAnchorDate = new DateOnly(2026, 11, 9), cropCycleId = (Guid?)null }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, advisorCreate.StatusCode);
        using var shared = await ownerClient.PostAsJsonAsync($"/api/farms/{farmId}/weekly-summaries/{summaryId}/share",
            new { advisorUserId = advisor }, ct);
        Assert.Equal(HttpStatusCode.NoContent, shared.StatusCode);
        using var advisorRead = await advisorClient.GetAsync($"/api/farms/{farmId}/weekly-summaries/{summaryId}", ct);
        Assert.Equal(HttpStatusCode.OK, advisorRead.StatusCode);
        using var advisorJson = JsonDocument.Parse(await advisorRead.Content.ReadAsStringAsync(ct));
        var projection = advisorJson.RootElement.GetProperty("projection");
        Assert.Empty(projection.GetProperty("pendingTasks").EnumerateArray());
        Assert.Single(projection.GetProperty("sources").EnumerateArray());
        Assert.Equal("expense", projection.GetProperty("sources")[0].GetProperty("kind").GetString());
        var sharedSourceId = projection.GetProperty("sources")[0].GetProperty("id").GetGuid();
        using var sourceRead = await advisorClient.GetAsync($"/api/farms/{farmId}/weekly-summaries/{summaryId}/sources/{sharedSourceId}", ct);
        Assert.Equal(HttpStatusCode.OK, sourceRead.StatusCode);
        using var sourceJson = JsonDocument.Parse(await sourceRead.Content.ReadAsStringAsync(ct));
        Assert.Equal("expense", sourceJson.RootElement.GetProperty("kind").GetString());
        Assert.DoesNotContain("requestedByUserId", advisorJson.RootElement.ToString(), StringComparison.Ordinal);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (await db.AdvisorAccessGrants.SingleAsync(x => x.Id == grantId, ct)).Revoke(owner, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(ct);
        }
        using var revokedRead = await advisorClient.GetAsync($"/api/farms/{farmId}/weekly-summaries/{summaryId}", ct);
        using var revokedSourceRead = await advisorClient.GetAsync(
            $"/api/farms/{farmId}/weekly-summaries/{summaryId}/sources/{sharedSourceId}", ct);
        Assert.Equal(HttpStatusCode.Forbidden, revokedRead.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, revokedSourceRead.StatusCode);
    }

    [Theory]
    [InlineData(AdvisorReportScope.Costs, "ungranted-production-secret")]
    [InlineData(AdvisorReportScope.Production, "ungranted-cost-secret")]
    public async Task Advisor_never_receives_unfiltered_narrative_for_a_single_scope_grant(
        AdvisorReportScope scopeType, string hiddenFact)
    {
        var ct = TestContext.Current.CancellationToken;
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var ownerClient = factory.CreateClient();
        using var advisorClient = factory.CreateClient();
        var owner = $"weekly-summary-narrative-owner-{Guid.NewGuid():N}";
        var advisor = $"weekly-summary-narrative-advisor-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        Guid farmId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (farmId, _) = await TestData.SeedFarmAsync(db, owner, withParcel: false);
            db.Users.Add(new ApplicationUser
            {
                Id = advisor, UserName = $"{advisor}@example.test", NormalizedUserName = $"{advisor}@EXAMPLE.TEST",
                Email = $"{advisor}@example.test", NormalizedEmail = $"{advisor}@EXAMPLE.TEST", EmailConfirmed = true
            });
            var settings = new FarmWorkerSettings(farmId);
            settings.SetTimeZone("America/Santo_Domingo");
            db.FarmWorkerSettings.Add(settings);
            db.AdvisorAccessGrants.Add(new AdvisorAccessGrant(Guid.NewGuid(), farmId, advisor, [scopeType], owner,
                "Single-scope narrative test", now.AddMinutes(-1), now.AddDays(10), now));
            await db.SaveChangesAsync(ct);
        }

        TestData.SetBearer(ownerClient, owner);
        TestData.SetBearer(advisorClient, advisor);
        using var created = await ownerClient.PostAsJsonAsync($"/api/farms/{farmId}/weekly-summaries",
            new { weekAnchorDate = new DateOnly(2026, 11, 9), cropCycleId = (Guid?)null }, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync(ct));
        var summaryId = createdJson.RootElement.GetProperty("id").GetGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            var summary = await db.Set<WeeklySummary>().SingleAsync(x => x.Id == summaryId, ct);
            summary.SetNarrative($"The unrestricted narrative contains {hiddenFact}.",
                WeeklySummaryNarrativeStatus.Available);
            await db.SaveChangesAsync(ct);
        }
        using var shared = await ownerClient.PostAsJsonAsync($"/api/farms/{farmId}/weekly-summaries/{summaryId}/share",
            new { advisorUserId = advisor }, ct);
        Assert.Equal(HttpStatusCode.NoContent, shared.StatusCode);

        using var advisorRead = await advisorClient.GetAsync($"/api/farms/{farmId}/weekly-summaries/{summaryId}", ct);
        Assert.Equal(HttpStatusCode.OK, advisorRead.StatusCode);
        using var advisorJson = JsonDocument.Parse(await advisorRead.Content.ReadAsStringAsync(ct));
        Assert.Null(advisorJson.RootElement.GetProperty("narrativeText").GetString());
        Assert.DoesNotContain(hiddenFact, advisorJson.RootElement.ToString(), StringComparison.Ordinal);
    }
}

public sealed class WeeklySummaryOpenApiTests
{
    private const string UnusedConnectionString = "Host=127.0.0.1;Port=5432;Database=not_used;Username=test;Password=test";

    [Fact]
    public async Task Development_openapi_and_scalar_include_weekly_summary_routes()
    {
        await using var factory = new ApiTestFactory(UnusedConnectionString, disableBackgroundServices: true);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var documentResponse = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, documentResponse.StatusCode);
        using var document = JsonDocument.Parse(await documentResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/farms/{farmId}/weekly-summaries", out var createPath));
        Assert.True(createPath.TryGetProperty("post", out _));
        Assert.True(paths.TryGetProperty("/api/farms/{farmId}/weekly-summaries/{summaryId}", out var readPath));
        Assert.True(readPath.TryGetProperty("get", out _));
        Assert.True(paths.TryGetProperty("/api/farms/{farmId}/weekly-summaries/{summaryId}/share", out _));
        Assert.True(paths.TryGetProperty("/api/farms/{farmId}/weekly-summaries/{summaryId}/sources/{sourceId}", out _));

        using var scalarResponse = await client.GetAsync("/scalar/", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, scalarResponse.StatusCode);
        Assert.Contains("Scalar", await scalarResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Production_hides_weekly_summary_documentation()
    {
        await using var factory = new ApiTestFactory(UnusedConnectionString, "Production", disableBackgroundServices: true);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var scalar = await client.GetAsync("/scalar", TestContext.Current.CancellationToken);
        using var document = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, scalar.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, document.StatusCode);
    }
}
