using System.ComponentModel.DataAnnotations;
using AgroPlan.Api.Authorization;
using AgroPlan.Application.Crops;
using AgroPlan.Domain.Crops;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Api.Controllers;

[ApiController]
[Route("api/farms/{farmId:guid}/crop-cycles")]
[Authorize(Policy = FarmMembershipRequirement.PolicyName)]
public sealed class CropCyclesController(AgroPlanDbContext dbContext, CropCatalogService catalogService) : ControllerBase
{
    [HttpGet("catalog")]
    public async Task<ActionResult<IReadOnlyList<CropCatalogItem>>> GetCatalog(Guid farmId, CancellationToken cancellationToken)
    {
        var catalog = await catalogService.ListAsync(cancellationToken);
        return Ok(catalog);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CropCycleResponse>>> List(Guid farmId, CancellationToken cancellationToken)
    {
        var cycles = await dbContext.CropCycles.AsNoTracking()
            .Where(cycle => cycle.FarmId == farmId)
            .Include(cycle => cycle.CropCatalogEntry)
            .ThenInclude(crop => crop.Parameters)
            .OrderBy(cycle => cycle.CreatedAt)
            .ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        return Ok(cycles.Select(cycle => ToResponse(cycle, now)).ToArray());
    }

    [HttpGet("{cycleId:guid}")]
    public async Task<ActionResult<CropCycleResponse>> Get(Guid farmId, Guid cycleId, CancellationToken cancellationToken)
    {
        var cycle = await dbContext.CropCycles.AsNoTracking()
            .Where(candidate => candidate.FarmId == farmId && candidate.Id == cycleId)
            .Include(candidate => candidate.CropCatalogEntry)
            .ThenInclude(crop => crop.Parameters)
            .SingleOrDefaultAsync(cancellationToken);
        return cycle is null ? NotFound() : Ok(ToResponse(cycle, DateTimeOffset.UtcNow));
    }

    [HttpPost]
    public async Task<ActionResult<CropCycleResponse>> Create(Guid farmId, CropCycleRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var parcelExists = await dbContext.Parcels.AsNoTracking()
            .AnyAsync(parcel => parcel.FarmId == farmId && parcel.Id == request.ParcelId, cancellationToken);
        if (!parcelExists) return BadRequest(new { message = "The parcel does not belong to this farm." });

        var crop = await dbContext.CropCatalogEntries
            .Include(entry => entry.Parameters)
            .SingleOrDefaultAsync(entry => entry.Code == request.CropCode.Trim().ToUpperInvariant(), cancellationToken);
        if (crop is null) return BadRequest(new { message = "Unknown crop code." });

        var now = DateTimeOffset.UtcNow;
        CropCycle cycle;
        try
        {
            cycle = new CropCycle(Guid.NewGuid(), farmId, request.ParcelId, crop,
                request.DesiredPlantingDate, request.Variety, request.Notes, now);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }

        dbContext.CropCycles.Add(cycle);
        await dbContext.SaveChangesAsync(cancellationToken);
        var response = ToResponse(cycle, now);
        return CreatedAtAction(nameof(Get), new { farmId, cycleId = cycle.Id }, response);
    }

    [HttpPut("{cycleId:guid}")]
    public async Task<ActionResult<CropCycleResponse>> Update(Guid farmId, Guid cycleId, CropCycleRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var cycle = await dbContext.CropCycles
            .Where(candidate => candidate.FarmId == farmId && candidate.Id == cycleId)
            .SingleOrDefaultAsync(cancellationToken);
        if (cycle is null) return NotFound();

        var parcelExists = await dbContext.Parcels.AsNoTracking()
            .AnyAsync(parcel => parcel.FarmId == farmId && parcel.Id == request.ParcelId, cancellationToken);
        if (!parcelExists) return BadRequest(new { message = "The parcel does not belong to this farm." });
        var crop = await dbContext.CropCatalogEntries
            .Include(entry => entry.Parameters)
            .SingleOrDefaultAsync(entry => entry.Code == request.CropCode.Trim().ToUpperInvariant(), cancellationToken);
        if (crop is null) return BadRequest(new { message = "Unknown crop code." });

        try
        {
            cycle.Update(request.ParcelId, crop, request.DesiredPlantingDate,
                request.Variety, request.Notes, DateTimeOffset.UtcNow);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(cycle, DateTimeOffset.UtcNow));
    }

    private static CropCycleResponse ToResponse(CropCycle cycle, DateTimeOffset now) =>
        new(cycle.Id, cycle.FarmId, cycle.ParcelId, cycle.DesiredPlantingDate, cycle.Variety, cycle.Notes,
            cycle.CreatedAt, cycle.UpdatedAt, CropCatalogService.ToItem(cycle.CropCatalogEntry, now));
}

public sealed record CropCycleRequest(
    [param: Required] Guid ParcelId,
    [param: Required, StringLength(40, MinimumLength = 2)] string CropCode,
    DateOnly? DesiredPlantingDate,
    [param: StringLength(120)] string? Variety,
    [param: StringLength(2000)] string? Notes);

public sealed record CropCycleResponse(
    Guid Id,
    Guid FarmId,
    Guid ParcelId,
    DateOnly? DesiredPlantingDate,
    string? Variety,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    CropCatalogItem Crop);
