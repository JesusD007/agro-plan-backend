namespace AgroPlan.Domain.Planning;

public enum SowingProposalStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
    Confirmed = 3
}

public sealed class SowingProposal
{
    private SowingProposal() { }

    public SowingProposal(
        Guid id,
        Guid farmId,
        Guid cropCycleId,
        string submittedByUserId,
        string inputSnapshotJson,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty) throw new ArgumentException("Proposal id is required.", nameof(id));
        if (farmId == Guid.Empty) throw new ArgumentException("Farm id is required.", nameof(farmId));
        if (cropCycleId == Guid.Empty) throw new ArgumentException("Crop cycle id is required.", nameof(cropCycleId));
        if (string.IsNullOrWhiteSpace(submittedByUserId)) throw new ArgumentException("Submitter id is required.", nameof(submittedByUserId));
        if (string.IsNullOrWhiteSpace(inputSnapshotJson)) throw new ArgumentException("Input snapshot is required.", nameof(inputSnapshotJson));

        Id = id;
        FarmId = farmId;
        CropCycleId = cropCycleId;
        SubmittedByUserId = submittedByUserId.Trim();
        InputSnapshotJson = inputSnapshotJson;
        Status = SowingProposalStatus.Pending;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        StatusChangedAt = createdAt;
        StatusVersion = 1;
    }

    public Guid Id { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid CropCycleId { get; private set; }
    public string SubmittedByUserId { get; private set; } = string.Empty;
    public string InputSnapshotJson { get; private set; } = "{}";
    public string? ResultJson { get; private set; }
    public SowingProposalStatus Status { get; private set; }
    public string? FailureCode { get; private set; }
    public string? FailureSummary { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset StatusChangedAt { get; private set; }
    public long StatusVersion { get; private set; }
    public long NotifiedStatusVersion { get; private set; }
    public string? ConfirmedByUserId { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }

    public void MarkCompleted(string resultJson, DateTimeOffset completedAt)
    {
        if (Status != SowingProposalStatus.Pending) return;
        if (string.IsNullOrWhiteSpace(resultJson)) throw new ArgumentException("Result is required.", nameof(resultJson));
        ResultJson = resultJson;
        FailureCode = null;
        FailureSummary = null;
        SetStatus(SowingProposalStatus.Completed, completedAt);
    }

    public void MarkFailed(string code, string summary, DateTimeOffset failedAt)
    {
        if (Status != SowingProposalStatus.Pending) return;
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Failure code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(summary)) throw new ArgumentException("Failure summary is required.", nameof(summary));
        FailureCode = code.Trim();
        FailureSummary = summary.Trim();
        SetStatus(SowingProposalStatus.Failed, failedAt);
    }

    public bool Confirm(string userId, DateTimeOffset confirmedAt)
    {
        if (Status != SowingProposalStatus.Completed || string.IsNullOrWhiteSpace(userId)) return false;
        ConfirmedByUserId = userId.Trim();
        ConfirmedAt = confirmedAt;
        SetStatus(SowingProposalStatus.Confirmed, confirmedAt);
        return true;
    }

    public void MarkStatusNotified(long version)
    {
        if (version > NotifiedStatusVersion && version <= StatusVersion)
            NotifiedStatusVersion = version;
    }

    private void SetStatus(SowingProposalStatus status, DateTimeOffset changedAt)
    {
        Status = status;
        UpdatedAt = changedAt;
        StatusChangedAt = changedAt;
        StatusVersion++;
    }
}
