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
[Route("api/farms/{farmId:guid}/input-applications")]
[Authorize(Policy = FarmMembershipRequirement.PolicyName)]
public sealed class InputApplicationsController(AgroPlanDbContext db) : ControllerBase
{
    private const string Jurisdiction = "DO";

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InputApplicationResponse>>> List(Guid farmId, CancellationToken ct)
    {
        var rows = await db.InputApplicationEntries.AsNoTracking().Where(x => x.FarmId == farmId)
            .OrderByDescending(x => x.AppliedAt).Take(500).ToListAsync(ct);
        return Ok(rows.Select(x => ToResponse(x, Unavailable("Assessment is only calculated when an application is recorded."))).ToArray());
    }

    [HttpPost]
    public async Task<ActionResult<InputApplicationResponse>> Append(Guid farmId, InputApplicationRequest request, CancellationToken ct)
    {
        if (request.Method is null) return BadRequest(new { message = "Application method is required." });
        var actor = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actor is null) return Unauthorized();
        InputApplicationEntry entry;
        try { entry = Create(farmId, request, actor, DateTimeOffset.UtcNow); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        if (!await db.Parcels.AsNoTracking().AnyAsync(x => x.FarmId == farmId && x.Id == entry.ParcelId, ct))
            return BadRequest(new { message = "The parcel does not belong to this farm." });
        if (entry.CropCycleId.HasValue && !await db.CropCycles.AsNoTracking()
                .AnyAsync(x => x.FarmId == farmId && x.ParcelId == entry.ParcelId && x.Id == entry.CropCycleId, ct))
            return BadRequest(new { message = "The crop cycle does not belong to this farm and parcel." });

        var matchingRows = await db.InputApplicationEntries.AsNoTracking().Where(x => x.FarmId == farmId &&
            x.ProductRegistrationId == entry.ProductRegistrationId && x.FormulationId == entry.FormulationId &&
            x.CropId == entry.CropId && x.IntendedUseId == entry.IntendedUseId).ToListAsync(ct);
        var matchingRules = await db.ValidatedApplicationFrequencyRules.AsNoTracking().Where(x =>
            x.ProductRegistrationId == entry.ProductRegistrationId && x.FormulationId == entry.FormulationId &&
            x.CropId == entry.CropId && x.IntendedUseId == entry.IntendedUseId && x.Jurisdiction == Jurisdiction).ToListAsync(ct);
        var assessment = ApplicationFrequencyEvaluator.Assess(entry, matchingRows, matchingRules, DateTimeOffset.UtcNow);
        db.InputApplicationEntries.Add(entry);
        ConfirmedRecordOutbox.Add(db, farmId, entry.ParcelId, entry.CropCycleId, "input-application", entry.Id, entry.AppliedAt);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), new { farmId }, ToResponse(entry, assessment));
    }

    [HttpPost("{entryId:guid}/corrections")]
    public async Task<ActionResult<InputApplicationResponse>> Correct(Guid farmId, Guid entryId,
        CorrectInputApplicationRequest request, CancellationToken ct)
    {
        if (request.Method is null) return BadRequest(new { message = "Application method is required." });
        var original = await db.InputApplicationEntries.AsNoTracking().SingleOrDefaultAsync(x => x.FarmId == farmId && x.Id == entryId, ct);
        if (original is null) return NotFound();
        var actor = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actor is null) return Unauthorized();
        InputApplicationEntry correction;
        try { correction = new(Guid.NewGuid(), original.FarmId, original.ParcelId, original.CropCycleId,
            request.ProductRegistrationId, request.FormulationId, request.CropId, request.IntendedUseId,
            request.AppliedAt, request.Method.Value, request.Quantity, request.Unit, actor, DateTimeOffset.UtcNow,
            original.Id, request.Reason, request.CitedLabelSource, request.CitedLabelVersion); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        var matchingRows = await db.InputApplicationEntries.AsNoTracking().Where(x => x.FarmId == farmId &&
            x.ProductRegistrationId == correction.ProductRegistrationId && x.FormulationId == correction.FormulationId &&
            x.CropId == correction.CropId && x.IntendedUseId == correction.IntendedUseId).ToListAsync(ct);
        var matchingRules = await db.ValidatedApplicationFrequencyRules.AsNoTracking().Where(x =>
            x.ProductRegistrationId == correction.ProductRegistrationId && x.FormulationId == correction.FormulationId &&
            x.CropId == correction.CropId && x.IntendedUseId == correction.IntendedUseId && x.Jurisdiction == Jurisdiction).ToListAsync(ct);
        var assessment = ApplicationFrequencyEvaluator.Assess(correction, matchingRows, matchingRules, DateTimeOffset.UtcNow);
        db.InputApplicationEntries.Add(correction);
        ConfirmedRecordOutbox.Add(db, farmId, correction.ParcelId, correction.CropCycleId, "input-application", correction.Id, correction.AppliedAt, 2);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), new { farmId }, ToResponse(correction, assessment));
    }

    private static InputApplicationEntry Create(Guid farmId, InputApplicationRequest request, string actor, DateTimeOffset now) =>
        new(Guid.NewGuid(), farmId, request.ParcelId, request.CropCycleId, request.ProductRegistrationId,
            request.FormulationId, request.CropId, request.IntendedUseId, request.AppliedAt,
            request.Method!.Value, request.Quantity, request.Unit, actor, now,
            citedLabelSource: request.CitedLabelSource, citedLabelVersion: request.CitedLabelVersion);

    private static ApplicationFrequencyAssessment Unavailable(string reason) =>
        new(ApplicationFrequencyStatus.Unavailable, null, null, null, null, reason);

    private static InputApplicationResponse ToResponse(InputApplicationEntry x, ApplicationFrequencyAssessment assessment) =>
        new(x.Id, x.FarmId, x.ParcelId, x.CropCycleId, x.ProductRegistrationId, x.FormulationId, x.CropId,
            x.IntendedUseId, x.AppliedAt, x.Method, x.Quantity, x.Unit, x.ActorUserId, x.CreatedAt,
            x.CorrectsEntryId, x.CorrectionReason, x.CitedLabelSource, x.CitedLabelVersion, "unverified",
            assessment.Status.ToString().ToLowerInvariant(), assessment.ApplicationsInPeriod,
            assessment.Source, assessment.SourceVersion, assessment.RuleValidUntil, assessment.MissingEvidenceReason);
}

