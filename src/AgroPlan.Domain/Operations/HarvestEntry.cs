namespace AgroPlan.Domain.Operations;

/// <summary>Immutable producer-confirmed harvest; corrections are appended and linked.</summary>
public sealed class HarvestEntry
{
    private HarvestEntry() { }

    public HarvestEntry(Guid id, Guid farmId, Guid parcelId, Guid cropCycleId, DateTimeOffset harvestedAt,
        decimal quantity, string unit, string actorUserId, DateTimeOffset createdAt,
        string assessmentEvidenceJson, Guid? correctsEntryId = null, string? correctionReason = null)
    {
        if (id == Guid.Empty || farmId == Guid.Empty || parcelId == Guid.Empty || cropCycleId == Guid.Empty)
            throw new ArgumentException("Entry, farm, parcel and crop cycle ids are required.");
        if (harvestedAt == default) throw new ArgumentException("Harvest date is required.", nameof(harvestedAt));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Harvest quantity must be positive.");
        if (string.IsNullOrWhiteSpace(unit) || unit.Trim().Length > 64) throw new ArgumentException("Original unit is required.", nameof(unit));
        if (string.IsNullOrWhiteSpace(actorUserId)) throw new ArgumentException("Actor is required.", nameof(actorUserId));
        if (string.IsNullOrWhiteSpace(assessmentEvidenceJson)) throw new ArgumentException("Safety assessment provenance is required.", nameof(assessmentEvidenceJson));
        if (correctsEntryId.HasValue && string.IsNullOrWhiteSpace(correctionReason)) throw new ArgumentException("Correction reason is required.", nameof(correctionReason));
        Id = id; FarmId = farmId; ParcelId = parcelId; CropCycleId = cropCycleId; HarvestedAt = harvestedAt;
        Quantity = quantity; Unit = unit.Trim(); ActorUserId = actorUserId; CreatedAt = createdAt;
        AssessmentEvidenceJson = assessmentEvidenceJson; CorrectsEntryId = correctsEntryId;
        CorrectionReason = string.IsNullOrWhiteSpace(correctionReason) ? null : correctionReason.Trim();
    }

    public Guid Id { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid ParcelId { get; private set; }
    public Guid CropCycleId { get; private set; }
    public DateTimeOffset HarvestedAt { get; private set; }
    public decimal Quantity { get; private set; }
    public string Unit { get; private set; } = null!;
    public string ActorUserId { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public string AssessmentEvidenceJson { get; private set; } = null!;
    public Guid? CorrectsEntryId { get; private set; }
    public string? CorrectionReason { get; private set; }
}
