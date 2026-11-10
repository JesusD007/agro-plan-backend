using AgroPlan.Application.Calendars;
using AgroPlan.Domain.Calendars;
using Xunit;

namespace AgroPlan.UnitTests.Calendars;

public sealed class CropCalendarEvaluatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid CropId = Guid.NewGuid();

    [Fact]
    public void Evaluate_returns_sourced_calendar_and_harvest_window_from_validated_inputs()
    {
        var template = Template();
        var progress = new[] { new CalendarProgressEvent(Guid.NewGuid(), CropCalendarOperationKind.Irrigation, Now.AddDays(-2)) };

        var result = CropCalendarEvaluator.Evaluate(CropId, new DateOnly(2026, 10, 1), template, progress, Now);

        Assert.Equal("available", result.Status);
        Assert.Equal("MIDA approved dataset", result.RuleSource);
        Assert.Equal("2026.2", result.RuleSourceVersion);
        Assert.Equal(new DateOnly(2026, 10, 31), result.EstimatedHarvestWindow!.StartsOn);
        Assert.Equal(new DateOnly(2026, 11, 10), result.EstimatedHarvestWindow.EndsOn);
        Assert.Equal("irrigate", Assert.Single(result.Tasks).Key);
        Assert.Equal("completed", result.Tasks[0].Status);
        Assert.True(result.ActualHarvestRequiresValidatedPhi);
        Assert.Empty(result.MissingInputs);
    }

    [Fact]
    public void Evaluate_returns_missing_codes_without_estimating_when_inputs_are_missing()
    {
        var result = CropCalendarEvaluator.Evaluate(CropId, null, null, [], Now);

        Assert.Equal("unavailable", result.Status);
        Assert.Null(result.EstimatedHarvestWindow);
        Assert.Empty(result.Tasks);
        Assert.Contains("validated_crop_calendar_rule", result.MissingInputs);
        Assert.Contains("confirmed_planting_date", result.MissingInputs);
        Assert.Contains("confirmed_crop_cycle_progress", result.MissingInputs);
        Assert.True(result.ActualHarvestRequiresValidatedPhi);
    }

    [Fact]
    public void Evaluate_suppresses_stale_or_crop_mismatched_templates()
    {
        var expired = Template(effectiveUntil: Now.AddDays(-1));
        var stale = CropCalendarEvaluator.Evaluate(CropId, new DateOnly(2026, 10, 1), expired,
            [new(Guid.NewGuid(), CropCalendarOperationKind.Irrigation, Now.AddDays(-2))], Now);
        var wrongCrop = CropCalendarEvaluator.Evaluate(Guid.NewGuid(), new DateOnly(2026, 10, 1), Template(),
            [new(Guid.NewGuid(), CropCalendarOperationKind.Irrigation, Now.AddDays(-2))], Now);

        Assert.Equal("unavailable", stale.Status);
        Assert.Null(stale.EstimatedHarvestWindow);
        Assert.Contains("current_matching_crop_calendar_rule", stale.MissingInputs);
        Assert.Null(wrongCrop.EstimatedHarvestWindow);
        Assert.Contains("current_matching_crop_calendar_rule", wrongCrop.MissingInputs);
    }

    [Fact]
    public void Evaluate_returns_partial_calendar_without_harvest_estimate_if_confirmed_progress_is_absent()
    {
        var result = CropCalendarEvaluator.Evaluate(CropId, new DateOnly(2026, 10, 1), Template(), [], Now);

        Assert.Equal("partial", result.Status);
        Assert.Null(result.EstimatedHarvestWindow);
        Assert.Contains("confirmed_crop_cycle_progress", result.MissingInputs);
    }

    private static CropCalendarTemplate Template(DateTimeOffset? effectiveUntil = null) => new(
        Guid.NewGuid(), CropId, "DO-NORTH-1", "MIDA approved dataset", "2026.2",
        CropCalendarTemplateStatus.Validated, null, null, Now.AddDays(-90), effectiveUntil,
        30, 40,
        [new CropCalendarTask("irrigate", "growth", "Check irrigation", "Use the validated schedule.", 20, 1,
            CropCalendarOperationKind.Irrigation)]);
}
