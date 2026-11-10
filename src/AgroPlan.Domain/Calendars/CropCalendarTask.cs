using System.Text.Json;

namespace AgroPlan.Domain.Calendars;

public enum CropCalendarTemplateStatus { Unvalidated, Validated }
public enum CropCalendarOperationKind { Sowing, Irrigation, Application, Expense, Harvest }

/// <summary>A versioned, sourced schedule task in an agronomically validated template.</summary>
public sealed record CropCalendarTask(string Key, string Stage, string Title, string Instruction,
    int DaysAfterPlanting, int Order, CropCalendarOperationKind CompletionEvidence);

/// <summary>
/// Immutable, crop-specific calendar evidence. No templates are seeded by the pilot; callers
/// receive an explicit unavailable result until an approved, current source is loaded.
/// </summary>
public sealed class CropCalendarTemplate
{
    private CropCalendarTemplate() { }

    public CropCalendarTemplate(Guid id, Guid cropCatalogEntryId, string version, string source,
        string sourceVersion, CropCalendarTemplateStatus status, string? region, string? cultivationMethod,
        DateTimeOffset effectiveFrom, DateTimeOffset? effectiveUntil, int? harvestWindowStartDays,
        int? harvestWindowEndDays, IReadOnlyCollection<CropCalendarTask> tasks)
    {
        if (id == Guid.Empty || cropCatalogEntryId == Guid.Empty) throw new ArgumentException("Template and crop ids are required.");
        if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(sourceVersion))
            throw new ArgumentException("Template version and source provenance are required.");
        if (effectiveUntil.HasValue && effectiveUntil.Value < effectiveFrom) throw new ArgumentException("Template validity is invalid.");
        if (harvestWindowStartDays.HasValue != harvestWindowEndDays.HasValue ||
            (harvestWindowStartDays.HasValue && (harvestWindowStartDays < 0 || harvestWindowEndDays < harvestWindowStartDays)))
            throw new ArgumentException("Harvest window boundaries must be a valid pair of days after planting.");
        if (status == CropCalendarTemplateStatus.Validated && tasks.Any(task =>
                string.IsNullOrWhiteSpace(task.Key) || string.IsNullOrWhiteSpace(task.Stage) ||
                string.IsNullOrWhiteSpace(task.Title) || string.IsNullOrWhiteSpace(task.Instruction) || task.DaysAfterPlanting < 0))
            throw new ArgumentException("Validated templates require complete, non-negative task definitions.");

        Id = id; CropCatalogEntryId = cropCatalogEntryId; Version = version.Trim(); Source = source.Trim();
        SourceVersion = sourceVersion.Trim(); Status = status; Region = Normalize(region);
        CultivationMethod = Normalize(cultivationMethod); EffectiveFrom = effectiveFrom; EffectiveUntil = effectiveUntil;
        HarvestWindowStartDays = harvestWindowStartDays; HarvestWindowEndDays = harvestWindowEndDays;
        TasksJson = JsonSerializer.Serialize(tasks.OrderBy(task => task.Order).ToArray());
    }

    public Guid Id { get; private set; }
    public Guid CropCatalogEntryId { get; private set; }
    public string Version { get; private set; } = null!;
    public string Source { get; private set; } = null!;
    public string SourceVersion { get; private set; } = null!;
    public CropCalendarTemplateStatus Status { get; private set; }
    public string? Region { get; private set; }
    public string? CultivationMethod { get; private set; }
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveUntil { get; private set; }
    public int? HarvestWindowStartDays { get; private set; }
    public int? HarvestWindowEndDays { get; private set; }
    public string TasksJson { get; private set; } = "[]";
    public IReadOnlyList<CropCalendarTask> Tasks => JsonSerializer.Deserialize<CropCalendarTask[]>(TasksJson) ?? [];

    public bool IsUsableAt(DateTimeOffset at, Guid cropId, string? region, string? cultivationMethod) =>
        Status == CropCalendarTemplateStatus.Validated && CropCatalogEntryId == cropId &&
        EffectiveFrom <= at && (!EffectiveUntil.HasValue || EffectiveUntil.Value >= at) &&
        (Region is null || string.Equals(Region, region, StringComparison.OrdinalIgnoreCase)) &&
        (CultivationMethod is null || string.Equals(CultivationMethod, cultivationMethod, StringComparison.OrdinalIgnoreCase));

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record CalendarProgressEvent(Guid Id, CropCalendarOperationKind Kind, DateTimeOffset OccurredAt);
public sealed record CalendarTask(string Key, string Stage, string Title, string Instruction,
    DateOnly DueDate, int Order, string Status, Guid? ConfirmedOperationId);
public sealed record EstimatedHarvestWindow(DateOnly StartsOn, DateOnly EndsOn, string Source, string SourceVersion,
    string TemplateVersion, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveUntil);
public sealed record CropCalendarEvaluation(string Status, IReadOnlyList<string> MissingInputs,
    string? RuleSource, string? RuleSourceVersion, string? TemplateVersion,
    DateTimeOffset? EffectiveFrom, DateTimeOffset? EffectiveUntil,
    IReadOnlyList<CalendarTask> Tasks, EstimatedHarvestWindow? EstimatedHarvestWindow,
    IReadOnlyList<string> NextActions, bool ActualHarvestRequiresValidatedPhi);
