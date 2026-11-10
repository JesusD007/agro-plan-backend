using AgroPlan.Domain.Workforce;
using Xunit;

namespace AgroPlan.UnitTests.Workforce;

public sealed class WorkTaskTests
{
    [Fact]
    public void Work_task_aggregate_is_available_for_assignment()
    {
        var task = Create();
        Assert.Equal(WorkTaskStatus.Pending, task.Status);

        task.Start();
        Assert.Equal(WorkTaskStatus.InProgress, task.Status);
        task.Submit(DateTimeOffset.UtcNow);
        Assert.Equal(WorkTaskStatus.Submitted, task.Status);
        task.RequestCorrection();
        task.Submit(DateTimeOffset.UtcNow);
        task.Verify(DateTimeOffset.UtcNow);
        Assert.Equal(WorkTaskStatus.Verified, task.Status);
        Assert.Throws<InvalidOperationException>(() => task.Reject());
    }

    [Fact]
    public void Task_requires_worker_and_complete_same_farm_impact_links()
    {
        Assert.Throws<ArgumentException>(() => new WorkTask(Guid.NewGuid(), Guid.NewGuid(), " ",
            DateOnly.FromDateTime(DateTime.UtcNow), "Prune", false, null, null, null, null));
        Assert.Throws<ArgumentException>(() => new WorkTask(Guid.NewGuid(), Guid.NewGuid(), "worker-1",
            DateOnly.FromDateTime(DateTime.UtcNow), "Prune", false, null, null, Guid.NewGuid(), null));
        Assert.Throws<ArgumentException>(() => new WorkTask(Guid.NewGuid(), Guid.NewGuid(), "worker-1",
            DateOnly.FromDateTime(DateTime.UtcNow), "Prune", false, null, null, Guid.NewGuid(), null, "prune"));
    }

    [Fact]
    public void Wage_amount_and_unit_are_explicit_and_operation_kind_is_normalized()
    {
        Assert.Throws<ArgumentException>(() => new WorkTask(Guid.NewGuid(), Guid.NewGuid(), "worker-1",
            DateOnly.FromDateTime(DateTime.UtcNow), "Prune", false, 100m, null, null, null));
        var task = new WorkTask(Guid.NewGuid(), Guid.NewGuid(), "worker-1", DateOnly.FromDateTime(DateTime.UtcNow),
            "Spray", true, 125m, " day ", Guid.NewGuid(), Guid.NewGuid(), " APPLY_INPUT ");
        Assert.Equal("day", task.WageUnit);
        Assert.Equal("apply_input", task.OperationKind);
    }

    [Fact]
    public void Schedule_changes_advance_the_task_version()
    {
        var task = Create();
        Assert.Equal(1, task.Version);
        task.Update("Inspect the east rows", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)));
        Assert.Equal(2, task.Version);
        task.Start();
        Assert.Equal(3, task.Version);
    }

    [Fact]
    public void Review_decision_requires_reason_and_verified_wage_is_an_immutable_fact()
    {
        var farmId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() => new TaskReviewDecision(Guid.NewGuid(), farmId, taskId,
            submissionId, "admin-1", TaskReviewAction.Rejected, null, now));
        var decision = new TaskReviewDecision(Guid.NewGuid(), farmId, taskId, submissionId, "admin-1",
            TaskReviewAction.CorrectionRequested, " Please upload a clearer image. ", now);
        Assert.Equal("Please upload a clearer image.", decision.Reason);
        var wage = new VerifiedWageEntry(Guid.NewGuid(), farmId, taskId, "worker-1", 125m, "day", now);
        Assert.Equal(125m, wage.Amount);
        Assert.Equal("day", wage.Unit);
    }

    private static WorkTask Create() => new(Guid.NewGuid(), Guid.NewGuid(), "worker-1",
        DateOnly.FromDateTime(DateTime.UtcNow), "Prune", false, 125m, "day", null, null);
}
