using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<PlaceholderWorker>();
await builder.Build().RunAsync();

internal sealed class PlaceholderWorker(ILogger<PlaceholderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("AgroPlan Worker host is ready; this plan does not configure job consumers.");
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}
