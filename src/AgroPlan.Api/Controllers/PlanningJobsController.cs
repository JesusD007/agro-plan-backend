using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using AgroPlan.Api.Authorization;
using AgroPlan.Application.UseCases;
using AgroPlan.Domain.Planning;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Api.Controllers;

[ApiController]
[Route("api/farms/{farmId:guid}/planning-jobs")]
[Authorize(Policy = FarmMembershipRequirement.PolicyName)]
public sealed class PlanningJobsController(AgroPlanDbContext dbContext, SubmitSowingPlan submitSowingPlan) : ControllerBase
{
    [HttpPost("sowing-plans")]
    public async Task<ActionResult<SowingJobResponse>> SubmitSowingPlan(
        Guid farmId,
        SubmitSowingPlanRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var userId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var cycle = await dbContext.CropCycles.AsNoTracking()
            .Where(candidate => candidate.FarmId == farmId && candidate.Id == request.CropCycleId)
            .Include(candidate => candidate.CropCatalogEntry)
            .SingleOrDefaultAsync(cancellationToken);
        if (cycle is null) return BadRequest(new { message = "The crop cycle does not belong to this farm." });

        var parcel = await dbContext.Parcels.AsNoTracking()
            .Where(candidate => candidate.FarmId == farmId && candidate.Id == cycle.ParcelId)
            .Select(candidate => new { candidate.Id, candidate.AreaHectares, candidate.Location })
            .SingleOrDefaultAsync(cancellationToken);
        if (parcel is null) return Conflict(new { message = "The crop cycle has no valid farm parcel." });

        var now = DateTimeOffset.UtcNow;
        var (proposal, outboxMessage) = submitSowingPlan.Create(
            farmId,
            cycle.Id,
            userId,
            cycle.CropCatalogEntry.Code,
            cycle.DesiredPlantingDate,
            parcel.Id,
            parcel.AreaHectares,
            parcel.Location,
            now);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.SowingProposals.Add(proposal);
        dbContext.OutboxMessages.Add(outboxMessage);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var response = ToResponse(proposal);
        return AcceptedAtAction(nameof(Get), new { farmId, jobId = proposal.Id }, response);
    }

    [HttpGet("{jobId:guid}")]
    public async Task<ActionResult<SowingJobResponse>> Get(Guid farmId, Guid jobId, CancellationToken cancellationToken)
    {
        var proposal = await dbContext.SowingProposals.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.FarmId == farmId && candidate.Id == jobId, cancellationToken);
        return proposal is null ? NotFound() : Ok(ToResponse(proposal));
    }

    private static SowingJobResponse ToResponse(SowingProposal proposal) => new(
        proposal.Id,
        proposal.FarmId,
        proposal.CropCycleId,
        proposal.Status.ToString().ToLowerInvariant(),
        proposal.FailureCode,
        proposal.FailureSummary,
        proposal.CreatedAt,
        proposal.UpdatedAt);
}

public sealed record SubmitSowingPlanRequest([property: Required] Guid CropCycleId);

public sealed record SowingJobResponse(
    Guid Id,
    Guid FarmId,
    Guid CropCycleId,
    string Status,
    string? FailureCode,
    string? FailureSummary,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
