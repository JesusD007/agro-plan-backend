using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AgroPlan.Api.Authorization;
using AgroPlan.Domain.Operations;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Api.Controllers;

[ApiController]
[Route("api/farms/{farmId:guid}/irrigation-entries")]
[Authorize(Policy = FarmMembershipRequirement.PolicyName)]
public sealed class IrrigationEntriesController(AgroPlanDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<IrrigationEntryResponse>>> List(Guid farmId, CancellationToken ct)
    {
        var rows = await db.IrrigationEntries.AsNoTracking().Where(x => x.FarmId == farmId)
            .OrderByDescending(x => x.OccurredAt).Take(500).ToListAsync(ct);
        return Ok(rows.Select(ToResponse).ToArray());
    }

    [HttpPost]
    public async Task<ActionResult<IrrigationEntryResponse>> Append(Guid farmId, IrrigationEntryRequest request, CancellationToken ct)
    {
        if (request.Method is null) return BadRequest(new { message = "Irrigation method is required." });
        var actor = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actor is null) return Unauthorized();
        IrrigationEntry entry;
        try { entry = new(Guid.NewGuid(), farmId, request.ParcelId, request.CropCycleId, request.Method.Value,
            request.WaterSource, request.OccurredAt, actor, DateTimeOffset.UtcNow); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        if (!await db.Parcels.AsNoTracking().AnyAsync(x => x.FarmId == farmId && x.Id == entry.ParcelId, ct))
            return BadRequest(new { message = "The parcel does not belong to this farm." });
        if (entry.CropCycleId.HasValue && !await db.CropCycles.AsNoTracking()
                .AnyAsync(x => x.FarmId == farmId && x.ParcelId == entry.ParcelId && x.Id == entry.CropCycleId, ct))
            return BadRequest(new { message = "The crop cycle does not belong to this farm and parcel." });
        db.IrrigationEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), new { farmId }, ToResponse(entry));
    }

    [HttpPost("{entryId:guid}/corrections")]
    public async Task<ActionResult<IrrigationEntryResponse>> Correct(Guid farmId, Guid entryId, CorrectIrrigationEntryRequest request, CancellationToken ct)
    {
        if (request.Method is null) return BadRequest(new { message = "Irrigation method is required." });
        var original = await db.IrrigationEntries.AsNoTracking().SingleOrDefaultAsync(x => x.FarmId == farmId && x.Id == entryId, ct);
        if (original is null) return NotFound();
        var actor = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actor is null) return Unauthorized();
        IrrigationEntry correction;
        try { correction = new(Guid.NewGuid(), original.FarmId, original.ParcelId, original.CropCycleId,
            request.Method.Value, request.WaterSource, request.OccurredAt, actor, DateTimeOffset.UtcNow, original.Id, request.Reason); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        db.IrrigationEntries.Add(correction);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), new { farmId }, ToResponse(correction));
    }

    private static IrrigationEntryResponse ToResponse(IrrigationEntry x) => new(x.Id, x.FarmId, x.ParcelId,
        x.CropCycleId, x.Method, x.WaterSource, x.OccurredAt, x.ActorUserId, x.CreatedAt, x.CorrectsEntryId, x.CorrectionReason);
}

public sealed record IrrigationEntryRequest([param: Required] Guid ParcelId, Guid? CropCycleId,
    [param: Required] IrrigationMethod? Method, [param: Required, StringLength(200, MinimumLength = 1)] string WaterSource,
    [param: Required] DateTimeOffset OccurredAt);
public sealed record CorrectIrrigationEntryRequest([param: Required, StringLength(1000, MinimumLength = 1)] string Reason,
    [param: Required] IrrigationMethod? Method, [param: Required, StringLength(200, MinimumLength = 1)] string WaterSource,
    [param: Required] DateTimeOffset OccurredAt);
public sealed record IrrigationEntryResponse(Guid Id, Guid FarmId, Guid ParcelId, Guid? CropCycleId,
    IrrigationMethod Method, string WaterSource, DateTimeOffset OccurredAt, string ActorUserId, DateTimeOffset CreatedAt,
    Guid? CorrectsEntryId, string? CorrectionReason);