public sealed record InputApplicationRequest([param: Required] Guid ParcelId, Guid? CropCycleId,
    [param: Required, StringLength(120, MinimumLength = 1)] string ProductRegistrationId,
    [param: Required, StringLength(120, MinimumLength = 1)] string FormulationId,
    [param: Required, StringLength(120, MinimumLength = 1)] string CropId,
    [param: Required, StringLength(120, MinimumLength = 1)] string IntendedUseId,
    [param: Required] DateTimeOffset AppliedAt, [param: Required] InputApplicationMethod? Method,
    [param: Required, Range(typeof(decimal), "0", "99999999999999")] decimal Quantity,
    [param: Required, StringLength(64, MinimumLength = 1)] string Unit,
    [StringLength(500)] string? CitedLabelSource = null, [StringLength(200)] string? CitedLabelVersion = null);
public sealed record CorrectInputApplicationRequest([param: Required, StringLength(1000, MinimumLength = 1)] string Reason,
    [param: Required, StringLength(120, MinimumLength = 1)] string ProductRegistrationId,
    [param: Required, StringLength(120, MinimumLength = 1)] string FormulationId,
    [param: Required, StringLength(120, MinimumLength = 1)] string CropId,
    [param: Required, StringLength(120, MinimumLength = 1)] string IntendedUseId,
    [param: Required] DateTimeOffset AppliedAt, [param: Required] InputApplicationMethod? Method,
    [param: Required, Range(typeof(decimal), "0", "99999999999999")] decimal Quantity,
    [param: Required, StringLength(64, MinimumLength = 1)] string Unit,
    [StringLength(500)] string? CitedLabelSource = null, [StringLength(200)] string? CitedLabelVersion = null);
public sealed record InputApplicationResponse(Guid Id, Guid FarmId, Guid ParcelId, Guid? CropCycleId,
    string ProductRegistrationId, string FormulationId, string CropId, string IntendedUseId,
    DateTimeOffset AppliedAt, InputApplicationMethod Method, decimal Quantity, string Unit,
    string ActorUserId, DateTimeOffset CreatedAt, Guid? CorrectsEntryId, string? CorrectionReason,
    string? CitedLabelSource, string? CitedLabelVersion, string LabelEvidenceStatus,
    string FrequencyStatus, int? ApplicationsInPeriod, string? FrequencyRuleSource, string? FrequencyRuleVersion,
    DateTimeOffset? FrequencyRuleValidUntil, string? FrequencyMissingEvidenceReason);
