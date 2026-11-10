using System.Net;
using System.Net.Http.Json;
using AgroPlan.Domain.AdvisoryAccess;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgroPlan.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class AdvisorAccessTests(PostgreSqlFixture postgres)
{
    [Fact]
    public async Task Owner_grant_is_read_only_scoped_and_revocation_applies_to_same_token()
    {
        var ct = TestContext.Current.CancellationToken;
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var ownerClient = factory.CreateClient();
        using var advisorClient = factory.CreateClient();
        var owner = $"advisor-owner-{Guid.NewGuid():N}";
        var advisor = $"advisor-user-{Guid.NewGuid():N}";
        Guid farmId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (farmId, _) = await TestData.SeedFarmAsync(db, owner);
            db.Users.Add(new AgroPlan.Infrastructure.Identity.ApplicationUser
            {
                Id = advisor,
                UserName = $"{advisor}@example.test",
                NormalizedUserName = $"{advisor}@EXAMPLE.TEST",
                Email = $"{advisor}@example.test",
                NormalizedEmail = $"{advisor}@EXAMPLE.TEST",
                EmailConfirmed = true
            });
            await db.SaveChangesAsync(ct);
        }
        TestData.SetBearer(ownerClient, owner);
        TestData.SetBearer(advisorClient, advisor);

        var now = DateTimeOffset.UtcNow;
        using var grantResponse = await ownerClient.PostAsJsonAsync($"/api/farms/{farmId}/advisor-access", new
        {
            advisorUserId = advisor,
            scopes = new[] { AdvisorReportScope.Production },
            purpose = "Crop production review",
            startsAt = now.AddMinutes(-1),
            expiresAt = now.AddDays(14)
        }, ct);
        Assert.Equal(HttpStatusCode.Created, grantResponse.StatusCode);
        using var grantJson = System.Text.Json.JsonDocument.Parse(await grantResponse.Content.ReadAsStringAsync(ct));
        var grantId = grantJson.RootElement.GetProperty("id").GetGuid();

        using var allowed = await advisorClient.GetAsync($"/api/farms/{farmId}/advisor-reports/production", ct);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        using var allowedCsv = await advisorClient.GetAsync($"/api/farms/{farmId}/advisor-reports/production.csv", ct);
        Assert.Equal(HttpStatusCode.OK, allowedCsv.StatusCode);
        using var deniedCategory = await advisorClient.GetAsync($"/api/farms/{farmId}/advisor-reports/costs", ct);
        Assert.Equal(HttpStatusCode.Forbidden, deniedCategory.StatusCode);
        using var deniedCsvScope = await advisorClient.GetAsync($"/api/farms/{farmId}/advisor-reports/costs.csv", ct);
        Assert.Equal(HttpStatusCode.Forbidden, deniedCsvScope.StatusCode);
        using var deniedFarm = await advisorClient.GetAsync($"/api/farms/{Guid.NewGuid()}/advisor-reports/production", ct);
        Assert.Equal(HttpStatusCode.Forbidden, deniedFarm.StatusCode);
        using var deniedWrite = await advisorClient.PostAsJsonAsync($"/api/farms/{farmId}/advisor-reports/production", new { value = "write" }, ct);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, deniedWrite.StatusCode);

        using var revoke = await ownerClient.DeleteAsync($"/api/farms/{farmId}/advisor-access/{grantId}", ct);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        using var revokedSameToken = await advisorClient.GetAsync($"/api/farms/{farmId}/advisor-reports/production", ct);
        Assert.Equal(HttpStatusCode.Forbidden, revokedSameToken.StatusCode);

        await using var verify = factory.Services.CreateAsyncScope();
        var verifiedGrant = await verify.ServiceProvider.GetRequiredService<AgroPlanDbContext>().AdvisorAccessGrants.SingleAsync(x => x.Id == grantId, ct);
        Assert.NotNull(verifiedGrant.RevokedAt);
        Assert.Equal(owner, verifiedGrant.RevokedByUserId);
        Assert.NotNull(verifiedGrant.LastUsedAt);
    }

    [Fact]
    public async Task Expired_grant_and_non_owner_cannot_read_or_manage_access()
    {
        var ct = TestContext.Current.CancellationToken;
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var ownerClient = factory.CreateClient();
        using var adminClient = factory.CreateClient();
        using var advisorClient = factory.CreateClient();
        var owner = $"advisor-exp-owner-{Guid.NewGuid():N}";
        var admin = $"advisor-exp-admin-{Guid.NewGuid():N}";
        var advisor = $"advisor-exp-user-{Guid.NewGuid():N}";
        Guid farmId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (farmId, _) = await TestData.SeedFarmAsync(db, owner, admin);
            db.Users.Add(new AgroPlan.Infrastructure.Identity.ApplicationUser
            {
                Id = advisor,
                UserName = $"{advisor}@example.test",
                NormalizedUserName = $"{advisor}@EXAMPLE.TEST",
                Email = $"{advisor}@example.test",
                NormalizedEmail = $"{advisor}@EXAMPLE.TEST",
                EmailConfirmed = true
            });
            var now = DateTimeOffset.UtcNow;
            db.AdvisorAccessGrants.Add(new AdvisorAccessGrant(Guid.NewGuid(), farmId, advisor,
                [AdvisorReportScope.Production], owner, "Expired review", now.AddDays(-10), now.AddDays(-1), now.AddDays(-10)));
            await db.SaveChangesAsync(ct);
        }
        TestData.SetBearer(ownerClient, owner);
        TestData.SetBearer(adminClient, admin);
        TestData.SetBearer(advisorClient, advisor);

        using var expired = await advisorClient.GetAsync($"/api/farms/{farmId}/advisor-reports/production", ct);
        Assert.Equal(HttpStatusCode.Forbidden, expired.StatusCode);
        using var adminList = await adminClient.GetAsync($"/api/farms/{farmId}/advisor-access", ct);
        Assert.Equal(HttpStatusCode.Forbidden, adminList.StatusCode);
        using var adminRevoke = await adminClient.DeleteAsync($"/api/farms/{farmId}/advisor-access/{Guid.NewGuid()}", ct);
        Assert.Equal(HttpStatusCode.Forbidden, adminRevoke.StatusCode);
    }
}
