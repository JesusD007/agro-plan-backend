using AgroPlan.Domain.Operations;
using AgroPlan.Domain.Safety;

namespace AgroPlan.Application.Safety;

public enum HarvestEligibilityStatus { Eligible, Blocked }
public sealed record HarvestEvidence(Guid ApplicationId, Guid LabelId, string Source, string SourceVersion,
    int PhiDays, DateTimeOffset EarliestSafeAt);
public sealed record HarvestEligibilityAssessment(HarvestEligibilityStatus Status, DateTimeOffset? EarliestSafeAt,
    IReadOnlyList<HarvestEvidence> Evidence, IReadOnlyList<string> MissingEvidenceReasons)
{
    public bool IsEligible => Status == HarvestEligibilityStatus.Eligible;
}

/// <summary>Deterministic fail-closed harvest safety check. MRL and re-entry intervals are not PHI.</summary>
public static class HarvestEligibilityEvaluator
{
    public static HarvestEligibilityAssessment Assess(Guid cropCycleId,
        IReadOnlyCollection<InputApplicationEntry> applications,
        IReadOnlyCollection<ValidatedProductLabel> labels,
        DateTimeOffset harvestedAt, string jurisdiction = "DO", string? expectedCropId = null)
    {
        var cycleApplications = applications.Where(x => x.CropCycleId == cropCycleId).ToArray();
        var superseded = cycleApplications.Where(x => x.CorrectsEntryId.HasValue).Select(x => x.CorrectsEntryId!.Value).ToHashSet();
        var active = cycleApplications.Where(x => !superseded.Contains(x.Id)).ToArray();
        var evidence = new List<HarvestEvidence>();
        var missing = new List<string>();
        foreach (var application in active)
        {
            if (!string.IsNullOrWhiteSpace(expectedCropId) &&
                !string.Equals(application.CropId, expectedCropId, StringComparison.OrdinalIgnoreCase))
            {
                missing.Add($"Application {application.Id}: crop identity does not match the crop cycle.");
                continue;
            }
            var exact = labels.Where(label => KeysMatch(application, label) &&
                string.Equals(label.Jurisdiction, jurisdiction, StringComparison.OrdinalIgnoreCase)).ToArray();
            var valid = exact.Where(label => label.Validated && label.PreHarvestIntervalDays.HasValue &&
                label.EffectiveFrom <= application.AppliedAt &&
                (!label.EffectiveUntil.HasValue || label.EffectiveUntil.Value >= harvestedAt)).ToArray();
            if (valid.Length != 1)
            {
                var reason = valid.Length > 1 ? "Ambiguous exact validated label evidence" : exact.Length == 0 ?
                    "No exact product/formulation/crop/use label evidence" : exact.Any(x => !x.Validated) ?
                    "Label evidence is not validated" : exact.Any(x => !x.PreHarvestIntervalDays.HasValue) ?
                    "Pre-harvest interval is missing; MRL and worker re-entry intervals cannot replace it" :
                    "Label evidence is outside its effective dates";
                missing.Add($"Application {application.Id}: {reason}.");
                continue;
            }

            var label = valid[0];
            var phiDays = label.PreHarvestIntervalDays!.Value;
            evidence.Add(new(application.Id, label.Id, label.Source, label.SourceVersion, phiDays,
                application.AppliedAt.AddDays(phiDays)));
        }

        var earliestSafe = evidence.Count == 0 ? (DateTimeOffset?)null : evidence.Max(x => x.EarliestSafeAt);
        if (missing.Count > 0)
            return new(HarvestEligibilityStatus.Blocked, earliestSafe, evidence, missing);
        if (earliestSafe.HasValue && harvestedAt < earliestSafe.Value)
            missing.Add($"Harvest date is before the latest earliest-safe date ({earliestSafe.Value:O}).");
        return new(missing.Count == 0 ? HarvestEligibilityStatus.Eligible : HarvestEligibilityStatus.Blocked,
            earliestSafe, evidence, missing);
    }

    private static bool KeysMatch(InputApplicationEntry application, ValidatedProductLabel label) =>
        string.Equals(application.ProductRegistrationId, label.ProductRegistrationId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(application.FormulationId, label.FormulationId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(application.CropId, label.CropId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(application.IntendedUseId, label.IntendedUseId, StringComparison.OrdinalIgnoreCase);
}
