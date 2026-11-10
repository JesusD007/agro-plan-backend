namespace AgroPlan.Application.Abstractions;

public sealed record ExternalWorkCalendarEntry(Guid FarmId, Guid TaskId, Guid RevisionId, string WorkerUserId,
    DateOnly DueDate, int TaskVersion, string IdempotencyKey);

public sealed record ExternalCalendarDeliveryResult(bool Succeeded, bool Retryable, string? ErrorCode);

/// <summary>Provider-neutral calendar synchronization port. Implementations must upsert by idempotency key.</summary>
public interface IExternalWorkCalendar
{
    Task<ExternalCalendarDeliveryResult> UpsertAsync(ExternalWorkCalendarEntry entry, CancellationToken cancellationToken);
}
