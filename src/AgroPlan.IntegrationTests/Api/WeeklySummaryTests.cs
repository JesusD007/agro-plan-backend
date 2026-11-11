using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgroPlan.Domain.Workforce;
using AgroPlan.Infrastructure.Persistence;
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
    }
}
