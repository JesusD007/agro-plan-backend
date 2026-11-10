namespace AgroPlan.Domain.Operations;

public enum InputApplicationMethod { Manual, Tractor, Drone, Irrigation }
public enum ApplicationFrequencyStatus { Compliant, Approaching, Exceeded, Unavailable }

/// <summary>Immutable application evidence. Client-supplied PHI is deliberately not stored as authority.</summary>
public sealed class InputApplicationEntry
{
    private InputApplicationEntry() { }

    public InputApplicationEntry(Guid id, Guid farmId, Guid parcelId, Guid? cropCycleId,
        string productRegistrationId, string formulationId, string cropId, string intendedUseId,
        DateTimeOffset appliedAt, InputApplicationMethod method, decimal quantity, string unit,
        string actorUserId, DateTimeOffset createdAt, Guid? correctsEntryId = null, string? correctionReason = null,
        string? citedLabelSource = null, string? citedLabelVersion = null)
    {
        if (id == Guid.Empty || farmId == Guid.Empty || parcelId == Guid.Empty)
            throw new ArgumentException("Entry, farm and parcel ids are required.");
        if (string.IsNullOrWhiteSpace(productRegistrationId) || string.IsNullOrWhiteSpace(formulationId) ||
            string.IsNullOrWhiteSpace(cropId) || string.IsNullOrWhiteSpace(intendedUseId))
            throw new ArgumentException("Registered product, formulation, crop and intended use are required.");
        if (!Enum.IsDefined(method)) throw new ArgumentException("Unsupported application method.", nameof(method));
        if (appliedAt == default) throw new ArgumentException("Application date is required.", nameof(appliedAt));
        if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity cannot be negative.");
        if (string.IsNullOrWhiteSpace(unit)) throw new ArgumentException("Original unit is required.", nameof(unit));
        if (unit.Trim().Length > 64) throw new ArgumentException("Unit must not exceed 64 characters.", nameof(unit));
        if (string.IsNullOrWhiteSpace(actorUserId)) throw new ArgumentException("Actor is required.", nameof(actorUserId));
        if (correctsEntryId.HasValue && string.IsNullOrWhiteSpace(correctionReason))
            throw new ArgumentException("A correction reason is required.", nameof(correctionReason));
        if (citedLabelSource?.Trim().Length > 500 || citedLabelVersion?.Trim().Length > 200)
            throw new ArgumentException("Cited label source/version exceeds its maximum length.");
        Id = id; FarmId = farmId; ParcelId = parcelId; CropCycleId = cropCycleId;
        ProductRegistrationId = productRegistrationId.Trim(); FormulationId = formulationId.Trim();
        CropId = cropId.Trim(); IntendedUseId = intendedUseId.Trim(); AppliedAt = appliedAt; Method = method;
        Quantity = quantity; Unit = unit.Trim(); ActorUserId = actorUserId; CreatedAt = createdAt;
        CorrectsEntryId = correctsEntryId; CorrectionReason = string.IsNullOrWhiteSpace(correctionReason) ? null : correctionReason.Trim();
        CitedLabelSource = string.IsNullOrWhiteSpace(citedLabelSource) ? null : citedLabelSource.Trim();
        CitedLabelVersion = string.IsNullOrWhiteSpace(citedLabelVersion) ? null : citedLabelVersion.Trim();
    }

