using AgroPlan.Domain.Workforce;
using Xunit;

namespace AgroPlan.UnitTests.Workforce;

public sealed class WorkSyncTests
{
    [Fact]
    public void Sync_operation_receipt_aggregate_is_available()
    {
        var assembly = typeof(WorkTask).Assembly;
        Assert.NotNull(assembly.GetType("AgroPlan.Domain.Workforce.SyncOperationReceipt"));
    }

    [Fact]
    public void Receipt_requires_a_sha256_fingerprint_and_stable_terminal_result()
    {
        var farmId = Guid.NewGuid();
        var workerId = "worker-1";
        var operationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() => new SyncOperationReceipt(Guid.NewGuid(), farmId, workerId,
            operationId, "short", "Applied", "{}", now));
        Assert.Throws<ArgumentException>(() => new SyncOperationReceipt(Guid.NewGuid(), farmId, workerId,
            operationId, new string('a', 64), "Retryable", "{}", now));
        var receipt = new SyncOperationReceipt(Guid.NewGuid(), farmId, workerId, operationId,
            new string('A', 64), "Applied", "{\"taskVersion\":2}", now);

        Assert.Equal(new string('a', 64), receipt.PayloadFingerprint);
        Assert.Equal("Applied", receipt.Status);
        Assert.Equal("{\"taskVersion\":2}", receipt.ResultJson);
    }

    [Fact]
    public void Task_sync_conflict_aggregate_is_available()
    {
        Assert.NotNull(typeof(WorkTask).Assembly.GetType("AgroPlan.Domain.Workforce.TaskSyncConflict"));
    }
}
