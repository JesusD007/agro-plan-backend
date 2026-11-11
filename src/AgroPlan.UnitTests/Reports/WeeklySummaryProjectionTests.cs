using AgroPlan.Application.Reports;
using AgroPlan.Domain.Logbook;
using AgroPlan.Domain.Operations;
using AgroPlan.Domain.Workforce;
using Xunit;

namespace AgroPlan.UnitTests.Reports;

public sealed class WeeklySummaryProjectionTests
{
    [Fact]
    public void Farm_local_week_uses_monday_boundaries_and_keeps_sources_and_units_deterministic()
    {
        var farmId = Guid.NewGuid();
        var otherFarmId = Guid.NewGuid();
        var cycleId = Guid.NewGuid();
        var parcelId = Guid.NewGuid();
        var weekStart = new DateOnly(2026, 11, 9);
        var start = new DateTimeOffset(2026, 11, 9, 4, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 11, 16, 4, 0, 0, TimeSpan.Zero);
        var originalExpense = NewExpense(farmId, parcelId, cycleId, start.AddDays(1), 10m, "DOP");
        var correctedExpense = new ExpenseEntry(Guid.NewGuid(), farmId, parcelId, cycleId, OperationCostCategory.Material,
            start.AddDays(2), "Corrected material", 12m, "DOP", 2m, "kg", "producer", start.AddDays(2), originalExpense.Id, "Correction");
        var dollarExpense = NewExpense(farmId, parcelId, cycleId, start.AddDays(3), 5m, "USD");
        var outsideExpense = NewExpense(farmId, parcelId, cycleId, end, 80m, "DOP");
        var foreignExpense = NewExpense(otherFarmId, parcelId, cycleId, start.AddDays(1), 99m, "EUR");
        var firstHarvest = NewHarvest(farmId, parcelId, cycleId, start.AddDays(4), 3m, "kg");
        var secondHarvest = NewHarvest(farmId, parcelId, cycleId, start.AddDays(4), 7m, "sacks");
        var boundaryHarvest = NewHarvest(farmId, parcelId, cycleId, end, 100m, "kg");
        var irrigation = new IrrigationEntry(Guid.NewGuid(), farmId, parcelId, cycleId, IrrigationMethod.Drip,
            "Reservoir", start.AddDays(2), "producer", start.AddDays(2));
        var application = new InputApplicationEntry(Guid.NewGuid(), farmId, parcelId, cycleId,
            "registration", "formulation", "ARROZ", "field-use", start.AddDays(3), InputApplicationMethod.Manual,
            4m, "L", "producer", start.AddDays(3));
        var logbook = new LogbookEntry(Guid.NewGuid(), farmId, parcelId, cycleId, LogbookActivityType.Sowing,
            start.AddDays(1), "Sowing", "producer", start.AddDays(1));
        var pending = NewTask(farmId, cycleId, weekStart.AddDays(1));
        var verified = NewTask(farmId, cycleId, weekStart.AddDays(2));
        verified.Start();
        verified.Submit(start.AddDays(2));
        verified.Verify(start.AddDays(3));
        var rejected = NewTask(farmId, cycleId, weekStart.AddDays(3));
        rejected.Start();
        rejected.Submit(start.AddDays(3));
        rejected.Reject();

        var result = WeeklySummaryProjection.Create(farmId, new DateOnly(2026, 11, 15), "America/Santo_Domingo", null,
            [originalExpense, correctedExpense, dollarExpense, outsideExpense, foreignExpense],
            [firstHarvest, secondHarvest, boundaryHarvest], [irrigation], [application], [logbook], [pending, verified, rejected]);

        Assert.Equal(WeeklySummaryProjectionStatus.Available, result.Status);
        var projection = Assert.IsType<WeeklySummaryProjection>(result.Projection);
        Assert.Equal(weekStart, projection.WeekStartDate);
        Assert.Equal(weekStart.AddDays(6), projection.WeekEndDate);
        Assert.Equal(start, projection.WindowStartUtc);
        Assert.Equal(end, projection.WindowEndUtcExclusive);
        Assert.Equal(new[] { ("DOP", 12m), ("USD", 5m) },
            projection.CostTotals.Select(x => (x.CurrencyCode, x.Amount)).ToArray());
        Assert.DoesNotContain(projection.Sources, x => x.Id == originalExpense.Id || x.Id == outsideExpense.Id || x.Id == foreignExpense.Id || x.Id == boundaryHarvest.Id);
        Assert.Contains(projection.Sources, x => x.Id == correctedExpense.Id && x.LocalDate == weekStart.AddDays(2));
        Assert.Equal(new[] { ("kg", 3m), ("sacks", 7m) },
            projection.HarvestTotals.Select(x => (x.Unit, x.Quantity)).ToArray());
        Assert.Single(projection.PendingTasks);
        Assert.Equal(pending.Id, projection.PendingTasks[0].Id);
        Assert.DoesNotContain(projection.PendingTasks, x => x.Id == rejected.Id);
        Assert.Contains("no_harvest_records", WeeklySummaryProjection.Create(farmId, weekStart, "America/Santo_Domingo", cycleId,
            [correctedExpense], [], [], [], [], []).Projection?.MissingData ?? []);
    }

