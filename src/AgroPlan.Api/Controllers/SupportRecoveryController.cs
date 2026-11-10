using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AgroPlan.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AgroPlan.Api.Controllers;

[ApiController]
[Route("api/recovery")]
public sealed class SupportRecoveryController(RecoveryService recoveryService) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("recovery")]
    [HttpPost("requests")]
    public async Task<IActionResult> StartRequest(RecoveryStartRequest request, CancellationToken cancellationToken)
    {
        var created = await recoveryService.StartAsync(request.Email, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return Accepted(new RecoveryRequestAccepted(created.RequestId,
            "If an account matches that address, recovery instructions will be sent."));
    }

    [AllowAnonymous]
    [EnableRateLimiting("recovery")]
    [HttpPost("requests/{requestId:guid}/verify")]
    public async Task<IActionResult> VerifyEmailCode(Guid requestId, RecoveryCodeRequest request, CancellationToken cancellationToken)
    {
        await recoveryService.VerifyEmailCodeAsync(requestId, request.Code, cancellationToken);
        return Accepted(new { message = "If the request is eligible, it will be reviewed by support." });
    }

    [Authorize(Roles = SupportRole.Name)]
    [HttpGet("support/pending")]
    public async Task<ActionResult<IReadOnlyList<PendingRecoveryRequest>>> GetPending(CancellationToken cancellationToken)
    {
        return Ok(await recoveryService.GetPendingAsync(cancellationToken));
    }

    [Authorize(Roles = SupportRole.Name)]
    [HttpPost("support/{requestId:guid}/review")]
    public async Task<IActionResult> Review(Guid requestId, SupportReviewRequest request, CancellationToken cancellationToken)
    {
        var supportUserId = User.FindFirstValue("sub");
        if (supportUserId is null) return Unauthorized();
        var result = await recoveryService.ReviewAsync(requestId, supportUserId, request.Approve!.Value, request.Note, cancellationToken);
        if (!result.Succeeded) return Conflict(new { message = result.Error });
        return NoContent();
    }

    [AllowAnonymous]
    [EnableRateLimiting("recovery")]
    [HttpPost("password-change")]
    public async Task<IActionResult> CompletePasswordChange(PasswordChangeRequest request, CancellationToken cancellationToken)
    {
        var result = await recoveryService.CompletePasswordChangeAsync(
            request.RequestId, request.Token, request.NewPassword, cancellationToken);
        if (!result.Succeeded)
        {
            if (result.Errors.Count == 1 && result.Errors[0] == "The recovery token is invalid or expired.")
                return BadRequest(new { message = result.Errors[0] });
            var errors = result.Errors.Select((error, index) => (error, index))
                .ToDictionary(item => $"password_{item.index}", item => new[] { item.error });
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        return Ok(new { message = "Password changed. Sign in again on each device." });
    }
}

public sealed record RecoveryStartRequest([property: Required, EmailAddress, StringLength(256)] string Email);
public sealed record RecoveryCodeRequest([property: Required, RegularExpression("^[0-9]{8}$")] string Code);
public sealed record SupportReviewRequest([property: Required] bool? Approve, [property: StringLength(500)] string? Note);
public sealed record PasswordChangeRequest(
    [property: Required] Guid RequestId,
    [property: Required, StringLength(128, MinimumLength = 32)] string Token,
    [property: Required, StringLength(128, MinimumLength = 12)] string NewPassword);
public sealed record RecoveryRequestAccepted(Guid RequestId, string Message);
