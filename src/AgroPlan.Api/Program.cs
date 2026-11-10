using AgroPlan.Api.Authorization;
using AgroPlan.Api.Identity;
using AgroPlan.Infrastructure.Identity;
using AgroPlan.Infrastructure.Persistence;
using AgroPlan.Application.Abstractions;
using AgroPlan.Infrastructure.Email;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("AgroPlan");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Configure ConnectionStrings__AgroPlan before starting AgroPlan.Api.");
}

var issuer = builder.Configuration["Jwt:Issuer"] ?? "AgroPlan.Api";
var audience = builder.Configuration["Jwt:Audience"] ?? TokenIssuer.Audience;
var signingKey = TokenIssuer.ResolveSigningKey(builder.Configuration, builder.Environment);
builder.Services.AddDbContext<AgroPlanDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AgroPlanDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton(new TokenIssuer(signingKey, issuer, audience));
builder.Services.AddScoped<RefreshSessionService>();
builder.Services.AddSingleton<IEmailSender, ConfiguredEmailSender>();
builder.Services.AddScoped<RecoveryService>(services => new RecoveryService(
    services.GetRequiredService<AgroPlanDbContext>(),
    services.GetRequiredService<UserManager<ApplicationUser>>(),
    services.GetRequiredService<IEmailSender>(),
    services.GetRequiredService<RefreshSessionService>(),
    builder.Configuration,
    signingKey,
    services.GetRequiredService<ILogger<RecoveryService>>()));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(signingKey),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(FarmMembershipRequirement.PolicyName, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new FarmMembershipRequirement());
    });
});
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, FarmMembershipAuthorizationHandler>();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("identity", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-client",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.AddPolicy("recovery", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-client",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});
builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapIdentityEndpoints();
app.MapControllers();
app.Run();
