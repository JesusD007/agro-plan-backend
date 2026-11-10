using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgroPlan.Domain.Logbook;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgroPlan.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class LogbookAndAttachmentsTests(PostgreSqlFixture postgres)
{
    [Fact]
    public async Task Corrections_append_history_and_database_rejects_mutating_logbook_rows()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var ownerId = $"owner-{Guid.NewGuid():N}";
        var (farmId, parcelId, originalId) = await SeedLogbookEntryAsync(factory, ownerId);
        TestData.SetBearer(client, ownerId);
        var occurredAt = DateTimeOffset.UtcNow.AddDays(-1);

        using var correctionResponse = await client.PostAsJsonAsync(
            $"/api/farms/{farmId}/logbook/{originalId}/corrections",
            new { reason = "Corrected irrigation quantity", occurredAt, details = "512 liters" });
        Assert.Equal(HttpStatusCode.Created, correctionResponse.StatusCode);
        using var correctionJson = JsonDocument.Parse(await correctionResponse.Content.ReadAsStringAsync());
        var correctionId = correctionJson.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(originalId, correctionJson.RootElement.GetProperty("correctsEntryId").GetGuid());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            var rows = await db.LogbookEntries.Where(item => item.FarmId == farmId).ToListAsync();
            Assert.Equal(2, rows.Count);
            Assert.Equal("100 liters", Assert.Single(rows, item => item.Id == originalId).Details);
            Assert.Equal("512 liters", Assert.Single(rows, item => item.Id == correctionId).Details);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            var original = await db.LogbookEntries.SingleAsync(item => item.Id == originalId);
            db.Entry(original).Property(item => item.Details).CurrentValue = "attempted mutation";
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            var original = await db.LogbookEntries.SingleAsync(item => item.Id == originalId);
            db.LogbookEntries.Remove(original);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            Assert.Equal(2, await db.LogbookEntries.CountAsync(item => item.FarmId == farmId));
            Assert.Equal("100 liters", (await db.LogbookEntries.SingleAsync(item => item.Id == originalId)).Details);
        }
        Assert.NotEqual(Guid.Empty, parcelId);
    }

    [Fact]
    public async Task Attachment_finalization_uses_immutable_key_and_requires_active_farm_membership()
    {
        postgres.EnsureAvailable();
        await using var factory = new ApiTestFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var ownerId = $"owner-{Guid.NewGuid():N}";
        var (farmId, _, entryId) = await SeedLogbookEntryAsync(factory, ownerId);
        TestData.SetBearer(client, ownerId);

        using var uploadResponse = await client.PostAsJsonAsync(
            $"/api/farms/{farmId}/logbook/{entryId}/attachments",
            new { contentType = "image/png", sizeBytes = 512L });
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        using var uploadJson = JsonDocument.Parse(await uploadResponse.Content.ReadAsStringAsync());
        var attachmentId = uploadJson.RootElement.GetProperty("attachmentId").GetGuid();
        Assert.StartsWith("https://uploads.invalid/", uploadJson.RootElement.GetProperty("signedUrl").GetString());

        using var finalizeResponse = await client.PostAsync(
            $"/api/farms/{farmId}/logbook/{entryId}/attachments/{attachmentId}/finalize", content: null);
        Assert.Equal(HttpStatusCode.OK, finalizeResponse.StatusCode);
        using var finalJson = JsonDocument.Parse(await finalizeResponse.Content.ReadAsStringAsync());
        var finalKey = finalJson.RootElement.GetProperty("objectKey").GetString();
        Assert.EndsWith("/final", finalKey);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
            var attachment = await db.LogbookAttachments.SingleAsync(item => item.Id == attachmentId);
            Assert.NotEqual(attachment.UploadObjectKey, attachment.ObjectKey);
            Assert.Equal(finalKey, attachment.ObjectKey);
            Assert.NotNull(attachment.FinalizedAt);
        }

        using var crossFarmResponse = await client.GetAsync(
            $"/api/farms/{Guid.NewGuid()}/logbook/{entryId}/attachments/{attachmentId}");
        Assert.Equal(HttpStatusCode.Forbidden, crossFarmResponse.StatusCode);
    }

    private static async Task<(Guid FarmId, Guid ParcelId, Guid EntryId)> SeedLogbookEntryAsync(
        ApiTestFactory factory,
        string ownerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        var (farmId, parcelId) = await TestData.SeedFarmAsync(db, ownerId);
        var entryId = Guid.NewGuid();
        db.LogbookEntries.Add(new LogbookEntry(entryId, farmId, parcelId, null,
            LogbookActivityType.Irrigation, DateTimeOffset.UtcNow.AddDays(-2), "100 liters", ownerId,
            DateTimeOffset.UtcNow.AddDays(-2)));
        await db.SaveChangesAsync();
        return (farmId, parcelId, entryId);
    }
}
