namespace AgroPlan.Infrastructure.Messaging.Events;

public sealed record TaskCalendarSyncRequested(Guid FarmId, Guid TaskId, Guid RevisionId, int TaskVersion,
    DateOnly DueDate, DateTimeOffset RequestedAt);
