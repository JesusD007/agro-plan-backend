using AgroPlan.Domain.Workforce;
using Xunit;

namespace AgroPlan.UnitTests.Workforce;

public sealed class WorkSyncTests
{
    [Fact]
    public void Sync_operation_receipt_and_conflict_aggregates_are_available()
    {
        var assembly = typeof(WorkTask).Assembly;
        Assert.NotNull(assembly.GetType("AgroPlan.Domain.Workforce.SyncOperationReceipt"));
        Assert.NotNull(assembly.GetType("AgroPlan.Domain.Workforce.TaskSyncConflict"));
    }
}
