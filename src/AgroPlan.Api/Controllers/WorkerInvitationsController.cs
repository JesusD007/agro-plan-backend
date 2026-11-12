using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AgroPlan.Api.Authorization;
using AgroPlan.Application.Abstractions;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Workforce;
using AgroPlan.Infrastructure.Identity;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Api.Controllers;

[ApiController]
[Authorize]
public sealed class WorkerInvitationsController(
    AgroPlanDbContext db,
    IFarmAuthorization farmAuthorization,
    UserManager<ApplicationUser> users,
    IEmailSender emailSender,
    IConfiguration configuration,
    ILogger<WorkerInvitationsController> logger) : ControllerBase
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

    [HttpGet("api/farms/{farmId:guid}/workers/invitations")]
    public async Task<IActionResult> List(Guid farmId, CancellationToken ct)
    {
        var actor = CurrentUserId();
        if (actor is null) return Unauthorized();
        if (!await IsFarmManager(farmId, actor, ct)) return Forbid();
        return Ok(await db.WorkerInvitations.AsNoTracking().Where(x => x.FarmId == farmId)
            .OrderByDescending(x => x.IssuedAt).Take(100)
            .Select(x => new { x.Id, x.Email, x.ExpiresAt, x.AcceptedAt, x.CancelledAt }).ToArrayAsync(ct));
    }

    [HttpPost("api/farms/{farmId:guid}/workers/invitations")]
    public async Task<IActionResult> Create(Guid farmId, CreateWorkerInvitationRequest request, CancellationToken ct)
    {
        var actorId = CurrentUserId();
        if (actorId is null) return Unauthorized();
        if (!await IsFarmManager(farmId, actorId, ct)) return Forbid();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var acceptanceUrl = ResolveAcceptanceUrl();
        if (acceptanceUrl is null)
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Worker invitation links are not configured.",
                detail: "Set WorkerInvitations:AcceptanceUrl to the client acceptance page before issuing invitations.");

        var now = DateTimeOffset.UtcNow;
        var normalizedEmail = NormalizeEmail(request.Email);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM agro.farms WHERE \"Id\" = {farmId} FOR UPDATE", ct);
        var farm = await db.Farms.SingleOrDefaultAsync(item => item.Id == farmId, ct);
        if (farm is null) return NotFound();
        var existingUserId = await db.Users.Where(user => user.NormalizedEmail == normalizedEmail)
            .Select(user => user.Id).SingleOrDefaultAsync(ct);
        if (existingUserId is not null && await db.FarmMemberships.AnyAsync(item => item.FarmId == farmId &&
            item.UserId == existingUserId && item.IsActive, ct))
            return Conflict(new { message = "The account is already an active farm member." });
        if (await db.WorkerInvitations.AnyAsync(item => item.FarmId == farmId && item.Email == normalizedEmail &&
            item.AcceptedAt == null && item.CancelledAt == null && item.ExpiresAt > now, ct))
            return Conflict(new { message = "A pending invitation already exists for this email." });

        if (farm.WorkerEntitlement == WorkerEntitlement.Basic)
        {
            var activeWorkers = await db.FarmMemberships.CountAsync(item => item.FarmId == farmId &&
                item.IsActive && item.Role == FarmRole.Worker, ct);
            var pendingInvitations = await db.WorkerInvitations.CountAsync(item => item.FarmId == farmId &&
                item.AcceptedAt == null && item.CancelledAt == null && item.ExpiresAt > now, ct);
            if (activeWorkers + pendingInvitations >= 5)
                return Conflict(new { message = "The basic plan allows up to five workers, including pending invitations." });
        }

        var token = CreateToken();
        var invitation = new WorkerInvitation(Guid.NewGuid(), farmId, normalizedEmail, Digest(token), actorId,
            now, now.Add(InvitationLifetime));
        db.WorkerInvitations.Add(invitation);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await SendInvitationEmail(normalizedEmail, token, acceptanceUrl, ct);
        logger.LogInformation("Worker invitation {InvitationId} issued for farm {FarmId}", invitation.Id, farmId);
        return Created($"/api/farms/{farmId}/workers/invitations/{invitation.Id}", new { id = invitation.Id, expiresAt = invitation.ExpiresAt });
    }

    [HttpPost("api/farms/{farmId:guid}/workers/invitations/{invitationId:guid}/renew")]
    public async Task<IActionResult> Renew(Guid farmId, Guid invitationId, CancellationToken ct)
    {
        var actorId = CurrentUserId();
        if (actorId is null) return Unauthorized();
        if (!await IsFarmManager(farmId, actorId, ct)) return Forbid();
        var acceptanceUrl = ResolveAcceptanceUrl();
        if (acceptanceUrl is null)
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Worker invitation links are not configured.",
                detail: "Set WorkerInvitations:AcceptanceUrl to the client acceptance page before issuing invitations.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM agro.farms WHERE \"Id\" = {farmId} FOR UPDATE", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM agro.worker_invitations WHERE \"Id\" = {invitationId} AND \"FarmId\" = {farmId} FOR UPDATE", ct);
        var invitation = await db.WorkerInvitations.SingleOrDefaultAsync(item => item.Id == invitationId && item.FarmId == farmId, ct);
        if (invitation is null) return NotFound();
        var now = DateTimeOffset.UtcNow;
        var token = CreateToken();
        try { invitation.Renew(Digest(token), now); }
        catch (InvalidOperationException) { return Conflict(new { message = "Only a pending invitation can be renewed." }); }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await SendInvitationEmail(invitation.Email, token, acceptanceUrl, ct);
        return NoContent();
    }

    [HttpDelete("api/farms/{farmId:guid}/workers/invitations/{invitationId:guid}")]
    public async Task<IActionResult> Cancel(Guid farmId, Guid invitationId, CancellationToken ct)
    {
        var actorId = CurrentUserId();
        if (actorId is null) return Unauthorized();
        if (!await IsFarmManager(farmId, actorId, ct)) return Forbid();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM agro.worker_invitations WHERE \"Id\" = {invitationId} AND \"FarmId\" = {farmId} FOR UPDATE", ct);
        var invitation = await db.WorkerInvitations.SingleOrDefaultAsync(item => item.Id == invitationId && item.FarmId == farmId, ct);
        if (invitation is null) return NotFound();
        try { invitation.Cancel(DateTimeOffset.UtcNow); }
        catch (InvalidOperationException) { return Conflict(new { message = "Only a pending invitation can be cancelled." }); }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return NoContent();
    }

    [HttpPost("api/worker-invitations/accept")]
    public async Task<IActionResult> Accept(AcceptWorkerInvitationRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        var user = await users.FindByIdAsync(userId);
        if (user is null || !user.EmailConfirmed || string.IsNullOrWhiteSpace(user.NormalizedEmail))
            return Forbid();
        var digest = Digest(request.Token);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var invitationId = await db.WorkerInvitations.Where(item => item.TokenDigest == digest)
            .Select(item => (Guid?)item.Id).SingleOrDefaultAsync(ct);
        if (invitationId is null) return NotFound(new { message = "Invitation is invalid or no longer available." });
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM agro.worker_invitations WHERE \"Id\" = {invitationId.Value} FOR UPDATE", ct);
        var invitation = await db.WorkerInvitations.SingleAsync(item => item.Id == invitationId.Value, ct);
        var now = DateTimeOffset.UtcNow;
        if (!invitation.IsPending(now)) return Conflict(new { message = "Invitation is expired, cancelled, or already used." });
        if (!string.Equals(invitation.Email, user.NormalizedEmail, StringComparison.Ordinal)) return Forbid();
        var membership = await db.FarmMemberships.SingleOrDefaultAsync(item => item.FarmId == invitation.FarmId && item.UserId == userId, ct);
        if (membership is { IsActive: true }) return Conflict(new { message = "Account is already an active farm member." });
        invitation.Accept(userId, user.NormalizedEmail, now);
        if (membership is null) db.FarmMemberships.Add(new FarmMembership(invitation.FarmId, userId, FarmRole.Worker, now));
        else membership.SetRole(FarmRole.Worker);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return NoContent();
    }

    private async Task<bool> IsFarmManager(Guid farmId, string actorId, CancellationToken ct)
    {
        var role = await farmAuthorization.GetActiveRoleAsync(farmId, actorId, ct);
        return role is FarmRole.Owner or FarmRole.Administrator;
    }

    private string? CurrentUserId() => User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

    private string? ResolveAcceptanceUrl()
    {
        var baseUrl = configuration["WorkerInvitations:AcceptanceUrl"];
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || !string.IsNullOrEmpty(uri.UserInfo)) return null;
        if (uri.Scheme != Uri.UriSchemeHttps) return null;
        return uri.ToString().TrimEnd('/');
    }

    private Task SendInvitationEmail(string email, string token, string acceptanceUrl, CancellationToken ct)
    {
        var link = $"{acceptanceUrl}?token={Uri.EscapeDataString(token)}";
        return emailSender.SendAsync(email.ToLowerInvariant(), "AgroPlan farm invitation", $"Accept this invitation using your verified AgroPlan account: {link}", ct);
    }

    private static string CreateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Digest(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
}

public sealed record CreateWorkerInvitationRequest([param: Required, EmailAddress, StringLength(256)] string Email);
public sealed record AcceptWorkerInvitationRequest([param: Required, StringLength(128, MinimumLength = 32)] string Token);
