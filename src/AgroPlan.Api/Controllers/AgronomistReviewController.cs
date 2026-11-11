using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AgroPlan.Application.Abstractions;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Symptoms;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/farms/{farmId:guid}/symptom-reports")]
public sealed class AgronomistReviewController(AgroPlanDbContext db, IFarmAuthorization authorization) : ControllerBase
{
    [HttpPost("{reportId:guid}/review")]
    public async Task<IActionResult> Review(Guid farmId, Guid reportId, ReviewSymptomReportRequest request, CancellationToken ct)
    {
        var actor = CurrentUserId();
        if (actor is null) return Unauthorized();
        if (!await IsAuthorizedAgronomistAsync(farmId, actor, ct)) return Forbid();
        var report = await db.Set<SymptomReport>().Include(x => x.Photos).Include(x => x.Decisions)
            .SingleOrDefaultAsync(x => x.FarmId == farmId && x.Id == reportId, ct);
        if (report is null) return NotFound();
        if (report.Status != SymptomReportStatus.Pending) return Conflict(new { code = "SYMPTOM_REPORT_NOT_REVIEWABLE" });
        if (!Enum.IsDefined(request.Action) || request.RequestedEvidence?.Length > 2000 ||
            request.Diagnosis?.Length > 2000 || request.Treatment?.Length > 4000) return BadRequest();
        try
        {
            report.AddDecision(new SymptomReportDecision(Guid.NewGuid(), request.Action, actor, DateTimeOffset.UtcNow,
                request.RequestedEvidence, request.Diagnosis, request.Treatment));
            await db.SaveChangesAsync(ct);
        }
        catch (InvalidOperationException exception) { return Conflict(new { code = "SYMPTOM_REVIEW_INVALID", message = exception.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { code = "SYMPTOM_REPORT_STALE" }); }
        return Ok(new SymptomReviewResponse(report.Id, report.Status.ToString(), report.VisibleSigns, report.Uncertainty,
            report.ImageSufficient, report.RequestedEvidence, report.HumanDiagnosis, report.HumanTreatment));
    }

    [HttpPost("agronomist-authorizations")]
    public async Task<IActionResult> AuthorizeAgronomist(Guid farmId, SetFarmAgronomistAuthorizationRequest request, CancellationToken ct)
    {
        var actor = CurrentUserId();
        if (actor is null) return Unauthorized();
        var role = await authorization.GetActiveRoleAsync(farmId, actor, ct);
        if (role is not (FarmRole.Owner or FarmRole.Administrator)) return Forbid();
        var verified = await db.Set<TrustedAgronomistVerification>().AnyAsync(x => x.UserId == request.UserId && x.RevokedAt == null, ct);
        if (!verified) return Conflict(new { code = "AGRONOMIST_NOT_VERIFIED" });
        var existing = await db.Set<FarmAgronomistAuthorization>().SingleOrDefaultAsync(x => x.FarmId == farmId && x.AgronomistUserId == request.UserId, ct);
        if (existing is not null) return existing.IsActive ? NoContent() : Conflict(new { code = "AGRONOMIST_AUTHORIZATION_REVOKED" });
        db.Add(new FarmAgronomistAuthorization(farmId, request.UserId, actor, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("agronomist-authorizations/{userId}")]
    public async Task<IActionResult> RevokeAgronomist(Guid farmId, string userId, CancellationToken ct)
    {
        var actor = CurrentUserId();
        if (actor is null) return Unauthorized();
        var role = await authorization.GetActiveRoleAsync(farmId, actor, ct);
        if (role is not (FarmRole.Owner or FarmRole.Administrator)) return Forbid();
        var assignment = await db.Set<FarmAgronomistAuthorization>().SingleOrDefaultAsync(x => x.FarmId == farmId && x.AgronomistUserId == userId, ct);
        if (assignment is null) return NotFound();
        assignment.Revoke(actor, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("{reportId:guid}/review")]
    public async Task<IActionResult> GetReviewedReport(Guid farmId, Guid reportId, CancellationToken ct)
    {
        var actor = CurrentUserId();
        if (actor is null) return Unauthorized();
        if (!await IsAuthorizedAgronomistAsync(farmId, actor, ct)) return Forbid();
        var report = await db.Set<SymptomReport>().AsNoTracking().SingleOrDefaultAsync(x => x.FarmId == farmId && x.Id == reportId, ct);
        return report is null ? NotFound() : Ok(new SymptomReviewResponse(report.Id, report.Status.ToString(), report.VisibleSigns,
            report.Uncertainty, report.ImageSufficient, report.RequestedEvidence, report.HumanDiagnosis, report.HumanTreatment));
    }

    private string? CurrentUserId() => User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
    private async Task<bool> IsAuthorizedAgronomistAsync(Guid farmId, string actor, CancellationToken ct) =>
        await db.Set<TrustedAgronomistVerification>().AsNoTracking().AnyAsync(x => x.UserId == actor && x.RevokedAt == null, ct) &&
        await db.Set<FarmAgronomistAuthorization>().AsNoTracking().AnyAsync(x => x.FarmId == farmId &&
            x.AgronomistUserId == actor && x.RevokedAt == null, ct);
}

public sealed record SetFarmAgronomistAuthorizationRequest([param: Required] string UserId);
public sealed record ReviewSymptomReportRequest(SymptomReviewAction Action, string? RequestedEvidence, string? Diagnosis, string? Treatment);
public sealed record SymptomReviewResponse(Guid Id, string Status, string? VisibleSigns, string? Uncertainty,
    bool? ImageSufficient, string[] RequestedEvidence, string? HumanDiagnosis, string? HumanTreatment);
