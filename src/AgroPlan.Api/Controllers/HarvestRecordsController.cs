using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using AgroPlan.Api.Authorization;
using AgroPlan.Application.Safety;
using AgroPlan.Domain.Operations;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Api.Controllers;

[ApiController]
[Route("api/farms/{farmId:guid}/harvest-records")]
[Authorize(Policy = FarmMembershipRequirement.PolicyName)]
public sealed class HarvestRecordsController(AgroPlanDbContext db) : ControllerBase
{
    private const string Jurisdiction = "DO";

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<HarvestRecordResponse>>> List(Guid farmId, CancellationToken ct)
    {
        var rows = await db.HarvestEntries.AsNoTracking().Where(x => x.FarmId == farmId)
            .OrderByDescending(x => x.HarvestedAt).Take(500).ToListAsync(ct);
        return Ok(rows.Select(ToResponse).ToArray());
    }

    [HttpGet("eligibility")]
    public async Task<ActionResult<HarvestEligibilityResponse>> Eligibility(Guid farmId, Guid cropCycleId,
        DateTimeOffset harvestedAt, CancellationToken ct)
    {
        var cycle = await db.CropCycles.AsNoTracking().SingleOrDefaultAsync(x => x.FarmId == farmId && x.Id == cropCycleId, ct);
        if (cycle is null) return NotFound();
        var assessment = await Assess(farmId, cropCycleId, harvestedAt, ct);
        return Ok(ToEligibilityResponse(assessment));
    }

    [HttpPost]
    public async Task<ActionResult> Append(Guid farmId, HarvestRecordRequest request, CancellationToken ct)
    {
        var parcelOk = await db.Parcels.AsNoTracking().AnyAsync(x => x.FarmId == farmId && x.Id == request.ParcelId, ct);
        var cycle = await db.CropCycles.AsNoTracking().SingleOrDefaultAsync(x => x.FarmId == farmId &&
            x.ParcelId == request.ParcelId && x.Id == request.CropCycleId, ct);
        if (!parcelOk || cycle is null) return BadRequest(new { message = "Parcel and crop cycle must belong to this farm." });
        var assessment = await Assess(farmId, request.CropCycleId, request.HarvestedAt, ct);
        if (!assessment.IsEligible) return Conflict(ToEligibilityResponse(assessment));
        var actor = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actor is null) return Unauthorized();
        var entry = new HarvestEntry(Guid.NewGuid(), farmId, request.ParcelId, request.CropCycleId,
            request.HarvestedAt, request.Quantity, request.Unit, actor, DateTimeOffset.UtcNow,
            JsonSerializer.Serialize(assessment.Evidence));
        db.HarvestEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), new { farmId }, ToResponse(entry));
    }

    [HttpPost("{entryId:guid}/corrections")]
    public async Task<ActionResult> Correct(Guid farmId, Guid entryId, CorrectHarvestRecordRequest request, CancellationToken ct)
    {
        var original = await db.HarvestEntries.AsNoTracking().SingleOrDefaultAsync(x => x.FarmId == farmId && x.Id == entryId, ct);
        if (original is null) return NotFound();
        var assessment = await Assess(farmId, original.CropCycleId, request.HarvestedAt, ct);
        if (!assessment.IsEligible) return Conflict(ToEligibilityResponse(assessment));
        var actor = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actor is null) return Unauthorized();
        HarvestEntry correction;
        try { correction = new HarvestEntry(Guid.NewGuid(), original.FarmId, original.ParcelId, original.CropCycleId,
            request.HarvestedAt, request.Quantity, request.Unit, actor, DateTimeOffset.UtcNow,
            JsonSerializer.Serialize(assessment.Evidence), original.Id, request.Reason); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        db.HarvestEntries.Add(correction);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), new { farmId }, ToResponse(correction));
    }

    private async Task<HarvestEligibilityAssessment> Assess(Guid farmId, Guid cycleId, DateTimeOffset harvestedAt, CancellationToken ct)
    {
        var cycle = await db.CropCycles.AsNoTracking().Include(x => x.CropCatalogEntry)
            .SingleOrDefaultAsync(x => x.FarmId == farmId && x.Id == cycleId, ct);
        if (cycle is null)
            return new(HarvestEligibilityStatus.Blocked, null, [], ["Farm-scoped crop cycle is unavailable."]);
        var applications = await db.InputApplicationEntries.AsNoTracking()
            .Where(x => x.FarmId == farmId && x.CropCycleId == cycleId).ToListAsync(ct);
        var labels = await db.ValidatedProductLabels.AsNoTracking().Where(x => x.Jurisdiction == Jurisdiction).ToListAsync(ct);
        return HarvestEligibilityEvaluator.Assess(cycleId, applications, labels, harvestedAt, Jurisdiction, cycle.CropCatalogEntry.Code);
    }

    private static HarvestEligibilityResponse ToEligibilityResponse(HarvestEligibilityAssessment x) =>
        new(x.Status.ToString().ToLowerInvariant(), x.IsEligible, x.EarliestSafeAt, x.Evidence, x.MissingEvidenceReasons);
    private static HarvestRecordResponse ToResponse(HarvestEntry x) => new(x.Id, x.FarmId, x.ParcelId, x.CropCycleId,
        x.HarvestedAt, x.Quantity, x.Unit, x.ActorUserId, x.CreatedAt, x.AssessmentEvidenceJson, x.CorrectsEntryId, x.CorrectionReason);
}

public sealed record HarvestRecordRequest([param: Required] Guid ParcelId, [param: Required] Guid CropCycleId,
    [param: Required] DateTimeOffset HarvestedAt, [param: Required, Range(typeof(decimal), "0.0001", "99999999999999")] decimal Quantity,
    [param: Required, StringLength(64, MinimumLength = 1)] string Unit);
public sealed record CorrectHarvestRecordRequest([param: Required, StringLength(1000, MinimumLength = 1)] string Reason,
    [param: Required] DateTimeOffset HarvestedAt, [param: Required, Range(typeof(decimal), "0.0001", "99999999999999")] decimal Quantity,
    [param: Required, StringLength(64, MinimumLength = 1)] string Unit);
public sealed record HarvestEligibilityResponse(string Status, bool Eligible, DateTimeOffset? EarliestSafeAt,
    IReadOnlyList<HarvestEvidence> Evidence, IReadOnlyList<string> MissingEvidenceReasons);
public sealed record HarvestRecordResponse(Guid Id, Guid FarmId, Guid ParcelId, Guid CropCycleId,
    DateTimeOffset HarvestedAt, decimal Quantity, string Unit, string ActorUserId, DateTimeOffset CreatedAt,
    string AssessmentEvidenceJson, Guid? CorrectsEntryId, string? CorrectionReason);
