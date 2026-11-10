using System.Text.Json;
using AgroPlan.Domain.Planning;

namespace AgroPlan.Application.UseCases;

public sealed class SubmitPlantingFrameProposal
{
    public (PlantingFrameProposal Proposal, OutboxMessage OutboxMessage) Create(Guid farmId, Guid cropCycleId,
        string userId, string cropCode, Guid parcelId, DateTimeOffset requestedAt)
    {
        var id = Guid.NewGuid();
        var snapshot = JsonSerializer.Serialize(new { cropCode, parcelId, missingInputs = new[] { "parcel.usableAreaSquareMeters", "parcel.aisleGeometry", "crop.validatedFrameRules" }, capturedAt = requestedAt });
        var proposal = new PlantingFrameProposal(id, farmId, cropCycleId, userId, snapshot, requestedAt);
        return (proposal, new OutboxMessage(Guid.NewGuid(), id, "planting-frame.requested", JsonSerializer.Serialize(new { jobId = id, version = 1 }), requestedAt));
    }
}
