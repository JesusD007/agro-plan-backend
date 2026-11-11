using System.Buffers.Binary;
using AgroPlan.Domain.Farms;

namespace AgroPlan.Domain.Symptoms;

public enum SymptomReportStatus
{
    Uploading = 1,
    Pending = 2,
    Processing = 3,
    Closed = 4,
    Rejected = 5
}

public enum SymptomReviewAction
{
    MoreEvidenceRequested = 1,
    Confirmed = 2,
    Rejected = 3
}

public sealed class SymptomReport
{
    private SymptomReport() { }

    public SymptomReport(Guid id, Guid farmId, string submittedByUserId, Guid? cropCycleId, string? notes,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || farmId == Guid.Empty) throw new ArgumentException("Report and farm ids are required.");
        if (string.IsNullOrWhiteSpace(submittedByUserId)) throw new ArgumentException("Report submitter is required.", nameof(submittedByUserId));
        if (notes?.Length > 2000) throw new ArgumentException("Notes must not exceed 2,000 characters.", nameof(notes));
        Id = id;
        FarmId = farmId;
        SubmittedByUserId = submittedByUserId;
        CropCycleId = cropCycleId;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        CreatedAt = createdAt;
        Status = SymptomReportStatus.Uploading;
        Photos = [];
        Decisions = [];
        Version = 1;
    }

    public Guid Id { get; private set; }
    public Guid FarmId { get; private set; }
    public string SubmittedByUserId { get; private set; } = string.Empty;
    public Guid? CropCycleId { get; private set; }
    public string? Notes { get; private set; }
    public SymptomReportStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public DateTimeOffset? ProcessingStartedAt { get; private set; }
    public DateTimeOffset? ObservedAt { get; private set; }
    public int ProcessingAttempts { get; private set; }
    public int Version { get; private set; }
    public string? VisibleSigns { get; private set; }
    public string? Uncertainty { get; private set; }
    public bool? ImageSufficient { get; private set; }
    public string[] RequestedEvidence { get; private set; } = [];
    public string? HumanDiagnosis { get; private set; }
    public string? HumanTreatment { get; private set; }
    public string? ReviewedByUserId { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public List<SymptomReportPhoto> Photos { get; private set; } = [];
    public List<SymptomReportDecision> Decisions { get; private set; } = [];

    public void AddPhoto(SymptomReportPhoto photo)
    {
        ArgumentNullException.ThrowIfNull(photo);
        if (Status != SymptomReportStatus.Uploading) throw new InvalidOperationException("Photos can only be added before submission.");
        if (Photos.Count >= SymptomPhotoInspector.MaximumPhotosPerReport) throw new InvalidOperationException("A report can include at most five photos.");
        if (Photos.Any(x => x.Id == photo.Id)) throw new InvalidOperationException("Photo id is already used in this report.");
        Photos.Add(photo);
        Version++;
    }

    public void Submit(DateTimeOffset at)
    {
        if (Status != SymptomReportStatus.Uploading) throw new InvalidOperationException("Report is not accepting uploads.");
        if (Photos.Count == 0 || Photos.Any(photo => photo.FinalizedAt is null))
            throw new InvalidOperationException("All report photos must be finalized before submission.");
        Status = SymptomReportStatus.Pending;
        SubmittedAt = at;
        Version++;
    }

    public bool TryStartObservation(DateTimeOffset now)
    {
        if (Status != SymptomReportStatus.Pending) return false;
        if (ProcessingStartedAt.HasValue && ProcessingStartedAt.Value > now.AddMinutes(-15)) return false;
        Status = SymptomReportStatus.Processing;
        ProcessingStartedAt = now;
        ProcessingAttempts++;
        Version++;
        return true;
    }

    public void CompleteObservation(string visibleSigns, string uncertainty, bool imageSufficient,
        IReadOnlyCollection<string> requestedEvidence, DateTimeOffset at)
    {
        if (Status != SymptomReportStatus.Processing) throw new InvalidOperationException("Report is not being processed.");
        VisibleSigns = visibleSigns;
        Uncertainty = uncertainty;
        ImageSufficient = imageSufficient;
        RequestedEvidence = requestedEvidence.ToArray();
        ObservedAt = at;
        ProcessingStartedAt = null;
        Status = SymptomReportStatus.Pending;
        Version++;
    }

    public void ReturnToPending(DateTimeOffset at)
    {
        if (Status != SymptomReportStatus.Processing) return;
        ProcessingStartedAt = null;
        Status = SymptomReportStatus.Pending;
        Version++;
    }

    public void AddDecision(SymptomReportDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (Status is SymptomReportStatus.Closed or SymptomReportStatus.Rejected)
            throw new InvalidOperationException("A closed report cannot receive another review decision.");
        switch (decision.Action)
        {
            case SymptomReviewAction.MoreEvidenceRequested:
                if (string.IsNullOrWhiteSpace(decision.RequestedEvidence))
                    throw new InvalidOperationException("A request for more evidence must identify the missing evidence.");
                Status = SymptomReportStatus.Pending;
                break;
            case SymptomReviewAction.Confirmed:
                if (ImageSufficient != true || string.IsNullOrWhiteSpace(decision.Diagnosis) || string.IsNullOrWhiteSpace(decision.Treatment))
                    throw new InvalidOperationException("A diagnosis and treatment require sufficient evidence and explicit review.");
                HumanDiagnosis = decision.Diagnosis.Trim();
                HumanTreatment = decision.Treatment.Trim();
                Status = SymptomReportStatus.Closed;
                QueuePhotoDeletion(decision.DecidedAt);
                break;
            case SymptomReviewAction.Rejected:
                Status = SymptomReportStatus.Rejected;
                QueuePhotoDeletion(decision.DecidedAt);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(decision));
        }
        Decisions.Add(decision);
        ReviewedByUserId = decision.ReviewerUserId;
        ReviewedAt = decision.DecidedAt;
        Version++;
    }

    public void CancelForAuthorizationRevocation(DateTimeOffset at)
    {
        if (Status == SymptomReportStatus.Processing)
        {
            Status = SymptomReportStatus.Pending;
            ProcessingStartedAt = null;
            Version++;
        }
    }

    public void QueuePhotoDeletion(DateTimeOffset at)
    {
        foreach (var photo in Photos) photo.QueueDeletion(at);
    }
}

