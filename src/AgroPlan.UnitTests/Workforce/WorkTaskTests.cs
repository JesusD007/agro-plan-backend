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
    public void Review_decision_and_verified_wage_are_separate_auditable_records()
    {
        Assert.NotNull(typeof(WorkTask).Assembly.GetType("AgroPlan.Domain.Workforce.TaskReviewDecision"));
        Assert.NotNull(typeof(WorkTask).Assembly.GetType("AgroPlan.Domain.Workforce.VerifiedWageEntry"));
    }

    private static WorkTask Create() => new(Guid.NewGuid(), Guid.NewGuid(), "worker-1",
        DateOnly.FromDateTime(DateTime.UtcNow), "Prune", false, 125m, "day", null, null);
}
