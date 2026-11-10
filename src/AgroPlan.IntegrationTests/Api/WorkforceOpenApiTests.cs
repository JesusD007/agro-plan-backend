using System.Net;
using System.Text.Json;
using Xunit;

namespace AgroPlan.IntegrationTests.Api;

public sealed class WorkforceOpenApiTests
{
    private const string UnusedConnectionString = "Host=127.0.0.1;Port=5432;Database=not_used;Username=test;Password=test";

    [Fact]
    public async Task Development_documents_workforce_and_offline_endpoints_in_scalar_contract()
    {
        await using var factory = new ApiTestFactory(UnusedConnectionString, disableBackgroundServices: true);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var paths = document.RootElement.GetProperty("paths");
        foreach (var expected in new[]
                 {
                     "/api/farms/{farmId}/workers/invitations",
                     "/api/worker-invitations/accept",
                     "/api/farms/{farmId}/work-tasks",
                     "/api/farms/{farmId}/work-sync",
                     "/api/farms/{farmId}/work-routes/generate",
                     "/api/farms/{farmId}/task-schedule/tasks/{taskId}/revisions"
                 })
            Assert.True(paths.TryGetProperty(expected, out _), $"OpenAPI is missing {expected}.");

        using var scalar = await client.GetAsync("/scalar/", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, scalar.StatusCode);
        Assert.Contains("Scalar", await scalar.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Production_keeps_workforce_and_offline_docs_hidden()
    {
        await using var factory = new ApiTestFactory(UnusedConnectionString, "Production", disableBackgroundServices: true);
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var document = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        using var scalar = await client.GetAsync("/scalar/", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, document.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, scalar.StatusCode);
    }
}