    public Guid Id { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid ParcelId { get; private set; }
    public Guid? CropCycleId { get; private set; }
    public string ProductRegistrationId { get; private set; } = null!;
    public string FormulationId { get; private set; } = null!;
    public string CropId { get; private set; } = null!;
    public string IntendedUseId { get; private set; } = null!;
    public DateTimeOffset AppliedAt { get; private set; }
    public InputApplicationMethod Method { get; private set; }
    public decimal Quantity { get; private set; }
    public string Unit { get; private set; } = null!;
    public string ActorUserId { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CorrectsEntryId { get; private set; }
    public string? CorrectionReason { get; private set; }
    public string? CitedLabelSource { get; private set; }
    public string? CitedLabelVersion { get; private set; }
}

/// <summary>Validated, versioned rule; only exact and currently effective matches may drive frequency status.</summary>
public sealed class ValidatedApplicationFrequencyRule
{
    private ValidatedApplicationFrequencyRule() { }

    public ValidatedApplicationFrequencyRule(Guid id, string productRegistrationId, string formulationId,
        string cropId, string intendedUseId, string jurisdiction, string source, string sourceVersion,
        DateTimeOffset validFrom, DateTimeOffset? validUntil, int periodDays, int maximumApplications,
        int? approachingAt, bool validated)
    {
        if (id == Guid.Empty) throw new ArgumentException("Rule id is required.", nameof(id));
        var required = new[] { productRegistrationId, formulationId, cropId, intendedUseId, jurisdiction, source, sourceVersion };
        if (required.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Rule keys and provenance are required.");
        if (periodDays <= 0 || maximumApplications <= 0 || (approachingAt.HasValue && (approachingAt <= 0 || approachingAt >= maximumApplications)))
            throw new ArgumentOutOfRangeException(nameof(periodDays), "Frequency limits must be positive and internally consistent.");
        Id = id; ProductRegistrationId = productRegistrationId.Trim(); FormulationId = formulationId.Trim();
        CropId = cropId.Trim(); IntendedUseId = intendedUseId.Trim(); Jurisdiction = jurisdiction.Trim().ToUpperInvariant();
        Source = source.Trim(); SourceVersion = sourceVersion.Trim(); ValidFrom = validFrom; ValidUntil = validUntil;
        PeriodDays = periodDays; MaximumApplications = maximumApplications; ApproachingAt = approachingAt; Validated = validated;
    }

    public Guid Id { get; private set; }
    public string ProductRegistrationId { get; private set; } = null!;
    public string FormulationId { get; private set; } = null!;
    public string CropId { get; private set; } = null!;
    public string IntendedUseId { get; private set; } = null!;
    public string Jurisdiction { get; private set; } = null!;
    public string Source { get; private set; } = null!;
    public string SourceVersion { get; private set; } = null!;
    public DateTimeOffset ValidFrom { get; private set; }
    public DateTimeOffset? ValidUntil { get; private set; }
    public int PeriodDays { get; private set; }
    public int MaximumApplications { get; private set; }
    public int? ApproachingAt { get; private set; }
    public bool Validated { get; private set; }
}

public sealed record ApplicationFrequencyAssessment(ApplicationFrequencyStatus Status, int? ApplicationsInPeriod,
    string? Source, string? SourceVersion, DateTimeOffset? RuleValidUntil, string? MissingEvidenceReason);

public static class ApplicationFrequencyEvaluator
{
    public static ApplicationFrequencyAssessment Assess(InputApplicationEntry candidate,
        IReadOnlyCollection<InputApplicationEntry> existingEntries,
        IReadOnlyCollection<ValidatedApplicationFrequencyRule> rules, DateTimeOffset asOf, string jurisdiction = "DO")
    {
        var matching = rules.Where(rule => KeysMatch(candidate, rule)).ToArray();
        var validRules = matching.Where(item => item.Validated && item.ValidFrom <= asOf &&
            (!item.ValidUntil.HasValue || item.ValidUntil.Value >= asOf) &&
            string.Equals(item.Jurisdiction, jurisdiction, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (validRules.Length != 1)
        {
            var reason = validRules.Length > 1 ? "Multiple active rules match this exact product/crop/use and jurisdiction." :
                matching.Length == 0 ? "No exact validated product/crop/use frequency rule is available." :
                matching.Any(x => x.Validated) ? "The exact frequency rule is stale, outside its effective dates, or for another jurisdiction." :
                "The matching frequency rule has not been validated.";
            return new(ApplicationFrequencyStatus.Unavailable, null, null, null, null, reason);
        }
        var rule = validRules[0];
        var start = candidate.AppliedAt.AddDays(-rule.PeriodDays);
        var rowsAfterCandidate = existingEntries.Append(candidate).ToArray();
        var supersededIds = rowsAfterCandidate.Where(item => item.CorrectsEntryId.HasValue)
            .Select(item => item.CorrectsEntryId!.Value).ToHashSet();
        var count = rowsAfterCandidate.Count(item => !supersededIds.Contains(item.Id) && item.FarmId == candidate.FarmId &&
            KeysMatch(item, rule) && item.AppliedAt > start && item.AppliedAt <= candidate.AppliedAt);
        var status = count >= rule.MaximumApplications ? ApplicationFrequencyStatus.Exceeded :
            rule.ApproachingAt.HasValue && count >= rule.ApproachingAt.Value ? ApplicationFrequencyStatus.Approaching :
            ApplicationFrequencyStatus.Compliant;
        return new(status, count, rule.Source, rule.SourceVersion, rule.ValidUntil, null);
    }

    private static bool KeysMatch(InputApplicationEntry entry, ValidatedApplicationFrequencyRule rule) =>
        string.Equals(entry.ProductRegistrationId, rule.ProductRegistrationId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(entry.FormulationId, rule.FormulationId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(entry.CropId, rule.CropId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(entry.IntendedUseId, rule.IntendedUseId, StringComparison.OrdinalIgnoreCase);
}
