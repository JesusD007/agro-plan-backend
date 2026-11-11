using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
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

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        Assert.True(document.RootElement.TryGetProperty("photoId", out _));
        Assert.True(document.RootElement.TryGetProperty("uploadUrl", out _));
        Assert.False(document.RootElement.TryGetProperty("publicUrl", out _));
    }
}
