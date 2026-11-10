using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgroPlan.Domain.Operations;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgroPlan.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class InputApplicationsTests(PostgreSqlFixture postgres)
{
    [Fact]
    public async Task Append_returns_unavailable_without_exact_validated_frequency_rule_and_correction_preserves_history()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var owner = $"owner-{Guid.NewGuid():N}";
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        var (farmId, parcelId) = await TestData.SeedFarmAsync(db, owner);
        TestData.SetBearer(client, owner);
        var appliedAt = DateTimeOffset.UtcNow.AddDays(-1);
        using var append = await client.PostAsJsonAsync($"/api/farms/{farmId}/input-applications", new
        {
            parcelId, productRegistrationId = "REG-42", formulationId = "FORM-EC", cropId = "rice",
            intendedUseId = "fungicide-use", appliedAt, method = InputApplicationMethod.Drone,
            quantity = 2.5m, unit = "L/ha", phiDays = 0
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, append.StatusCode);
        using var json = JsonDocument.Parse(await append.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("unavailable", json.RootElement.GetProperty("frequencyStatus").GetString());
        Assert.NotNull(json.RootElement.GetProperty("frequencyMissingEvidenceReason").GetString());
        Assert.False(json.RootElement.TryGetProperty("phiDays", out _));
        var originalId = json.RootElement.GetProperty("id").GetGuid();
        using var correction = await client.PostAsJsonAsync($"/api/farms/{farmId}/input-applications/{originalId}/corrections", new
        {
            reason = "Corrected amount", productRegistrationId = "REG-42", formulationId = "FORM-EC",
            cropId = "rice", intendedUseId = "fungicide-use", appliedAt,
            method = InputApplicationMethod.Drone, quantity = 3m, unit = "L/ha"
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, correction.StatusCode);
        using var correctionJson = JsonDocument.Parse(await correction.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(originalId, correctionJson.RootElement.GetProperty("correctsEntryId").GetGuid());
        using var crossFarm = await client.GetAsync($"/api/farms/{Guid.NewGuid()}/input-applications", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, crossFarm.StatusCode);

        var rows = await db.InputApplicationEntries.Where(x => x.FarmId == farmId).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, rows.Count);
        Assert.Equal("L/ha", Assert.Single(rows, x => x.Id == originalId).Unit);
        var original = await db.InputApplicationEntries.SingleAsync(x => x.Id == originalId, TestContext.Current.CancellationToken);
        db.InputApplicationEntries.Remove(original);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Exact_validated_frequency_rule_returns_sourced_assessment()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var owner = $"owner-{Guid.NewGuid():N}";
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        var (farmId, parcelId) = await TestData.SeedFarmAsync(db, owner);
        db.ValidatedApplicationFrequencyRules.Add(new(Guid.NewGuid(), "REG-42", "FORM-EC", "rice", "fungicide-use",
            "DO", "MIDA label registry", "rev-2026-1", DateTimeOffset.UtcNow.AddDays(-10),
            DateTimeOffset.UtcNow.AddDays(10), 30, 3, 2, true));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        TestData.SetBearer(client, owner);
        using var response = await client.PostAsJsonAsync($"/api/farms/{farmId}/input-applications", new
        {
            parcelId, productRegistrationId = "REG-42", formulationId = "FORM-EC", cropId = "rice",
            intendedUseId = "fungicide-use", appliedAt = DateTimeOffset.UtcNow,
            method = InputApplicationMethod.Manual, quantity = 1m, unit = "L"
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("compliant", json.RootElement.GetProperty("frequencyStatus").GetString());
        Assert.Equal("MIDA label registry", json.RootElement.GetProperty("frequencyRuleSource").GetString());
        Assert.Equal("rev-2026-1", json.RootElement.GetProperty("frequencyRuleVersion").GetString());
    }
}
