using AgroPlan.Domain.Calendars;

namespace AgroPlan.Application.Calendars;

public static class CropCalendarEvaluator
{
    public static CropCalendarEvaluation Evaluate(Guid cropCatalogEntryId, DateOnly? plantingDate,
        CropCalendarTemplate? template, IReadOnlyCollection<CalendarProgressEvent> progress,
        DateTimeOffset asOf, string? region = null, string? cultivationMethod = null)
    {
        var missing = new List<string>();
        var validTemplate = template is not null && template.IsUsableAt(asOf, cropCatalogEntryId, region, cultivationMethod);
        if (!validTemplate) missing.Add(template is null ? "validated_crop_calendar_rule" : "current_matching_crop_calendar_rule");
        if (!plantingDate.HasValue) missing.Add("confirmed_planting_date");

        var activeProgress = RemoveCorrected(progress).Where(item => item.OccurredAt <= asOf).ToArray();
        if (activeProgress.Length == 0) missing.Add("confirmed_crop_cycle_progress");

        if (!validTemplate || template is null)
            return Unavailable(missing);

        var tasks = new List<CalendarTask>();
        if (plantingDate.HasValue)
        {
            foreach (var task in template.Tasks.OrderBy(item => item.Order))
            {
                var due = plantingDate.Value.AddDays(task.DaysAfterPlanting);
                var evidence = activeProgress.Where(item => item.Kind == task.CompletionEvidence &&
                        DateOnly.FromDateTime(item.OccurredAt.UtcDateTime) >= plantingDate.Value)
                    .OrderBy(item => item.OccurredAt).FirstOrDefault();
                var status = evidence is not null ? "completed" : due < DateOnly.FromDateTime(asOf.UtcDateTime) ? "overdue" : "scheduled";
                tasks.Add(new(task.Key, task.Stage, task.Title, task.Instruction, due,
                    task.Order, status, evidence?.Id));
            }
        }

        EstimatedHarvestWindow? window = null;
        var harvestMissing = new List<string>();
        if (!plantingDate.HasValue) harvestMissing.Add("confirmed_planting_date");
        if (!template.HarvestWindowStartDays.HasValue || !template.HarvestWindowEndDays.HasValue)
            harvestMissing.Add("validated_harvest_window_parameters");
        if (activeProgress.Length == 0) harvestMissing.Add("confirmed_crop_cycle_progress");
        if (template.Region is not null && !string.Equals(template.Region, region, StringComparison.OrdinalIgnoreCase))
            harvestMissing.Add("matching_farm_region");
        if (template.CultivationMethod is not null && !string.Equals(template.CultivationMethod, cultivationMethod, StringComparison.OrdinalIgnoreCase))
            harvestMissing.Add("matching_cultivation_method");
        if (plantingDate.HasValue && template.HarvestWindowStartDays.HasValue && template.HarvestWindowEndDays.HasValue &&
            activeProgress.Length > 0 && harvestMissing.Count == 0)
        {
            window = new(plantingDate.Value.AddDays(template.HarvestWindowStartDays.Value),
                plantingDate.Value.AddDays(template.HarvestWindowEndDays.Value), template.Source, template.SourceVersion,
                template.Version, template.EffectiveFrom, template.EffectiveUntil);
        }

        var overallMissing = new List<string>();
        if (tasks.Count == 0) overallMissing.Add("validated_calendar_tasks");
        if (!plantingDate.HasValue) overallMissing.Add("confirmed_planting_date");
        if (activeProgress.Length == 0) overallMissing.Add("confirmed_crop_cycle_progress");
        if (window is null) overallMissing.AddRange(harvestMissing);
        var statusOverall = overallMissing.Count == 0 ? "available" : "partial";
        return new(statusOverall, overallMissing.Distinct(StringComparer.Ordinal).ToArray(), template.Source,
            template.SourceVersion, template.Version, template.EffectiveFrom, template.EffectiveUntil,
            tasks, window, tasks.Where(task => task.Status != "completed").OrderBy(task => task.Order)
                .Select(task => task.Title).ToArray(), ActualHarvestRequiresValidatedPhi: true);
    }

    private static IReadOnlyCollection<CalendarProgressEvent> RemoveCorrected(IReadOnlyCollection<CalendarProgressEvent> events)
    {
        // Correction links are applied by the repository when it forms the event set. Keeping this
        // pure evaluator's input active-only makes it impossible to infer corrections from IDs alone.
        return events;
    }

    private static CropCalendarEvaluation Unavailable(IReadOnlyList<string> missing) =>
        new("unavailable", missing.Distinct(StringComparer.Ordinal).ToArray(), null, null, null,
            null, null, [], null, [], ActualHarvestRequiresValidatedPhi: true);
}
