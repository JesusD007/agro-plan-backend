using AgroPlan.Domain.Workforce;
using AgroPlan.Application.Workforce;
using AgroPlan.Application.Abstractions;
using AgroPlan.Infrastructure.Calendars;
using Xunit;

namespace AgroPlan.UnitTests.Workforce;

public sealed class TaskScheduleTests
{
    [Fact]
    public void Confirmed_record_projection_and_append_only_schedule_revision_are_available()
    {
        var domainAssembly = typeof(WorkTask).Assembly;
        Assert.NotNull(domainAssembly.GetType("AgroPlan.Domain.Workforce.TaskScheduleRevision"));
        Assert.NotNull(typeof(WorkTaskImpactProjector).Assembly.GetType("AgroPlan.Application.Workforce.WorkTaskImpactProjector"));
    }

    [Fact]
    public void Confirmed_record_produces_explained_proposal_for_only_exact_pending_match()
    {
        var farm = Guid.NewGuid(); var parcel = Guid.NewGuid(); var cycle = Guid.NewGuid();
        var expected = new WorkTask(Guid.NewGuid(), farm, "worker-a", new DateOnly(2026, 11, 12), "Irrigate", false,
            null, null, parcel, cycle, "irrigation");
        var wrongFarm = new WorkTask(Guid.NewGuid(), Guid.NewGuid(), "worker-b", new DateOnly(2026, 11, 12), "Irrigate", false,
            null, null, parcel, cycle, "irrigation");
        var record = new ConfirmedFarmRecord(Guid.NewGuid(), farm, parcel, cycle, "irrigation", Guid.NewGuid(), 1,
            new DateOnly(2026, 11, 10));

        var impacts = new WorkTaskImpactProjector().Project(record, [expected, wrongFarm]);

        var impact = Assert.Single(impacts);
        Assert.Equal(expected.Id, impact.Task.Id);
        Assert.Equal(new DateOnly(2026, 11, 10), impact.ProposedDate);
        Assert.Contains(record.SourceRecordId.ToString("D"), impact.Explanation);
        Assert.Equal(new DateOnly(2026, 11, 12), expected.DueDate);
    }

    [Fact]
    public void Schedule_revision_requires_admin_confirmation_and_rechecks_task_version()
    {
        var revision = new TaskScheduleRevision(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 1, 2, new DateOnly(2026, 11, 12), new DateOnly(2026, 11, 10),
            "Confirmed record explains the proposed change.", DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => revision.Confirm("admin", DateTimeOffset.UtcNow, 3));
        revision.Confirm("admin", DateTimeOffset.UtcNow, 2);
        Assert.Equal(TaskScheduleRevisionStatus.Confirmed, revision.Status);
        Assert.Equal(new DateOnly(2026, 11, 12), revision.PreviousDate);
        Assert.Equal(new DateOnly(2026, 11, 10), revision.ProposedDate);
        Assert.Throws<InvalidOperationException>(() => revision.Confirm("admin", DateTimeOffset.UtcNow, 2));
    }

    [Fact]
    public void Calendar_adapter_contract_and_per_event_delivery_status_are_available()
    {
        var applicationAssembly = typeof(WorkTaskImpactProjector).Assembly;
        var domainAssembly = typeof(WorkTask).Assembly;
        Assert.NotNull(applicationAssembly.GetType("AgroPlan.Application.Abstractions.IExternalWorkCalendar"));
        Assert.NotNull(domainAssembly.GetType("AgroPlan.Domain.Workforce.CalendarDeliveryStatus"));
    }

    [Fact]
    public async Task Unconfigured_calendar_remains_explicitly_retryable()
    {
        var adapter = new UnconfiguredWorkCalendar();
        var result = await adapter.UpsertAsync(new ExternalWorkCalendarEntry(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "worker", new DateOnly(2026, 11, 20), 2, Guid.NewGuid().ToString("N")), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.True(result.Retryable);
        Assert.Equal("CALENDAR_PROVIDER_UNCONFIGURED", result.ErrorCode);
    }

    [Fact]
    public void Delivery_status_persists_backoff_and_makes_success_terminal()
    {
        var now = DateTimeOffset.UtcNow;
        var status = new CalendarDeliveryStatus(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 2, now);
        status.StartAttempt(now);
        status.MarkFailed("NETWORK", true, now, TimeSpan.FromSeconds(5), 3);
        Assert.Equal(CalendarDeliveryState.Retryable, status.State);
        Assert.False(status.CanAttempt(now.AddSeconds(4)));
        status.StartAttempt(now.AddSeconds(5));
        status.MarkSucceeded(now.AddSeconds(6));
        Assert.Equal(CalendarDeliveryState.Succeeded, status.State);
        Assert.False(status.CanAttempt(now.AddMinutes(1)));
        Assert.Equal(2, status.AttemptCount);
    }

    [Fact]
    public void Permanent_calendar_failure_is_visible_and_cannot_be_retried()
    {
        var now = DateTimeOffset.UtcNow;
        var status = new CalendarDeliveryStatus(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 1, now);
        status.StartAttempt(now);
        status.MarkFailed("INVALID_CREDENTIALS", false, now, TimeSpan.Zero, 5);

        Assert.Equal(CalendarDeliveryState.Terminal, status.State);
        Assert.Equal("INVALID_CREDENTIALS", status.LastErrorCode);
        Assert.False(status.CanAttempt(now.AddHours(1)));
    }
}
