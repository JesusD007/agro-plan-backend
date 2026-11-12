using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AgroPlan.Application.Abstractions;
using AgroPlan.Infrastructure.Email;
using AgroPlan.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AgroPlan.Api.Controllers;

/// <summary>Complete Identity's existing verified-email requirement without bypassing it.</summary>
[ApiController]
[Authorize]
[EnableRateLimiting("identity")]
[Route("api/identity")]
public sealed class EmailVerificationController(UserManager<ApplicationUser> users, IEmailSender email,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost("email-verification")]
    public async Task<IActionResult> Send(CancellationToken ct)
    {
        var user = await users.FindByIdAsync(User.FindFirstValue("sub") ?? "");
        if (user is null) return Unauthorized();
        if (user.EmailConfirmed) return Accepted(new { message = "Verification instructions will be sent if needed." });
        if (!Uri.TryCreate(configuration["Identity:EmailConfirmationUrl"], UriKind.Absolute, out var target) ||
            target.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(target.UserInfo) ||
            !string.IsNullOrEmpty(target.Query) || !string.IsNullOrEmpty(target.Fragment))
            return Problem(statusCode: 503, title: "Email confirmation is not configured.");
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        try
        {
            await email.SendAsync(user.Email!, "Verify your AgroPlan email",
                $"Sign in with this account and confirm your email: {target}?token={Uri.EscapeDataString(token)}", ct);
        }
        catch (EmailDeliveryNotConfiguredException) { return Problem(statusCode: 503, title: "Email delivery is not configured."); }
        return Accepted(new { message = "Verification instructions will be sent if needed." });
    }

    [HttpPost("confirm-email")]
    public async Task<IActionResult> Confirm(ConfirmEmailRequest request)
    {
        var user = await users.FindByIdAsync(User.FindFirstValue("sub") ?? "");
        if (user is null) return Unauthorized();
        var result = await users.ConfirmEmailAsync(user, request.Token);
        return result.Succeeded ? Ok(new { message = "Email confirmed." })
            : BadRequest(new { message = "The email confirmation token is invalid or expired." });
    }
}

public sealed record ConfirmEmailRequest([param: Required, StringLength(4096, MinimumLength = 1)] string Token);
