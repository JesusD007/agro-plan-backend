using System.ComponentModel.DataAnnotations;
using AgroPlan.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;

namespace AgroPlan.Api.Identity;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/identity").WithTags("Identity").RequireRateLimiting("identity");

        group.MapPost("/register", async (RegisterRequest request, UserManager<ApplicationUser> users, TokenIssuer tokens) =>
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

            return Results.Created("/api/identity/me", new SessionResponse(tokens.Issue(user, DateTimeOffset.UtcNow), "Bearer", 900));
        }).AllowAnonymous();

        group.MapPost("/login", async (LoginRequest request, SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users, TokenIssuer tokens) =>
        {
            if (!IsValid(request, out var errors)) return Results.ValidationProblem(errors);
            var user = await users.FindByEmailAsync(request.Email.Trim());
            if (user is null) return Results.Unauthorized();

            var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!result.Succeeded) return Results.Unauthorized();
            return Results.Ok(new SessionResponse(tokens.Issue(user, DateTimeOffset.UtcNow), "Bearer", 900));
        }).AllowAnonymous();

        return endpoints;
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

public sealed record SessionResponse(string AccessToken, string TokenType, int ExpiresIn);
