using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgroPlan.Infrastructure.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AgroPlan.Api.Identity;

public sealed class TokenIssuer(byte[] signingKey, string issuer, string audience)
{
    public const string Audience = "AgroPlan.Client";
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);

    public string Issue(ApplicationUser user, DateTimeOffset now, IEnumerable<string>? roles = null)
    {
        var header = new Dictionary<string, string> { ["alg"] = "HS256", ["typ"] = "JWT" };
        var payload = new Dictionary<string, object>
        {
            ["iss"] = issuer,
            ["aud"] = audience,
            ["sub"] = user.Id,
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.Add(AccessTokenLifetime).ToUnixTimeSeconds()
        };

        if (!string.IsNullOrWhiteSpace(user.Email)) payload["email"] = user.Email;
        var roleList = roles?.Distinct(StringComparer.Ordinal).ToArray() ?? [];
        if (roleList.Length > 0) payload["role"] = roleList;
        var encodedHeader = Base64UrlEncoder.Encode(JsonSerializer.SerializeToUtf8Bytes(header));
        var encodedPayload = Base64UrlEncoder.Encode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var unsignedToken = $"{encodedHeader}.{encodedPayload}";
        var signature = HMACSHA256.HashData(signingKey, Encoding.ASCII.GetBytes(unsignedToken));
        return $"{unsignedToken}.{Base64UrlEncoder.Encode(signature)}";
    }

    public static byte[] ResolveSigningKey(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configured = configuration["Jwt:SigningKey"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var key = Encoding.UTF8.GetBytes(configured);
            if (key.Length < 32) throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 UTF-8 bytes.");
            return key;
        }

        if (environment.IsDevelopment()) return RandomNumberGenerator.GetBytes(32);
        throw new InvalidOperationException("Configure Jwt__SigningKey as a server-side secret before starting outside Development.");
    }
}