public sealed class SymptomReportPhoto
{
    private SymptomReportPhoto() { }

    public SymptomReportPhoto(Guid id, string uploadObjectKey, string contentType, long expectedSizeBytes,
        string consentActorUserId, bool consentGranted, string? consentNoticeVersion, DateTimeOffset consentRecordedAt,
        DateTimeOffset expiresAt)
    {
        if (id == Guid.Empty) throw new ArgumentException("Photo id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(uploadObjectKey) || uploadObjectKey.Length > 1024) throw new ArgumentException("Private upload key is required.", nameof(uploadObjectKey));
        if (string.IsNullOrWhiteSpace(contentType) || contentType.Length > 64) throw new ArgumentException("Photo content type is required.", nameof(contentType));
        if (expectedSizeBytes is <= 0 or > SymptomPhotoInspector.MaximumPhotoBytes) throw new ArgumentOutOfRangeException(nameof(expectedSizeBytes));
        if (string.IsNullOrWhiteSpace(consentActorUserId)) throw new ArgumentException("Consent actor is required.", nameof(consentActorUserId));
        if (consentGranted && string.IsNullOrWhiteSpace(consentNoticeVersion)) throw new ArgumentException("Consent notice version is required for a grant.", nameof(consentNoticeVersion));
        Id = id;
        UploadObjectKey = uploadObjectKey;
        ContentType = contentType;
        ExpectedSizeBytes = expectedSizeBytes;
        ConsentActorUserId = consentActorUserId;
        ConsentGranted = consentGranted;
        ConsentNoticeVersion = consentNoticeVersion;
        ConsentRecordedAt = consentRecordedAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }
    public string UploadObjectKey { get; private set; } = string.Empty;
    public string? ObjectKey { get; private set; }
    public string ContentType { get; private set; } = string.Empty;
    public long ExpectedSizeBytes { get; private set; }
    public long? SizeBytes { get; private set; }
    public string ConsentActorUserId { get; private set; } = string.Empty;
    public bool ConsentGranted { get; private set; }
    public string? ConsentNoticeVersion { get; private set; }
    public DateTimeOffset ConsentRecordedAt { get; private set; }
    public DateTimeOffset? ConsentRevokedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? FinalizedAt { get; private set; }
    public DateTimeOffset? DeleteAfter { get; private set; }
    public DateTimeOffset? DeleteRetryAt { get; private set; }
    public DateTimeOffset? DeleteClaimedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public int DeleteAttempts { get; private set; }
    public string? DeleteLastError { get; private set; }

    public void Finalize(string objectKey, long sizeBytes, DateTimeOffset at)
    {
        if (FinalizedAt.HasValue)
        {
            if (!string.Equals(ObjectKey, objectKey, StringComparison.Ordinal) || SizeBytes != sizeBytes)
                throw new InvalidOperationException("Photo is already finalized with different metadata.");
            return;
        }
        if (string.IsNullOrWhiteSpace(objectKey) || objectKey.Length > 1024) throw new ArgumentException("Private photo key is required.", nameof(objectKey));
        if (sizeBytes != ExpectedSizeBytes) throw new InvalidOperationException("Photo size does not match its upload authorization.");
        ObjectKey = objectKey;
        SizeBytes = sizeBytes;
        FinalizedAt = at;
    }

    public void RevokeConsent(string actorUserId, DateTimeOffset at)
    {
        if (!ConsentGranted || ConsentRevokedAt.HasValue) return;
        if (string.IsNullOrWhiteSpace(actorUserId)) throw new ArgumentException("Consent actor is required.", nameof(actorUserId));
        ConsentGranted = false;
        ConsentActorUserId = actorUserId;
        ConsentRevokedAt = at;
    }

    public void QueueDeletion(DateTimeOffset at)
    {
        if (DeletedAt.HasValue || !FinalizedAt.HasValue || string.IsNullOrWhiteSpace(ObjectKey)) return;
        DeleteAfter ??= at;
        DeleteRetryAt ??= at;
    }

    public bool TryClaimDeletion(DateTimeOffset now, TimeSpan claimLease)
    {
        if (!DeleteAfter.HasValue || DeletedAt.HasValue || DeleteRetryAt > now ||
            DeleteClaimedAt >= now.Subtract(claimLease)) return false;
        DeleteClaimedAt = now;
        DeleteAttempts++;
        return true;
    }

    public void MarkDeleted(DateTimeOffset at)
    {
        DeletedAt ??= at;
        DeleteClaimedAt = null;
        DeleteLastError = null;
    }

    public void MarkDeletionFailed(string error, DateTimeOffset now)
    {
        DeleteClaimedAt = null;
        DeleteLastError = string.IsNullOrWhiteSpace(error) ? "StorageError" : error[..Math.Min(error.Length, 80)];
        DeleteRetryAt = now.AddSeconds(Math.Min(300, Math.Pow(2, Math.Min(DeleteAttempts, 8))));
    }
}

public sealed class SymptomReportDecision
{
    private SymptomReportDecision() { }

