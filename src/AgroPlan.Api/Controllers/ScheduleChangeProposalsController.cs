using System.Security.Claims;
using AgroPlan.Application.Abstractions;
using AgroPlan.Api.Authorization;
using AgroPlan.Domain.Calendars;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Logbook;
using AgroPlan.Domain.Planning;
using AgroPlan.Infrastructure.Messaging;
using AgroPlan.Infrastructure.Messaging.Events;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AgroPlan.Api.Controllers;

[ApiController]
[Route("api/farms/{farmId:guid}/crop-cycles/{cropCycleId:guid}/schedule-change-proposals")]
[Authorize(Policy = FarmMembershipRequirement.PolicyName)]
public sealed class ScheduleChangeProposalsController(AgroPlanDbContext db, IFarmAuthorization farmAuthorization) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(Guid farmId, Guid cropCycleId, CancellationToken ct)
    {
        var cycle = await db.CropCycles.AsNoTracking().Include(x => x.CropCatalogEntry).SingleOrDefaultAsync(x => x.FarmId == farmId && x.Id == cropCycleId, ct);
        if (cycle is null) return NotFound();
        var now = DateTimeOffset.UtcNow;
        var templates = await db.CropCalendarTemplates.AsNoTracking().Where(x => x.CropCatalogEntryId == cycle.CropCatalogEntryId &&
            x.Status == CropCalendarTemplateStatus.Validated && x.EffectiveFrom <= now && (!x.EffectiveUntil.HasValue || x.EffectiveUntil >= now))
            .ToListAsync(ct);
        templates = templates.Where(x => x.IsUsableAt(now, cycle.CropCatalogEntryId, cycle.CropCatalogEntry.Region, null)).ToList();
        if (templates.Count != 1) return Conflict(new { status = "unavailable", missingInputs = new[] { "current_matching_crop_calendar_rule" } });
        var template = templates[0];
        var sowing = await db.LogbookEntries.AsNoTracking().Where(x => x.FarmId == farmId && x.CropCycleId == cropCycleId && x.ActivityType == LogbookActivityType.Sowing)
            .OrderBy(x => x.OccurredAt).FirstOrDefaultAsync(ct);
        if (sowing is null) return Conflict(new { status = "unavailable", missingInputs = new[] { "confirmed_planting_date" } });
        var userId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        // Initial schedule deltas are computed exclusively from the validated template and confirmed planting record.
        var currentSchedule = await db.CalendarScheduleItems.AsNoTracking().Where(x => x.FarmId == farmId && x.CropCycleId == cropCycleId)
            .ToDictionaryAsync(x => x.TaskKey, ct);
        var deltas = template.Tasks.Where(x => x.CompletionEvidence != CropCalendarOperationKind.Harvest)
            .OrderBy(x => x.Order).Select(x =>
            {
                var proposedDate = DateOnly.FromDateTime(sowing.OccurredAt.UtcDateTime).AddDays(x.DaysAfterPlanting);
                currentSchedule.TryGetValue(x.Key, out var current);
                return current?.DueDate == proposedDate ? null : new ScheduleTaskDelta(x.Key, current?.DueDate, proposedDate, [template.Id.ToString("N")]);
            }).Where(x => x is not null).Select(x => x!).ToArray();
        if (deltas.Length == 0) return Conflict(new { status = "unavailable", missingInputs = new[] { "deterministic_schedule_delta" } });
        var evidenceVersion = $"{template.Version}:{template.SourceVersion}:{template.EffectiveFrom:O}:{template.EffectiveUntil:O}";
        var proposal = new ScheduleChangeProposal(Guid.NewGuid(), farmId, cropCycleId, 1, evidenceVersion, deltas,
            [template.Id.ToString("N"), template.SourceVersion, sowing.Id.ToString("N")], now, now.AddDays(7));
        var message = new OutboxMessage(Guid.NewGuid(), proposal.Id, "schedule-change.requested",
            JsonSerializer.Serialize(new ScheduleChangeRequested(proposal.Id, proposal.Version), new JsonSerializerOptions(JsonSerializerDefaults.Web)), now);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.ScheduleChangeProposals.Add(proposal); db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return AcceptedAtAction(nameof(Get), new { farmId, cropCycleId, proposalId = proposal.Id }, ToResponse(proposal));
    }

    [HttpGet("{proposalId:guid}")]
    public async Task<IActionResult> Get(Guid farmId, Guid cropCycleId, Guid proposalId, CancellationToken ct)
    {
        var p = await db.ScheduleChangeProposals.AsNoTracking().SingleOrDefaultAsync(x => x.FarmId == farmId && x.CropCycleId == cropCycleId && x.Id == proposalId, ct);
        return p is null ? NotFound() : Ok(ToResponse(p));
    }

    [HttpPost("{proposalId:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid farmId, Guid cropCycleId, Guid proposalId, ConfirmScheduleProposalRequest request, CancellationToken ct)
    {
        var user = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(user)) return Unauthorized();
        if (await farmAuthorization.GetActiveRoleAsync(farmId, user, ct) is not (FarmRole.Owner or FarmRole.Administrator)) return Forbid();
        var p = await db.ScheduleChangeProposals.SingleOrDefaultAsync(x => x.FarmId == farmId && x.CropCycleId == cropCycleId && x.Id == proposalId, ct);
        if (p is null) return NotFound();
        var cycle = await db.CropCycles.AsNoTracking().Include(x => x.CropCatalogEntry).SingleOrDefaultAsync(x => x.FarmId == farmId && x.Id == cropCycleId, ct);
        var now = DateTimeOffset.UtcNow;
        List<CropCalendarTemplate> templates = cycle is null ? new List<CropCalendarTemplate>() : await db.CropCalendarTemplates.AsNoTracking().Where(x => x.CropCatalogEntryId == cycle.CropCatalogEntryId &&
            x.Status == CropCalendarTemplateStatus.Validated && x.EffectiveFrom <= now && (!x.EffectiveUntil.HasValue || x.EffectiveUntil >= now)).ToListAsync(ct);
        if (cycle is not null) templates = templates.Where(x => x.IsUsableAt(now, cycle.CropCatalogEntryId, cycle.CropCatalogEntry.Region, null)).ToList();
        if (templates.Count != 1) return Conflict(new { code = "EVIDENCE_STALE" });
        var currentEvidence = $"{templates[0].Version}:{templates[0].SourceVersion}:{templates[0].EffectiveFrom:O}:{templates[0].EffectiveUntil:O}";
        var sowingRows = await db.LogbookEntries.AsNoTracking().Where(x => x.FarmId == farmId && x.CropCycleId == cropCycleId && x.ActivityType == LogbookActivityType.Sowing)
            .Select(x => new { x.Id, x.CorrectsEntryId, x.OccurredAt }).ToListAsync(ct);
        var corrected = sowingRows.Where(x => x.CorrectsEntryId.HasValue).Select(x => x.CorrectsEntryId!.Value).ToHashSet();
        var activeSowingIds = sowingRows.Where(x => !corrected.Contains(x.Id)).Select(x => x.Id.ToString("N"));
        if (!activeSowingIds.Any(id => p.EvidenceIds.Contains(id, StringComparer.Ordinal))) return Conflict(new { code = "PLANTING_EVIDENCE_STALE" });
        if (!p.Confirm(request.Version, currentEvidence, user, now)) return Conflict(new { code = "PROPOSAL_STALE_OR_NOT_READY" });
        var activeSowing = sowingRows.Where(x => !corrected.Contains(x.Id)).OrderBy(x => x.OccurredAt).FirstOrDefault();
        if (activeSowing is null) return Conflict(new { code = "PLANTING_EVIDENCE_STALE" });
        var sowingAt = activeSowing.OccurredAt;
        var templateTasks = templates[0].Tasks.ToDictionary(x => x.Key, StringComparer.Ordinal);
        foreach (var delta in p.Deltas)
        {
            if (!templateTasks.TryGetValue(delta.TaskKey, out var task) || task.CompletionEvidence == CropCalendarOperationKind.Harvest ||
                DateOnly.FromDateTime(sowingAt.UtcDateTime).AddDays(task.DaysAfterPlanting) != delta.ProposedDate)
                return Conflict(new { code = "DETERMINISTIC_DELTA_STALE" });
        }
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        foreach (var delta in p.Deltas)
        {
            var item = await db.CalendarScheduleItems.SingleOrDefaultAsync(x => x.FarmId == farmId && x.CropCycleId == cropCycleId && x.TaskKey == delta.TaskKey, ct);
            if (item is null)
            {
                item = new CalendarScheduleItem(farmId, cropCycleId, delta.TaskKey, delta.ProposedDate, now);
                item.Apply(p.Id, delta.ProposedDate, now);
                db.CalendarScheduleItems.Add(item);
            }
            else item.Apply(p.Id, delta.ProposedDate, now);
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(ToResponse(p));
    }

    private static object ToResponse(ScheduleChangeProposal p) => new { p.Id, p.FarmId, p.CropCycleId, p.Version, p.EvidenceVersion,
        status = p.Status.ToString().ToLowerInvariant(), p.Deltas, p.EvidenceIds, p.ExplanationJson, p.ExplanationProvider, p.ExplanationModel,
        p.ExplanationPromptVersion, p.ExplanationSchemaVersion, p.FailureCode, p.CreatedAt, p.ExpiresAt, p.ConfirmedAt, p.ConfirmedBy, p.StatusVersion };
}

public sealed record ConfirmScheduleProposalRequest(int Version);
