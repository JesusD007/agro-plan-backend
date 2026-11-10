using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AgroPlan.Api.Authorization;
using AgroPlan.Application.Abstractions;
using AgroPlan.Domain.Logbook;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Api.Controllers;

[ApiController]
[Route("api/farms/{farmId:guid}/logbook/{entryId:guid}/attachments")]
[Authorize(Policy = FarmMembershipRequirement.PolicyName)]
public sealed class LogbookAttachmentsController(AgroPlanDbContext db, IFileStorage storage) : ControllerBase
{
    private static readonly HashSet<string> AllowedTypes = ["image/jpeg", "image/png", "image/webp"];

    [HttpPost]
    public async Task<ActionResult<UploadAuthorizationResponse>> AuthorizeUpload(Guid farmId, Guid entryId, CreateAttachmentRequest request, CancellationToken ct)
    {
        if (!AllowedTypes.Contains(request.ContentType) || request.SizeBytes is <= 0 or > 10 * 1024 * 1024)
            return BadRequest(new { message = "Only JPEG, PNG or WebP files up to 10 MiB are accepted." });
        if (!await db.LogbookEntries.AsNoTracking().AnyAsync(x => x.FarmId == farmId && x.Id == entryId, ct)) return NotFound();
        var actor = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actor is null) return Unauthorized();
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(10);
        var id = Guid.NewGuid();
        var authorization = storage.CreateUploadAuthorization(farmId, entryId, id, request.ContentType, expires);
        var finalObjectKey = $"farms/{farmId:D}/logbook/{entryId:D}/{id:D}/final";
        db.LogbookAttachments.Add(new LogbookAttachment(id, farmId, entryId, finalObjectKey, authorization.ObjectKey,
            request.ContentType, request.SizeBytes, actor, now, expires));
        await db.SaveChangesAsync(ct);
        return Ok(new UploadAuthorizationResponse(id, authorization.UploadUrl, authorization.ExpiresAt,
            authorization.RequiredContentType, request.SizeBytes));
    }

    [HttpPost("{attachmentId:guid}/finalize")]
    public async Task<IActionResult> FinalizeUpload(Guid farmId, Guid entryId, Guid attachmentId, CancellationToken ct)
    {
        var attachment = await db.LogbookAttachments.SingleOrDefaultAsync(x => x.FarmId == farmId && x.LogbookEntryId == entryId && x.Id == attachmentId, ct);
        if (attachment is null) return NotFound();
        if (attachment.FinalizedAt.HasValue) return Ok(new { attachment.Id, attachment.ObjectKey, attachment.ContentType, attachment.SizeBytes, attachment.FinalizedAt });
        var now = DateTimeOffset.UtcNow;
        if (now > attachment.ExpiresAt) return BadRequest(new { message = "Upload authorization has expired." });
        var finalizingBefore = now.AddMinutes(-15);
        var claimed = await db.LogbookAttachments.Where(x => x.Id == attachmentId && x.FarmId == farmId &&
                x.LogbookEntryId == entryId && x.FinalizedAt == null && (x.FinalizingAt == null || x.FinalizingAt < finalizingBefore))
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.FinalizingAt, now), ct);
        await db.Entry(attachment).ReloadAsync(ct);
        if (attachment.FinalizedAt.HasValue) return Ok(new { attachment.Id, attachment.ObjectKey, attachment.ContentType, attachment.SizeBytes, attachment.FinalizedAt });
        if (claimed == 0) return Conflict(new { message = "Attachment finalization is already in progress; retry shortly." });

        try
        {
            var metadata = await storage.GetMetadataAsync(attachment.UploadObjectKey, ct);
            if (metadata is not null && (!AllowedTypes.Contains(metadata.ContentType) || metadata.ContentType != attachment.ContentType ||
                metadata.SizeBytes != attachment.ExpectedSizeBytes || metadata.SizeBytes is <= 0 or > 10 * 1024 * 1024))
                return BadRequest(new { message = "Uploaded object metadata does not match the authorized file." });
            var finalized = await storage.FinalizeUploadAsync(attachment.UploadObjectKey, attachment.ObjectKey, ct);
            if (finalized is null) return BadRequest(new { message = "Uploaded object was not found." });
            metadata = finalized.Metadata;
            if (!AllowedTypes.Contains(metadata.ContentType) || metadata.ContentType != attachment.ContentType ||
                metadata.SizeBytes != attachment.ExpectedSizeBytes || metadata.SizeBytes is <= 0 or > 10 * 1024 * 1024)
                return BadRequest(new { message = "Uploaded object metadata does not match the authorized file." });
            attachment.Finalize(finalized.ObjectKey, metadata.SizeBytes, now);
            await db.SaveChangesAsync(ct);
            return Ok(new { attachment.Id, attachment.ObjectKey, attachment.ContentType, attachment.SizeBytes, attachment.FinalizedAt });
        }
        finally
        {
            if (!attachment.FinalizedAt.HasValue)
                await db.LogbookAttachments.Where(x => x.Id == attachmentId && x.FarmId == farmId && x.FinalizedAt == null)
                    .ExecuteUpdateAsync(update => update.SetProperty(x => x.FinalizingAt, (DateTimeOffset?)null), ct);
        }
    }

    [HttpGet("{attachmentId:guid}")]
    public async Task<ActionResult<UploadAuthorizationResponse>> AuthorizeDownload(Guid farmId, Guid entryId, Guid attachmentId, CancellationToken ct)
    {
        var attachment = await db.LogbookAttachments.AsNoTracking().SingleOrDefaultAsync(
            x => x.FarmId == farmId && x.LogbookEntryId == entryId && x.Id == attachmentId && x.FinalizedAt != null, ct);
        if (attachment is null) return NotFound();
        var expires = DateTimeOffset.UtcNow.AddMinutes(10);
        var authorization = storage.CreateDownloadAuthorization(attachment.ObjectKey, expires);
        return Ok(new UploadAuthorizationResponse(attachmentId, authorization.UploadUrl, expires,
            attachment.ContentType, attachment.SizeBytes ?? 0));
    }
}

public sealed record CreateAttachmentRequest([property: Required] string ContentType, [property: Range(1, 10485760)] long SizeBytes);
public sealed record UploadAuthorizationResponse(Guid AttachmentId, string SignedUrl, DateTimeOffset ExpiresAt, string ContentType, long SizeBytes);
