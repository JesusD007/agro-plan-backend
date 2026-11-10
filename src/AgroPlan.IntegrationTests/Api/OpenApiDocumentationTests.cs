using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AgroPlan.IntegrationTests.Api;

public sealed class OpenApiDocumentationTests
{
    private const string UnusedConnectionString = "Host=127.0.0.1;Port=5432;Database=not_used;Username=test;Password=test";

    [Fact]
    public async Task Development_exposes_generated_openapi_and_interactive_scalar_reference()
    {
        await using var factory = new ApiTestFactory(UnusedConnectionString, disableBackgroundServices: true);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.Timeout = TimeSpan.FromSeconds(10);

        using var documentResponse = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, documentResponse.StatusCode);
        using var document = JsonDocument.Parse(await documentResponse.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.StartsWith("3.1.", root.GetProperty("openapi").GetString());
        var paths = root.GetProperty("paths");
        Assert.Contains(paths.EnumerateObject(), item => item.Name.Contains("/api/farms", StringComparison.Ordinal));
        Assert.Contains(paths.EnumerateObject(), item => item.Name.Contains("/api/identity/register", StringComparison.Ordinal));
        Assert.True(root.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _));

        using var scalarResponse = await client.GetAsync("/scalar");
        Assert.Equal(HttpStatusCode.OK, scalarResponse.StatusCode);
        Assert.Contains("Scalar", await scalarResponse.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Production_does_not_map_api_documentation_routes()
    {
        await using var factory = new ApiTestFactory(UnusedConnectionString, "Production", disableBackgroundServices: true);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.Timeout = TimeSpan.FromSeconds(10);

        using var scalarResponse = await client.GetAsync("/scalar");
        using var documentResponse = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.NotFound, scalarResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, documentResponse.StatusCode);
    }
}
