using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AgroPlan.Infrastructure.Messaging;
using AgroPlan.Infrastructure.Persistence;
using AgroPlan.Worker.Jobs;
using AgroPlan.Worker.AI;
using AgroPlan.Worker.Climate;
using AgroPlan.Application.Abstractions;
using AgroPlan.Application.Workforce;
using AgroPlan.Infrastructure.Calendars;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var builder = Host.CreateApplicationBuilder(args);
var rabbitMqUrl = builder.Configuration["RABBITMQ_URL"];
if (!string.IsNullOrWhiteSpace(rabbitMqUrl))
{
    var connectionString = builder.Configuration.GetConnectionString("AgroPlan");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("Configure ConnectionStrings__AgroPlan when RABBITMQ_URL is enabled.");
    builder.Services.AddDbContext<AgroPlanDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddSingleton<RabbitMqConnection>();
    builder.Services.AddSingleton<IAiProposalProvider, GeminiProposalProvider>();
    builder.Services.AddSingleton<IClimateProvider, UnconfiguredClimateProvider>();
    builder.Services.AddSingleton<IExternalWorkCalendar, UnconfiguredWorkCalendar>();
    builder.Services.AddSingleton<WorkTaskImpactProjector>();
    builder.Services.AddHostedService<SowingPlanConsumer>();
    builder.Services.AddHostedService<PlantingFrameConsumer>();
    builder.Services.AddHostedService<ScheduleChangeProposalConsumer>();
    builder.Services.AddHostedService<VerifiedWorkRewardsConsumer>();
    builder.Services.AddHostedService<ConfirmedRecordTaskImpactConsumer>();
    builder.Services.AddHostedService<TaskCalendarSyncConsumer>();
}
else
{
    builder.Services.AddHostedService<PlaceholderWorker>();
}
await builder.Build().RunAsync();

internal sealed class PlaceholderWorker(ILogger<PlaceholderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("AgroPlan Worker is idle because RABBITMQ_URL is not configured.");
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}
