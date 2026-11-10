using AgroPlan.Domain.Workforce;
using AgroPlan.Domain.Farms;
using Xunit;

namespace AgroPlan.UnitTests.Workforce;

public sealed class WorkTaskTests
{
    [Fact]
    public void Work_task_aggregate_is_available_for_assignment()
    {
        var aggregate = typeof(WorkerInvitation).Assembly.GetType("AgroPlan.Domain.Workforce.WorkTask");
        Assert.NotNull(aggregate);
    }
}
