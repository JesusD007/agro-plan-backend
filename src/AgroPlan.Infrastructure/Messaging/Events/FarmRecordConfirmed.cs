namespace AgroPlan.Infrastructure.Messaging.Events;

/// <summary>Minimal immutable source reference for projecting a confirmed farm operation onto workforce tasks.</summary>
public sealed record FarmRecordConfirmed(Guid EventId, Guid FarmId, Guid ParcelId, Guid? CropCycleId,
    string OperationKind, Guid SourceRecordId, int SourceRecordVersion, DateOnly OccurredOn);
