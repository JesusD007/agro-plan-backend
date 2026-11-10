using AgroPlan.Application.Safety;
using AgroPlan.Domain.Operations;
using AgroPlan.Domain.Safety;
using Xunit;

namespace AgroPlan.UnitTests.Safety;

public sealed class HarvestEligibilityEvaluatorTests
{
    private static readonly DateTimeOffset Applied = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Harvest = new(2026, 11, 6, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid Cycle = Guid.NewGuid();

    [Fact]
    public void Exact_current_validated_label_yields_traceable_safe_date()
    {
        var application = App();
        var label = Label(phiDays: 5);
        var assessment = HarvestEligibilityEvaluator.Assess(Cycle, [application], [label], Harvest);
        Assert.True(assessment.IsEligible);
        Assert.Equal(Harvest, assessment.EarliestSafeAt);
        var evidence = Assert.Single(assessment.Evidence);
        Assert.Equal(application.Id, evidence.ApplicationId);
        Assert.Equal(label.Id, evidence.LabelId);
        Assert.Equal("label-v3", evidence.SourceVersion);
    }

    [Fact]
    public void Unknown_phi_never_uses_mrl_or_worker_reentry_interval()
    {
        var assessment = HarvestEligibilityEvaluator.Assess(Cycle, [App()],
            [Label(phiDays: null, mrl: 0, reEntryHours: 0)], Harvest);
        Assert.False(assessment.IsEligible);
        Assert.Contains(assessment.MissingEvidenceReasons, reason => reason.Contains("Pre-harvest interval is missing"));
        Assert.Empty(assessment.Evidence);
    }

    [Theory]
    [InlineData(false, "REG-OTHER", true)]
    [InlineData(true, "REG-42", false)]
    public void Unvalidated_or_mismatched_label_fails_closed(bool validated, string registration, bool expectMismatch)
    {
        var assessment = HarvestEligibilityEvaluator.Assess(Cycle, [App()],
            [Label(phiDays: null, validated: validated, registration: registration)], Harvest);
        Assert.False(assessment.IsEligible);
        Assert.NotEmpty(assessment.MissingEvidenceReasons);
        if (expectMismatch) Assert.Contains("No exact", assessment.MissingEvidenceReasons[0]);
    }

    [Fact]
    public void Multiple_applications_use_latest_earliest_safe_date_and_block_early_harvest()
    {
        var first = App(applied: Applied);
        var second = App(applied: Applied.AddDays(2));
        var label = Label(phiDays: 5);
        var tooEarly = HarvestEligibilityEvaluator.Assess(Cycle, [first, second], [label], Harvest);
        Assert.False(tooEarly.IsEligible);
        Assert.Equal(Applied.AddDays(7), tooEarly.EarliestSafeAt);
        Assert.Contains(tooEarly.MissingEvidenceReasons, x => x.Contains("before the latest earliest-safe date"));
    }

    [Fact]
    public void Expired_label_and_wrong_jurisdiction_are_not_accepted()
    {
        var expired = Label(phiDays: 0, until: Harvest.AddTicks(-1));
        var assessment = HarvestEligibilityEvaluator.Assess(Cycle, [App()], [expired], Harvest);
        Assert.False(assessment.IsEligible);
        var wrongJurisdiction = Label(phiDays: 0, jurisdiction: "US");
        Assert.False(HarvestEligibilityEvaluator.Assess(Cycle, [App()], [wrongJurisdiction], Harvest).IsEligible);
    }

    [Fact]
    public void Application_crop_must_match_the_crop_cycle_identity()
    {
        var assessment = HarvestEligibilityEvaluator.Assess(Cycle, [App()], [Label(phiDays: 0)], Harvest,
            expectedCropId: "YUCA");
        Assert.False(assessment.IsEligible);
        Assert.Contains("crop identity does not match", assessment.MissingEvidenceReasons[0]);
    }

    private static InputApplicationEntry App(DateTimeOffset? applied = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Cycle, "REG-42", "FORM-EC", "rice", "fungicide-use",
            applied ?? Applied, InputApplicationMethod.Manual, 1m, "L/ha", "producer", Applied);

    private static ValidatedProductLabel Label(int? phiDays, bool validated = true, string registration = "REG-42",
        decimal? mrl = null, int? reEntryHours = null, DateTimeOffset? until = null, string jurisdiction = "DO") =>
        new(Guid.NewGuid(), registration, "FORM-EC", "rice", "fungicide-use", jurisdiction,
            "MIDA approved product label", "label-v3", Applied.AddDays(-30), until ?? Harvest.AddDays(30),
            phiDays, mrl, reEntryHours, validated);
}
