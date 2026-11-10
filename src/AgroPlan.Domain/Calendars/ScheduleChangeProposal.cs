using System.Text.Json;

namespace AgroPlan.Domain.Calendars;

public enum ScheduleChangeProposalStatus { Pending, Processing, Explained, Confirmed, Rejected, Stale, Failed }

/// <summary>An immutable, deterministic calendar delta. AI explanations are attached as advisory text only.</summary>
public sealed class ScheduleChangeProposal
{
    private ScheduleChangeProposal() { }

    public ScheduleChangeProposal(Guid id, Guid farmId, Guid cropCycleId, int version, string evidenceVersion,
        IReadOnlyCollection<ScheduleTaskDelta> deltas, IReadOnlyCollection<string> evidenceIds, DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        if (id == Guid.Empty || farmId == Guid.Empty || cropCycleId == Guid.Empty || version < 1)
            throw new ArgumentException("Proposal, farm, cycle and positive version are required.");
        if (string.IsNullOrWhiteSpace(evidenceVersion)) throw new ArgumentException("Evidence version is required.");
        if (deltas.Count == 0 || evidenceIds.Count == 0) throw new ArgumentException("A proposal requires deterministic changes and evidence.");
        if (expiresAt <= createdAt) throw new ArgumentException("Proposal expiry must be after creation.");
        Id = id; FarmId = farmId; CropCycleId = cropCycleId; Version = version;
        EvidenceVersion = evidenceVersion.Trim(); DeltasJson = JsonSerializer.Serialize(deltas);
        EvidenceIdsJson = JsonSerializer.Serialize(evidenceIds.Distinct(StringComparer.Ordinal).ToArray());
        CreatedAt = createdAt; ExpiresAt = expiresAt; Status = ScheduleChangeProposalStatus.Pending;
    }

    public Guid Id { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid CropCycleId { get; private set; }
    public int Version { get; private set; }
    public string EvidenceVersion { get; private set; } = "";
    public string DeltasJson { get; private set; } = "[]";
    public string EvidenceIdsJson { get; private set; } = "[]";
    public string? ExplanationJson { get; private set; }
    public string? ExplanationProvider { get; private set; }
    public string? ExplanationModel { get; private set; }
    public string? ExplanationPromptVersion { get; private set; }
    public string? ExplanationSchemaVersion { get; private set; }
    public ScheduleChangeProposalStatus Status { get; private set; }
    public string? FailureCode { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public string? ConfirmedBy { get; private set; }
    public DateTimeOffset? ProcessingStartedAt { get; private set; }
    public int ProcessingAttempts { get; private set; }
    public int StatusVersion { get; private set; } = 1;
    public IReadOnlyList<ScheduleTaskDelta> Deltas => JsonSerializer.Deserialize<ScheduleTaskDelta[]>(DeltasJson) ?? [];
    public IReadOnlyList<string> EvidenceIds => JsonSerializer.Deserialize<string[]>(EvidenceIdsJson) ?? [];

    public bool TryClaim(DateTimeOffset now)
    {
        if (Status != ScheduleChangeProposalStatus.Pending || ExpiresAt <= now) return false;
        Status = ScheduleChangeProposalStatus.Processing; ProcessingStartedAt = now; ProcessingAttempts++; StatusVersion++; return true;
    }
    public void SetExplanation(CalendarExplanationDraft draft, DateTimeOffset now)
    {
        if (Status != ScheduleChangeProposalStatus.Processing || ExpiresAt <= now) throw new InvalidOperationException("Proposal is no longer current.");
        ExplanationJson = JsonSerializer.Serialize(draft); ExplanationProvider = draft.Provider; ExplanationModel = draft.Model;
        ExplanationPromptVersion = draft.PromptVersion; ExplanationSchemaVersion = draft.SchemaVersion;
        Status = ScheduleChangeProposalStatus.Explained; ProcessingStartedAt = null; StatusVersion++;
    }
    public void Fail(string code) { if (Status is ScheduleChangeProposalStatus.Confirmed or ScheduleChangeProposalStatus.Rejected) return; FailureCode = code; Status = ScheduleChangeProposalStatus.Failed; ProcessingStartedAt = null; StatusVersion++; }
    public bool Confirm(int version, string evidenceVersion, string producerId, DateTimeOffset now)
    {
        if (Status != ScheduleChangeProposalStatus.Explained || version != Version || evidenceVersion != EvidenceVersion || ExpiresAt <= now || string.IsNullOrWhiteSpace(producerId)) return false;
        ConfirmedBy = producerId; ConfirmedAt = now; Status = ScheduleChangeProposalStatus.Confirmed; StatusVersion++; return true;
    }
}

public sealed record ScheduleTaskDelta(string TaskKey, DateOnly? PreviousDate, DateOnly ProposedDate, IReadOnlyList<string> EvidenceIds);
public sealed record CalendarExplanationInput(int ProposalVersion, string EvidenceVersion, IReadOnlyList<ScheduleTaskDelta> Deltas, IReadOnlyList<string> AllowedEvidenceIds);
public sealed record CalendarExplanationDraft(string Summary, IReadOnlyList<string> SourceIds, string Provider, string Model, string PromptVersion, string SchemaVersion, DateTimeOffset GeneratedAt);

public sealed class CalendarScheduleItem
{
    private CalendarScheduleItem() { }
    public CalendarScheduleItem(Guid farmId, Guid cropCycleId, string taskKey, DateOnly dueDate, DateTimeOffset updatedAt)
    {
        if (farmId == Guid.Empty || cropCycleId == Guid.Empty || string.IsNullOrWhiteSpace(taskKey)) throw new ArgumentException("Farm, cycle and task are required.");
        FarmId = farmId; CropCycleId = cropCycleId; TaskKey = taskKey.Trim(); DueDate = dueDate; UpdatedAt = updatedAt;
    }
    public Guid FarmId { get; private set; }
    public Guid CropCycleId { get; private set; }
    public string TaskKey { get; private set; } = "";
    public DateOnly DueDate { get; private set; }
    public Guid? LastProposalId { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public void Apply(Guid proposalId, DateOnly dueDate, DateTimeOffset now)
    {
        if (proposalId == Guid.Empty) throw new ArgumentException("Proposal id is required.", nameof(proposalId));
        DueDate = dueDate; LastProposalId = proposalId; UpdatedAt = now;
    }
}
