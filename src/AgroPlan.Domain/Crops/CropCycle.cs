namespace AgroPlan.Domain.Crops;

public sealed class CropCycle
{
    private CropCycle() { }

    public CropCycle(
        Guid id,
        Guid farmId,
        Guid parcelId,
        CropCatalogEntry cropCatalogEntry,
        DateOnly? desiredPlantingDate,
        string? variety,
        string? notes,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty) throw new ArgumentException("Crop cycle id is required.", nameof(id));
        if (farmId == Guid.Empty) throw new ArgumentException("Farm id is required.", nameof(farmId));
        if (parcelId == Guid.Empty) throw new ArgumentException("Parcel id is required.", nameof(parcelId));
        ArgumentNullException.ThrowIfNull(cropCatalogEntry);

        Id = id;
        FarmId = farmId;
        ParcelId = parcelId;
        CropCatalogEntryId = cropCatalogEntry.Id;
        CropCatalogEntry = cropCatalogEntry;
        DesiredPlantingDate = desiredPlantingDate;
        Variety = Normalize(variety);
        Notes = Normalize(notes);
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid ParcelId { get; private set; }
    public Guid CropCatalogEntryId { get; private set; }
    public DateOnly? DesiredPlantingDate { get; private set; }
    public string? Variety { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public CropCatalogEntry CropCatalogEntry { get; private set; } = null!;

    public void Update(
        Guid parcelId,
        CropCatalogEntry cropCatalogEntry,
        DateOnly? desiredPlantingDate,
        string? variety,
        string? notes,
        DateTimeOffset updatedAt)
    {
        if (parcelId == Guid.Empty) throw new ArgumentException("Parcel id is required.", nameof(parcelId));
        ArgumentNullException.ThrowIfNull(cropCatalogEntry);

        ParcelId = parcelId;
        CropCatalogEntryId = cropCatalogEntry.Id;
        CropCatalogEntry = cropCatalogEntry;
        DesiredPlantingDate = desiredPlantingDate;
        Variety = Normalize(variety);
        Notes = Normalize(notes);
        UpdatedAt = updatedAt;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
