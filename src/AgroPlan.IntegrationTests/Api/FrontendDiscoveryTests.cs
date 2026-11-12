using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Workforce;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.TestHost;
using AgroPlan.Application.Abstractions;
using AgroPlan.Infrastructure.Storage;
using Xunit;

namespace AgroPlan.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class FrontendDiscoveryTests(PostgreSqlFixture postgres)
{
    [Fact]
    public async Task Confirmation_requires_a_token_for_the_current_account_and_delivers_only_by_email()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString, disableBackgroundServices: true,
            additionalConfiguration: new Dictionary<string, string?> { ["Identity:EmailConfirmationUrl"] = "https://client.example.test/confirmar-correo" });
        using var client = factory.CreateClient();
        var email = $"verify-{Guid.NewGuid():N}@example.test";
        var registration = await client.PostAsJsonAsync("/api/identity/register", new { email, password = "LocalTest!A912345" }, TestContext.Current.CancellationToken);
        var session = await registration.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.GetProperty("accessToken").GetString());
        var sent = await client.PostAsync("/api/identity/email-verification", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, sent.StatusCode);
        var delivered = Assert.Single(factory.Email.Messages);
        Assert.Equal(email, delivered.Recipient);
        var token = Uri.UnescapeDataString(delivered.TextBody.Split("?token=")[1]);
        Assert.DoesNotContain(token, await sent.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var invalid = await client.PostAsJsonAsync("/api/identity/confirm-email", new { token = "invalid" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var otherRegistration = await client.PostAsJsonAsync("/api/identity/register", new { email = $"other-{Guid.NewGuid():N}@example.test", password = "LocalTest!A912345" }, TestContext.Current.CancellationToken);
        var otherSession = await otherRegistration.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", otherSession.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/identity/confirm-email", new { token }, TestContext.Current.CancellationToken)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.GetProperty("accessToken").GetString());
        var confirmed = await client.PostAsJsonAsync("/api/identity/confirm-email", new { token }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
    }

    [Fact]
    public async Task Worker_discovers_only_own_active_farm_and_gets_real_task_version()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString, disableBackgroundServices: true);
        using var client = factory.CreateClient();
        var worker = $"worker-{Guid.NewGuid():N}";
        var owner = $"owner-{Guid.NewGuid():N}";
        Guid farmId; Guid taskId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            (farmId, _) = await TestData.SeedFarmAsync(db, owner);
            db.Users.Add(new AgroPlan.Infrastructure.Identity.ApplicationUser { Id = worker, UserName = worker, NormalizedUserName = worker.ToUpperInvariant(), Email = $"{worker}@example.test", NormalizedEmail = $"{worker}@example.test".ToUpperInvariant() });
            db.FarmMemberships.Add(new FarmMembership(farmId, worker, FarmRole.Worker, DateTimeOffset.UtcNow));
            taskId = Guid.NewGuid();
            db.WorkTasks.Add(new WorkTask(taskId, farmId, worker, DateOnly.FromDateTime(DateTime.UtcNow), "Irrigate north parcel", false, null, null, null, null));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        TestData.SetBearer(client, worker);
        using var memberships = await client.GetAsync("/api/farms/memberships", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, memberships.StatusCode);
        var farms = await memberships.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Single(farms.EnumerateArray());
        Assert.Equal(farmId, farms[0].GetProperty("farmId").GetGuid());
        Assert.Equal("Worker", farms[0].GetProperty("role").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/farms/{farmId}/members", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/farms/{farmId}/work-sync/conflicts", TestContext.Current.CancellationToken)).StatusCode);
        var task = await client.GetFromJsonAsync<JsonElement>($"/api/farms/{farmId}/work-tasks/{taskId}", TestContext.Current.CancellationToken);
        Assert.Equal(1, task.GetProperty("version").GetInt32());
        await using var unconfigured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IFileStorage>();
            services.AddSingleton<IFileStorage, UnconfiguredFileStorage>();
        }));
        using var withoutStorage = unconfigured.CreateClient();
        TestData.SetBearer(withoutStorage, worker);
        Assert.Equal(HttpStatusCode.OK, (await withoutStorage.GetAsync($"/api/farms/{farmId}/work-tasks/{taskId}/evidence", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await withoutStorage.PostAsync($"/api/farms/{farmId}/work-tasks/{taskId}/start", null, TestContext.Current.CancellationToken)).StatusCode);
        var uploadUnavailable = await withoutStorage.PostAsJsonAsync($"/api/farms/{farmId}/work-tasks/{taskId}/evidence/uploads",
            new { clientOperationId = Guid.NewGuid(), contentType = "image/jpeg", sizeBytes = 100, consentLocation = false, consentCapturedAt = false }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, uploadUnavailable.StatusCode);
        TestData.SetBearer(client, owner);
        var members = await client.GetFromJsonAsync<JsonElement>($"/api/farms/{farmId}/members", TestContext.Current.CancellationToken);
        Assert.Equal(2, members.GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/farms/{farmId}/workers/invitations", TestContext.Current.CancellationToken)).StatusCode);
        TestData.SetBearer(client, $"stranger-{Guid.NewGuid():N}");
        var none = await client.GetFromJsonAsync<JsonElement>("/api/farms/memberships", TestContext.Current.CancellationToken);
        Assert.Empty(none.EnumerateArray());
    }
}
