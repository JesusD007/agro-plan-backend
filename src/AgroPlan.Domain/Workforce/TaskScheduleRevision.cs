namespace AgroPlan.Domain.Workforce;

public enum TaskScheduleRevisionStatus
{
    Proposed = 1,
    Confirmed = 2,
    Stale = 3
}

/// <summary>An append-only, source-backed proposal to change an assigned task date.</summary>
public sealed class TaskScheduleRevision
{
    private TaskScheduleRevision() { }

    public TaskScheduleRevision(Guid id, Guid farmId, Guid workTaskId, Guid sourceEventId,
        Guid sourceRecordId, int sourceRecordVersion, int taskVersion, DateOnly previousDate,
        DateOnly proposedDate, string explanation, DateTimeOffset proposedAt)
    {
        if (id == Guid.Empty || farmId == Guid.Empty || workTaskId == Guid.Empty || sourceEventId == Guid.Empty || sourceRecordId == Guid.Empty)
            throw new ArgumentException("Revision, farm, task, and source identifiers are required.");
        if (sourceRecordVersion <= 0 || taskVersion <= 0) throw new ArgumentOutOfRangeException(nameof(sourceRecordVersion));
        if (proposedDate == default || previousDate == default || previousDate == proposedDate)
            throw new ArgumentException("A revision requires distinct, valid previous and proposed dates.");
        if (string.IsNullOrWhiteSpace(explanation) || explanation.Trim().Length > 1000)
            throw new ArgumentException("A bounded explanation is required.", nameof(explanation));
        if (proposedAt == default) throw new ArgumentException("Proposal time is required.", nameof(proposedAt));
        Id = id; FarmId = farmId; WorkTaskId = workTaskId; SourceEventId = sourceEventId;
        SourceRecordId = sourceRecordId; SourceRecordVersion = sourceRecordVersion; TaskVersion = taskVersion;
        PreviousDate = previousDate; ProposedDate = proposedDate; Explanation = explanation.Trim();
        ProposedAt = proposedAt; Status = TaskScheduleRevisionStatus.Proposed;
    }

    public Guid Id { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid WorkTaskId { get; private set; }
    public Guid SourceEventId { get; private set; }
    public Guid SourceRecordId { get; private set; }
    public int SourceRecordVersion { get; private set; }
    public int TaskVersion { get; private set; }
    public DateOnly PreviousDate { get; private set; }
    public DateOnly ProposedDate { get; private set; }
    public string Explanation { get; private set; } = string.Empty;
    public TaskScheduleRevisionStatus Status { get; private set; }
    public DateTimeOffset ProposedAt { get; private set; }
    public string? ConfirmedByUserId { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }

    public void Confirm(string administratorUserId, DateTimeOffset confirmedAt, int currentTaskVersion)
    {
        if (Status != TaskScheduleRevisionStatus.Proposed) throw new InvalidOperationException("Only a pending revision can be confirmed.");
        if (string.IsNullOrWhiteSpace(administratorUserId) || administratorUserId.Length > 450)
            throw new ArgumentException("A farm administrator is required.", nameof(administratorUserId));
        if (confirmedAt == default) throw new ArgumentException("Confirmation time is required.", nameof(confirmedAt));
        if (currentTaskVersion != TaskVersion) throw new InvalidOperationException("The task changed after this proposal was created.");
        ConfirmedByUserId = administratorUserId.Trim(); ConfirmedAt = confirmedAt; Status = TaskScheduleRevisionStatus.Confirmed;
    }

    public void MarkStale()
    {
        if (Status != TaskScheduleRevisionStatus.Proposed) return;
        Status = TaskScheduleRevisionStatus.Stale;
    }
}
