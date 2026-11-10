using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using AgroPlan.Api.Authorization;
using AgroPlan.Application.Abstractions;
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
public sealed class PlanningJobsController(
    AgroPlanDbContext dbContext,
    SubmitSowingPlan submitSowingPlan,
    SubmitPlantingFrameProposal submitPlantingFrameProposal,
    ConfirmProposal confirmProposal,
    IFarmAuthorization farmAuthorization) : ControllerBase
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

    [HttpPost("{jobId:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid farmId, Guid jobId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var role = await farmAuthorization.GetActiveRoleAsync(farmId, userId, cancellationToken);
        if (role is not (AgroPlan.Domain.Farms.FarmRole.Owner or AgroPlan.Domain.Farms.FarmRole.Administrator))
            return Forbid();

        var proposal = await dbContext.SowingProposals.SingleOrDefaultAsync(
            item => item.FarmId == farmId && item.Id == jobId, cancellationToken);
        if (proposal is null) return NotFound();
        var result = confirmProposal.Execute(proposal, userId, DateTimeOffset.UtcNow);
        if (result == ProposalConfirmationResult.NotReady)
            return Conflict(new { code = "PROPOSAL_INCOMPLETE", message = "The draft has missing inputs or lacks current climate and validated agronomic sources; it cannot be confirmed." });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(proposal));
    }

    private static SowingJobResponse ToResponse(SowingProposal proposal) => new(
        proposal.Id,
        proposal.FarmId,
        proposal.CropCycleId,
        proposal.Status.ToString().ToLowerInvariant(),
        proposal.FailureCode,
        proposal.FailureSummary,
        proposal.ResultJson,
        proposal.CreatedAt,
        proposal.UpdatedAt);

    [HttpPost("planting-frames")]
    public async Task<ActionResult<PlantingFrameJobResponse>> SubmitPlantingFrames(Guid farmId,
        SubmitPlantingFrameRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var userId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var cycle = await dbContext.CropCycles.AsNoTracking().Where(item => item.FarmId == farmId && item.Id == request.CropCycleId)
            .Include(item => item.CropCatalogEntry).SingleOrDefaultAsync(cancellationToken);
        if (cycle is null) return BadRequest(new { message = "The crop cycle does not belong to this farm." });
        var parcelExists = await dbContext.Parcels.AsNoTracking().AnyAsync(item => item.FarmId == farmId && item.Id == cycle.ParcelId, cancellationToken);
        if (!parcelExists) return Conflict(new { message = "The crop cycle has no valid farm parcel." });
        var (proposal, message) = submitPlantingFrameProposal.Create(farmId, cycle.Id, userId, cycle.CropCatalogEntry.Code, cycle.ParcelId, DateTimeOffset.UtcNow);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.PlantingFrameProposals.Add(proposal);
        dbContext.OutboxMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AcceptedAtAction(nameof(GetPlantingFrames), new { farmId, jobId = proposal.Id }, ToFrameResponse(proposal));
    }

    [HttpGet("planting-frames/{jobId:guid}")]
    public async Task<ActionResult<PlantingFrameJobResponse>> GetPlantingFrames(Guid farmId, Guid jobId, CancellationToken cancellationToken)
    {
        var proposal = await dbContext.PlantingFrameProposals.AsNoTracking().SingleOrDefaultAsync(item => item.FarmId == farmId && item.Id == jobId, cancellationToken);
        return proposal is null ? NotFound() : Ok(ToFrameResponse(proposal));
    }

    [HttpPost("planting-frames/{jobId:guid}/confirm")]
    public async Task<IActionResult> ConfirmPlantingFrame(Guid farmId, Guid jobId, ConfirmPlantingFrameRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var role = await farmAuthorization.GetActiveRoleAsync(farmId, userId, cancellationToken);
        if (role is not (AgroPlan.Domain.Farms.FarmRole.Owner or AgroPlan.Domain.Farms.FarmRole.Administrator)) return Forbid();
        var proposal = await dbContext.PlantingFrameProposals.SingleOrDefaultAsync(item => item.FarmId == farmId && item.Id == jobId, cancellationToken);
        if (proposal is null) return NotFound();
        var cycle = await dbContext.CropCycles.SingleOrDefaultAsync(item => item.FarmId == farmId && item.Id == proposal.CropCycleId, cancellationToken);
        if (cycle is null) return Conflict(new { code = "CROP_CYCLE_NOT_FOUND" });
        var now = DateTimeOffset.UtcNow;
        if (!proposal.Confirm(userId, request.FrameworkId, now))
            return Conflict(new { code = "FRAME_OPTION_NOT_SELECTABLE", message = "The proposal is incomplete or the selected sourced frame is unavailable." });
        cycle.SelectFramework(request.FrameworkId, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToFrameResponse(proposal));
    }

    private static PlantingFrameJobResponse ToFrameResponse(PlantingFrameProposal proposal) => new(
        proposal.Id, proposal.FarmId, proposal.CropCycleId, proposal.Status.ToString().ToLowerInvariant(), proposal.FailureCode,
        proposal.FailureSummary, proposal.ResultJson, proposal.CreatedAt, proposal.UpdatedAt, proposal.SelectedFrameworkId);
}

public sealed record SubmitSowingPlanRequest([param: Required] Guid CropCycleId);

public sealed record SowingJobResponse(
    Guid Id,
    Guid FarmId,
    Guid CropCycleId,
    string Status,
    string? FailureCode,
    string? FailureSummary,
    string? ResultJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SubmitPlantingFrameRequest([param: Required] Guid CropCycleId);
public sealed record ConfirmPlantingFrameRequest([param: Required] Guid FrameworkId);
public sealed record PlantingFrameJobResponse(Guid Id, Guid FarmId, Guid CropCycleId, string Status,
    string? FailureCode, string? FailureSummary, string? ResultJson, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, Guid? SelectedFrameworkId);
