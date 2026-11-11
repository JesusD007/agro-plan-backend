namespace AgroPlan.UnitTests.AI;

internal enum Phase04EvaluationCapability
{
    Voice,
    WeeklySummary,
    SymptomObservation
}

/// <summary>
/// Fictional metadata-only evaluation descriptor. It deliberately contains no media,
/// transcript, farm identity, provider output, or real customer data.
/// </summary>
internal sealed record Phase04EvaluationCase(
    string CaseId,
    Phase04EvaluationCapability Capability,
    string Language,
    string Scenario,
    IReadOnlySet<string> Coverage,
    IReadOnlyList<string> ExpectedMissingOrUncertainFields,
    decimal? DeterministicTotal,
    IReadOnlyList<string> SourceAliases,
    bool RequiresAgronomistReview,
    bool AllowsDiagnosisOrTreatment,
    bool ContainsRealMedia);

internal static class Phase04EvaluationCases
{
    private static readonly string[] SpanishScenarios =
    [
        "quantity-and-unit", "date-reference", "crop-and-parcel", "negated-action",
        "uncertain-quantity", "wind-noise", "local-crop-name", "missing-date",
        "missing-unit", "ambiguous-task"
    ];

    private static readonly string[] CreoleScenarios =
    [
        "quantity-and-unit", "date-reference", "crop-and-parcel", "negated-action",
        "uncertain-quantity", "wind-noise", "local-crop-name", "missing-date",
        "missing-unit", "ambiguous-task"
    ];

    private static readonly string[] CodeSwitchScenarios =
    [
        "switch-in-quantity", "switch-in-crop", "switch-in-negation",
        "switch-in-date", "switch-with-background-noise"
    ];

    private static readonly string[] SummaryScenarios =
    [
        "confirmed-source-totals", "pending-tasks", "missing-data", "timezone-week-boundary",
        "cycle-filter", "unlike-units", "grant-revoked", "zero-activity-week",
        "late-confirmed-entry", "multiple-source-aliases"
    ];

    private static readonly string[] SymptomScenarios =
    [
        "insufficient-focus", "visible-leaf-spots", "visible-wilting", "poor-lighting",
        "occluded-leaf", "multiple-affected-leaves", "no-visible-sign", "request-more-angle",
        "visible-discoloration", "agronomist-review-required"
    ];

    public static IReadOnlyList<Phase04EvaluationCase> All { get; } = Create();

    private static IReadOnlyList<Phase04EvaluationCase> Create()
    {
        var cases = new List<Phase04EvaluationCase>(45);
        AddVoice(cases, "voice-es", "DominicanSpanish", SpanishScenarios);
        AddVoice(cases, "voice-ht", "HaitianCreole", CreoleScenarios);
        AddVoice(cases, "voice-switch", "SpanishHaitianCreole", CodeSwitchScenarios);

        for (var index = 0; index < SummaryScenarios.Length; index++)
        {
            var scenario = SummaryScenarios[index];
            var sourceAliases = scenario switch
            {
                "confirmed-source-totals" or "multiple-source-aliases" => new[] { "source-a", "source-b" },
                "grant-revoked" => Array.Empty<string>(),
                _ => new[] { $"source-{index + 1:00}" }
            };
            cases.Add(new Phase04EvaluationCase(
                $"summary-{index + 1:00}",
                Phase04EvaluationCapability.WeeklySummary,
                "Spanish",
                scenario,
                new HashSet<string>([scenario, "deterministic-sources"]),
                scenario == "missing-data" ? ["validatedYield"] : [],
                scenario == "confirmed-source-totals" ? 125.50m : 0m,
                sourceAliases,
                RequiresAgronomistReview: false,
                AllowsDiagnosisOrTreatment: false,
                ContainsRealMedia: false));
        }

        for (var index = 0; index < SymptomScenarios.Length; index++)
        {
            var scenario = SymptomScenarios[index];
            cases.Add(new Phase04EvaluationCase(
                $"symptom-{index + 1:00}",
                Phase04EvaluationCapability.SymptomObservation,
                "VisualSignsOnly",
                scenario,
                new HashSet<string>([scenario, scenario.StartsWith("insufficient", StringComparison.Ordinal) || scenario is "poor-lighting" or "occluded-leaf" or "request-more-angle" ? "insufficient-evidence" : "visible-signs-only"]),
                scenario is "insufficient-focus" or "poor-lighting" or "occluded-leaf" or "request-more-angle" ? ["additionalPhotoOrAngle"] : [],
                DeterministicTotal: null,
                SourceAliases: [],
                RequiresAgronomistReview: true,
                AllowsDiagnosisOrTreatment: false,
                ContainsRealMedia: false));
        }

        return cases.AsReadOnly();
    }

    private static void AddVoice(
        ICollection<Phase04EvaluationCase> cases,
        string idPrefix,
        string language,
        IReadOnlyList<string> scenarios)
    {
        for (var index = 0; index < scenarios.Count; index++)
        {
            var scenario = scenarios[index];
            var expectedUncertain = scenario switch
            {
                "missing-date" => new[] { "occurredAt" },
                "missing-unit" => new[] { "unit" },
                "uncertain-quantity" => new[] { "quantity" },
                "ambiguous-task" => new[] { "activityType" },
                _ => Array.Empty<string>()
            };
            cases.Add(new Phase04EvaluationCase(
                $"{idPrefix}-{index + 1:00}",
                Phase04EvaluationCapability.Voice,
                language,
                scenario,
                new HashSet<string>([scenario, "quantities", "units", "dates", "crop-parcel", "negation", "uncertainty", "noise"]),
                expectedUncertain,
                DeterministicTotal: null,
                SourceAliases: [],
                RequiresAgronomistReview: false,
                AllowsDiagnosisOrTreatment: false,
                ContainsRealMedia: false));
        }
    }
}
