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
        var now = DateTimeOffset.UtcNow;
        var conflict = new TaskSyncConflict(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "worker-1", Guid.NewGuid(),
            2, "{\"description\":\"client\"}", 3, "{\"description\":\"server\"}", now);
        var resolution = new TaskSyncConflictResolution(Guid.NewGuid(), conflict.FarmId, conflict.Id, "owner-1",
            "Client", "{\"description\":\"client\"}", 4, now);

        Assert.Equal(2, conflict.BaseVersion);
        Assert.Equal(3, conflict.ServerVersion);
        Assert.Equal("{\"description\":\"client\"}", conflict.ClientValuesJson);
        Assert.Equal("Client", resolution.Resolution);
        Assert.Equal(4, resolution.ResultTaskVersion);
        Assert.Throws<ArgumentOutOfRangeException>(() => new TaskSyncConflict(Guid.NewGuid(), conflict.FarmId,
            conflict.WorkTaskId, "worker-1", Guid.NewGuid(), 3, "{}", 3, "{}", now));
    }
}
