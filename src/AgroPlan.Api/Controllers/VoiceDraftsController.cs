using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using AgroPlan.Api.Authorization;
using AgroPlan.Application.Abstractions;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Logbook;
using AgroPlan.Domain.Planning;
using AgroPlan.Domain.Voice;
using AgroPlan.Domain.Workforce;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/farms/{farmId:guid}/voice-drafts")]
public sealed class VoiceDraftsController(AgroPlanDbContext db, IFileStorage storage,
    AssignedWorkerAuthorizationService authorization) : ControllerBase
{
    private const long MaximumAudioSize = 10 * 1024 * 1024;
    private const string AllowedContentType = "audio/wav";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpPost("upload-authorizations")]
    [EnableRateLimiting("phase4-media")]
    public async Task<ActionResult<VoiceUploadAuthorizationResponse>> AuthorizeUpload(Guid farmId,
        CreateVoiceUploadAuthorizationRequest request, CancellationToken ct)
    {
        var actor = CurrentUserId();
        if (actor is null) return Unauthorized();
        var role = await authorization.GetActiveRoleAsync(farmId, actor, ct);
        if (role is null) return Forbid();
        if (role is not (FarmRole.Owner or FarmRole.Administrator or FarmRole.Worker)) return Forbid();
        if (!request.ConsentToExternalProcessing || string.IsNullOrWhiteSpace(request.ConsentNoticeVersion))
            return BadRequest(new { code = "VOICE_CONSENT_REQUIRED", message = "Explicit consent is required for this recording before external processing." });
        if (!Enum.TryParse<SpeechLanguage>(request.Language, ignoreCase: false, out var language) || !Enum.IsDefined(language))
            return BadRequest(new { code = "VOICE_LANGUAGE_UNSUPPORTED" });
        if (!string.Equals(request.ContentType, AllowedContentType, StringComparison.OrdinalIgnoreCase) ||
            request.SizeBytes is <= 0 or > MaximumAudioSize)
            return BadRequest(new { code = "VOICE_AUDIO_INVALID", message = "Upload mono 16 kHz, 16-bit PCM WAV audio up to 10 MiB." });

        WorkTask? workTask = null;
        if (role == FarmRole.Worker)
        {
            if (!request.WorkTaskId.HasValue ||
                !await authorization.IsAssignedWorkerAsync(farmId, request.WorkTaskId.Value, actor, ct)) return NotFound();
            workTask = await db.WorkTasks.AsNoTracking().SingleOrDefaultAsync(task => task.FarmId == farmId &&
                task.Id == request.WorkTaskId.Value && task.AssignedWorkerUserId == actor, ct);
            if (workTask is null || workTask.Status is not (WorkTaskStatus.Pending or WorkTaskStatus.InProgress or WorkTaskStatus.CorrectionRequested))
                return NotFound();
        }
        else if (request.WorkTaskId.HasValue)
        {
            workTask = await db.WorkTasks.AsNoTracking().SingleOrDefaultAsync(task => task.FarmId == farmId &&
                task.Id == request.WorkTaskId.Value, ct);
            if (workTask is null) return NotFound();
        }

        var parcelId = workTask?.ParcelId ?? request.ParcelId;
        var cropCycleId = workTask?.CropCycleId ?? request.CropCycleId;
        if (parcelId == Guid.Empty || !await db.Parcels.AsNoTracking().AnyAsync(parcel => parcel.FarmId == farmId && parcel.Id == parcelId, ct))
            return BadRequest(new { code = "VOICE_PARCEL_INVALID" });
        if (cropCycleId.HasValue && !await db.CropCycles.AsNoTracking().AnyAsync(cycle =>
                cycle.FarmId == farmId && cycle.ParcelId == parcelId && cycle.Id == cropCycleId.Value, ct))
            return BadRequest(new { code = "VOICE_CROP_CYCLE_INVALID" });
        if (workTask?.ParcelId is Guid taskParcel && taskParcel != parcelId ||
            workTask?.CropCycleId is Guid taskCycle && cropCycleId != taskCycle)
            return BadRequest(new { code = "VOICE_TASK_SCOPE_MISMATCH" });

        var now = DateTimeOffset.UtcNow;
        var draftId = Guid.NewGuid();
        var expiresAt = now.AddMinutes(10);
        var upload = storage.CreateVoiceDraftUploadAuthorization(farmId, draftId, AllowedContentType, expiresAt);
        var draft = new VoiceActivityDraft(draftId, farmId, request.WorkTaskId, parcelId, cropCycleId,
            actor, actor, request.ConsentNoticeVersion, request.ConsentToExternalProcessing, language,
            upload.ObjectKey, AllowedContentType, request.SizeBytes, now, expiresAt);
        db.VoiceActivityDrafts.Add(draft);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { farmId, draftId },
            new VoiceUploadAuthorizationResponse(draft.Id, upload.UploadUrl, upload.ExpiresAt, AllowedContentType, request.SizeBytes));
    }

    [HttpPost]
    [EnableRateLimiting("phase4-media")]
    public async Task<ActionResult<VoiceActivityDraftResponse>> FinalizeUpload(Guid farmId,
        FinalizeVoiceDraftRequest request, CancellationToken ct)
    {
        var actor = CurrentUserId();
        if (actor is null) return Unauthorized();
        var draft = await db.VoiceActivityDrafts.SingleOrDefaultAsync(row => row.FarmId == farmId &&
            row.Id == request.UploadAuthorizationId, ct);
        if (draft is null || !await CanAccessDraftAsync(draft, actor, ct)) return NotFound();
        if (!draft.ConsentedToExternalProcessing || draft.ConsentRevokedAt.HasValue)
            return Conflict(new { code = "VOICE_CONSENT_REVOKED" });
        if (draft.Status != VoiceActivityDraftStatus.AwaitingUpload) return Conflict(new { code = "VOICE_UPLOAD_NOT_PENDING" });
        var now = DateTimeOffset.UtcNow;
        if (now > draft.UploadExpiresAt) return BadRequest(new { code = "VOICE_UPLOAD_EXPIRED" });

        var staged = await storage.GetMetadataAsync(draft.UploadObjectKey, ct);
        if (staged is null || staged.ContentType != AllowedContentType || staged.SizeBytes != draft.ExpectedSizeBytes ||
            staged.SizeBytes is <= 0 or > MaximumAudioSize)
            return BadRequest(new { code = "VOICE_AUDIO_METADATA_INVALID" });
        var bytes = await storage.ReadObjectAsync(draft.UploadObjectKey, MaximumAudioSize, ct);
        if (bytes is null || bytes.LongLength != draft.ExpectedSizeBytes ||
            !PcmWaveAudioInspector.TryGetDurationSeconds(bytes, out var durationSeconds))
            return BadRequest(new { code = "VOICE_AUDIO_FORMAT_INVALID", message = "The recording must be valid mono 16 kHz, 16-bit PCM WAV and no longer than five minutes." });

        var finalObjectKey = $"farms/{farmId:D}/voice-drafts/{draft.Id:D}/final";
        var finalized = await storage.FinalizeUploadAsync(draft.UploadObjectKey, finalObjectKey, ct);
        if (finalized is null || finalized.Metadata.ContentType != AllowedContentType ||
            finalized.Metadata.SizeBytes != draft.ExpectedSizeBytes || finalized.Metadata.SizeBytes > MaximumAudioSize)
            return BadRequest(new { code = "VOICE_AUDIO_FINALIZATION_FAILED" });

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            draft.FinalizeAudio(finalized.ObjectKey, finalized.Metadata.ContentType,
                finalized.Metadata.SizeBytes, durationSeconds, now);
            var outboxId = Guid.NewGuid();
            var eventPayload = JsonSerializer.Serialize(new VoiceDraftRequested(outboxId, farmId, draft.Id, draft.Version), JsonOptions);
            db.OutboxMessages.Add(new OutboxMessage(outboxId, draft.Id, "voice-draft.requested", eventPayload, now));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return AcceptedAtAction(nameof(Get), new { farmId, draftId = draft.Id }, ToResponse(draft));
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            return Conflict(new { code = "VOICE_DRAFT_STALE" });
        }
    }

    [HttpGet("{draftId:guid}")]
    public async Task<ActionResult<VoiceActivityDraftResponse>> Get(Guid farmId, Guid draftId, CancellationToken ct)
    {
        var actor = CurrentUserId();
        if (actor is null) return Unauthorized();
        var draft = await db.VoiceActivityDrafts.AsNoTracking().SingleOrDefaultAsync(row =>
            row.FarmId == farmId && row.Id == draftId, ct);
        if (draft is null || !await CanAccessDraftAsync(draft, actor, ct)) return NotFound();
        return Ok(ToResponse(draft));
    }

    [HttpPut("{draftId:guid}")]
    public async Task<ActionResult<VoiceActivityDraftResponse>> Edit(Guid farmId, Guid draftId,
        EditVoiceDraftRequest request, CancellationToken ct)
    {
        var actor = CurrentUserId();
        if (actor is null) return Unauthorized();
        var draft = await db.VoiceActivityDrafts.SingleOrDefaultAsync(row => row.FarmId == farmId && row.Id == draftId, ct);
        if (draft is null || !await CanAccessDraftAsync(draft, actor, ct)) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Details) || request.Details.Length > 4000 ||
            !Enum.IsDefined(request.ActivityType) || request.OccurredAt == default)
            return BadRequest(new { code = "VOICE_DRAFT_FIELDS_INVALID" });
        if (!draft.Edit(request.ExpectedVersion, request.ActivityType, request.OccurredAt,
                request.Details, DateTimeOffset.UtcNow)) return Conflict(new { code = "VOICE_DRAFT_STALE_OR_NOT_EDITABLE" });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { code = "VOICE_DRAFT_STALE" }); }
        return Ok(ToResponse(draft));
    }

    [HttpPost("{draftId:guid}/confirm")]
    public async Task<ActionResult<VoiceConfirmationResponse>> Confirm(Guid farmId, Guid draftId,
        ConfirmVoiceDraftRequest request, CancellationToken ct)
    {
        var actor = CurrentUserId();
        if (actor is null) return Unauthorized();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT \"Id\" FROM agro.voice_activity_drafts WHERE \"FarmId\" = {farmId} AND \"Id\" = {draftId} FOR UPDATE", ct);
        var draft = await db.VoiceActivityDrafts.SingleOrDefaultAsync(row => row.FarmId == farmId && row.Id == draftId, ct);
        if (draft is null || !await CanAccessDraftAsync(draft, actor, ct)) return NotFound();
        if (draft.Status == VoiceActivityDraftStatus.Confirmed && draft.LogbookEntryId.HasValue)
        {
            await transaction.CommitAsync(ct);
            return Ok(new VoiceConfirmationResponse(draft.LogbookEntryId.Value, true, draft.Version));
        }
        if (draft.ConsentRevokedAt.HasValue || !draft.ConsentedToExternalProcessing)
            return Conflict(new { code = "VOICE_CONSENT_REVOKED" });
        if (!Enum.TryParse<LogbookActivityType>(draft.ActivityType, out var activityType) ||
            !draft.OccurredAt.HasValue || string.IsNullOrWhiteSpace(draft.Details))
            return Conflict(new { code = "VOICE_DRAFT_INCOMPLETE" });

        var entryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var entry = new LogbookEntry(entryId, farmId, draft.ParcelId, draft.CropCycleId, activityType,
            draft.OccurredAt.Value, draft.Details, actor, now);
        var result = draft.Confirm(actor, entryId, request.ExpectedVersion, now);
        if (!result.ConfirmedNow) return Conflict(new { code = "VOICE_DRAFT_STALE" });
        db.LogbookEntries.Add(entry);
        ConfirmedRecordOutbox.Add(db, farmId, entry.ParcelId, entry.CropCycleId,
            entry.ActivityType.ToString().ToLowerInvariant(), entry.Id, entry.OccurredAt);
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            return Conflict(new { code = "VOICE_DRAFT_STALE" });
        }
        return Ok(new VoiceConfirmationResponse(entry.Id, false, draft.Version));
    }

    [HttpPost("{draftId:guid}/consent/revoke")]
    public async Task<IActionResult> RevokeConsent(Guid farmId, Guid draftId, CancellationToken ct)
    {
        var actor = CurrentUserId();
        if (actor is null) return Unauthorized();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT \"Id\" FROM agro.voice_activity_drafts WHERE \"FarmId\" = {farmId} AND \"Id\" = {draftId} FOR UPDATE", ct);
        var draft = await db.VoiceActivityDrafts.SingleOrDefaultAsync(row => row.FarmId == farmId && row.Id == draftId, ct);
        if (draft is null || draft.SubmittedByUserId != actor || !await CanAccessDraftAsync(draft, actor, ct)) return NotFound();
        draft.RevokeConsent(actor, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return NoContent();
    }

    private async Task<bool> CanAccessDraftAsync(VoiceActivityDraft draft, string actor, CancellationToken ct)
    {
        var role = await authorization.GetActiveRoleAsync(draft.FarmId, actor, ct);
        if (role is FarmRole.Owner or FarmRole.Administrator) return true;
        return role == FarmRole.Worker && actor == draft.SubmittedByUserId && draft.WorkTaskId.HasValue &&
            await authorization.IsAssignedWorkerAsync(draft.FarmId, draft.WorkTaskId.Value, actor, ct);
    }

    private string? CurrentUserId() => User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

    private static VoiceActivityDraftResponse ToResponse(VoiceActivityDraft draft)
    {
        var uncertain = JsonSerializer.Deserialize<string[]>(draft.UncertainFieldsJson, JsonOptions) ?? [];
        return new VoiceActivityDraftResponse(draft.Id, draft.FarmId, draft.WorkTaskId, draft.ParcelId,
            draft.CropCycleId, draft.Status.ToString(), draft.Language, draft.ActivityType, draft.OccurredAt,
            draft.Details, uncertain, draft.Confidence, draft.FailureCode, draft.Version, draft.CreatedAt,
            draft.UpdatedAt, draft.AudioDeleteAfter, draft.AudioDeletedAt, draft.LogbookEntryId);
    }
}

