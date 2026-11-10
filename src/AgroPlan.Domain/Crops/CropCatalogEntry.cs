namespace AgroPlan.Domain.Crops;

public enum CropCatalogValidationStatus
{
    IdentityOnly = 0,
    Validated = 1
}

public sealed class CropCatalogEntry
{
    private CropCatalogEntry() { }

    public CropCatalogEntry(
        Guid id,
        string code,
        string displayName,
        string sourceName,
        CropCatalogValidationStatus validationStatus,
        string? region,
        DateTimeOffset? validFrom,
        DateTimeOffset? validTo)
    {
        if (id == Guid.Empty) throw new ArgumentException("Catalog id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Crop code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Crop name is required.", nameof(displayName));
        if (string.IsNullOrWhiteSpace(sourceName)) throw new ArgumentException("Source name is required.", nameof(sourceName));
        if (validFrom.HasValue && validTo.HasValue && validTo < validFrom)
            throw new ArgumentException("Validity end must not precede validity start.", nameof(validTo));

        Id = id;
        Code = code.Trim().ToUpperInvariant();
        DisplayName = displayName.Trim();
        SourceName = sourceName.Trim();
        ValidationStatus = validationStatus;
        Region = string.IsNullOrWhiteSpace(region) ? null : region.Trim();
        ValidFrom = validFrom;
        ValidTo = validTo;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string SourceName { get; private set; } = string.Empty;
    public CropCatalogValidationStatus ValidationStatus { get; private set; }
    public string? Region { get; private set; }
    public DateTimeOffset? ValidFrom { get; private set; }
    public DateTimeOffset? ValidTo { get; private set; }
    public ICollection<AgronomicParameter> Parameters { get; private set; } = new List<AgronomicParameter>();
}
