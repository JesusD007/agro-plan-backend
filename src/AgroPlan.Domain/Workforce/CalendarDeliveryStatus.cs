namespace AgroPlan.Domain.Workforce;

public enum CalendarDeliveryState
{
    Pending = 1,
    Succeeded = 2,
    Retryable = 3,
    Terminal = 4
}

/// <summary>Durable per-event result for an external calendar side effect.</summary>
public sealed class CalendarDeliveryStatus
{
    private CalendarDeliveryStatus() { }

    public CalendarDeliveryStatus(Guid id, Guid outboxEventId, Guid farmId, Guid workTaskId, Guid revisionId,
        int taskVersion, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || outboxEventId == Guid.Empty || farmId == Guid.Empty || workTaskId == Guid.Empty || revisionId == Guid.Empty)
            throw new ArgumentException("Calendar delivery identifiers are required.");
        if (taskVersion <= 0) throw new ArgumentOutOfRangeException(nameof(taskVersion));
        if (createdAt == default) throw new ArgumentException("Creation time is required.", nameof(createdAt));
        Id = id; OutboxEventId = outboxEventId; FarmId = farmId; WorkTaskId = workTaskId;
        RevisionId = revisionId; TaskVersion = taskVersion; CreatedAt = createdAt;
        State = CalendarDeliveryState.Pending;
    }

    public Guid Id { get; private set; }
    public Guid OutboxEventId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid WorkTaskId { get; private set; }
    public Guid RevisionId { get; private set; }
    public int TaskVersion { get; private set; }
    public CalendarDeliveryState State { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastErrorCode { get; private set; }
    public DateTimeOffset? LastAttemptAt { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public bool CanAttempt(DateTimeOffset now) => State is CalendarDeliveryState.Pending or CalendarDeliveryState.Retryable &&
        (!NextAttemptAt.HasValue || NextAttemptAt <= now);

    public void StartAttempt(DateTimeOffset now)
    {
        if (!CanAttempt(now)) throw new InvalidOperationException("Calendar delivery is not due for an attempt.");
        AttemptCount++; LastAttemptAt = now; NextAttemptAt = null; State = CalendarDeliveryState.Pending;
    }

    public void MarkSucceeded(DateTimeOffset deliveredAt)
    {
        if (State != CalendarDeliveryState.Pending || AttemptCount == 0) throw new InvalidOperationException("A delivery attempt must be active before success.");
        if (deliveredAt == default) throw new ArgumentException("Delivery time is required.", nameof(deliveredAt));
        State = CalendarDeliveryState.Succeeded; DeliveredAt = deliveredAt; LastErrorCode = null; NextAttemptAt = null;
    }

    public void MarkFailed(string errorCode, bool retryable, DateTimeOffset failedAt, TimeSpan retryDelay, int maxAttempts)
    {
        if (State != CalendarDeliveryState.Pending || AttemptCount == 0) throw new InvalidOperationException("A delivery attempt must be active before failure.");
        if (string.IsNullOrWhiteSpace(errorCode) || errorCode.Length > 80) throw new ArgumentException("A bounded error code is required.", nameof(errorCode));
        if (failedAt == default || retryDelay < TimeSpan.Zero || maxAttempts < 1) throw new ArgumentOutOfRangeException(nameof(failedAt));
        LastErrorCode = errorCode.Trim();
        if (retryable && AttemptCount < maxAttempts)
        {
            State = CalendarDeliveryState.Retryable;
            NextAttemptAt = failedAt.Add(retryDelay);
        }
        else
        {
            State = CalendarDeliveryState.Terminal;
            NextAttemptAt = null;
        }
    }
}
