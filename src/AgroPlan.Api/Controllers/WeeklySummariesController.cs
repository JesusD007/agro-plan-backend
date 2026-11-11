using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using AgroPlan.Api.Authorization;
using AgroPlan.Application.Abstractions;
using AgroPlan.Application.Reports;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.AdvisoryAccess;
using AgroPlan.Domain.Operations;
using AgroPlan.Domain.Planning;
using AgroPlan.Domain.Reports;
using AgroPlan.Domain.Workforce;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Api.Controllers;

[ApiController]
[Route("api/farms/{farmId:guid}/weekly-summaries")]
[Authorize]
[EnableRateLimiting("phase4-summary")]
public sealed class WeeklySummariesController(AgroPlanDbContext db, IFarmAuthorization farmAuthorization,
    IServiceProvider services) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpPost]
    [ProducesResponseType(typeof(WeeklySummaryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<WeeklySummaryResponse>> Create(Guid farmId,
        [FromBody] CreateWeeklySummaryRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        if (request.WeekAnchorDate == default) return BadRequest(new { code = "invalid_week_anchor", message = "A week anchor date is required." });

        var role = await farmAuthorization.GetActiveRoleAsync(farmId, userId, ct);
        if (role is not (FarmRole.Owner or FarmRole.Administrator)) return Forbid();
        if (request.CropCycleId.HasValue && !await db.CropCycles.AsNoTracking()
                .AnyAsync(x => x.FarmId == farmId && x.Id == request.CropCycleId.Value, ct))
            return NotFound(new { code = "crop_cycle_not_found" });

        var settings = await db.FarmWorkerSettings.AsNoTracking()
            .Where(x => x.FarmId == farmId)
            .Select(x => x.TimeZoneId)
            .SingleOrDefaultAsync(ct);
        var window = WeeklySummaryProjection.ResolveWeekWindow(request.WeekAnchorDate, settings);
        if (window.Status != WeeklySummaryProjectionStatus.Available || window.TimeZoneId is null)
            return UnprocessableEntity(new { code = window.ErrorCode ?? "farm_time_zone_unavailable", message = "Configure a valid farm time zone before requesting a weekly summary." });

        // Membership is re-read immediately before loading any source rows.
        role = await farmAuthorization.GetActiveRoleAsync(farmId, userId, ct);
        if (role is not (FarmRole.Owner or FarmRole.Administrator)) return Forbid();

        var farmFilter = db.ExpenseEntries.AsNoTracking().Where(x => x.FarmId == farmId && x.OccurredAt >= window.WindowStartUtc &&
            x.OccurredAt < window.WindowEndUtcExclusive && (!request.CropCycleId.HasValue || x.CropCycleId == request.CropCycleId));
        var expenses = await farmFilter.Where(x => !db.ExpenseEntries.Any(corrected =>
                corrected.FarmId == farmId && corrected.CorrectsEntryId == x.Id))
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).Take(WeeklySummaryProjection.MaximumRows + 1).ToArrayAsync(ct);

        var harvests = await db.HarvestEntries.AsNoTracking().Where(x => x.FarmId == farmId &&
                x.HarvestedAt >= window.WindowStartUtc && x.HarvestedAt < window.WindowEndUtcExclusive &&
                (!request.CropCycleId.HasValue || x.CropCycleId == request.CropCycleId) &&
                !db.HarvestEntries.Any(corrected => corrected.FarmId == farmId && corrected.CorrectsEntryId == x.Id))
            .OrderBy(x => x.HarvestedAt).ThenBy(x => x.Id).Take(WeeklySummaryProjection.MaximumRows + 1).ToArrayAsync(ct);

        var irrigation = await db.IrrigationEntries.AsNoTracking().Where(x => x.FarmId == farmId &&
                x.OccurredAt >= window.WindowStartUtc && x.OccurredAt < window.WindowEndUtcExclusive &&
                (!request.CropCycleId.HasValue || x.CropCycleId == request.CropCycleId) &&
                !db.IrrigationEntries.Any(corrected => corrected.FarmId == farmId && corrected.CorrectsEntryId == x.Id))
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).Take(WeeklySummaryProjection.MaximumRows + 1).ToArrayAsync(ct);

        var applications = await db.InputApplicationEntries.AsNoTracking().Where(x => x.FarmId == farmId &&
                x.AppliedAt >= window.WindowStartUtc && x.AppliedAt < window.WindowEndUtcExclusive &&
                (!request.CropCycleId.HasValue || x.CropCycleId == request.CropCycleId) &&
                !db.InputApplicationEntries.Any(corrected => corrected.FarmId == farmId && corrected.CorrectsEntryId == x.Id))
            .OrderBy(x => x.AppliedAt).ThenBy(x => x.Id).Take(WeeklySummaryProjection.MaximumRows + 1).ToArrayAsync(ct);

        var logbook = await db.LogbookEntries.AsNoTracking().Where(x => x.FarmId == farmId &&
                x.OccurredAt >= window.WindowStartUtc && x.OccurredAt < window.WindowEndUtcExclusive &&
                (!request.CropCycleId.HasValue || x.CropCycleId == request.CropCycleId) &&
                !db.LogbookEntries.Any(corrected => corrected.FarmId == farmId && corrected.CorrectsEntryId == x.Id))
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).Take(WeeklySummaryProjection.MaximumRows + 1).ToArrayAsync(ct);

        var pendingTasks = await db.WorkTasks.AsNoTracking().Where(x => x.FarmId == farmId &&
                x.DueDate >= window.WeekStartDate && x.DueDate <= window.WeekEndDate &&
                (x.Status == WorkTaskStatus.Pending || x.Status == WorkTaskStatus.InProgress ||
                 x.Status == WorkTaskStatus.Submitted || x.Status == WorkTaskStatus.CorrectionRequested) &&
                (!request.CropCycleId.HasValue || x.CropCycleId == request.CropCycleId))
            .OrderBy(x => x.DueDate).ThenBy(x => x.Id).Take(WeeklySummaryProjection.MaximumRows + 1).ToArrayAsync(ct);

        var rowCount = expenses.Length + harvests.Length + irrigation.Length + applications.Length + logbook.Length + pendingTasks.Length;
        if (rowCount > WeeklySummaryProjection.MaximumRows)
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new { code = "projection_limit_exceeded", maximumRows = WeeklySummaryProjection.MaximumRows });

        var result = WeeklySummaryProjection.Create(farmId, request.WeekAnchorDate, window.TimeZoneId, request.CropCycleId,
            expenses, harvests, irrigation, applications, logbook, pendingTasks);
        if (result.Status != WeeklySummaryProjectionStatus.Available || result.Projection is null)
            return UnprocessableEntity(new { code = result.ErrorCode ?? "projection_unavailable" });

        var now = DateTimeOffset.UtcNow;
        var projection = result.Projection;
        var summary = new WeeklySummary(Guid.NewGuid(), farmId, userId, request.WeekAnchorDate,
            projection.WeekStartDate, projection.WeekEndDate, request.CropCycleId, projection.TimeZoneId,
            JsonSerializer.Serialize(projection, JsonOptions), now);
        if (services.GetService<IWeeklySummaryNarrativeProvider>() is not null)
        {
            summary.MarkNarrativePending();
            var eventId = Guid.NewGuid();
            var payload = JsonSerializer.Serialize(new WeeklySummaryNarrativeRequested(eventId, farmId, summary.Id, userId, summary.Version), JsonOptions);
            db.OutboxMessages.Add(new OutboxMessage(eventId, summary.Id, "weekly-summary.narrative.requested", payload, now));
        }
        db.Set<WeeklySummary>().Add(summary);
        await db.SaveChangesAsync(ct);
        var response = ToResponse(summary, projection);
        return CreatedAtAction(nameof(Get), new { farmId, summaryId = summary.Id }, response);
    }

    [HttpGet("{summaryId:guid}")]
    [ProducesResponseType(typeof(WeeklySummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WeeklySummaryResponse>> Get(Guid farmId, Guid summaryId, CancellationToken ct)
    {
        var summary = await db.Set<WeeklySummary>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.FarmId == farmId && x.Id == summaryId, ct);
        if (summary is null) return NotFound();
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        var role = await farmAuthorization.GetActiveRoleAsync(farmId, userId, ct);
        var projection = DeserializeProjection(summary);
        if (projection is null) return InvalidStoredProjection();
        if (role is FarmRole.Owner or FarmRole.Administrator) return Ok(ToResponse(summary, projection));
        if (summary.SharedAdvisorUserId != userId || summary.SharedAt is null) return Forbid();
        var grant = await GetActiveAdvisorGrantAsync(farmId, userId, ct);
        if (grant is null) return Forbid();
        var advisorProjection = FilterForAdvisor(projection, grant.Scopes);
        grant.RecordUse(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);
        return Ok(new AdvisorWeeklySummaryResponse(summary.Id, summary.WeekStartDate, summary.WeekEndDate,
            summary.TimeZoneId, summary.NarrativeStatus.ToString(), null, advisorProjection));
    }

    [HttpPost("{summaryId:guid}/share")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Share(Guid farmId, Guid summaryId, [FromBody] ShareWeeklySummaryRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        var role = await farmAuthorization.GetActiveRoleAsync(farmId, userId, ct);
        if (role is not (FarmRole.Owner or FarmRole.Administrator)) return Forbid();
        var summary = await db.Set<WeeklySummary>().SingleOrDefaultAsync(x => x.FarmId == farmId && x.Id == summaryId, ct);
        if (summary is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.AdvisorUserId) || request.AdvisorUserId == userId) return BadRequest();
        var grant = await GetActiveAdvisorGrantAsync(farmId, request.AdvisorUserId, ct);
        if (grant is null) return Forbid();
        role = await farmAuthorization.GetActiveRoleAsync(farmId, userId, ct);
        if (role is not (FarmRole.Owner or FarmRole.Administrator)) return Forbid();
        summary.ShareWithAdvisor(request.AdvisorUserId, userId, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("{summaryId:guid}/sources/{sourceId:guid}")]
    public async Task<ActionResult<WeeklySummarySource>> GetSource(Guid farmId, Guid summaryId, Guid sourceId, CancellationToken ct)
    {
        var summary = await db.Set<WeeklySummary>().AsNoTracking().SingleOrDefaultAsync(x => x.FarmId == farmId && x.Id == summaryId, ct);
        if (summary is null) return NotFound();
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        var role = await farmAuthorization.GetActiveRoleAsync(farmId, userId, ct);
        AdvisorReportScope[] scopes = [];
        if (role is not (FarmRole.Owner or FarmRole.Administrator))
        {
            if (summary.SharedAdvisorUserId != userId || summary.SharedAt is null) return Forbid();
            var grant = await GetActiveAdvisorGrantAsync(farmId, userId, ct);
            if (grant is null) return Forbid();
            scopes = grant.Scopes;
            grant.RecordUse(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(ct);
        }
        var projection = DeserializeProjection(summary);
        if (projection is null) return InvalidStoredProjection();
        var source = projection.Sources.SingleOrDefault(x => x.Id == sourceId);
        if (source is null || (scopes.Length > 0 && !CanAdvisorRead(source, scopes))) return NotFound();
        return Ok(source);
    }

    private async Task<AdvisorAccessGrant?> GetActiveAdvisorGrantAsync(Guid farmId, string advisorUserId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return await db.AdvisorAccessGrants.SingleOrDefaultAsync(x => x.FarmId == farmId && x.AdvisorUserId == advisorUserId &&
            x.RevokedAt == null && x.StartsAt <= now && x.ExpiresAt > now &&
            (x.Scopes.Contains(AdvisorReportScope.Costs) || x.Scopes.Contains(AdvisorReportScope.Production)), ct);
    }

    private static WeeklySummaryProjection? DeserializeProjection(WeeklySummary summary) =>
        JsonSerializer.Deserialize<WeeklySummaryProjection>(summary.ProjectionJson, JsonOptions);

    private ObjectResult InvalidStoredProjection() => Problem(statusCode: StatusCodes.Status500InternalServerError,
        title: "Stored summary projection is invalid.");

    private static WeeklySummaryProjection FilterForAdvisor(WeeklySummaryProjection projection, AdvisorReportScope[] scopes)
    {
        var canReadCosts = scopes.Contains(AdvisorReportScope.Costs);
        var canReadProduction = scopes.Contains(AdvisorReportScope.Production);
        return projection with
        {
            Sources = projection.Sources.Where(source => CanAdvisorRead(source, scopes)).ToArray(),
            PendingTasks = [],
            CostTotals = canReadCosts ? projection.CostTotals : [],
            RecordedQuantities = canReadCosts ? projection.RecordedQuantities : [],
            HarvestTotals = canReadProduction ? projection.HarvestTotals : [],
            MissingData = projection.MissingData.Where(item =>
                (canReadCosts && item.Contains("expense", StringComparison.Ordinal)) ||
                (canReadProduction && item.Contains("harvest", StringComparison.Ordinal))).ToArray()
        };
    }

    private static bool CanAdvisorRead(WeeklySummarySource source, AdvisorReportScope[] scopes) =>
        (scopes.Contains(AdvisorReportScope.Costs) && source.Kind == "expense") ||
        (scopes.Contains(AdvisorReportScope.Production) && (source.Kind == "harvest" || source.Kind == "logbook_planting"));

    private string? CurrentUserId() => User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

    private static WeeklySummaryResponse ToResponse(WeeklySummary summary, WeeklySummaryProjection projection) =>
        new(summary.Id, summary.FarmId, summary.RequestedByUserId, summary.RequestedWeekAnchorDate, summary.WeekStartDate,
            summary.WeekEndDate, summary.CropCycleId, summary.TimeZoneId, summary.CreatedAt,
            summary.NarrativeStatus.ToString(), summary.NarrativeText, projection);
}

public sealed record CreateWeeklySummaryRequest([param: Required] DateOnly WeekAnchorDate, Guid? CropCycleId);

public sealed record WeeklySummaryResponse(Guid Id, Guid FarmId, string RequestedByUserId, DateOnly RequestedWeekAnchorDate,
    DateOnly WeekStartDate, DateOnly WeekEndDate, Guid? CropCycleId, string TimeZoneId, DateTimeOffset CreatedAt,
    string NarrativeStatus, string? NarrativeText, WeeklySummaryProjection Projection);
public sealed record ShareWeeklySummaryRequest([param: Required, StringLength(450, MinimumLength = 1)] string AdvisorUserId);
public sealed record AdvisorWeeklySummaryResponse(Guid Id, DateOnly WeekStartDate, DateOnly WeekEndDate, string TimeZoneId,
    string NarrativeStatus, string? NarrativeText, WeeklySummaryProjection Projection);
