using System.Text.Json;

namespace AgroPlan.Domain.Planning;

public enum PlantingFrameProposalStatus { Pending, Completed, Failed, Confirmed }

public sealed class PlantingFrameProposal
{
    private PlantingFrameProposal() { }
    public PlantingFrameProposal(Guid id, Guid farmId, Guid cropCycleId, string submittedByUserId,
        string inputSnapshotJson, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || farmId == Guid.Empty || cropCycleId == Guid.Empty) throw new ArgumentException("Proposal identifiers are required.");
        if (string.IsNullOrWhiteSpace(submittedByUserId) || string.IsNullOrWhiteSpace(inputSnapshotJson)) throw new ArgumentException("Submitter and snapshot are required.");
        Id = id; FarmId = farmId; CropCycleId = cropCycleId; SubmittedByUserId = submittedByUserId.Trim();
        InputSnapshotJson = inputSnapshotJson; Status = PlantingFrameProposalStatus.Pending; CreatedAt = UpdatedAt = createdAt;
    }
    public Guid Id { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid CropCycleId { get; private set; }
    public string SubmittedByUserId { get; private set; } = string.Empty;
    public string InputSnapshotJson { get; private set; } = "{}";
    public string? ResultJson { get; private set; }
    public PlantingFrameProposalStatus Status { get; private set; }
    public string? FailureCode { get; private set; }
    public string? FailureSummary { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string? ConfirmedByUserId { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public Guid? SelectedFrameworkId { get; private set; }
    public void MarkCompleted(string resultJson, DateTimeOffset at) { if (Status != PlantingFrameProposalStatus.Pending) return; ResultJson = resultJson; Status = PlantingFrameProposalStatus.Completed; UpdatedAt = at; }
    public void MarkFailed(string code, string summary, DateTimeOffset at) { if (Status != PlantingFrameProposalStatus.Pending) return; FailureCode = code; FailureSummary = summary; Status = PlantingFrameProposalStatus.Failed; UpdatedAt = at; }
    public bool Confirm(string userId, Guid frameworkId, DateTimeOffset at)
    {
        if (Status != PlantingFrameProposalStatus.Completed || string.IsNullOrWhiteSpace(ResultJson) || string.IsNullOrWhiteSpace(userId)) return false;
        try
        {
            using var result = JsonDocument.Parse(ResultJson);
            var root = result.RootElement;
            if (!root.TryGetProperty("missingInputs", out var missing) || missing.ValueKind != JsonValueKind.Array || missing.GetArrayLength() > 0 ||
                !root.TryGetProperty("options", out var options) || options.ValueKind != JsonValueKind.Array ||
                !options.EnumerateArray().Any(option => option.TryGetProperty("frameworkId", out var id) && id.TryGetGuid(out var value) && value == frameworkId)) return false;
        }
        catch (JsonException) { return false; }
        SelectedFrameworkId = frameworkId; ConfirmedByUserId = userId.Trim(); ConfirmedAt = at; Status = PlantingFrameProposalStatus.Confirmed; UpdatedAt = at; return true;
    }
}
