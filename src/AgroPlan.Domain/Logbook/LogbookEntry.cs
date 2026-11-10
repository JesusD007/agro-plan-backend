using System.Text.Json.Serialization;

namespace AgroPlan.Domain.Logbook;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LogbookActivityType
{
    Sowing,
    Irrigation,
    Fumigation,
    Harvest
}

/// <summary>An append-only farm operation. Corrections are represented by a new linked row.</summary>
public sealed class LogbookEntry
{
    private LogbookEntry() { }

    public LogbookEntry(Guid id, Guid farmId, Guid parcelId, Guid? cropCycleId,
        LogbookActivityType activityType, DateTimeOffset occurredAt, string details,
        string actorUserId, DateTimeOffset createdAt, Guid? correctsEntryId = null,
        string? correctionReason = null)
    {
        if (id == Guid.Empty || farmId == Guid.Empty || parcelId == Guid.Empty)
            throw new ArgumentException("Entry, farm and parcel ids are required.");
        if (string.IsNullOrWhiteSpace(actorUserId)) throw new ArgumentException("Actor is required.", nameof(actorUserId));
        if (string.IsNullOrWhiteSpace(details) || details.Trim().Length > 4000)
            throw new ArgumentException("Details are required and must not exceed 4000 characters.", nameof(details));
        if (correctsEntryId.HasValue && string.IsNullOrWhiteSpace(correctionReason))
            throw new ArgumentException("A correction reason is required.", nameof(correctionReason));

        Id = id;
        FarmId = farmId;
        ParcelId = parcelId;
        CropCycleId = cropCycleId;
        ActivityType = activityType;
        OccurredAt = occurredAt;
        Details = details.Trim();
        ActorUserId = actorUserId;
        CreatedAt = createdAt;
        CorrectsEntryId = correctsEntryId;
        CorrectionReason = string.IsNullOrWhiteSpace(correctionReason) ? null : correctionReason.Trim();
    }

    public Guid Id { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid ParcelId { get; private set; }
    public Guid? CropCycleId { get; private set; }
    public LogbookActivityType ActivityType { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string Details { get; private set; } = null!;
    public string ActorUserId { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CorrectsEntryId { get; private set; }
    public string? CorrectionReason { get; private set; }
}
