using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgroPlan.Domain.Farms;
using AgroPlan.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgroPlan.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class IdentityAndFarmAuthorizationTests(PostgreSqlFixture postgres)
{
    [Fact]
    public async Task Producer_can_register_create_a_farm_and_parcel_then_read_them_over_http()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var email = $"producer-{Guid.NewGuid():N}@example.test";

        using var registration = await client.PostAsJsonAsync("/api/identity/register", new
        {
            email,
            password = "InitialPass!12345"
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        using var session = JsonDocument.Parse(await registration.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var accessToken = session.RootElement.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        using var farmResponse = await client.PostAsJsonAsync("/api/farms", new
        {
            name = "Integration Farm",
            location = "Test province"
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, farmResponse.StatusCode);
        using var farmDocument = JsonDocument.Parse(await farmResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var farmId = farmDocument.RootElement.GetProperty("id").GetGuid();

        using var parcelResponse = await client.PostAsJsonAsync($"/api/farms/{farmId}/parcels", new
        {
            name = "North Parcel",
            location = "Test province",
            areaHectares = 1.25m
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, parcelResponse.StatusCode);
        using var parcelDocument = JsonDocument.Parse(await parcelResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var parcelId = parcelDocument.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(farmId, parcelDocument.RootElement.GetProperty("farmId").GetGuid());

        using var farmRead = await client.GetAsync($"/api/farms/{farmId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, farmRead.StatusCode);
        using var parcelRead = await client.GetAsync($"/api/farms/{farmId}/parcels/{parcelId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, parcelRead.StatusCode);
    }

    [Fact]
    public async Task Farm_membership_is_rechecked_on_each_request_and_scoped_to_that_farm()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var ownerA = $"owner-{Guid.NewGuid():N}";
        var admin = $"admin-{Guid.NewGuid():N}";
        var ownerB = $"owner-{Guid.NewGuid():N}";

        Guid farmA;
        Guid farmB;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlan.Infrastructure.Persistence.AgroPlanDbContext>();
            farmA = (await TestData.SeedFarmAsync(db, ownerA, admin)).FarmId;
            farmB = (await TestData.SeedFarmAsync(db, ownerB)).FarmId;
        }

        TestData.SetBearer(client, admin);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/farms/{farmA}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/farms/{farmB}")).StatusCode);
        using var grantAttempt = await client.PostAsJsonAsync($"/api/farms/{farmA}/administrators", new
        {
            email = $"{ownerB}@example.test"
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, grantAttempt.StatusCode);
        using var revokeAttempt = await client.DeleteAsync($"/api/farms/{farmA}/administrators/{admin}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, revokeAttempt.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlan.Infrastructure.Persistence.AgroPlanDbContext>();
            var membership = await db.FarmMemberships.SingleAsync(item => item.FarmId == farmA && item.UserId == admin);
            membership.Revoke();
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/farms/{farmA}")).StatusCode);
    }

    [Fact]
    public async Task Support_mediated_recovery_verifies_email_and_changes_password_without_exposing_tokens()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var email = $"recovery-{Guid.NewGuid():N}@example.test";
        using var registration = await client.PostAsJsonAsync("/api/identity/register", new
        {
            email,
            password = "InitialPass!12345"
        });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);

        using var start = await client.PostAsJsonAsync("/api/recovery/requests", new { email });
        Assert.True(start.StatusCode == HttpStatusCode.Accepted,
            $"Expected accepted recovery request; got {(int)start.StatusCode}: {await start.Content.ReadAsStringAsync()}");
        var startText = await start.Content.ReadAsStringAsync();
        Assert.DoesNotContain("code", startText, StringComparison.OrdinalIgnoreCase);
        using var startJson = JsonDocument.Parse(startText);
        var requestId = startJson.RootElement.GetProperty("requestId").GetGuid();
        var codeEmail = Assert.Single(factory.Email.Messages);
        var code = Regex.Match(codeEmail.TextBody, @"code is (?<code>\d{8})").Groups["code"].Value;
        Assert.Equal(8, code.Length);

        using var verification = await client.PostAsJsonAsync($"/api/recovery/requests/{requestId}/verify", new { code });
        Assert.Equal(HttpStatusCode.Accepted, verification.StatusCode);

        var supportUserId = $"support-{Guid.NewGuid():N}";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlan.Infrastructure.Persistence.AgroPlanDbContext>();
            db.Users.Add(new ApplicationUser
            {
                Id = supportUserId,
                UserName = $"{supportUserId}@example.test",
                NormalizedUserName = $"{supportUserId}@EXAMPLE.TEST",
                Email = $"{supportUserId}@example.test",
                NormalizedEmail = $"{supportUserId}@EXAMPLE.TEST",
                EmailConfirmed = true
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        TestData.SetBearer(client, supportUserId, SupportRole.Name);
        using var pending = await client.GetAsync("/api/recovery/support/pending");
        Assert.Equal(HttpStatusCode.OK, pending.StatusCode);
        Assert.Contains(email, await pending.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        using var review = await client.PostAsJsonAsync($"/api/recovery/support/{requestId}/review", new
        {
            approve = true,
            note = "Identity checked by integration test"
        });
        Assert.True(review.StatusCode == HttpStatusCode.NoContent,
            $"Expected support review to complete; got {(int)review.StatusCode}: {await review.Content.ReadAsStringAsync()}");
        var resetEmail = Assert.Single(factory.Email.Messages.Skip(1));
        const string tokenMarker = "one-time password change token within 15 minutes: ";
        var tokenStart = resetEmail.TextBody.IndexOf(tokenMarker, StringComparison.Ordinal) + tokenMarker.Length;
        var resetToken = resetEmail.TextBody[tokenStart..].Split('.', 2)[0];
        Assert.True(resetToken.Length >= 32);

        using var passwordChange = await client.PostAsJsonAsync("/api/recovery/password-change", new
        {
            requestId,
            token = resetToken,
            newPassword = "ChangedPass!67890"
        });
        Assert.Equal(HttpStatusCode.OK, passwordChange.StatusCode);

        using var login = await client.PostAsJsonAsync("/api/identity/login", new
        {
            email,
            password = "ChangedPass!67890"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }
}
