using System.ComponentModel.DataAnnotations;
using AgroPlan.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace AgroPlan.Api.Identity;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/identity").WithTags("Identity").RequireRateLimiting("identity");

        group.MapPost("/register", async (RegisterRequest request, UserManager<ApplicationUser> users, TokenIssuer tokens,
            RefreshSessionService sessions, CancellationToken cancellationToken) =>
        {
            if (!IsValid(request, out var errors)) return Results.ValidationProblem(errors);

            var user = new ApplicationUser { UserName = request.Email.Trim(), Email = request.Email.Trim() };
            var result = await users.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                return Results.ValidationProblem(result.Errors
                    .GroupBy(error => error.Code)
                    .ToDictionary(grouping => grouping.Key, grouping => grouping.Select(error => error.Description).ToArray()));
            }

            var response = await CreateSessionAsync(user, users, tokens, sessions, cancellationToken);
            return Results.Json(response, statusCode: StatusCodes.Status201Created);
        }).AllowAnonymous();

        group.MapPost("/login", async (LoginRequest request, SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users,
            TokenIssuer tokens, RefreshSessionService sessions, CancellationToken cancellationToken) =>
        {
            if (!IsValid(request, out var errors)) return Results.ValidationProblem(errors);
            var user = await users.FindByEmailAsync(request.Email.Trim());
            if (user is null) return Results.Unauthorized();

            var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!result.Succeeded) return Results.Unauthorized();
            return Results.Ok(await CreateSessionAsync(user, users, tokens, sessions, cancellationToken));
        }).AllowAnonymous();

        group.MapPost("/refresh", async (RefreshRequest request, UserManager<ApplicationUser> users, TokenIssuer tokens,
            RefreshSessionService sessions, CancellationToken cancellationToken) =>
        {
            if (!IsValid(request, out var errors)) return Results.ValidationProblem(errors);
            var rotation = await sessions.RotateAsync(request.RefreshToken, cancellationToken);
            if (rotation is null) return Results.Unauthorized();
            var user = await users.FindByIdAsync(rotation.UserId);
            if (user is null) return Results.Unauthorized();
            var roles = await users.GetRolesAsync(user);
            return Results.Ok(new SessionResponse(tokens.Issue(user, DateTimeOffset.UtcNow, roles), rotation.Token, "Bearer", 900, rotation.ExpiresAt));
        }).AllowAnonymous();

        group.MapPost("/logout", async (LogoutRequest request, ClaimsPrincipal principal, RefreshSessionService sessions,
            CancellationToken cancellationToken) =>
        {
            if (!IsValid(request, out var errors)) return Results.ValidationProblem(errors);
            var userId = principal.FindFirstValue("sub");
            if (userId is null) return Results.Unauthorized();
            await sessions.RevokeAsync(request.RefreshToken, userId, cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization();

        group.MapPost("/logout-all", async (ClaimsPrincipal principal, RefreshSessionService sessions, CancellationToken cancellationToken) =>
        {
            var userId = principal.FindFirstValue("sub");
            if (userId is null) return Results.Unauthorized();
            await sessions.RevokeAllAsync(userId, cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization();

        return endpoints;
    }

    private static async Task<SessionResponse> CreateSessionAsync(ApplicationUser user, UserManager<ApplicationUser> users,
        TokenIssuer tokens, RefreshSessionService sessions, CancellationToken cancellationToken)
    {
        var roles = await users.GetRolesAsync(user);
        var refresh = await sessions.CreateAsync(user.Id, cancellationToken);
        return new SessionResponse(tokens.Issue(user, DateTimeOffset.UtcNow, roles), refresh.Token, "Bearer", 900, refresh.ExpiresAt);
    }

    private static bool IsValid<TRequest>(TRequest request, out IDictionary<string, string[]> errors)
        where TRequest : notnull
    {
        var results = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        errors = results
            .SelectMany(result => result.MemberNames.DefaultIfEmpty("request").Select(name => (name, message: result.ErrorMessage ?? "Invalid value.")))
            .GroupBy(item => item.name)
            .ToDictionary(grouping => grouping.Key, grouping => grouping.Select(item => item.message).ToArray());
        return valid;
    }
}

public sealed record RegisterRequest(
    [property: Required, EmailAddress, StringLength(256)] string Email,
    [property: Required, StringLength(128, MinimumLength = 12)] string Password);

public sealed record LoginRequest(
    [property: Required, EmailAddress, StringLength(256)] string Email,
    [property: Required, StringLength(128, MinimumLength = 1)] string Password);

public sealed record RefreshRequest([property: Required, StringLength(128, MinimumLength = 32)] string RefreshToken);

public sealed record LogoutRequest([property: Required, StringLength(128, MinimumLength = 32)] string RefreshToken);

public sealed record SessionResponse(string AccessToken, string RefreshToken, string TokenType, int ExpiresIn, DateTimeOffset RefreshExpiresAt);
