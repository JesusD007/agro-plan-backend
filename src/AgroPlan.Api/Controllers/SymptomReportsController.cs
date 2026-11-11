using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using AgroPlan.Application.Abstractions;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Planning;
using AgroPlan.Domain.Symptoms;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/farms/{farmId:guid}/symptom-reports")]
public sealed class SymptomReportsController(AgroPlanDbContext db, IFileStorage storage,
    IFarmAuthorization authorization, IServiceProvider services) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> Types = ["image/jpeg", "image/png", "image/webp"];

    [HttpPost("upload-authorizations")]
    [EnableRateLimiting("phase4-media")]
    public async Task<ActionResult<SymptomUploadAuthorizationResponse>> AuthorizeUpload(Guid farmId,
        CreateSymptomUploadAuthorizationRequest request, CancellationToken ct)
    {
        var actor = CurrentUserId();
        if (actor is null) return Unauthorized();
        var role = await authorization.GetActiveRoleAsync(farmId, actor, ct);
        if (role is not (FarmRole.Owner or FarmRole.Administrator or FarmRole.Worker)) return Forbid();
        if (!Types.Contains(request.ContentType) || request.SizeBytes is <= 0 or > SymptomPhotoInspector.MaximumPhotoBytes)
            return BadRequest(new { code = "SYMPTOM_PHOTO_INVALID" });
        if (request.ExternalProcessingConsent && string.IsNullOrWhiteSpace(request.ConsentNoticeVersion))
            return BadRequest(new { code = "SYMPTOM_CONSENT_NOTICE_REQUIRED" });
        if (request.ConsentNoticeVersion?.Length > 80 || request.Notes?.Length > 2000) return BadRequest();
        if (request.CropCycleId.HasValue && !await db.CropCycles.AsNoTracking().AnyAsync(x => x.FarmId == farmId && x.Id == request.CropCycleId, ct))
            return NotFound();

        var now = DateTimeOffset.UtcNow;
        var report = request.ReportId.HasValue
            ? await db.Set<SymptomReport>().Include(x => x.Photos).SingleOrDefaultAsync(x => x.FarmId == farmId && x.Id == request.ReportId, ct)
            : null;
        if (request.ReportId.HasValue && (report is null || report.SubmittedByUserId != actor || report.Status != SymptomReportStatus.Uploading)) return NotFound();
        if (report is not null && report.CropCycleId != request.CropCycleId) return BadRequest(new { code = "SYMPTOM_CROP_CYCLE_MISMATCH" });
        report ??= new SymptomReport(Guid.NewGuid(), farmId, actor, request.CropCycleId, request.Notes, now);
        if (report.Photos.Count >= SymptomPhotoInspector.MaximumPhotosPerReport) return BadRequest(new { code = "SYMPTOM_PHOTO_LIMIT" });

        var photoId = Guid.NewGuid();
        var expires = now.AddMinutes(10);
        var upload = storage.CreateSymptomReportUploadAuthorization(farmId, report.Id, photoId, request.ContentType, expires);
        report.AddPhoto(new SymptomReportPhoto(photoId, upload.ObjectKey, request.ContentType, request.SizeBytes,
            actor, request.ExternalProcessingConsent, request.ConsentNoticeVersion, now, expires));
        if (db.Entry(report).State == EntityState.Detached) db.Add(report);
        await db.SaveChangesAsync(ct);
        return Ok(new SymptomUploadAuthorizationResponse(report.Id, photoId, upload.UploadUrl, upload.ExpiresAt, request.ContentType, request.SizeBytes));
    }

    [HttpPost]
    [EnableRateLimiting("phase4-media")]
    public async Task<IActionResult> FinalizeReport(Guid farmId, FinalizeSymptomReportRequest request, CancellationToken ct)
    {
        var actor = CurrentUserId();
        if (actor is null) return Unauthorized();
        var role = await authorization.GetActiveRoleAsync(farmId, actor, ct);
        if (role is not (FarmRole.Owner or FarmRole.Administrator or FarmRole.Worker)) return Forbid();
        var report = await db.Set<SymptomReport>().Include(x => x.Photos)
            .SingleOrDefaultAsync(x => x.FarmId == farmId && x.Id == request.ReportId, ct);
        if (report is null || report.SubmittedByUserId != actor) return NotFound();
        if (report.Status != SymptomReportStatus.Uploading) return Conflict(new { code = "SYMPTOM_REPORT_NOT_UPLOADING" });
        var now = DateTimeOffset.UtcNow;
        var aggregatePixels = 0L;
        foreach (var photo in report.Photos)
        {
            if (photo.FinalizedAt.HasValue) { aggregatePixels += SymptomPhotoInspector.MaximumPixelsPerImage; continue; }
            if (photo.ExpiresAt < now) return BadRequest(new { code = "SYMPTOM_UPLOAD_EXPIRED" });
            var metadata = await storage.GetMetadataAsync(photo.UploadObjectKey, ct);
            if (metadata is null || metadata.ContentType != photo.ContentType || metadata.SizeBytes != photo.ExpectedSizeBytes ||
                metadata.SizeBytes is <= 0 or > SymptomPhotoInspector.MaximumPhotoBytes)
                return BadRequest(new { code = "SYMPTOM_PHOTO_METADATA_INVALID" });
            var bytes = await storage.ReadObjectAsync(photo.UploadObjectKey, SymptomPhotoInspector.MaximumPhotoBytes, ct);
            if (bytes is null || bytes.LongLength != photo.ExpectedSizeBytes ||
                !SymptomPhotoInspector.TryInspect(bytes, photo.ContentType, out var dimensions))
                return BadRequest(new { code = "SYMPTOM_PHOTO_FORMAT_INVALID" });
            aggregatePixels += dimensions.Pixels;
            if (aggregatePixels > SymptomPhotoInspector.MaximumPixelsPerReport) return BadRequest(new { code = "SYMPTOM_PIXEL_LIMIT" });
            var finalKey = $"farms/{farmId:D}/symptom-reports/{report.Id:D}/{photo.Id:D}/final";
            var finalized = await storage.FinalizeUploadAsync(photo.UploadObjectKey, finalKey, ct);
            if (finalized is null || finalized.Metadata.ContentType != photo.ContentType || finalized.Metadata.SizeBytes != photo.ExpectedSizeBytes)
                return BadRequest(new { code = "SYMPTOM_PHOTO_FINALIZATION_FAILED" });
            photo.Finalize(finalized.ObjectKey, finalized.Metadata.SizeBytes, now);
        }
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Membership can be revoked while private media is being finalized; re-read it
        // inside the same transaction that changes the report and writes its outbox event.
        role = await authorization.GetActiveRoleAsync(farmId, actor, ct);
        if (role is not (FarmRole.Owner or FarmRole.Administrator or FarmRole.Worker)) return Forbid();
        report.Submit(now);
        if (report.Photos.Any(x => x.ConsentGranted && !x.ConsentRevokedAt.HasValue) &&
            services.GetService<ISymptomObservationProvider>() is not null)
        {
            var id = Guid.NewGuid();
            var payload = JsonSerializer.Serialize(new SymptomObservationRequested(id, farmId, report.Id, report.Version), JsonOptions);
            db.OutboxMessages.Add(new OutboxMessage(id, report.Id, "symptom-observation.requested", payload, now));
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return AcceptedAtAction(nameof(Get), new { farmId, reportId = report.Id }, ToResponse(report));
    }

    [HttpGet("{reportId:guid}")]
    public async Task<IActionResult> Get(Guid farmId, Guid reportId, CancellationToken ct)
    {
        var actor = CurrentUserId();
        if (actor is null) return Unauthorized();
        var role = await authorization.GetActiveRoleAsync(farmId, actor, ct);
        if (role is null && !await IsAuthorizedAgronomistAsync(farmId, actor, ct)) return Forbid();
        var report = await db.Set<SymptomReport>().AsNoTracking().Include(x => x.Photos).Include(x => x.Decisions)
            .SingleOrDefaultAsync(x => x.FarmId == farmId && x.Id == reportId, ct);
        if (report is null || role == FarmRole.Worker && report.SubmittedByUserId != actor) return NotFound();
        return Ok(ToResponse(report));
    }

    [HttpGet("{reportId:guid}/photos/{photoId:guid}")]
    public async Task<IActionResult> GetPhoto(Guid farmId, Guid reportId, Guid photoId, CancellationToken ct)
    {
        var actor = CurrentUserId();
        if (actor is null) return Unauthorized();
        var role = await authorization.GetActiveRoleAsync(farmId, actor, ct);
        if (role is null && !await IsAuthorizedAgronomistAsync(farmId, actor, ct)) return Forbid();
        var report = await db.Set<SymptomReport>().Include(x => x.Photos).SingleOrDefaultAsync(x => x.FarmId == farmId && x.Id == reportId, ct);
        var photo = report?.Photos.SingleOrDefault(x => x.Id == photoId);
        if (report is null || photo is null || role == FarmRole.Worker && report.SubmittedByUserId != actor ||
            report.Status is SymptomReportStatus.Closed or SymptomReportStatus.Rejected || photo.DeletedAt.HasValue || photo.ObjectKey is null)
            return NotFound();
        var url = storage.CreateDownloadAuthorization(photo.ObjectKey, DateTimeOffset.UtcNow.AddMinutes(5));
        return Ok(new { photoId, signedUrl = url.UploadUrl, expiresAt = url.ExpiresAt });
    }

    [HttpPost("{reportId:guid}/photos/{photoId:guid}/consent/revoke")]
    public async Task<IActionResult> RevokeConsent(Guid farmId, Guid reportId, Guid photoId, CancellationToken ct)
    {
        var actor = CurrentUserId();
        if (actor is null) return Unauthorized();
        var report = await db.Set<SymptomReport>().Include(x => x.Photos).SingleOrDefaultAsync(x => x.FarmId == farmId && x.Id == reportId, ct);
        var photo = report?.Photos.SingleOrDefault(x => x.Id == photoId);
        if (report is null || photo is null || report.SubmittedByUserId != actor) return NotFound();
        photo.RevokeConsent(actor, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private string? CurrentUserId() => User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
    private async Task<bool> IsAuthorizedAgronomistAsync(Guid farmId, string actor, CancellationToken ct) =>
        await db.Set<TrustedAgronomistVerification>().AsNoTracking().AnyAsync(x => x.UserId == actor && x.RevokedAt == null, ct) &&
        await db.Set<FarmAgronomistAuthorization>().AsNoTracking().AnyAsync(x => x.FarmId == farmId && x.AgronomistUserId == actor && x.RevokedAt == null, ct);
    private static SymptomReportResponse ToResponse(SymptomReport report) => new(report.Id, report.FarmId, report.Status.ToString(),
        report.Notes, report.VisibleSigns, report.Uncertainty, report.ImageSufficient, report.RequestedEvidence,
        report.HumanDiagnosis, report.HumanTreatment, report.Photos.Select(x => new SymptomPhotoResponse(x.Id, x.ContentType,
            x.SizeBytes, x.ConsentGranted && !x.ConsentRevokedAt.HasValue, x.DeletedAt.HasValue)).ToArray());
}

public sealed record CreateSymptomUploadAuthorizationRequest(Guid? ReportId, Guid? CropCycleId, string ContentType,
    [param: Range(1, 10485760)] long SizeBytes, bool ExternalProcessingConsent, string? ConsentNoticeVersion, string? Notes);
public sealed record SymptomUploadAuthorizationResponse(Guid ReportId, Guid PhotoId, string UploadUrl, DateTimeOffset ExpiresAt, string ContentType, long SizeBytes);
public sealed record FinalizeSymptomReportRequest(Guid ReportId);
public sealed record SymptomObservationRequested(Guid EventId, Guid FarmId, Guid ReportId, int Version);
public sealed record SymptomPhotoResponse(Guid Id, string ContentType, long? SizeBytes, bool ConsentedToAi, bool Deleted);
public sealed record SymptomReportResponse(Guid Id, Guid FarmId, string Status, string? Notes, string? VisibleSigns, string? Uncertainty,
    bool? ImageSufficient, string[] RequestedEvidence, string? HumanDiagnosis, string? HumanTreatment, IReadOnlyList<SymptomPhotoResponse> Photos);
