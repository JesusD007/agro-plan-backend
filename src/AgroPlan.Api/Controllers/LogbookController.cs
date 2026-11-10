using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AgroPlan.Api.Authorization;
using AgroPlan.Application.UseCases;
using AgroPlan.Domain.Logbook;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Api.Controllers;

[ApiController]
[Route("api/farms/{farmId:guid}/logbook")]
[Authorize(Policy = FarmMembershipRequirement.PolicyName)]
public sealed class LogbookController(AgroPlanDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LogbookEntryResponse>>> List(Guid farmId, CancellationToken ct)
    {
        var rows = await db.LogbookEntries.AsNoTracking().Where(x => x.FarmId == farmId)
            .OrderByDescending(x => x.OccurredAt).Take(500).ToListAsync(ct);
        return Ok(rows.Select(ToResponse).ToArray());
    }

    [HttpPost]
    public async Task<ActionResult<LogbookEntryResponse>> Append(Guid farmId, LogbookEntryRequest request, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.ActivityType)) return BadRequest(new { message = "Unsupported activity type." });
        var actor = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actor is null) return Unauthorized();
        LogbookEntry entry;
        try { entry = new AppendLogbookEntry().Create(farmId, request.ParcelId, request.CropCycleId,
            request.ActivityType, request.OccurredAt, request.Details, actor, DateTimeOffset.UtcNow); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        if (!await db.Parcels.AsNoTracking().AnyAsync(x => x.FarmId == farmId && x.Id == entry.ParcelId, ct))
            return BadRequest(new { message = "The parcel does not belong to this farm." });
        if (entry.CropCycleId.HasValue && !await db.CropCycles.AsNoTracking().AnyAsync(x => x.FarmId == farmId && x.ParcelId == entry.ParcelId && x.Id == entry.CropCycleId, ct))
            return BadRequest(new { message = "The crop cycle does not belong to this farm and parcel." });
        db.LogbookEntries.Add(entry);
        ConfirmedRecordOutbox.Add(db, farmId, entry.ParcelId, entry.CropCycleId,
            entry.ActivityType.ToString().ToLowerInvariant(), entry.Id, entry.OccurredAt);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), new { farmId }, ToResponse(entry));
    }

    [HttpPost("{entryId:guid}/corrections")]
    public async Task<ActionResult<LogbookEntryResponse>> Correct(Guid farmId, Guid entryId, CorrectLogbookEntryRequest request, CancellationToken ct)
    {
        var original = await db.LogbookEntries.AsNoTracking().SingleOrDefaultAsync(x => x.FarmId == farmId && x.Id == entryId, ct);
        if (original is null) return NotFound();
        var actor = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actor is null) return Unauthorized();
        LogbookEntry correction;
        try { correction = new CorrectLogbookEntry().Create(original, request.Reason, request.OccurredAt, request.Details, actor, DateTimeOffset.UtcNow); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        db.LogbookEntries.Add(correction);
        ConfirmedRecordOutbox.Add(db, farmId, correction.ParcelId, correction.CropCycleId,
            correction.ActivityType.ToString().ToLowerInvariant(), correction.Id, correction.OccurredAt, 2);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), new { farmId }, ToResponse(correction));
    }

    private static LogbookEntryResponse ToResponse(LogbookEntry x) => new(x.Id, x.FarmId, x.ParcelId, x.CropCycleId,
        x.ActivityType, x.OccurredAt, x.Details, x.ActorUserId, x.CreatedAt, x.CorrectsEntryId, x.CorrectionReason);
}

public sealed record LogbookEntryRequest([param: Required] Guid ParcelId, Guid? CropCycleId,
    [param: Required] LogbookActivityType ActivityType, [param: Required] DateTimeOffset OccurredAt,
    [param: Required, StringLength(4000, MinimumLength = 1)] string Details);
public sealed record CorrectLogbookEntryRequest([param: Required, StringLength(1000, MinimumLength = 1)] string Reason,
    [param: Required] DateTimeOffset OccurredAt, [param: Required, StringLength(4000, MinimumLength = 1)] string Details);
public sealed record LogbookEntryResponse(Guid Id, Guid FarmId, Guid ParcelId, Guid? CropCycleId, LogbookActivityType ActivityType,
    DateTimeOffset OccurredAt, string Details, string ActorUserId, DateTimeOffset CreatedAt, Guid? CorrectsEntryId, string? CorrectionReason);
