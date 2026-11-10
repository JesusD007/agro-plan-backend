using AgroPlan.Domain.Logbook;

namespace AgroPlan.Application.UseCases;

public sealed class AppendLogbookEntry
{
    public LogbookEntry Create(Guid farmId, Guid parcelId, Guid? cycleId, LogbookActivityType type,
        DateTimeOffset occurredAt, string details, string actorId, DateTimeOffset now) =>
        new(Guid.NewGuid(), farmId, parcelId, cycleId, type, occurredAt, details, actorId, now);
}
