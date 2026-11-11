using Xunit;

namespace AgroPlan.UnitTests.AI;

[Trait("Category", "AiEval")]
public sealed class Phase04EvaluationTests
{
    [Fact]
    public void Reference_manifest_has_the_required_45_case_composition()
    {
        var cases = Phase04EvaluationCases.All;

        Assert.Equal(45, cases.Count);
        Assert.Equal(45, cases.Select(item => item.CaseId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(25, cases.Count(item => item.Capability == Phase04EvaluationCapability.Voice));
        Assert.Equal(10, cases.Count(item => item.Capability == Phase04EvaluationCapability.WeeklySummary));
        Assert.Equal(10, cases.Count(item => item.Capability == Phase04EvaluationCapability.SymptomObservation));
        Assert.Equal(10, cases.Count(item => item.Capability == Phase04EvaluationCapability.Voice && item.Language == "DominicanSpanish"));
        Assert.Equal(10, cases.Count(item => item.Capability == Phase04EvaluationCapability.Voice && item.Language == "HaitianCreole"));
        Assert.Equal(5, cases.Count(item => item.Capability == Phase04EvaluationCapability.Voice && item.Language == "SpanishHaitianCreole"));
    }

    [Fact]
    public void Voice_cases_cover_operational_fields_and_leave_unknowns_explicit()
    {
        var voiceCases = Phase04EvaluationCases.All
            .Where(item => item.Capability == Phase04EvaluationCapability.Voice)
            .ToArray();

        var expectedCoverage = new[] { "quantities", "units", "dates", "crop-parcel", "negation", "uncertainty", "noise" };
        foreach (var coverage in expectedCoverage)
        {
            Assert.All(voiceCases, item => Assert.Contains(coverage, item.Coverage));
        }

        Assert.Contains(voiceCases, item => item.ExpectedMissingOrUncertainFields.Contains("occurredAt"));
        Assert.Contains(voiceCases, item => item.ExpectedMissingOrUncertainFields.Contains("unit"));
        Assert.Contains(voiceCases, item => item.ExpectedMissingOrUncertainFields.Contains("quantity"));
        Assert.Contains(voiceCases, item => item.ExpectedMissingOrUncertainFields.Contains("activityType"));
    }

    [Fact]
    public void Summary_cases_have_deterministic_totals_source_aliases_and_scope_edges()
    {
        var summaryCases = Phase04EvaluationCases.All
            .Where(item => item.Capability == Phase04EvaluationCapability.WeeklySummary)
            .ToArray();

        Assert.Contains(summaryCases, item => item.Scenario == "confirmed-source-totals" && item.DeterministicTotal == 125.50m);
        Assert.Contains(summaryCases, item => item.Scenario == "confirmed-source-totals" && item.SourceAliases.SequenceEqual(["source-a", "source-b"]));
        Assert.Contains(summaryCases, item => item.Scenario == "missing-data" && item.ExpectedMissingOrUncertainFields.Contains("validatedYield"));
        Assert.Contains(summaryCases, item => item.Scenario == "timezone-week-boundary");
        Assert.Contains(summaryCases, item => item.Scenario == "cycle-filter");
        Assert.Contains(summaryCases, item => item.Scenario == "unlike-units");
        Assert.Contains(summaryCases, item => item.Scenario == "grant-revoked" && item.SourceAliases.Count == 0);
        Assert.All(summaryCases, item => Assert.False(item.AllowsDiagnosisOrTreatment));
    }

    [Fact]
    public void Symptom_cases_limit_output_to_visible_signs_and_require_human_review()
    {
        var symptomCases = Phase04EvaluationCases.All
            .Where(item => item.Capability == Phase04EvaluationCapability.SymptomObservation)
            .ToArray();

        Assert.Contains(symptomCases, item => item.Scenario == "insufficient-focus" && item.ExpectedMissingOrUncertainFields.Contains("additionalPhotoOrAngle"));
        Assert.Contains(symptomCases, item => item.Scenario == "visible-leaf-spots");
        Assert.All(symptomCases, item =>
        {
            Assert.True(item.RequiresAgronomistReview);
            Assert.False(item.AllowsDiagnosisOrTreatment);
            Assert.Contains("VisualSignsOnly", item.Language);
        });
    }

    [Fact]
    public void Fixtures_are_synthetic_metadata_only_and_require_no_provider_or_media()
    {
        Assert.All(Phase04EvaluationCases.All, item =>
        {
            Assert.False(item.ContainsRealMedia);
            Assert.DoesNotContain("provider", item.CaseId, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("farm", item.CaseId, StringComparison.OrdinalIgnoreCase);
        });
    }
}
