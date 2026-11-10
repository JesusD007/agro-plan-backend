using AgroPlan.Api.Authorization;
using AgroPlan.Api.Identity;
using AgroPlan.Application.UseCases;
using AgroPlan.Api.Hubs;
using AgroPlan.Api.Services;
using AgroPlan.Infrastructure.Identity;
using AgroPlan.Infrastructure.Persistence;
using AgroPlan.Application.Crops;
using AgroPlan.Application.Abstractions;
using AgroPlan.Infrastructure.Email;
using AgroPlan.Infrastructure.Messaging;
using AgroPlan.Infrastructure.Storage;
using AgroPlan.Infrastructure.Climate;
using AgroPlan.Infrastructure.Prices;
using AgroPlan.Application.AdvisoryAccess;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
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
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, AdvisorReportAuthorizationHandler>();
builder.Services.AddScoped<AdvisorGrantService>();
builder.Services.AddScoped<IFarmAuthorization, FarmAuthorizationService>();
builder.Services.AddScoped<AssignedWorkerAuthorizationService>();
builder.Services.AddScoped<ICropCatalogRepository, CropCatalogRepository>();
builder.Services.AddScoped<CropCatalogService>();
builder.Services.AddScoped<SubmitSowingPlan>();
builder.Services.AddScoped<SubmitPlantingFrameProposal>();
builder.Services.AddSingleton<IWeatherAdvisoryProvider, UnavailableWeatherAdvisoryProvider>();
builder.Services.AddSingleton<IReferencePriceProvider, UnavailableReferencePriceProvider>();
builder.Services.AddScoped<AgroPlan.Application.Planning.PlantingFrameCalculator>();
builder.Services.AddScoped<ConfirmProposal>();
builder.Services.AddSingleton<IFileStorage, SupabaseFileStorage>();
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
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddOperationTransformer((operation, context, _) =>
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (metadata?.OfType<IAuthorizeData>().Any() != true || metadata.OfType<IAllowAnonymous>().Any())
            return Task.CompletedTask;

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, context.Document!)] = []
        });
        return Task.CompletedTask;
    });
});
builder.Services.AddSignalR();
builder.Services.AddHostedService<JobStatusNotificationService>();
builder.Services.AddHealthChecks();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options
        .WithTitle("AgroPlan API")
        .WithOpenApiRoutePattern("/openapi/{documentName}.json")
        .AddPreferredSecuritySchemes(JwtBearerDefaults.AuthenticationScheme));
}
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

internal sealed class BearerSecuritySchemeTransformer(IAuthenticationSchemeProvider authenticationSchemeProvider)
    : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var schemes = await authenticationSchemeProvider.GetAllSchemesAsync();
        if (!schemes.Any(scheme => scheme.Name == JwtBearerDefaults.AuthenticationScheme))
            return;

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            [JwtBearerDefaults.AuthenticationScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                In = ParameterLocation.Header,
                BearerFormat = "JWT"
            }
        };
    }
}

public partial class Program { }
