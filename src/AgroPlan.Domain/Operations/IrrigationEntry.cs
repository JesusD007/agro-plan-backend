using System.Text.Json.Serialization;

namespace AgroPlan.Domain.Operations;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IrrigationMethod { Drip, Flood, Rain }

/// <summary>An immutable record of irrigation. Corrections are linked append-only entries.</summary>
public sealed class IrrigationEntry
{
    private IrrigationEntry() { }

    public IrrigationEntry(Guid id, Guid farmId, Guid parcelId, Guid? cropCycleId,
        IrrigationMethod method, string waterSource, DateTimeOffset occurredAt,
        string actorUserId, DateTimeOffset createdAt, Guid? correctsEntryId = null,
        string? correctionReason = null)
    {
        if (id == Guid.Empty || farmId == Guid.Empty || parcelId == Guid.Empty)
            throw new ArgumentException("Entry, farm and parcel ids are required.");
        if (!Enum.IsDefined(method)) throw new ArgumentException("Unsupported irrigation method.", nameof(method));
        if (occurredAt == default) throw new ArgumentException("Occurrence date is required.", nameof(occurredAt));
        if (string.IsNullOrWhiteSpace(waterSource) || waterSource.Trim().Length > 200)
            throw new ArgumentException("Water source is required and must not exceed 200 characters.", nameof(waterSource));
        if (string.IsNullOrWhiteSpace(actorUserId)) throw new ArgumentException("Actor is required.", nameof(actorUserId));
        if (correctsEntryId.HasValue && string.IsNullOrWhiteSpace(correctionReason))
            throw new ArgumentException("A correction reason is required.", nameof(correctionReason));

        Id = id; FarmId = farmId; ParcelId = parcelId; CropCycleId = cropCycleId;
        Method = method; WaterSource = waterSource.Trim(); OccurredAt = occurredAt;
        ActorUserId = actorUserId; CreatedAt = createdAt; CorrectsEntryId = correctsEntryId;
        CorrectionReason = string.IsNullOrWhiteSpace(correctionReason) ? null : correctionReason.Trim();
    }

    public Guid Id { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid ParcelId { get; private set; }
    public Guid? CropCycleId { get; private set; }
    public IrrigationMethod Method { get; private set; }
    public string WaterSource { get; private set; } = null!;
    public DateTimeOffset OccurredAt { get; private set; }
    public string ActorUserId { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CorrectsEntryId { get; private set; }
    public string? CorrectionReason { get; private set; }
}
