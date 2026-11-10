using AgroPlan.Domain.Workforce;
using AgroPlan.Application.Workforce;
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
}
