using System.Text.Json;
using AgroPlan.Domain.Planning;

namespace AgroPlan.Application.UseCases;

public sealed class SubmitSowingPlan
{
    public (SowingProposal Proposal, OutboxMessage OutboxMessage) Create(
        Guid farmId,
        Guid cropCycleId,
        string userId,
        string cropCode,
        DateOnly? desiredPlantingDate,
        Guid parcelId,
        decimal parcelAreaHectares,
        string? locationDescription,
        DateTimeOffset requestedAt)
    {
        var jobId = Guid.NewGuid();
        var snapshot = JsonSerializer.Serialize(new
        {
            cropCode,
            cropCycleId,
            desiredPlantingDate,
            parcelId,
            parcelAreaHectares,
            locationDescription,
            climateStatus = "unavailable",
            missingInputs = new[] { "climate.coordinates", "validated.crop.parameters" },
            capturedAt = requestedAt
        });
        var proposal = new SowingProposal(jobId, farmId, cropCycleId, userId, snapshot, requestedAt);
        var outboxMessage = new OutboxMessage(
            Guid.NewGuid(),
            jobId,
            "sowing-plan.requested",
            JsonSerializer.Serialize(new { jobId, version = 1 }),
            requestedAt);
        return (proposal, outboxMessage);
    }
}
