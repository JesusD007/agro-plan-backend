namespace AgroPlan.Domain.Crops;

/// <summary>A sourced, reviewed frame rule. The pilot has no validated rows yet.</summary>
public sealed class CropFramework
{
    private CropFramework() { }

    public CropFramework(Guid id, Guid cropCatalogEntryId, string name, decimal? plantSpacingMeters,
        decimal? rowSpacingMeters, decimal? aisleAllowanceRatio, string? sourceName, string? sourceUrl,
        AgronomicParameterValidationStatus validationStatus, string? region,
        DateTimeOffset? validFrom, DateTimeOffset? validTo)
    {
        if (id == Guid.Empty || cropCatalogEntryId == Guid.Empty) throw new ArgumentException("Framework and crop ids are required.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Framework name is required.", nameof(name));
        if (validFrom.HasValue && validTo.HasValue && validTo < validFrom) throw new ArgumentException("Invalid validity interval.");
        if (validationStatus == AgronomicParameterValidationStatus.Validated &&
            (!plantSpacingMeters.HasValue || !rowSpacingMeters.HasValue || !aisleAllowanceRatio.HasValue ||
             plantSpacingMeters.Value <= 0 || rowSpacingMeters.Value <= 0 || aisleAllowanceRatio.Value < 0 || aisleAllowanceRatio.Value >= 1 ||
             string.IsNullOrWhiteSpace(sourceName)))
            throw new ArgumentException("Validated frameworks require positive spacing, aisle allowance, and provenance.");

        Id = id; CropCatalogEntryId = cropCatalogEntryId; Name = name.Trim();
        PlantSpacingMeters = plantSpacingMeters; RowSpacingMeters = rowSpacingMeters;
        AisleAllowanceRatio = aisleAllowanceRatio; SourceName = sourceName; SourceUrl = sourceUrl;
        ValidationStatus = validationStatus; Region = region; ValidFrom = validFrom; ValidTo = validTo;
    }

    public Guid Id { get; private set; }
    public Guid CropCatalogEntryId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal? PlantSpacingMeters { get; private set; }
    public decimal? RowSpacingMeters { get; private set; }
    public decimal? AisleAllowanceRatio { get; private set; }
    public string? SourceName { get; private set; }
    public string? SourceUrl { get; private set; }
    public AgronomicParameterValidationStatus ValidationStatus { get; private set; }
    public string? Region { get; private set; }
    public DateTimeOffset? ValidFrom { get; private set; }
    public DateTimeOffset? ValidTo { get; private set; }
    public bool IsUsableAt(DateTimeOffset at) => ValidationStatus == AgronomicParameterValidationStatus.Validated &&
        PlantSpacingMeters.HasValue && PlantSpacingMeters.Value > 0 && RowSpacingMeters.HasValue && RowSpacingMeters.Value > 0 &&
        AisleAllowanceRatio.HasValue && AisleAllowanceRatio.Value >= 0 && AisleAllowanceRatio.Value < 1 &&
        !string.IsNullOrWhiteSpace(SourceName) && (!ValidFrom.HasValue || ValidFrom <= at) && (!ValidTo.HasValue || ValidTo >= at);
}
