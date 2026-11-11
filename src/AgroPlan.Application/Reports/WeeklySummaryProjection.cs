using AgroPlan.Domain.Crops;
using AgroPlan.Domain.Logbook;
using AgroPlan.Domain.Operations;
using AgroPlan.Domain.Workforce;

namespace AgroPlan.Application.Reports;

public enum WeeklySummaryProjectionStatus
{
    Available,
    Unavailable,
    LimitExceeded
}

public sealed record WeeklySummaryProjectionResult(
    WeeklySummaryProjectionStatus Status,
    string? ErrorCode,
    WeeklySummaryProjection? Projection);

public sealed record WeeklySummaryWeekWindowResult(
    WeeklySummaryProjectionStatus Status,
    string? ErrorCode,
    DateOnly WeekStartDate,
    DateOnly WeekEndDate,
    DateTimeOffset WindowStartUtc,
    DateTimeOffset WindowEndUtcExclusive,
    string? TimeZoneId);

public sealed record WeeklySummaryProjection(
    Guid FarmId,
    DateOnly RequestedWeekAnchorDate,
    DateOnly WeekStartDate,
    DateOnly WeekEndDate,
    DateTimeOffset WindowStartUtc,
    DateTimeOffset WindowEndUtcExclusive,
    string TimeZoneId,
    Guid? CropCycleId,
    IReadOnlyList<WeeklySummarySource> Sources,
    IReadOnlyList<WeeklySummaryPendingTask> PendingTasks,
    IReadOnlyList<CostCurrencyTotal> CostTotals,
    IReadOnlyList<SourceQuantityTotal> RecordedQuantities,
    IReadOnlyList<WeeklySummaryQuantityTotal> HarvestTotals,
    IReadOnlyList<string> MissingData)
{
    public const int MaximumRows = 500;

    public static WeeklySummaryWeekWindowResult ResolveWeekWindow(DateOnly weekAnchorDate, string? timeZoneId)
    {
        if (weekAnchorDate == default) return Invalid("invalid_week_anchor");
        if (string.IsNullOrWhiteSpace(timeZoneId)) return Invalid("farm_time_zone_not_configured");
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim()); }
        catch (TimeZoneNotFoundException) { return Invalid("farm_time_zone_invalid"); }
        catch (InvalidTimeZoneException) { return Invalid("farm_time_zone_invalid"); }

        var daysFromMonday = ((int)weekAnchorDate.DayOfWeek + 6) % 7;
        var weekStart = weekAnchorDate.AddDays(-daysFromMonday);
        var weekEnd = weekStart.AddDays(6);
        try
        {
            var startUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(weekStart.ToDateTime(TimeOnly.MinValue), zone));
            var endUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(weekEnd.AddDays(1).ToDateTime(TimeOnly.MinValue), zone));
            return new(WeeklySummaryProjectionStatus.Available, null, weekStart, weekEnd, startUtc, endUtc, timeZoneId.Trim());
        }
        catch (ArgumentException) { return Invalid("farm_week_boundary_invalid_in_time_zone"); }

        static WeeklySummaryWeekWindowResult Invalid(string reason) =>
            new(WeeklySummaryProjectionStatus.Unavailable, reason, default, default, default, default, null);
    }

    public static WeeklySummaryProjectionResult Create(Guid farmId, DateOnly weekAnchorDate, string? timeZoneId,
        Guid? cropCycleId, IEnumerable<ExpenseEntry> expenseRows, IEnumerable<HarvestEntry> harvestRows,
        IEnumerable<IrrigationEntry> irrigationRows, IEnumerable<InputApplicationEntry> applicationRows,
        IEnumerable<LogbookEntry> logbookRows, IEnumerable<WorkTask> taskRows)
    {
        if (farmId == Guid.Empty) throw new ArgumentException("Farm id is required.", nameof(farmId));
        if (cropCycleId == Guid.Empty) throw new ArgumentException("Crop cycle id cannot be empty.", nameof(cropCycleId));
        var window = ResolveWeekWindow(weekAnchorDate, timeZoneId);
        if (window.Status != WeeklySummaryProjectionStatus.Available || window.TimeZoneId is null)
            return Unavailable(window.ErrorCode ?? "farm_time_zone_unavailable");
        var zone = TimeZoneInfo.FindSystemTimeZoneById(window.TimeZoneId);
        var weekStart = window.WeekStartDate;
        var weekEnd = window.WeekEndDate;
        var startUtc = window.WindowStartUtc;
        var endUtc = window.WindowEndUtcExclusive;

        var expenses = expenseRows.ToArray();
        var harvests = harvestRows.ToArray();
        var irrigation = irrigationRows.ToArray();
        var applications = applicationRows.ToArray();
        var logbook = logbookRows.ToArray();
        var tasks = taskRows.ToArray();

        var expenseSuperseded = expenses.Where(x => x.CorrectsEntryId.HasValue).Select(x => x.CorrectsEntryId!.Value).ToHashSet();
        var harvestSuperseded = harvests.Where(x => x.CorrectsEntryId.HasValue).Select(x => x.CorrectsEntryId!.Value).ToHashSet();
        var irrigationSuperseded = irrigation.Where(x => x.CorrectsEntryId.HasValue).Select(x => x.CorrectsEntryId!.Value).ToHashSet();
        var applicationSuperseded = applications.Where(x => x.CorrectsEntryId.HasValue).Select(x => x.CorrectsEntryId!.Value).ToHashSet();
        var logbookSuperseded = logbook.Where(x => x.CorrectsEntryId.HasValue).Select(x => x.CorrectsEntryId!.Value).ToHashSet();

        bool CycleMatches(Guid? rowCycle) => !cropCycleId.HasValue || rowCycle == cropCycleId;
        bool TimeMatches(DateTimeOffset time) => time >= startUtc && time < endUtc;
        var selectedExpenses = expenses.Where(x => x.FarmId == farmId && CycleMatches(x.CropCycleId) && TimeMatches(x.OccurredAt) && !expenseSuperseded.Contains(x.Id)).ToArray();
        var selectedHarvests = harvests.Where(x => x.FarmId == farmId && CycleMatches(x.CropCycleId) && TimeMatches(x.HarvestedAt) && !harvestSuperseded.Contains(x.Id)).ToArray();
        var selectedIrrigation = irrigation.Where(x => x.FarmId == farmId && CycleMatches(x.CropCycleId) && TimeMatches(x.OccurredAt) && !irrigationSuperseded.Contains(x.Id)).ToArray();
        var selectedApplications = applications.Where(x => x.FarmId == farmId && CycleMatches(x.CropCycleId) && TimeMatches(x.AppliedAt) && !applicationSuperseded.Contains(x.Id)).ToArray();
        var selectedLogbook = logbook.Where(x => x.FarmId == farmId && CycleMatches(x.CropCycleId) && TimeMatches(x.OccurredAt) && !logbookSuperseded.Contains(x.Id)).ToArray();
        var pendingTasks = tasks.Where(x => x.FarmId == farmId && CycleMatches(x.CropCycleId) &&
                x.DueDate >= weekStart && x.DueDate <= weekEnd && x.Status is WorkTaskStatus.Pending or
                    WorkTaskStatus.InProgress or WorkTaskStatus.Submitted or WorkTaskStatus.CorrectionRequested)
            .OrderBy(x => x.DueDate).ThenBy(x => x.Id)
            .Select(x => new WeeklySummaryPendingTask(x.Id, x.Status.ToString(), x.DueDate, x.OperationKind))
            .ToArray();

        var sources = selectedExpenses.Select(x => new WeeklySummarySource(x.Id, "expense", LocalDate(x.OccurredAt, zone), x.CropCycleId, x.Amount, x.CurrencyCode, x.Quantity, x.Unit))
            .Concat(selectedHarvests.Select(x => new WeeklySummarySource(x.Id, "harvest", LocalDate(x.HarvestedAt, zone), x.CropCycleId, null, null, x.Quantity, x.Unit)))
            .Concat(selectedIrrigation.Select(x => new WeeklySummarySource(x.Id, "irrigation", LocalDate(x.OccurredAt, zone), x.CropCycleId, null, null, null, null)))
            .Concat(selectedApplications.Select(x => new WeeklySummarySource(x.Id, "input_application", LocalDate(x.AppliedAt, zone), x.CropCycleId, null, null, x.Quantity, x.Unit)))
            .Concat(selectedLogbook.Select(x => new WeeklySummarySource(x.Id, $"logbook_{x.ActivityType.ToString().ToLowerInvariant()}", LocalDate(x.OccurredAt, zone), x.CropCycleId, null, null, null, null)))
            .OrderBy(x => x.LocalDate).ThenBy(x => x.Kind, StringComparer.Ordinal).ThenBy(x => x.Id)
            .ToArray();

        if (sources.Length + pendingTasks.Length > MaximumRows)
            return new WeeklySummaryProjectionResult(WeeklySummaryProjectionStatus.LimitExceeded, "projection_limit_exceeded", null);

        var costs = new OperationsReportReader().ReadCosts(farmId, cropCycleId, startUtc, endUtc.AddTicks(-1), selectedExpenses, selectedHarvests);
        var harvestTotals = selectedHarvests.GroupBy(x => x.Unit, StringComparer.Ordinal)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new WeeklySummaryQuantityTotal(x.Key, x.Sum(row => row.Quantity), x.Select(row => row.Id).Order().ToArray()))
            .ToArray();

        var missing = new List<string>();
        if (sources.Length == 0) missing.Add("no_confirmed_source_records");
        if (pendingTasks.Length == 0) missing.Add("no_pending_tasks");
        if (selectedExpenses.Length == 0) missing.Add("no_expense_records");
        else if (selectedExpenses.Any(x => !x.Quantity.HasValue)) missing.Add("expense_quantity_not_recorded");
        if (selectedHarvests.Length == 0) missing.Add("no_harvest_records");
        if (selectedIrrigation.Length == 0) missing.Add("no_irrigation_records");
        if (selectedApplications.Length == 0) missing.Add("no_input_application_records");
        if (selectedLogbook.Length == 0) missing.Add("no_logbook_records");

        return new WeeklySummaryProjectionResult(WeeklySummaryProjectionStatus.Available, null,
            new WeeklySummaryProjection(farmId, weekAnchorDate, weekStart, weekEnd, startUtc, endUtc,
                window.TimeZoneId, cropCycleId, sources, pendingTasks, costs.CurrencyTotals, costs.RecordedQuantities,
                harvestTotals, missing));

        static WeeklySummaryProjectionResult Unavailable(string reason) =>
            new(WeeklySummaryProjectionStatus.Unavailable, reason, null);
    }

    private static DateOnly LocalDate(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);
}

public sealed record WeeklySummarySource(Guid Id, string Kind, DateOnly LocalDate, Guid? CropCycleId,
    decimal? Amount, string? CurrencyCode, decimal? Quantity, string? Unit);

public sealed record WeeklySummaryPendingTask(Guid Id, string Status, DateOnly DueDate, string? OperationKind);

public sealed record WeeklySummaryQuantityTotal(string Unit, decimal Quantity, IReadOnlyList<Guid> SourceIds);
