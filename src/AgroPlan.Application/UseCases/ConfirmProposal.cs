using System.Text.Json;
using AgroPlan.Domain.Planning;

namespace AgroPlan.Application.UseCases;

public enum ProposalConfirmationResult { Confirmed, NotFound, NotReady }

public sealed class ConfirmProposal
{
    public ProposalConfirmationResult Execute(SowingProposal proposal, string userId, DateTimeOffset now)
    {
        if (proposal.Status != SowingProposalStatus.Completed || string.IsNullOrWhiteSpace(proposal.ResultJson))
            return ProposalConfirmationResult.NotReady;

        using var result = JsonDocument.Parse(proposal.ResultJson);
        var root = result.RootElement;
        var missing = root.TryGetProperty("missingInputs", out var missingInputs) &&
                     missingInputs.ValueKind == JsonValueKind.Array && missingInputs.GetArrayLength() > 0;
        var climateAvailable = root.TryGetProperty("climate", out var climate) &&
                               climate.TryGetProperty("status", out var climateStatus) &&
                               climateStatus.GetString() == "available" &&
                               climate.TryGetProperty("sourceName", out var source) &&
                               !string.IsNullOrWhiteSpace(source.GetString()) &&
                               climate.TryGetProperty("forecastIssuedAt", out var issuedAt) &&
                               issuedAt.ValueKind != JsonValueKind.Null;
        var hasValidatedSources = root.TryGetProperty("sources", out var sources) &&
                                  sources.ValueKind == JsonValueKind.Array && sources.GetArrayLength() > 0;
        if (missing || !climateAvailable || !hasValidatedSources)
            return ProposalConfirmationResult.NotReady;

        return proposal.Confirm(userId, now)
            ? ProposalConfirmationResult.Confirmed
            : ProposalConfirmationResult.NotReady;
    }
}
