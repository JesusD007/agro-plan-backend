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
[Route("api/farms")]
[Authorize]
public sealed class FarmsController(AgroPlanDbContext dbContext) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<FarmResponse>> Create(CreateFarmRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var userId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var farm = new Farm(Guid.NewGuid(), request.Name, request.Location, DateTimeOffset.UtcNow);
        var membership = new FarmMembership(farm.Id, userId, FarmRole.Owner, DateTimeOffset.UtcNow);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.Farms.Add(farm);
        dbContext.FarmMemberships.Add(membership);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { farmId = farm.Id }, new FarmResponse(farm.Id, farm.Name, farm.Location, farm.CreatedAt));
    }

    [HttpGet("{farmId:guid}")]
    [Authorize(Policy = FarmMembershipRequirement.PolicyName)]
    public async Task<ActionResult<FarmResponse>> GetById(Guid farmId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var farm = await dbContext.Farms.AsNoTracking()
            .Where(candidate => candidate.Id == farmId && candidate.Memberships.Any(
                membership => membership.UserId == userId && membership.IsActive &&
                    (membership.Role == FarmRole.Owner || membership.Role == FarmRole.Administrator)))
            .Select(candidate => new FarmResponse(candidate.Id, candidate.Name, candidate.Location, candidate.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);
        return farm is null ? NotFound() : Ok(farm);
    }
}

public sealed record CreateFarmRequest(
    [property: Required, StringLength(160, MinimumLength = 2)] string Name,
    [property: StringLength(240)] string? Location);

public sealed record FarmResponse(Guid Id, string Name, string? Location, DateTimeOffset CreatedAt);
