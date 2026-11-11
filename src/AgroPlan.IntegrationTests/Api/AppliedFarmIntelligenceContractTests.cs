using System.Net;
using System.Text.Json;
using Xunit;

namespace AgroPlan.IntegrationTests.Api;

public sealed class AppliedFarmIntelligenceContractTests
{
    private const string UnusedConnectionString = "Host=127.0.0.1;Port=5432;Database=not_used;Username=test;Password=test";

    [Fact]
    public async Task Development_openapi_contains_all_phase_four_routes_and_scalar_loads()
    {
        await using var factory = new ApiTestFactory(UnusedConnectionString, "Development", disableBackgroundServices: true);
        using var client = factory.CreateClient();

        using var documentResponse = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, documentResponse.StatusCode);
        using var document = JsonDocument.Parse(await documentResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var paths = document.RootElement.GetProperty("paths");

        string[] requiredPaths =
        [
            "/api/farms/{farmId}/voice-drafts/upload-authorizations",
            "/api/farms/{farmId}/voice-drafts",
            "/api/farms/{farmId}/voice-drafts/{draftId}/confirm",
            "/api/farms/{farmId}/weekly-summaries",
            "/api/farms/{farmId}/weekly-summaries/{summaryId}/share",
            "/api/farms/{farmId}/weekly-summaries/{summaryId}/sources/{sourceId}",
            "/api/farms/{farmId}/symptom-reports/upload-authorizations",
            "/api/farms/{farmId}/symptom-reports",
            "/api/farms/{farmId}/symptom-reports/{reportId}/photos/{photoId}",
            "/api/farms/{farmId}/symptom-reports/{reportId}/review",
            "/api/farms/{farmId}/symptom-reports/agronomist-authorizations"
        ];

        foreach (var path in requiredPaths)
            Assert.True(paths.TryGetProperty(path, out _), $"Missing Phase 4 API path: {path}");

        using var scalarResponse = await client.GetAsync("/scalar/", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, scalarResponse.StatusCode);
    }

    [Fact]
    public async Task Production_hides_phase_four_openapi_and_scalar_contracts()
    {
        await using var factory = new ApiTestFactory(UnusedConnectionString, "Production", disableBackgroundServices: true);
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var openApi = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        using var scalar = await client.GetAsync("/scalar/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, openApi.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, scalar.StatusCode);
    }

    [Fact]
    public async Task Phase_four_media_and_summary_rate_limits_return_429_without_authorization_or_queueing()
    {
        await using var factory = new ApiTestFactory(UnusedConnectionString, "Development", disableBackgroundServices: true);
        using var client = factory.CreateClient();
        var farmId = Guid.NewGuid();
        var otherFarmId = Guid.NewGuid();

        var media = await SendRepeatedlyAsync(client, HttpMethod.Post,
            $"/api/farms/{farmId}/voice-drafts/upload-authorizations", 11);
        Assert.Equal(Enumerable.Repeat(HttpStatusCode.Unauthorized, 10), media.Take(10));
        Assert.Equal(HttpStatusCode.TooManyRequests, media[10]);

        using var separateFarmRequest = await client.PostAsync(
            $"/api/farms/{otherFarmId}/voice-drafts/upload-authorizations", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, separateFarmRequest.StatusCode);

        var summaries = await SendRepeatedlyAsync(client, HttpMethod.Post,
            $"/api/farms/{farmId}/weekly-summaries", 21);
        Assert.All(summaries.Take(20), status => Assert.Equal(HttpStatusCode.Unauthorized, status));
        Assert.Equal(HttpStatusCode.TooManyRequests, summaries[20]);
    }

    private static async Task<HttpStatusCode[]> SendRepeatedlyAsync(HttpClient client, HttpMethod method, string path, int count)
    {
        var statuses = new HttpStatusCode[count];
        for (var index = 0; index < count; index++)
        {
            using var request = new HttpRequestMessage(method, path);
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            statuses[index] = response.StatusCode;
        }
        return statuses;
    }
}
