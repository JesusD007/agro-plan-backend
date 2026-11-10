namespace AgroPlan.Domain.Crops;

public enum AgronomicParameterValidationStatus
{
    Unvalidated = 0,
    Validated = 1
}

public sealed class AgronomicParameter
{
    private AgronomicParameter() { }

    public AgronomicParameter(
        Guid id,
        Guid cropCatalogEntryId,
        string key,
        decimal? numericValue,
        string? unit,
        AgronomicParameterValidationStatus validationStatus,
        string? sourceName,
        string? sourceUrl,
        string? region,
        DateTimeOffset? validFrom,
        DateTimeOffset? validTo)
    {
        if (id == Guid.Empty) throw new ArgumentException("Parameter id is required.", nameof(id));
        if (cropCatalogEntryId == Guid.Empty) throw new ArgumentException("Crop catalog id is required.", nameof(cropCatalogEntryId));
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Parameter key is required.", nameof(key));
        if (validFrom.HasValue && validTo.HasValue && validTo < validFrom)
            throw new ArgumentException("Validity end must not precede validity start.", nameof(validTo));

        if (validationStatus == AgronomicParameterValidationStatus.Validated &&
            (!numericValue.HasValue || string.IsNullOrWhiteSpace(unit) || string.IsNullOrWhiteSpace(sourceName)))
            throw new ArgumentException("Validated numeric parameters require a value, unit, and source.");

        Id = id;
        CropCatalogEntryId = cropCatalogEntryId;
        Key = key.Trim();
        NumericValue = numericValue;
        Unit = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim();
        ValidationStatus = validationStatus;
        SourceName = string.IsNullOrWhiteSpace(sourceName) ? null : sourceName.Trim();
        SourceUrl = string.IsNullOrWhiteSpace(sourceUrl) ? null : sourceUrl.Trim();
        Region = string.IsNullOrWhiteSpace(region) ? null : region.Trim();
        ValidFrom = validFrom;
        ValidTo = validTo;
    }

    public Guid Id { get; private set; }
    public Guid CropCatalogEntryId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public decimal? NumericValue { get; private set; }
    public string? Unit { get; private set; }
    public AgronomicParameterValidationStatus ValidationStatus { get; private set; }
    public string? SourceName { get; private set; }
    public string? SourceUrl { get; private set; }
    public string? Region { get; private set; }
    public DateTimeOffset? ValidFrom { get; private set; }
    public DateTimeOffset? ValidTo { get; private set; }
    public CropCatalogEntry CropCatalogEntry { get; private set; } = null!;

    public bool IsUsableAt(DateTimeOffset at) =>
        ValidationStatus == AgronomicParameterValidationStatus.Validated &&
        NumericValue.HasValue &&
        !string.IsNullOrWhiteSpace(Unit) &&
        !string.IsNullOrWhiteSpace(SourceName) &&
        (!ValidFrom.HasValue || ValidFrom.Value <= at) &&
        (!ValidTo.HasValue || ValidTo.Value >= at);
}
