using AgroPlan.Domain.Operations;
using Xunit;

namespace AgroPlan.UnitTests.Operations;

public sealed class InputApplicationEntryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Farm = Guid.NewGuid();
    private static readonly Guid Parcel = Guid.NewGuid();

    [Fact]
    public void Entry_preserves_product_use_quantity_and_original_unit_without_phiguess()
    {
        var entry = Create();
        Assert.Equal("REG-42", entry.ProductRegistrationId);
        Assert.Equal("FORM-EC", entry.FormulationId);
        Assert.Equal("rice", entry.CropId);
        Assert.Equal(2.5m, entry.Quantity);
        Assert.Equal("L/ha", entry.Unit);
    }

    [Fact]
    public void Missing_or_unvalidated_rule_returns_unavailable_with_reason()
    {
        var entry = Create();
        var noRules = ApplicationFrequencyEvaluator.Assess(entry, [], [], Now);
        Assert.Equal(ApplicationFrequencyStatus.Unavailable, noRules.Status);
        Assert.NotNull(noRules.MissingEvidenceReason);

        var unvalidated = Rule(validated: false);
        Assert.Equal(ApplicationFrequencyStatus.Unavailable,
            ApplicationFrequencyEvaluator.Assess(entry, [], [unvalidated], Now).Status);
    }

    [Fact]
    public void Stale_or_exact_key_mismatch_rule_returns_unavailable()
    {
        var entry = Create();
        var stale = Rule(validUntil: Now.AddDays(-1));
        Assert.Equal(ApplicationFrequencyStatus.Unavailable,
            ApplicationFrequencyEvaluator.Assess(entry, [], [stale], Now).Status);
        var mismatch = Rule(product: "OTHER");
        Assert.Equal(ApplicationFrequencyStatus.Unavailable,
            ApplicationFrequencyEvaluator.Assess(entry, [], [mismatch], Now).Status);
    }

    [Fact]
    public void Exact_validated_rule_returns_sourced_compliant_approaching_and_exceeded_status()
    {
        var rule = Rule(approachingAt: 2, maximum: 3);
        var first = Create(appliedAt: Now);
        var compliant = ApplicationFrequencyEvaluator.Assess(first, [], [rule], Now);
        Assert.Equal(ApplicationFrequencyStatus.Compliant, compliant.Status);
        Assert.Equal("MIDA label registry", compliant.Source);
        Assert.Equal("rev-2026-1", compliant.SourceVersion);

        var prior = Create(appliedAt: Now.AddDays(-1));
        var approaching = ApplicationFrequencyEvaluator.Assess(first, [prior], [rule], Now);
        Assert.Equal(ApplicationFrequencyStatus.Approaching, approaching.Status);

        var second = Create(appliedAt: Now);
        var exceeded = ApplicationFrequencyEvaluator.Assess(second, [prior, first], [rule], Now);
        Assert.Equal(ApplicationFrequencyStatus.Exceeded, exceeded.Status);
    }

    [Fact]
    public void Correction_does_not_double_count_superseded_application()
    {
        var rule = Rule(approachingAt: 2, maximum: 3);
        var original = Create(appliedAt: Now.AddDays(-1));
        var correction = new InputApplicationEntry(Guid.NewGuid(), original.FarmId, original.ParcelId, null,
            original.ProductRegistrationId, original.FormulationId, original.CropId, original.IntendedUseId,
            original.AppliedAt, original.Method, original.Quantity, original.Unit, "producer", Now,
            original.Id, "Corrected record");
        var next = Create(appliedAt: Now);
        var assessment = ApplicationFrequencyEvaluator.Assess(next, [original, correction], [rule], Now);
        Assert.Equal(ApplicationFrequencyStatus.Approaching, assessment.Status);
        Assert.Equal(2, assessment.ApplicationsInPeriod);
    }

    [Fact]
    public void Entry_rejects_missing_identity_unit_negative_quantity_and_unreasoned_correction()
    {
        Assert.Throws<ArgumentException>(() => Create(product: " "));
        Assert.Throws<ArgumentException>(() => Create(unit: " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(quantity: -1));
        var entry = Create();
        Assert.Throws<ArgumentException>(() => new InputApplicationEntry(Guid.NewGuid(), entry.FarmId, entry.ParcelId, null,
            entry.ProductRegistrationId, entry.FormulationId, entry.CropId, entry.IntendedUseId,
            Now, entry.Method, entry.Quantity, entry.Unit, "producer", Now, entry.Id, " "));
    }

    private static InputApplicationEntry Create(string product = "REG-42", string unit = "L/ha",
        decimal quantity = 2.5m, DateTimeOffset? appliedAt = null) =>
        new(Guid.NewGuid(), Farm, Parcel, null, product, "FORM-EC", "rice", "fungicide-use",
            appliedAt ?? Now, InputApplicationMethod.Drone, quantity, unit, "producer", Now);

    private static ValidatedApplicationFrequencyRule Rule(string product = "REG-42", int? approachingAt = null,
        int maximum = 5, DateTimeOffset? validUntil = null, bool validated = true) =>
        new(Guid.NewGuid(), product, "FORM-EC", "rice", "fungicide-use", "DO", "MIDA label registry", "rev-2026-1",
            Now.AddDays(-30), validUntil ?? Now.AddDays(30), 30, maximum, approachingAt, validated);
}
