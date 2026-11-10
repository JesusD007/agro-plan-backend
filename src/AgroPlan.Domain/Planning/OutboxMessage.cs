namespace AgroPlan.Domain.Planning;

public sealed class OutboxMessage
{
    private OutboxMessage() { }

    public OutboxMessage(Guid id, Guid aggregateId, string eventType, string payloadJson, DateTimeOffset occurredAt)
    {
        if (id == Guid.Empty) throw new ArgumentException("Outbox id is required.", nameof(id));
        if (aggregateId == Guid.Empty) throw new ArgumentException("Aggregate id is required.", nameof(aggregateId));
        if (string.IsNullOrWhiteSpace(eventType)) throw new ArgumentException("Event type is required.", nameof(eventType));
        if (string.IsNullOrWhiteSpace(payloadJson)) throw new ArgumentException("Event payload is required.", nameof(payloadJson));

        Id = id;
        AggregateId = aggregateId;
        EventType = eventType.Trim();
        PayloadJson = payloadJson;
        OccurredAt = occurredAt;
        AvailableAt = occurredAt;
    }

    public Guid Id { get; private set; }
    public Guid AggregateId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = "{}";
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset AvailableAt { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset? DeadLetteredAt { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }

    public void Claim(DateTimeOffset now, TimeSpan lease)
    {
        if (PublishedAt.HasValue || DeadLetteredAt.HasValue) return;
        AttemptCount++;
        LockedUntil = now.Add(lease);
    }

    public void MarkPublished(DateTimeOffset publishedAt)
    {
        if (DeadLetteredAt.HasValue) return;
        PublishedAt = publishedAt;
        LockedUntil = null;
        LastError = null;
    }

    public bool MarkDeliveryFailed(string errorType, DateTimeOffset failedAt, TimeSpan retryDelay, int maxAttempts)
    {
        if (PublishedAt.HasValue || DeadLetteredAt.HasValue) return false;
        LastError = string.IsNullOrWhiteSpace(errorType) ? "PublishFailure" : errorType.Trim();
        LockedUntil = null;
        if (AttemptCount >= maxAttempts)
        {
            DeadLetteredAt = failedAt;
            return true;
        }

        AvailableAt = failedAt.Add(retryDelay);
        return false;
    }
}