    public SymptomReportDecision(Guid id, SymptomReviewAction action, string reviewerUserId, DateTimeOffset decidedAt,
        string? requestedEvidence = null, string? diagnosis = null, string? treatment = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Decision id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(reviewerUserId)) throw new ArgumentException("Reviewer is required.", nameof(reviewerUserId));
        if (!Enum.IsDefined(action)) throw new ArgumentOutOfRangeException(nameof(action));
        if (diagnosis?.Length > 2000 || treatment?.Length > 4000 || requestedEvidence?.Length > 2000)
            throw new ArgumentException("Review text exceeds its allowed length.");
        Id = id;
        Action = action;
        ReviewerUserId = reviewerUserId;
        DecidedAt = decidedAt;
        RequestedEvidence = requestedEvidence;
        Diagnosis = diagnosis;
        Treatment = treatment;
    }

    public Guid Id { get; private set; }
    public SymptomReviewAction Action { get; private set; }
    public string ReviewerUserId { get; private set; } = string.Empty;
    public DateTimeOffset DecidedAt { get; private set; }
    public string? RequestedEvidence { get; private set; }
    public string? Diagnosis { get; private set; }
    public string? Treatment { get; private set; }
}

public sealed record SymptomImageDimensions(int Width, int Height)
{
    public long Pixels => (long)Width * Height;
}

