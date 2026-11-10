namespace AgroPlan.Domain.Workforce;

public sealed class SyncOperationReceipt
{
    private SyncOperationReceipt() { }

    public SyncOperationReceipt(Guid id, Guid farmId, string workerUserId, Guid clientOperationId,
        string payloadFingerprint, string status, string resultJson, DateTimeOffset recordedAt)
    {
        if (id == Guid.Empty || farmId == Guid.Empty || clientOperationId == Guid.Empty)
            throw new ArgumentException("Receipt, farm, and operation ids are required.");
        if (string.IsNullOrWhiteSpace(workerUserId) || workerUserId.Length > 450)
            throw new ArgumentException("Worker is required.", nameof(workerUserId));
        if (payloadFingerprint.Length != 64 || !payloadFingerprint.All(Uri.IsHexDigit))
            throw new ArgumentException("A SHA-256 payload fingerprint is required.", nameof(payloadFingerprint));
        if (status is not ("Applied" or "Conflict")) throw new ArgumentException("Invalid terminal operation status.", nameof(status));
        if (string.IsNullOrWhiteSpace(resultJson) || recordedAt == default)
            throw new ArgumentException("A stable result and recorded time are required.");
        Id = id;
        FarmId = farmId;
        WorkerUserId = workerUserId.Trim();
        ClientOperationId = clientOperationId;
        PayloadFingerprint = payloadFingerprint.ToLowerInvariant();
        Status = status;
        ResultJson = resultJson;
        RecordedAt = recordedAt;
    }

    public Guid Id { get; private set; }
    public Guid FarmId { get; private set; }
    public string WorkerUserId { get; private set; } = string.Empty;
    public Guid ClientOperationId { get; private set; }
    public string PayloadFingerprint { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string ResultJson { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }
}
