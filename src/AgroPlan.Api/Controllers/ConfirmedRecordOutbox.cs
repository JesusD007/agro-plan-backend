using System.Text.Json;
using AgroPlan.Domain.Planning;
using AgroPlan.Infrastructure.Messaging.Events;
using AgroPlan.Infrastructure.Persistence;

namespace AgroPlan.Api.Controllers;

internal static class ConfirmedRecordOutbox
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void Add(AgroPlanDbContext db, Guid farmId, Guid parcelId, Guid? cropCycleId,
        string operationKind, Guid recordId, DateTimeOffset occurredAt, int sourceRecordVersion = 1)
    {
        var eventId = Guid.NewGuid();
        var message = new FarmRecordConfirmed(eventId, farmId, parcelId, cropCycleId, operationKind,
            recordId, sourceRecordVersion, DateOnly.FromDateTime(occurredAt.UtcDateTime));
        db.OutboxMessages.Add(new OutboxMessage(eventId, recordId, "farm-record.confirmed",
            JsonSerializer.Serialize(message, JsonOptions), DateTimeOffset.UtcNow));
    }
}
