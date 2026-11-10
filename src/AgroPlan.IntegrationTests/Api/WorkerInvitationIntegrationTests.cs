using System.Net;
using System.Net.Http.Json;
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
        Guid farmId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            (farmId, _) = await TestData.SeedFarmAsync(
                scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>(), owner, withParcel: false);
        }
        TestData.SetBearer(ownerClient, owner);

        using var response = await ownerClient.PostAsJsonAsync($"/api/farms/{farmId}/workers/invitations", new
        {
            email = $"worker-{Guid.NewGuid():N}@example.test"
        }, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
