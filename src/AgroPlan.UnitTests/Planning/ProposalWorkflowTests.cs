using AgroPlan.Application.UseCases;
using AgroPlan.Domain.Planning;
using Xunit;

namespace AgroPlan.UnitTests.Planning;

public sealed class ProposalWorkflowTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    private readonly ConfirmProposal _confirm = new();

    [Fact]
    public void Execute_blocks_completed_safe_mode_proposal_with_named_missing_inputs()
    {
        var proposal = Completed("""
            {"status":"insufficient_data","missingInputs":["climate.forecast","validated.crop.parameters"],"sources":[],"climate":{"status":"unavailable"}}
            """);

        var result = _confirm.Execute(proposal, "producer-1", CreatedAt.AddMinutes(1));

        Assert.Equal(ProposalConfirmationResult.NotReady, result);
        Assert.Equal(SowingProposalStatus.Completed, proposal.Status);
        Assert.Null(proposal.ConfirmedAt);
    }

    [Fact]
    public void Execute_blocks_proposal_without_validated_sources_or_climate()
    {
        var proposal = Completed("""
            {"status":"draft","missingInputs":[],"sources":[],"climate":{"status":"unavailable"}}
            """);

        var result = _confirm.Execute(proposal, "producer-1", CreatedAt.AddMinutes(1));

        Assert.Equal(ProposalConfirmationResult.NotReady, result);
        Assert.Equal(SowingProposalStatus.Completed, proposal.Status);
    }

    [Fact]
    public void Execute_confirms_only_complete_proposal_with_climate_and_sourced_evidence()
    {
        var proposal = Completed("""
            {"status":"draft","missingInputs":[],"sources":[{"sourceName":"Validated source"}],"climate":{"status":"available","sourceName":"Weather source","forecastIssuedAt":"2026-11-10T11:00:00Z"}}
            """);
        var confirmedAt = CreatedAt.AddMinutes(5);

        var result = _confirm.Execute(proposal, "producer-1", confirmedAt);

        Assert.Equal(ProposalConfirmationResult.Confirmed, result);
        Assert.Equal(SowingProposalStatus.Confirmed, proposal.Status);
        Assert.Equal("producer-1", proposal.ConfirmedByUserId);
        Assert.Equal(confirmedAt, proposal.ConfirmedAt);
    }

    [Fact]
    public void Execute_blocks_proposal_that_has_not_completed()
    {
        var proposal = new SowingProposal(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "producer-1", "{}", CreatedAt);

        Assert.Equal(ProposalConfirmationResult.NotReady, _confirm.Execute(proposal, "producer-1", CreatedAt));
    }

    private static SowingProposal Completed(string json)
    {
        var proposal = new SowingProposal(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "producer-1", "{}", CreatedAt);
        proposal.MarkCompleted(json, CreatedAt.AddMinutes(1));
        return proposal;
    }
}
