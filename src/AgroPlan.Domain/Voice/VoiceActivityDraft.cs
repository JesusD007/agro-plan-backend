using System.Text.Json;
using System.Text.Json.Serialization;
using AgroPlan.Domain.Logbook;

namespace AgroPlan.Domain.Voice;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SpeechLanguage
{
    DominicanSpanish = 1,
    HaitianCreole = 2
}

public enum VoiceActivityDraftStatus
{
    AwaitingUpload = 1,
    Pending = 2,
    Processing = 3,
    DraftReady = 4,
    Unavailable = 5,
    Confirmed = 6,
    Cancelled = 7
}

/// <summary>Private, farm-scoped audio job and editable draft; it never writes the logbook itself.</summary>
public sealed class VoiceActivityDraft
{
    private VoiceActivityDraft() { }

    public VoiceActivityDraft(Guid id, Guid farmId, Guid? workTaskId, Guid parcelId, Guid? cropCycleId,
        string submittedByUserId, string consentActorUserId, string consentNoticeVersion,
        bool consentedToExternalProcessing, SpeechLanguage language, string uploadObjectKey,
        string contentType, long expectedSizeBytes, DateTimeOffset createdAt, DateTimeOffset uploadExpiresAt)
    {
        if (id == Guid.Empty || farmId == Guid.Empty || parcelId == Guid.Empty)
            throw new ArgumentException("Draft, farm and parcel ids are required.");
        if (workTaskId == Guid.Empty || cropCycleId == Guid.Empty)
            throw new ArgumentException("Optional task and crop-cycle ids cannot be empty.");
        if (string.IsNullOrWhiteSpace(submittedByUserId) || submittedByUserId.Length > 450)
            throw new ArgumentException("A valid submitter is required.", nameof(submittedByUserId));
        if (!consentedToExternalProcessing) throw new ArgumentException("Explicit per-recording consent is required.", nameof(consentedToExternalProcessing));
        if (string.IsNullOrWhiteSpace(consentActorUserId) || consentActorUserId != submittedByUserId)
            throw new ArgumentException("The consent actor must be the recording submitter.", nameof(consentActorUserId));
        if (string.IsNullOrWhiteSpace(consentNoticeVersion) || consentNoticeVersion.Length > 80)
            throw new ArgumentException("A consent notice version is required.", nameof(consentNoticeVersion));
        if (!Enum.IsDefined(language)) throw new ArgumentOutOfRangeException(nameof(language));
        if (string.IsNullOrWhiteSpace(uploadObjectKey) || uploadObjectKey.Length > 1024)
            throw new ArgumentException("A private upload object key is required.", nameof(uploadObjectKey));
        if (!string.Equals(contentType, "audio/wav", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only validated WAV audio is accepted.", nameof(contentType));
        if (expectedSizeBytes is <= 0 or > 10 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(expectedSizeBytes));
        if (createdAt == default || uploadExpiresAt <= createdAt) throw new ArgumentException("A valid upload expiry is required.");

        Id = id;
        FarmId = farmId;
        WorkTaskId = workTaskId;
        ParcelId = parcelId;
        CropCycleId = cropCycleId;
        SubmittedByUserId = submittedByUserId.Trim();
        ConsentActorUserId = consentActorUserId.Trim();
        ConsentNoticeVersion = consentNoticeVersion.Trim();
        ConsentedToExternalProcessing = true;
        ConsentedAt = createdAt;
        Language = language;
        UploadObjectKey = uploadObjectKey;
        ContentType = contentType.ToLowerInvariant();
        ExpectedSizeBytes = expectedSizeBytes;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        UploadExpiresAt = uploadExpiresAt;
        Status = VoiceActivityDraftStatus.AwaitingUpload;
        Version = 1;
        StatusVersion = 1;
        UncertainFieldsJson = "[]";
    }

    public Guid Id { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid? WorkTaskId { get; private set; }
    public Guid ParcelId { get; private set; }
    public Guid? CropCycleId { get; private set; }
    public string SubmittedByUserId { get; private set; } = string.Empty;
    public string ConsentActorUserId { get; private set; } = string.Empty;
    public string ConsentNoticeVersion { get; private set; } = string.Empty;
    public bool ConsentedToExternalProcessing { get; private set; }
    public DateTimeOffset ConsentedAt { get; private set; }
    public DateTimeOffset? ConsentRevokedAt { get; private set; }
    public SpeechLanguage Language { get; private set; }
    public string UploadObjectKey { get; private set; } = string.Empty;
    public string? AudioObjectKey { get; private set; }
    public string ContentType { get; private set; } = string.Empty;
    public long ExpectedSizeBytes { get; private set; }
    public long? AudioSizeBytes { get; private set; }
    public int? AudioDurationSeconds { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset UploadExpiresAt { get; private set; }
    public DateTimeOffset? AudioFinalizedAt { get; private set; }
    public DateTimeOffset? AudioFinalizingAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public DateTimeOffset? AudioDeleteAfter { get; private set; }
    public DateTimeOffset? AudioDeletedAt { get; private set; }
    public DateTimeOffset? AudioDeleteRetryAt { get; private set; }
    public DateTimeOffset? AudioCleanupClaimedAt { get; private set; }
    public int AudioDeleteAttemptCount { get; private set; }
    public string? AudioDeleteLastError { get; private set; }
    public VoiceActivityDraftStatus Status { get; private set; }
    public string? ActivityType { get; private set; }
    public DateTimeOffset? OccurredAt { get; private set; }
    public string? Details { get; private set; }
    public string UncertainFieldsJson { get; private set; } = "[]";
    public double? Confidence { get; private set; }
    public string? FailureCode { get; private set; }
    public DateTimeOffset? RetryAt { get; private set; }
    public int ProcessingAttemptCount { get; private set; }
    public long Version { get; private set; }
    public long StatusVersion { get; private set; }
    public long NotifiedStatusVersion { get; private set; }
    public string? ConfirmedByUserId { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public Guid? LogbookEntryId { get; private set; }

    public void FinalizeAudio(string objectKey, string actualContentType, long sizeBytes, int durationSeconds,
        DateTimeOffset finalizedAt)
    {
        if (Status != VoiceActivityDraftStatus.AwaitingUpload) throw new InvalidOperationException("The voice upload is not awaiting finalization.");
        if (string.IsNullOrWhiteSpace(objectKey) || objectKey.Length > 1024) throw new ArgumentException("A private object key is required.", nameof(objectKey));
        if (!string.Equals(actualContentType, ContentType, StringComparison.OrdinalIgnoreCase) ||
            sizeBytes != ExpectedSizeBytes || sizeBytes is <= 0 or > 10 * 1024 * 1024)
            throw new InvalidOperationException("Uploaded audio metadata does not match its authorization.");
        if (durationSeconds is <= 0 or > 300) throw new InvalidOperationException("Audio duration must be between one second and five minutes.");
        if (finalizedAt == default || finalizedAt > UploadExpiresAt) throw new InvalidOperationException("The audio upload authorization expired.");

        AudioObjectKey = objectKey;
        AudioSizeBytes = sizeBytes;
        AudioDurationSeconds = durationSeconds;
        AudioFinalizedAt = finalizedAt;
        Status = VoiceActivityDraftStatus.Pending;
        UpdatedAt = finalizedAt;
        BumpVersion();
    }

    public bool TryStartProcessing(DateTimeOffset now, DateTimeOffset staleBefore)
    {
        if (!ConsentedToExternalProcessing || ConsentRevokedAt.HasValue || AudioObjectKey is null || ProcessingAttemptCount >= 3)
            return false;
        if (Status == VoiceActivityDraftStatus.Pending && (!RetryAt.HasValue || RetryAt.Value <= now))
        {
            Status = VoiceActivityDraftStatus.Processing;
            ProcessingAttemptCount++;
            RetryAt = null;
            UpdatedAt = now;
            BumpVersion();
            return true;
        }
        if (Status == VoiceActivityDraftStatus.Processing && UpdatedAt < staleBefore && ProcessingAttemptCount < 3)
        {
            ProcessingAttemptCount++;
            UpdatedAt = now;
            BumpVersion();
            return true;
        }
        return false;
    }

    public bool MarkDraftReady(LogbookActivityType? activityType, DateTimeOffset? occurredAt, string? details,
        IReadOnlyCollection<string> uncertainFields, double confidence, DateTimeOffset processedAt)
    {
        if (Status != VoiceActivityDraftStatus.Processing || ConsentRevokedAt.HasValue || !ConsentedToExternalProcessing) return false;
        if (activityType.HasValue && !Enum.IsDefined(activityType.Value)) throw new ArgumentOutOfRangeException(nameof(activityType));
        if (details?.Length > 4000) throw new ArgumentOutOfRangeException(nameof(details));
        if (!double.IsFinite(confidence) || confidence is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(confidence));
        var uncertain = NormalizeUncertainFields(uncertainFields, activityType, occurredAt, details);
        ActivityType = activityType?.ToString();
        OccurredAt = occurredAt;
        Details = string.IsNullOrWhiteSpace(details) ? null : details.Trim();
        UncertainFieldsJson = JsonSerializer.Serialize(uncertain);
        Confidence = confidence;
        ProcessedAt = processedAt;
        AudioDeleteAfter = processedAt.AddDays(7);
        FailureCode = null;
        Status = VoiceActivityDraftStatus.DraftReady;
        UpdatedAt = processedAt;
        RetryAt = null;
        BumpVersion();
        return true;
    }

    public void ScheduleTransientRetry(DateTimeOffset retryAt, DateTimeOffset now)
    {
        if (Status != VoiceActivityDraftStatus.Processing) throw new InvalidOperationException("Only processing work can be retried.");
        if (ProcessingAttemptCount >= 3)
        {
            MarkUnavailable("SPEECH_TEMPORARILY_UNAVAILABLE", now);
            return;
        }
        Status = VoiceActivityDraftStatus.Pending;
        RetryAt = retryAt;
        UpdatedAt = now;
        BumpVersion();
    }

    public void MarkUnavailable(string failureCode, DateTimeOffset processedAt)
    {
        if (Status is not (VoiceActivityDraftStatus.Pending or VoiceActivityDraftStatus.Processing)) return;
        if (string.IsNullOrWhiteSpace(failureCode) || failureCode.Length > 80)
            throw new ArgumentException("A safe failure code is required.", nameof(failureCode));
        ActivityType = null;
        OccurredAt = null;
        Details = null;
        UncertainFieldsJson = "[]";
        Confidence = null;
        FailureCode = failureCode.Trim();
        ProcessedAt = processedAt;
        AudioDeleteAfter = processedAt.AddDays(7);
        RetryAt = null;
        Status = VoiceActivityDraftStatus.Unavailable;
        UpdatedAt = processedAt;
        BumpVersion();
    }

    public void CancelForAuthorizationRevocation(DateTimeOffset cancelledAt)
    {
        if (Status is not (VoiceActivityDraftStatus.Pending or VoiceActivityDraftStatus.Processing)) return;
        ActivityType = null;
        OccurredAt = null;
        Details = null;
        UncertainFieldsJson = "[]";
        Confidence = null;
        FailureCode = "AUTHORIZATION_REVOKED";
        ProcessedAt = cancelledAt;
        AudioDeleteAfter = cancelledAt.AddDays(7);
        RetryAt = null;
        Status = VoiceActivityDraftStatus.Cancelled;
        UpdatedAt = cancelledAt;
        BumpVersion();
    }

    public void RevokeConsent(string actorUserId, DateTimeOffset revokedAt)
    {
        if (!ConsentedToExternalProcessing || ConsentRevokedAt.HasValue) return;
        if (!string.Equals(actorUserId, SubmittedByUserId, StringComparison.Ordinal))
            throw new InvalidOperationException("Only the consent actor can revoke this recording's consent.");
        ConsentedToExternalProcessing = false;
        ConsentRevokedAt = revokedAt;
        if (Status is VoiceActivityDraftStatus.AwaitingUpload or VoiceActivityDraftStatus.Pending or VoiceActivityDraftStatus.Processing)
        {
            Status = VoiceActivityDraftStatus.Cancelled;
            ProcessedAt = revokedAt;
            AudioDeleteAfter = revokedAt;
            RetryAt = null;
        }
        else if (Status is VoiceActivityDraftStatus.DraftReady or VoiceActivityDraftStatus.Unavailable)
        {
            AudioDeleteAfter = revokedAt;
        }
        UpdatedAt = revokedAt;
        BumpVersion();
    }

    public bool Edit(long expectedVersion, LogbookActivityType? activityType, DateTimeOffset? occurredAt,
        string? details, DateTimeOffset updatedAt)
    {
        if (expectedVersion != Version || Status != VoiceActivityDraftStatus.DraftReady) return false;
        if (activityType.HasValue && !Enum.IsDefined(activityType.Value)) throw new ArgumentOutOfRangeException(nameof(activityType));
        if (details?.Length > 4000) throw new ArgumentOutOfRangeException(nameof(details));
        ActivityType = activityType?.ToString();
        OccurredAt = occurredAt;
        Details = string.IsNullOrWhiteSpace(details) ? null : details.Trim();
        var uncertain = NormalizeUncertainFields([], activityType, occurredAt, Details);
        UncertainFieldsJson = JsonSerializer.Serialize(uncertain);
        UpdatedAt = updatedAt;
        BumpVersion();
        return true;
    }

    public VoiceConfirmationResult Confirm(string userId, Guid logbookEntryId, long expectedVersion, DateTimeOffset confirmedAt)
    {
        if (Status == VoiceActivityDraftStatus.Confirmed && LogbookEntryId.HasValue)
            return new VoiceConfirmationResult(false, true, LogbookEntryId.Value);
        if (Status != VoiceActivityDraftStatus.DraftReady || expectedVersion != Version ||
            string.IsNullOrWhiteSpace(userId) || logbookEntryId == Guid.Empty ||
            !Enum.TryParse<LogbookActivityType>(ActivityType, out _) || !OccurredAt.HasValue || string.IsNullOrWhiteSpace(Details))
            return new VoiceConfirmationResult(false, false, null);
        ConfirmedByUserId = userId.Trim();
        ConfirmedAt = confirmedAt;
        LogbookEntryId = logbookEntryId;
        AudioDeleteAfter = confirmedAt;
        Status = VoiceActivityDraftStatus.Confirmed;
        UpdatedAt = confirmedAt;
        BumpVersion();
        return new VoiceConfirmationResult(true, false, logbookEntryId);
    }

    public bool TryClaimAudioCleanup(DateTimeOffset now, DateTimeOffset staleClaimBefore)
    {
        if (!AudioDeleteAfter.HasValue || AudioDeleteAfter.Value > now || AudioDeletedAt.HasValue ||
            AudioDeleteRetryAt.HasValue && AudioDeleteRetryAt.Value > now ||
            AudioObjectKey is null || AudioCleanupClaimedAt.HasValue && AudioCleanupClaimedAt.Value >= staleClaimBefore) return false;
        AudioCleanupClaimedAt = now;
        AudioDeleteAttemptCount++;
        UpdatedAt = now;
        BumpVersion();
        return true;
    }

    public void MarkAudioDeleted(DateTimeOffset deletedAt)
    {
        if (!AudioDeleteAfter.HasValue || string.IsNullOrWhiteSpace(AudioObjectKey)) return;
        AudioDeletedAt = deletedAt;
        AudioObjectKey = null;
        AudioCleanupClaimedAt = null;
        AudioDeleteRetryAt = null;
        AudioDeleteLastError = null;
        UpdatedAt = deletedAt;
        BumpVersion();
    }

    public void MarkAudioDeleteFailed(string safeErrorCode, DateTimeOffset retryAt, DateTimeOffset failedAt)
    {
        if (string.IsNullOrWhiteSpace(safeErrorCode) || safeErrorCode.Length > 80)
            throw new ArgumentException("A safe storage error code is required.", nameof(safeErrorCode));
        AudioDeleteLastError = safeErrorCode.Trim();
        AudioCleanupClaimedAt = null;
        AudioDeleteRetryAt = retryAt;
        UpdatedAt = failedAt;
        BumpVersion();
    }

    public void MarkStatusNotified(long version)
    {
        if (version > NotifiedStatusVersion && version <= StatusVersion) NotifiedStatusVersion = version;
    }

    private static IReadOnlyList<string> NormalizeUncertainFields(IReadOnlyCollection<string> supplied,
        LogbookActivityType? activityType, DateTimeOffset? occurredAt, string? details)
    {
        var fields = new HashSet<string>(supplied.Where(item => item is "activityType" or "occurredAt" or "details"), StringComparer.Ordinal);
        if (!activityType.HasValue) fields.Add("activityType");
        if (!occurredAt.HasValue) fields.Add("occurredAt");
        if (string.IsNullOrWhiteSpace(details)) fields.Add("details");
        return fields.Order(StringComparer.Ordinal).ToArray();
    }

    private void BumpVersion()
    {
        Version++;
        StatusVersion++;
    }
}

public sealed record VoiceConfirmationResult(bool ConfirmedNow, bool AlreadyConfirmed, Guid? LogbookEntryId);
public sealed record VoiceDraftRequested(Guid EventId, Guid FarmId, Guid DraftId, long DraftVersion);
