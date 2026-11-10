using AgroPlan.Api.Authorization;
using AgroPlan.Application.Calendars;
using AgroPlan.Domain.Calendars;
using AgroPlan.Domain.Logbook;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Api.Controllers;

[ApiController]
[Route("api/farms/{farmId:guid}/crop-cycles/{cropCycleId:guid}/calendar")]
[Authorize(Policy = FarmMembershipRequirement.PolicyName)]
public sealed class CropCalendarsController(AgroPlanDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CropCalendarEvaluation>> Get(Guid farmId, Guid cropCycleId,
        CancellationToken cancellationToken)
    {
        var cycle = await db.CropCycles.AsNoTracking().Include(item => item.CropCatalogEntry)
            .SingleOrDefaultAsync(item => item.FarmId == farmId && item.Id == cropCycleId, cancellationToken);
        if (cycle is null) return NotFound();

        var now = DateTimeOffset.UtcNow;
        var candidateTemplates = await db.CropCalendarTemplates.AsNoTracking()
            .Where(item => item.CropCatalogEntryId == cycle.CropCatalogEntryId && item.Status == CropCalendarTemplateStatus.Validated &&
                item.EffectiveFrom <= now && (!item.EffectiveUntil.HasValue || item.EffectiveUntil >= now))
            .OrderBy(item => item.Version).ToListAsync(cancellationToken);
        // Ambiguous simultaneously effective versions fail closed. Region/method constrained
        // templates also remain unavailable until those farm facts are modeled and confirmed.
        var template = candidateTemplates.Count == 1 ? candidateTemplates[0] : null;

        var logbook = await db.LogbookEntries.AsNoTracking()
            .Where(item => item.FarmId == farmId && item.CropCycleId == cropCycleId)
            .Select(item => new { item.Id, item.CorrectsEntryId, item.ActivityType, item.OccurredAt })
            .ToListAsync(cancellationToken);
        var activeLogbook = Active(logbook, item => item.Id, item => item.CorrectsEntryId);
        var planting = activeLogbook.Where(item => item.ActivityType == LogbookActivityType.Sowing)
            .OrderBy(item => item.OccurredAt).Select(item => (DateOnly?)DateOnly.FromDateTime(item.OccurredAt.UtcDateTime)).FirstOrDefault();

        var progress = new List<CalendarProgressEvent>();
        progress.AddRange(activeLogbook.Select(item => new CalendarProgressEvent(item.Id,
            item.ActivityType switch
            {
                LogbookActivityType.Sowing => CropCalendarOperationKind.Sowing,
                LogbookActivityType.Irrigation => CropCalendarOperationKind.Irrigation,
                LogbookActivityType.Fumigation => CropCalendarOperationKind.Application,
                LogbookActivityType.Harvest => CropCalendarOperationKind.Harvest,
                _ => CropCalendarOperationKind.Sowing
            }, item.OccurredAt)));

        var irrigations = await db.IrrigationEntries.AsNoTracking().Where(item => item.FarmId == farmId && item.CropCycleId == cropCycleId)
            .Select(item => new { item.Id, item.CorrectsEntryId, item.OccurredAt }).ToListAsync(cancellationToken);
        progress.AddRange(Active(irrigations, item => item.Id, item => item.CorrectsEntryId)
            .Select(item => new CalendarProgressEvent(item.Id, CropCalendarOperationKind.Irrigation, item.OccurredAt)));

        var applications = await db.InputApplicationEntries.AsNoTracking().Where(item => item.FarmId == farmId && item.CropCycleId == cropCycleId)
            .Select(item => new { item.Id, item.CorrectsEntryId, item.AppliedAt }).ToListAsync(cancellationToken);
        progress.AddRange(Active(applications, item => item.Id, item => item.CorrectsEntryId)
            .Select(item => new CalendarProgressEvent(item.Id, CropCalendarOperationKind.Application, item.AppliedAt)));

        var expenses = await db.ExpenseEntries.AsNoTracking().Where(item => item.FarmId == farmId && item.CropCycleId == cropCycleId)
            .Select(item => new { item.Id, item.CorrectsEntryId, item.OccurredAt }).ToListAsync(cancellationToken);
        progress.AddRange(Active(expenses, item => item.Id, item => item.CorrectsEntryId)
            .Select(item => new CalendarProgressEvent(item.Id, CropCalendarOperationKind.Expense, item.OccurredAt)));

        var harvests = await db.HarvestEntries.AsNoTracking().Where(item => item.FarmId == farmId && item.CropCycleId == cropCycleId)
            .Select(item => new { item.Id, item.CorrectsEntryId, item.HarvestedAt }).ToListAsync(cancellationToken);
        progress.AddRange(Active(harvests, item => item.Id, item => item.CorrectsEntryId)
            .Select(item => new CalendarProgressEvent(item.Id, CropCalendarOperationKind.Harvest, item.HarvestedAt)));

        var result = CropCalendarEvaluator.Evaluate(cycle.CropCatalogEntryId, planting, template, progress, now,
            cycle.CropCatalogEntry.Region);
        var confirmedSchedule = await db.CalendarScheduleItems.AsNoTracking().Where(item => item.FarmId == farmId && item.CropCycleId == cropCycleId)
            .ToDictionaryAsync(item => item.TaskKey, item => item.DueDate, cancellationToken);
        return Ok(result with { Tasks = result.Tasks.Select(task => confirmedSchedule.TryGetValue(task.Key, out var dueDate)
            ? task with { DueDate = dueDate } : task).ToArray() });
    }

    private static IReadOnlyList<T> Active<T>(IReadOnlyCollection<T> source, Func<T, Guid> id, Func<T, Guid?> corrects)
    {
        var superseded = source.Select(corrects).Where(value => value.HasValue).Select(value => value!.Value).ToHashSet();
        return source.Where(item => !superseded.Contains(id(item))).ToArray();
    }
}
