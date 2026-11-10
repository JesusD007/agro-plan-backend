namespace AgroPlan.Domain.Workforce;

public sealed class TaskEvidenceSubmission
{
    private TaskEvidenceSubmission() { }

    public TaskEvidenceSubmission(Guid id, Guid farmId, Guid taskId, string workerUserId, int revision,
        Guid? previousSubmissionId, Guid clientOperationId, string uploadObjectKey, string contentType,
        long expectedSizeBytes, bool consentedToLocation, string? location,
        bool consentedToCapturedAt, DateTimeOffset? capturedAt, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        if (id == Guid.Empty || farmId == Guid.Empty || taskId == Guid.Empty || clientOperationId == Guid.Empty)
            throw new ArgumentException("Evidence, farm, task and operation ids are required.");
        if (string.IsNullOrWhiteSpace(workerUserId) || workerUserId.Length > 450)
            throw new ArgumentException("Worker is required.", nameof(workerUserId));
        if (revision < 1 || (revision == 1) == previousSubmissionId.HasValue)
            throw new ArgumentException("Evidence revisions must be sequential and link to the prior revision.");
        if (string.IsNullOrWhiteSpace(uploadObjectKey) || uploadObjectKey.Length > 1024)
            throw new ArgumentException("A private upload reference is required.", nameof(uploadObjectKey));
        if (contentType is not ("image/jpeg" or "image/png" or "image/webp"))
            throw new ArgumentException("Only JPEG, PNG, or WebP evidence is accepted.", nameof(contentType));
        if (expectedSizeBytes is <= 0 or > 10 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(expectedSizeBytes));
        if (!consentedToLocation && !string.IsNullOrWhiteSpace(location))
            throw new ArgumentException("Location cannot be stored without consent.", nameof(location));
        if (consentedToLocation && location?.Length > 256) throw new ArgumentException("Location is too long.", nameof(location));
        if (!consentedToCapturedAt && capturedAt.HasValue)
            throw new ArgumentException("Capture time cannot be stored without consent.", nameof(capturedAt));
        if (createdAt == default || expiresAt <= createdAt) throw new ArgumentException("Upload expiry must follow creation time.");

        Id = id; FarmId = farmId; WorkTaskId = taskId; WorkerUserId = workerUserId.Trim(); Revision = revision;
        PreviousSubmissionId = previousSubmissionId; ClientOperationId = clientOperationId;
        UploadObjectKey = uploadObjectKey; ContentType = contentType; ExpectedSizeBytes = expectedSizeBytes;
        ConsentedToLocation = consentedToLocation; Location = consentedToLocation ? location?.Trim() : null;
        ConsentedToCapturedAt = consentedToCapturedAt; CapturedAt = consentedToCapturedAt ? capturedAt : null;
        CreatedAt = createdAt; ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid WorkTaskId { get; private set; }
    public string WorkerUserId { get; private set; } = string.Empty;
    public int Revision { get; private set; }
    public Guid? PreviousSubmissionId { get; private set; }
    public Guid ClientOperationId { get; private set; }
    public string UploadObjectKey { get; private set; } = string.Empty;
    public string? ObjectKey { get; private set; }
    public string ContentType { get; private set; } = string.Empty;
    public long ExpectedSizeBytes { get; private set; }
    public long? SizeBytes { get; private set; }
    public bool ConsentedToLocation { get; private set; }
    public string? Location { get; private set; }
    public bool ConsentedToCapturedAt { get; private set; }
    public DateTimeOffset? CapturedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? FinalizedAt { get; private set; }

    public void Finalize(string objectKey, long sizeBytes, DateTimeOffset finalizedAt)
    {
        if (FinalizedAt.HasValue) return;
        if (finalizedAt > ExpiresAt) throw new InvalidOperationException("Evidence upload authorization has expired.");
        if (string.IsNullOrWhiteSpace(objectKey) || objectKey.Length > 1024)
            throw new ArgumentException("A private evidence object is required.", nameof(objectKey));
        if (sizeBytes != ExpectedSizeBytes) throw new InvalidOperationException("Uploaded evidence size does not match authorization.");
        ObjectKey = objectKey;
        SizeBytes = sizeBytes;
        FinalizedAt = finalizedAt;
    }
}
