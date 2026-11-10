using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgroPlan.Domain.Farms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AgroPlan.Infrastructure.Persistence;
using Xunit;

namespace AgroPlan.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class WorkerInvitationIntegrationTests(PostgreSqlFixture postgres)
{
    [Fact]
    public async Task Owner_can_invite_verified_email_and_only_digest_is_persisted()
    {
        var ct = TestContext.Current.CancellationToken;
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var ownerClient = factory.CreateClient();
        var owner = $"invite-owner-{Guid.NewGuid():N}";
        var email = $"worker-{Guid.NewGuid():N}@example.test";
        Guid farmId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            (farmId, _) = await TestData.SeedFarmAsync(
                scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>(), owner, withParcel: false);
        }
        TestData.SetBearer(ownerClient, owner);

        using var response = await ownerClient.PostAsJsonAsync($"/api/farms/{farmId}/workers/invitations", new
        {
            email
        }, ct);

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Expected Created, got {(int)response.StatusCode}: {responseBody}");
        using var responseJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var invitationId = responseJson.RootElement.GetProperty("id").GetGuid();
        Assert.Single(factory.Email.Messages);
        var token = System.Text.RegularExpressions.Regex.Match(factory.Email.Messages[0].TextBody, @"token=([^\s]+)").Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(token));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            var invitation = await db.WorkerInvitations.SingleAsync(item => item.Id == invitationId, ct);
            Assert.DoesNotContain(token, invitation.TokenDigest, StringComparison.Ordinal);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))), invitation.TokenDigest);
            Assert.Equal(email.ToUpperInvariant(), invitation.Email);
        }

        var workerId = $"invite-worker-{Guid.NewGuid():N}";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>().Users.Add(new AgroPlan.Infrastructure.Identity.ApplicationUser
            {
                Id = workerId,
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                EmailConfirmed = true
            });
            await scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>().SaveChangesAsync(ct);
        }
        using var workerClient = factory.CreateClient();
        TestData.SetBearer(workerClient, workerId);
        using var accept = await workerClient.PostAsJsonAsync("/api/worker-invitations/accept", new { token }, ct);
        Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);
        using var replay = await workerClient.PostAsJsonAsync("/api/worker-invitations/accept", new { token }, ct);
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            var membership = await db.FarmMemberships.SingleAsync(item => item.FarmId == farmId && item.UserId == workerId, ct);
            Assert.True(membership.IsActive);
            Assert.Equal(FarmRole.Worker, membership.Role);
        }

        for (var index = 0; index < 4; index++)
        {
            using var extra = await ownerClient.PostAsJsonAsync($"/api/farms/{farmId}/workers/invitations",
                new { email = $"extra-{index}-{Guid.NewGuid():N}@example.test" }, ct);
            Assert.Equal(HttpStatusCode.Created, extra.StatusCode);
        }
        using var full = await ownerClient.PostAsJsonAsync($"/api/farms/{farmId}/workers/invitations",
            new { email = $"over-cap-{Guid.NewGuid():N}@example.test" }, ct);
        Assert.Equal(HttpStatusCode.Conflict, full.StatusCode);
        }

    [Fact]
    public async Task Unverified_or_mismatched_account_cannot_accept_invitation()
    {
        var ct = TestContext.Current.CancellationToken;
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var ownerClient = factory.CreateClient();
        var owner = $"invite-verify-owner-{Guid.NewGuid():N}";
        Guid farmId;
        await using (var scope = factory.Services.CreateAsyncScope())
            (farmId, _) = await TestData.SeedFarmAsync(scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>(), owner, withParcel: false);
        TestData.SetBearer(ownerClient, owner);
        var invitedEmail = $"target-{Guid.NewGuid():N}@example.test";
        using var created = await ownerClient.PostAsJsonAsync($"/api/farms/{farmId}/workers/invitations", new { email = invitedEmail }, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var token = System.Text.RegularExpressions.Regex.Match(factory.Email.Messages.Single().TextBody, @"token=([^\s]+)").Groups[1].Value;

        var mismatchedUser = $"invite-wrong-{Guid.NewGuid():N}";
        var unverifiedUser = $"invite-unverified-{Guid.NewGuid():N}";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            db.Users.AddRange(
                new AgroPlan.Infrastructure.Identity.ApplicationUser
                {
                    Id = mismatchedUser, UserName = $"{mismatchedUser}@example.test", NormalizedUserName = $"{mismatchedUser}@EXAMPLE.TEST",
                    Email = $"{mismatchedUser}@example.test", NormalizedEmail = $"{mismatchedUser}@EXAMPLE.TEST", EmailConfirmed = true
                },
                new AgroPlan.Infrastructure.Identity.ApplicationUser
                {
                    Id = unverifiedUser, UserName = invitedEmail, NormalizedUserName = invitedEmail.ToUpperInvariant(),
                    Email = invitedEmail, NormalizedEmail = invitedEmail.ToUpperInvariant(), EmailConfirmed = false
                });
            await db.SaveChangesAsync(ct);
        }
        using var mismatchedClient = factory.CreateClient();
        TestData.SetBearer(mismatchedClient, mismatchedUser);
        using var mismatch = await mismatchedClient.PostAsJsonAsync("/api/worker-invitations/accept", new { token }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, mismatch.StatusCode);
        using var unverifiedClient = factory.CreateClient();
        TestData.SetBearer(unverifiedClient, unverifiedUser);
        using var unverified = await unverifiedClient.PostAsJsonAsync("/api/worker-invitations/accept", new { token }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, unverified.StatusCode);
    }

    [Fact]
    public async Task Basic_tier_reserves_five_seats_atomically_and_premium_has_no_worker_cap()
    {
        var ct = TestContext.Current.CancellationToken;
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var owner = $"invite-cap-owner-{Guid.NewGuid():N}";
        Guid farmId;
        await using (var scope = factory.Services.CreateAsyncScope())
            (farmId, _) = await TestData.SeedFarmAsync(scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>(), owner, withParcel: false);
        TestData.SetBearer(client, owner);

        var requests = Enumerable.Range(0, 6).Select(index => client.PostAsJsonAsync($"/api/farms/{farmId}/workers/invitations",
            new { email = $"basic-{index}-{Guid.NewGuid():N}@example.test" }, ct));
        var responses = await Task.WhenAll(requests);
        Assert.Equal(5, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
        Assert.Single(responses.Where(response => response.StatusCode == HttpStatusCode.Conflict));
        foreach (var response in responses) response.Dispose();

        Guid premiumFarmId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (premiumFarmId, _) = await TestData.SeedFarmAsync(db, owner + "-premium", withParcel: false);
            var premiumFarm = await db.Farms.SingleAsync(item => item.Id == premiumFarmId, ct);
            premiumFarm.ProvisionWorkerEntitlement(WorkerEntitlement.Premium);
            await db.SaveChangesAsync(ct);
        }
        TestData.SetBearer(client, owner + "-premium");
        for (var index = 0; index < 6; index++)
        {
            using var response = await client.PostAsJsonAsync($"/api/farms/{premiumFarmId}/workers/invitations",
                new { email = $"premium-{index}-{Guid.NewGuid():N}@example.test" }, ct);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
    }

    [Fact]
    public async Task Administrator_can_renew_and_cancel_pending_invitation()
    {
        var ct = TestContext.Current.CancellationToken;
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var ownerClient = factory.CreateClient();
        var owner = $"invite-lifecycle-owner-{Guid.NewGuid():N}";
        var admin = $"invite-lifecycle-admin-{Guid.NewGuid():N}";
        Guid farmId;
        await using (var scope = factory.Services.CreateAsyncScope())
            (farmId, _) = await TestData.SeedFarmAsync(scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>(), owner, admin, withParcel: false);
        TestData.SetBearer(ownerClient, owner);
        var email = $"lifecycle-{Guid.NewGuid():N}@example.test";
        using var created = await ownerClient.PostAsJsonAsync($"/api/farms/{farmId}/workers/invitations", new { email }, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var content = JsonDocument.Parse(await created.Content.ReadAsStringAsync(ct));
        var invitationId = content.RootElement.GetProperty("id").GetGuid();
        var originalDigest = await GetDigest(factory, invitationId, ct);

        TestData.SetBearer(ownerClient, admin);
        using var renewed = await ownerClient.PostAsync($"/api/farms/{farmId}/workers/invitations/{invitationId}/renew", null, ct);
        Assert.Equal(HttpStatusCode.NoContent, renewed.StatusCode);
        Assert.NotEqual(originalDigest, await GetDigest(factory, invitationId, ct));
        using var cancelled = await ownerClient.DeleteAsync($"/api/farms/{farmId}/workers/invitations/{invitationId}", ct);
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var invitation = await verifyScope.ServiceProvider.GetRequiredService<AgroPlanDbContext>().WorkerInvitations.SingleAsync(item => item.Id == invitationId, ct);
        Assert.NotNull(invitation.CancelledAt);
    }

    private static async Task<string> GetDigest(ApiTestFactory factory, Guid invitationId, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>().WorkerInvitations
            .Where(item => item.Id == invitationId).Select(item => item.TokenDigest).SingleAsync(ct);
    }
}
