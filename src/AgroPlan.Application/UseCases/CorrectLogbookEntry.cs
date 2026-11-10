using AgroPlan.Domain.Logbook;

namespace AgroPlan.Application.UseCases;

public sealed class CorrectLogbookEntry
{
    public LogbookEntry Create(LogbookEntry original, string reason, DateTimeOffset occurredAt,
        string details, string actorId, DateTimeOffset now) =>
        new(Guid.NewGuid(), original.FarmId, original.ParcelId, original.CropCycleId,
            original.ActivityType, occurredAt, details, actorId, now, original.Id, reason);
}
