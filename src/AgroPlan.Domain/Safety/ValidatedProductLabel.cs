namespace AgroPlan.Domain.Safety;

/// <summary>Verified, jurisdiction-specific label facts. PHI, MRL and worker re-entry are distinct values.</summary>
public sealed class ValidatedProductLabel
{
    private ValidatedProductLabel() { }

    public ValidatedProductLabel(Guid id, string productRegistrationId, string formulationId, string cropId,
        string intendedUseId, string jurisdiction, string source, string sourceVersion,
        DateTimeOffset effectiveFrom, DateTimeOffset? effectiveUntil, int? preHarvestIntervalDays,
        decimal? maximumResidueLimit, int? workerReEntryIntervalHours, bool validated)
    {
        if (id == Guid.Empty) throw new ArgumentException("Label id is required.", nameof(id));
        var required = new[] { productRegistrationId, formulationId, cropId, intendedUseId, jurisdiction, source, sourceVersion };
        if (required.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Exact label identity and provenance are required.");
        if (preHarvestIntervalDays < 0) throw new ArgumentOutOfRangeException(nameof(preHarvestIntervalDays));
        if (maximumResidueLimit < 0) throw new ArgumentOutOfRangeException(nameof(maximumResidueLimit));
        if (workerReEntryIntervalHours < 0) throw new ArgumentOutOfRangeException(nameof(workerReEntryIntervalHours));
        if (effectiveFrom == default || (effectiveUntil.HasValue && effectiveUntil < effectiveFrom))
            throw new ArgumentException("Label effective period is invalid.");
        Id = id; ProductRegistrationId = productRegistrationId.Trim(); FormulationId = formulationId.Trim();
        CropId = cropId.Trim(); IntendedUseId = intendedUseId.Trim(); Jurisdiction = jurisdiction.Trim().ToUpperInvariant();
        Source = source.Trim(); SourceVersion = sourceVersion.Trim(); EffectiveFrom = effectiveFrom; EffectiveUntil = effectiveUntil;
        PreHarvestIntervalDays = preHarvestIntervalDays; MaximumResidueLimit = maximumResidueLimit;
        WorkerReEntryIntervalHours = workerReEntryIntervalHours; Validated = validated;
    }

    public Guid Id { get; private set; }
    public string ProductRegistrationId { get; private set; } = null!;
    public string FormulationId { get; private set; } = null!;
    public string CropId { get; private set; } = null!;
    public string IntendedUseId { get; private set; } = null!;
    public string Jurisdiction { get; private set; } = null!;
    public string Source { get; private set; } = null!;
    public string SourceVersion { get; private set; } = null!;
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveUntil { get; private set; }
    public int? PreHarvestIntervalDays { get; private set; }
    public decimal? MaximumResidueLimit { get; private set; }
    public int? WorkerReEntryIntervalHours { get; private set; }
    public bool Validated { get; private set; }
}