    [Fact]
    public void Farm_local_week_window_accounts_for_daylight_saving_transition()
    {
        var window = WeeklySummaryProjection.ResolveWeekWindow(new DateOnly(2026, 3, 8), "America/New_York");

        Assert.Equal(WeeklySummaryProjectionStatus.Available, window.Status);
        Assert.Equal(new DateTimeOffset(2026, 3, 2, 5, 0, 0, TimeSpan.Zero), window.WindowStartUtc);
        Assert.Equal(new DateTimeOffset(2026, 3, 9, 4, 0, 0, TimeSpan.Zero), window.WindowEndUtcExclusive);
        Assert.Equal(TimeSpan.FromHours(167), window.WindowEndUtcExclusive - window.WindowStartUtc);
    }

    [Fact]
    public void Invalid_farm_time_zone_returns_explicit_unavailable_result()
    {
        var result = WeeklySummaryProjection.Create(Guid.NewGuid(), new DateOnly(2026, 11, 9), "not-a-time-zone", null,
            [], [], [], [], [], []);

        Assert.Equal(WeeklySummaryProjectionStatus.Unavailable, result.Status);
        Assert.Equal("farm_time_zone_invalid", result.ErrorCode);
        Assert.Null(result.Projection);
    }

    [Fact]
    public void Projection_above_500_source_and_pending_rows_fails_without_partial_data()
    {
        var farmId = Guid.NewGuid();
        var parcelId = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(2026, 11, 9, 4, 0, 0, TimeSpan.Zero);
        var expenses = Enumerable.Range(0, WeeklySummaryProjection.MaximumRows + 1)
            .Select(index => NewExpense(farmId, parcelId, null, occurredAt.AddMinutes(index), 1m, "DOP"))
            .ToArray();

        var result = WeeklySummaryProjection.Create(farmId, new DateOnly(2026, 11, 9), "America/Santo_Domingo", null,
            expenses, [], [], [], [], []);

        Assert.Equal(WeeklySummaryProjectionStatus.LimitExceeded, result.Status);
        Assert.Equal("projection_limit_exceeded", result.ErrorCode);
        Assert.Null(result.Projection);
    }

    private static ExpenseEntry NewExpense(Guid farmId, Guid parcelId, Guid? cycleId, DateTimeOffset occurredAt,
        decimal amount, string currency) => new(Guid.NewGuid(), farmId, parcelId, cycleId, OperationCostCategory.Material,
        occurredAt, "Material purchase", amount, currency, 1m, "unit", "producer", occurredAt);

    private static HarvestEntry NewHarvest(Guid farmId, Guid parcelId, Guid cycleId, DateTimeOffset harvestedAt,
        decimal quantity, string unit) => new(Guid.NewGuid(), farmId, parcelId, cycleId, harvestedAt,
        quantity, unit, "producer", harvestedAt, "{}");

    private static WorkTask NewTask(Guid farmId, Guid cycleId, DateOnly dueDate) => new(Guid.NewGuid(), farmId,
        "worker", dueDate, "Field work", false, null, null, Guid.NewGuid(), cycleId, "sowing");
}
