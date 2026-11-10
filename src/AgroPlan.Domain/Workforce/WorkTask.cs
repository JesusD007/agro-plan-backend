namespace AgroPlan.Domain.Workforce;

public enum WorkTaskStatus
{
    Pending = 1,
    InProgress = 2,
    Submitted = 3,
    Verified = 4,
    Rejected = 5,
    CorrectionRequested = 6
}

/// <summary>A farm-bound assignment with an auditable worker lifecycle.</summary>
public sealed class WorkTask
{
    private WorkTask() { }

    public WorkTask(Guid id, Guid farmId, string assignedWorkerUserId, DateOnly dueDate,
        string description, bool photoRequired, decimal? wageAmount, string? wageUnit,
        Guid? parcelId, Guid? cropCycleId, string? operationKind = null)
    {
        if (id == Guid.Empty || farmId == Guid.Empty) throw new ArgumentException("Task and farm ids are required.");
        if (string.IsNullOrWhiteSpace(assignedWorkerUserId) || assignedWorkerUserId.Length > 450)
            throw new ArgumentException("An assigned worker is required.", nameof(assignedWorkerUserId));
        if (dueDate == default) throw new ArgumentException("A due date is required.", nameof(dueDate));
        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > 1000)
            throw new ArgumentException("Task description is required and must not exceed 1000 characters.", nameof(description));
        if (wageAmount is < 0) throw new ArgumentOutOfRangeException(nameof(wageAmount));
        if (wageAmount.HasValue != !string.IsNullOrWhiteSpace(wageUnit))
            throw new ArgumentException("Wage amount and unit must be supplied together.");
        if (wageUnit?.Trim().Length > 64) throw new ArgumentException("Wage unit must not exceed 64 characters.", nameof(wageUnit));
        if (parcelId.HasValue != cropCycleId.HasValue || parcelId.HasValue != !string.IsNullOrWhiteSpace(operationKind))
            throw new ArgumentException("Confirmed-record impacts require parcel, crop cycle, and operation kind together.");
        if (operationKind?.Trim().Length > 64) throw new ArgumentException("Operation kind must not exceed 64 characters.", nameof(operationKind));

        Id = id;
        FarmId = farmId;
        AssignedWorkerUserId = assignedWorkerUserId.Trim();
        DueDate = dueDate;
        Description = description.Trim();
        PhotoRequired = photoRequired;
        WageAmount = wageAmount;
        WageUnit = wageUnit?.Trim();
        ParcelId = parcelId;
        CropCycleId = cropCycleId;
        OperationKind = operationKind?.Trim().ToLowerInvariant();
        Status = WorkTaskStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid FarmId { get; private set; }
    public string AssignedWorkerUserId { get; private set; } = string.Empty;
    public DateOnly DueDate { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public WorkTaskStatus Status { get; private set; }
    public bool PhotoRequired { get; private set; }
    public decimal? WageAmount { get; private set; }
    public string? WageUnit { get; private set; }
    public Guid? ParcelId { get; private set; }
    public Guid? CropCycleId { get; private set; }
    public string? OperationKind { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }

    public void Start()
    {
        RequireStatus(WorkTaskStatus.Pending);
        Status = WorkTaskStatus.InProgress;
    }

    public void Submit(DateTimeOffset submittedAt)
    {
        if (submittedAt == default) throw new ArgumentException("Submission time is required.", nameof(submittedAt));
        if (Status is not (WorkTaskStatus.InProgress or WorkTaskStatus.CorrectionRequested))
            throw new InvalidOperationException("Only in-progress or correction-requested work can be submitted.");
        Status = WorkTaskStatus.Submitted;
        SubmittedAt = submittedAt;
    }

    public void Verify(DateTimeOffset verifiedAt)
    {
        RequireStatus(WorkTaskStatus.Submitted);
        if (verifiedAt == default) throw new ArgumentException("Verification time is required.", nameof(verifiedAt));
        Status = WorkTaskStatus.Verified;
        VerifiedAt = verifiedAt;
    }

    public void Reject()
    {
        RequireStatus(WorkTaskStatus.Submitted);
        Status = WorkTaskStatus.Rejected;
    }

    public void RequestCorrection()
    {
        RequireStatus(WorkTaskStatus.Submitted);
        Status = WorkTaskStatus.CorrectionRequested;
    }

    private void RequireStatus(WorkTaskStatus expected)
    {
        if (Status != expected) throw new InvalidOperationException($"Task must be {expected} to perform this action.");
    }
}
