namespace AgroPlan.Domain.Workforce;

/// <summary>Records a wage confirmation after task verification; it is not a payment.</summary>
public sealed class VerifiedWageEntry
{
    private VerifiedWageEntry() { }

    public VerifiedWageEntry(Guid id, Guid farmId, Guid workTaskId, string workerUserId,
        decimal amount, string unit, DateTimeOffset verifiedAt)
    {
        if (id == Guid.Empty || farmId == Guid.Empty || workTaskId == Guid.Empty)
            throw new ArgumentException("Wage, farm and task ids are required.");
        if (string.IsNullOrWhiteSpace(workerUserId) || workerUserId.Length > 450)
            throw new ArgumentException("Worker is required.", nameof(workerUserId));
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (string.IsNullOrWhiteSpace(unit) || unit.Trim().Length > 64)
            throw new ArgumentException("Wage unit is required.", nameof(unit));
        if (verifiedAt == default) throw new ArgumentException("Verification time is required.", nameof(verifiedAt));
        Id = id; FarmId = farmId; WorkTaskId = workTaskId; WorkerUserId = workerUserId.Trim();
        Amount = amount; Unit = unit.Trim(); VerifiedAt = verifiedAt;
    }

    public Guid Id { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid WorkTaskId { get; private set; }
    public string WorkerUserId { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Unit { get; private set; } = string.Empty;
    public DateTimeOffset VerifiedAt { get; private set; }
}
