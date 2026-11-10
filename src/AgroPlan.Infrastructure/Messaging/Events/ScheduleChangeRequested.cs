namespace AgroPlan.Infrastructure.Messaging.Events;

public sealed record ScheduleChangeRequested(Guid ProposalId, int Version);