public static class SymptomPhotoInspector
{
    public const int MaximumPhotosPerReport = 5;
    public const long MaximumPhotoBytes = 10 * 1024 * 1024;
    public const int MaximumEdgePixels = 8192;
    public const long MaximumPixelsPerImage = 20_000_000;
    public const long MaximumPixelsPerReport = 50_000_000;

    public static bool TryInspect(ReadOnlySpan<byte> bytes, string contentType, out SymptomImageDimensions dimensions)
    {
        dimensions = new SymptomImageDimensions(0, 0);
        if (bytes.IsEmpty || bytes.Length > MaximumPhotoBytes) return false;
        var parsed = contentType switch
        {
            "image/png" => TryPng(bytes, out dimensions),
            "image/jpeg" => TryJpeg(bytes, out dimensions),
            "image/webp" => TryWebP(bytes, out dimensions),
            _ => false
        };
        return parsed && dimensions.Width > 0 && dimensions.Height > 0 &&
               dimensions.Width <= MaximumEdgePixels && dimensions.Height <= MaximumEdgePixels &&
               dimensions.Pixels <= MaximumPixelsPerImage;
    }

    private static bool TryPng(ReadOnlySpan<byte> bytes, out SymptomImageDimensions dimensions)
    {
        dimensions = new SymptomImageDimensions(0, 0);
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (bytes.Length < 24 || !bytes[..8].SequenceEqual(signature) || !bytes.Slice(12, 4).SequenceEqual("IHDR"u8)) return false;
        var width = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(20, 4));
        dimensions = new SymptomImageDimensions(width, height);
        return width > 0 && height > 0;
    }

    private static bool TryJpeg(ReadOnlySpan<byte> bytes, out SymptomImageDimensions dimensions)
    {
        dimensions = new SymptomImageDimensions(0, 0);
        if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8) return false;
        var index = 2;
        while (index + 4 <= bytes.Length)
        {
            while (index < bytes.Length && bytes[index] != 0xFF) index++;
            while (index < bytes.Length && bytes[index] == 0xFF) index++;
            if (index >= bytes.Length) return false;
            var marker = bytes[index++];
            if (marker is 0xD9 or 0xDA) return false;
            if (marker is 0xD8 or 0x01 or >= 0xD0 and <= 0xD7) continue;
            if (index + 2 > bytes.Length) return false;
            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(index, 2));
            if (length < 2 || index + length > bytes.Length) return false;
            if (marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF)
            {
                if (length < 7) return false;
                var height = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(index + 3, 2));
                var width = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(index + 5, 2));
                dimensions = new SymptomImageDimensions(width, height);
                return width > 0 && height > 0;
            }
            index += length;
        }
        return false;
    }

    private static bool TryWebP(ReadOnlySpan<byte> bytes, out SymptomImageDimensions dimensions)
    {
        dimensions = new SymptomImageDimensions(0, 0);
        if (bytes.Length < 30 || !bytes[..4].SequenceEqual("RIFF"u8) || !bytes.Slice(8, 4).SequenceEqual("WEBP"u8)) return false;
        var chunk = bytes.Slice(12, 4);
        if (chunk.SequenceEqual("VP8X"u8))
        {
            var width = 1 + bytes[24] + (bytes[25] << 8) + (bytes[26] << 16);
            var height = 1 + bytes[27] + (bytes[28] << 8) + (bytes[29] << 16);
            dimensions = new SymptomImageDimensions(width, height);
            return true;
        }
        if (chunk.SequenceEqual("VP8L"u8) && bytes.Length >= 25 && bytes[20] == 0x2F)
        {
            var bits = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(21, 4));
            dimensions = new SymptomImageDimensions((int)(bits & 0x3FFF) + 1, (int)((bits >> 14) & 0x3FFF) + 1);
            return true;
        }
        if (chunk.SequenceEqual("VP8 "u8) && bytes.Length >= 30 && bytes[23] == 0x9D && bytes[24] == 0x01 && bytes[25] == 0x2A)
        {
            var width = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(26, 2)) & 0x3FFF;
            var height = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(28, 2)) & 0x3FFF;
            dimensions = new SymptomImageDimensions(width, height);
            return width > 0 && height > 0;
        }
        return false;
    }
}
