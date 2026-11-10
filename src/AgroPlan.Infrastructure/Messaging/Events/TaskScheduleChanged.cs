namespace AgroPlan.Infrastructure.Messaging.Events;

public sealed record TaskScheduleChanged(Guid FarmId, Guid TaskId, Guid RevisionId, string WorkerUserId,
    DateOnly DueDate, int TaskVersion, DateTimeOffset ChangedAt);
