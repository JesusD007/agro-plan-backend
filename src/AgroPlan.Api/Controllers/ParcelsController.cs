using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AgroPlan.Api.Authorization;
using AgroPlan.Domain.Farms;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Api.Controllers;

[ApiController]
[Route("api/farms/{farmId:guid}/parcels")]
[Authorize(Policy = FarmMembershipRequirement.PolicyName)]
public sealed class ParcelsController(AgroPlanDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ParcelResponse>>> List(Guid farmId, CancellationToken cancellationToken)
    {
        var parcels = await dbContext.Parcels.AsNoTracking()
            .Where(parcel => parcel.FarmId == farmId)
            .OrderBy(parcel => parcel.Name)
            .Select(parcel => new ParcelResponse(parcel.Id, parcel.FarmId, parcel.Name, parcel.Location, parcel.AreaHectares, parcel.CreatedAt, parcel.UpdatedAt))
            .ToListAsync(cancellationToken);
        return Ok(parcels);
    }

    [HttpGet("{parcelId:guid}")]
    public async Task<ActionResult<ParcelResponse>> Get(Guid farmId, Guid parcelId, CancellationToken cancellationToken)
    {
        var parcel = await dbContext.Parcels.AsNoTracking()
            .Where(candidate => candidate.FarmId == farmId && candidate.Id == parcelId)
            .Select(candidate => new ParcelResponse(candidate.Id, candidate.FarmId, candidate.Name, candidate.Location, candidate.AreaHectares, candidate.CreatedAt, candidate.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);
        return parcel is null ? NotFound() : Ok(parcel);
    }

    [HttpPost]
    public async Task<ActionResult<ParcelResponse>> Create(Guid farmId, ParcelRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (!await dbContext.Farms.AnyAsync(farm => farm.Id == farmId, cancellationToken)) return NotFound();
        var now = DateTimeOffset.UtcNow;
        Parcel parcel;
        try
        {
            parcel = new Parcel(Guid.NewGuid(), farmId, request.Name, request.Location, request.AreaHectares, now);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        dbContext.Parcels.Add(parcel);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "A parcel with that name already exists in this farm." });
        }
        var response = ToResponse(parcel);
        return CreatedAtAction(nameof(Get), new { farmId, parcelId = parcel.Id }, response);
    }

    [HttpPut("{parcelId:guid}")]
    public async Task<ActionResult<ParcelResponse>> Update(Guid farmId, Guid parcelId, ParcelRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var parcel = await dbContext.Parcels.SingleOrDefaultAsync(
            candidate => candidate.FarmId == farmId && candidate.Id == parcelId, cancellationToken);
        if (parcel is null) return NotFound();
        try
        {
            parcel.Update(request.Name, request.Location, request.AreaHectares, DateTimeOffset.UtcNow);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "A parcel with that name already exists in this farm." });
        }
        return Ok(ToResponse(parcel));
    }

    [HttpDelete("{parcelId:guid}")]
    public async Task<IActionResult> Delete(Guid farmId, Guid parcelId, CancellationToken cancellationToken)
    {
        var parcel = await dbContext.Parcels.SingleOrDefaultAsync(
            candidate => candidate.FarmId == farmId && candidate.Id == parcelId, cancellationToken);
        if (parcel is null) return NotFound();
        dbContext.Parcels.Remove(parcel);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "The parcel is referenced by farm records and cannot be deleted." });
        }
        return NoContent();
    }

    private static ParcelResponse ToResponse(Parcel parcel) =>
        new(parcel.Id, parcel.FarmId, parcel.Name, parcel.Location, parcel.AreaHectares, parcel.CreatedAt, parcel.UpdatedAt);
}

public sealed record ParcelRequest(
    [property: Required, StringLength(160, MinimumLength = 2)] string Name,
    [property: StringLength(240)] string? Location,
    [property: Range(typeof(decimal), "0.0001", "1000000")] decimal AreaHectares);

public sealed record ParcelResponse(Guid Id, Guid FarmId, string Name, string? Location, decimal AreaHectares, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
