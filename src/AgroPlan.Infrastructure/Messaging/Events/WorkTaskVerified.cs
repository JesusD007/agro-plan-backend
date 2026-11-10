namespace AgroPlan.Infrastructure.Messaging.Events;

public sealed record WorkTaskVerified(Guid FarmId, Guid TaskId, string WorkerUserId,
    decimal? WageAmount, string? WageUnit, DateTimeOffset VerifiedAt);
