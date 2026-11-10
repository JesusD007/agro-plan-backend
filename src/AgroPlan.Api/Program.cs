using AgroPlan.Api.Authorization;
using AgroPlan.Api.Identity;
using AgroPlan.Application.UseCases;
using AgroPlan.Api.Hubs;
using AgroPlan.Infrastructure.Identity;
using AgroPlan.Infrastructure.Persistence;
using AgroPlan.Application.Crops;
using AgroPlan.Application.Abstractions;
using AgroPlan.Infrastructure.Email;
using AgroPlan.Infrastructure.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Http.Connections;
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
builder.Services.AddSingleton<RabbitMqConnection>();
builder.Services.AddSingleton<RabbitMqPublisher>();
builder.Services.AddHostedService<OutboxPublisher>();
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
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs/jobs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
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
builder.Services.AddScoped<IFarmAuthorization, FarmAuthorizationService>();
builder.Services.AddScoped<ICropCatalogRepository, CropCatalogRepository>();
builder.Services.AddScoped<CropCatalogService>();
builder.Services.AddScoped<SubmitSowingPlan>();
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
builder.Services.AddSignalR();
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
app.MapHub<JobStatusHub>("/hubs/jobs");
app.Run();
