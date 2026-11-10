using AgroPlan.Domain.Workforce;

namespace AgroPlan.Application.Workforce;

public sealed record WorkTaskImpact(WorkTask Task, DateOnly ProposedDate, string Explanation);
public sealed record ConfirmedFarmRecord(Guid EventId, Guid FarmId, Guid ParcelId, Guid? CropCycleId,
    string OperationKind, Guid SourceRecordId, int SourceRecordVersion, DateOnly OccurredOn);

/// <summary>Produces deterministic, reviewable proposals without mutating a task.</summary>
public sealed class WorkTaskImpactProjector
{
    public IReadOnlyList<WorkTaskImpact> Project(ConfirmedFarmRecord record, IEnumerable<WorkTask> candidates)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(candidates);
        if (record.EventId == Guid.Empty || record.FarmId == Guid.Empty || record.ParcelId == Guid.Empty ||
            record.SourceRecordId == Guid.Empty || record.SourceRecordVersion <= 0 || record.OccurredOn == default ||
            string.IsNullOrWhiteSpace(record.OperationKind) || !record.CropCycleId.HasValue)
            return [];
        var operation = record.OperationKind.Trim().ToLowerInvariant();
        return candidates.Where(task => task.FarmId == record.FarmId && task.ParcelId == record.ParcelId &&
                task.CropCycleId == record.CropCycleId && string.Equals(task.OperationKind, operation, StringComparison.OrdinalIgnoreCase) &&
                task.Status is WorkTaskStatus.Pending or WorkTaskStatus.InProgress && task.DueDate != record.OccurredOn)
            .Select(task => new WorkTaskImpact(task, record.OccurredOn,
                $"Confirmed {operation} record {record.SourceRecordId:D} version {record.SourceRecordVersion} proposes changing the due date from {task.DueDate:yyyy-MM-dd} to {record.OccurredOn:yyyy-MM-dd}."))
            .ToArray();
    }
}
