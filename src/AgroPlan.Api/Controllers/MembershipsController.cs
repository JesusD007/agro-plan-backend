using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AgroPlan.Api.Authorization;
using AgroPlan.Application.Abstractions;
using AgroPlan.Domain.Farms;
using AgroPlan.Infrastructure.Identity;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Api.Controllers;

[ApiController]
[Route("api/farms/{farmId:guid}/administrators")]
[Authorize(Policy = FarmMembershipRequirement.PolicyName)]
public sealed class MembershipsController(
    AgroPlanDbContext dbContext,
    IFarmAuthorization farmAuthorization,
    UserManager<ApplicationUser> users) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> GrantAdministrator(Guid farmId, GrantAdministratorRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var actorId = CurrentUserId();
        if (actorId is null) return Unauthorized();
        var actorRole = await farmAuthorization.GetActiveRoleAsync(farmId, actorId, cancellationToken);
        if (actorRole != FarmRole.Owner) return Forbid();

        var target = await users.FindByEmailAsync(request.Email.Trim());
        if (target is null) return NotFound(new { message = "The account was not found." });
        if (target.Id == actorId) return Conflict(new { message = "The farm owner already has owner permissions." });

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var membership = await dbContext.FarmMemberships.SingleOrDefaultAsync(
            item => item.FarmId == farmId && item.UserId == target.Id, cancellationToken);
        if (membership?.Role == FarmRole.Owner) return Conflict(new { message = "Farm ownership cannot be changed through administrator membership." });
        if (membership is { IsActive: true }) return Conflict(new { message = "The account is already an active farm member." });

        if (membership is null)
        {
            membership = new FarmMembership(farmId, target.Id, FarmRole.Administrator, DateTimeOffset.UtcNow);
            dbContext.FarmMemberships.Add(membership);
        }
        else
        {
            membership.SetRole(FarmRole.Administrator);
        }

        dbContext.FarmMembershipAuditEntries.Add(new FarmMembershipAuditEntry(
            farmId, target.Id, actorId, "admin_granted", DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{targetUserId}")]
    public async Task<IActionResult> RevokeAdministrator(Guid farmId, string targetUserId, CancellationToken cancellationToken)
    {
        var actorId = CurrentUserId();
        if (actorId is null) return Unauthorized();
        var actorRole = await farmAuthorization.GetActiveRoleAsync(farmId, actorId, cancellationToken);
        if (actorRole != FarmRole.Owner) return Forbid();
        if (targetUserId == actorId) return Conflict(new { message = "The owner membership cannot be revoked through this endpoint." });

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var membership = await dbContext.FarmMemberships.SingleOrDefaultAsync(
            item => item.FarmId == farmId && item.UserId == targetUserId && item.Role == FarmRole.Administrator,
            cancellationToken);
        if (membership is null || !membership.IsActive) return NotFound();
        membership.Revoke();
        dbContext.FarmMembershipAuditEntries.Add(new FarmMembershipAuditEntry(
            farmId, targetUserId, actorId, "admin_revoked", DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    private string? CurrentUserId() => User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
}

public sealed record GrantAdministratorRequest([property: Required, EmailAddress, StringLength(256)] string Email);