public sealed record CreateVoiceUploadAuthorizationRequest(
    [param: Required] Guid ParcelId,
    Guid? CropCycleId,
    Guid? WorkTaskId,
    [param: Required] string Language,
    [param: Required] string ContentType,
    [param: Range(1, 10485760)] long SizeBytes,
    bool ConsentToExternalProcessing,
    [param: Required, StringLength(80, MinimumLength = 1)] string ConsentNoticeVersion);

public sealed record VoiceUploadAuthorizationResponse(Guid DraftId, string SignedUrl, DateTimeOffset ExpiresAt,
    string ContentType, long SizeBytes);
public sealed record FinalizeVoiceDraftRequest([param: Required] Guid UploadAuthorizationId);
public sealed record EditVoiceDraftRequest(long ExpectedVersion, [param: Required] LogbookActivityType ActivityType,
    [param: Required] DateTimeOffset OccurredAt, [param: Required, StringLength(4000, MinimumLength = 1)] string Details);
public sealed record ConfirmVoiceDraftRequest(long ExpectedVersion);
public sealed record VoiceActivityDraftResponse(Guid Id, Guid FarmId, Guid? WorkTaskId, Guid ParcelId,
    Guid? CropCycleId, string Status, SpeechLanguage Language, string? ActivityType, DateTimeOffset? OccurredAt,
    string? Details, IReadOnlyList<string> UncertainFields, double? Confidence, string? FailureCode,
    long Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? AudioDeleteAfter,
    DateTimeOffset? AudioDeletedAt, Guid? LogbookEntryId);
public sealed record VoiceConfirmationResponse(Guid LogbookEntryId, bool AlreadyConfirmed, long DraftVersion);
