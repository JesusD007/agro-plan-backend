namespace AgroPlan.Domain.Logbook;

public sealed class LogbookAttachment
{
    private LogbookAttachment() { }

    public LogbookAttachment(Guid id, Guid farmId, Guid logbookEntryId, string objectKey,
        string contentType, long expectedSizeBytes, string uploadedByUserId, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        if (id == Guid.Empty || farmId == Guid.Empty || logbookEntryId == Guid.Empty) throw new ArgumentException("Attachment identity is required.");
        if (expectedSizeBytes is <= 0 or > 10 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(expectedSizeBytes));
        Id = id;
        FarmId = farmId;
        LogbookEntryId = logbookEntryId;
        ObjectKey = objectKey;
        ContentType = contentType;
        ExpectedSizeBytes = expectedSizeBytes;
        UploadedByUserId = uploadedByUserId;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid LogbookEntryId { get; private set; }
    public string ObjectKey { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long ExpectedSizeBytes { get; private set; }
    public long? SizeBytes { get; private set; }
    public string UploadedByUserId { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? FinalizedAt { get; private set; }

    public void Finalize(long sizeBytes, DateTimeOffset finalizedAt)
    {
        if (FinalizedAt.HasValue) throw new InvalidOperationException("Attachment is already finalized.");
        if (sizeBytes != ExpectedSizeBytes || sizeBytes is <= 0 or > 10 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        SizeBytes = sizeBytes;
        FinalizedAt = finalizedAt;
    }
}
