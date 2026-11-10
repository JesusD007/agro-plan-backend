using AgroPlan.Application.Abstractions;

namespace AgroPlan.Infrastructure.Calendars;

/// <summary>Keeps calendar sync visible and retryable until a provider is explicitly configured.</summary>
public sealed class UnconfiguredWorkCalendar : IExternalWorkCalendar
{
    public Task<ExternalCalendarDeliveryResult> UpsertAsync(ExternalWorkCalendarEntry entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ExternalCalendarDeliveryResult(false, true, "CALENDAR_PROVIDER_UNCONFIGURED"));
    }
}
