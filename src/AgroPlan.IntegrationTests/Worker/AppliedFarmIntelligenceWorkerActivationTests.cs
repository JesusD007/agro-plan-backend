using System.Reflection;
using AgroPlan.Application.Abstractions;
using AgroPlan.Worker.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace AgroPlan.IntegrationTests.Worker;

public sealed class AppliedFarmIntelligenceWorkerActivationTests
{
    [Fact]
    public void Production_without_phase_four_providers_keeps_new_consumers_disabled_and_gemini_isolated()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAiProposalProvider>(_ => throw new InvalidOperationException("Gemini provider must not be resolved by phase 4."));

        InvokeRegistration(services, new ConfigurationBuilder().Build(), "Production");

        var hostedTypes = services
            .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
            .Select(descriptor => descriptor.ImplementationType)
            .ToArray();

        Assert.DoesNotContain(typeof(VoiceDraftConsumer), hostedTypes);
        Assert.DoesNotContain(typeof(WeeklySummaryConsumer), hostedTypes);
        Assert.DoesNotContain(typeof(SymptomObservationConsumer), hostedTypes);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAiProposalProvider));
    }

    private static void InvokeRegistration(IServiceCollection services, IConfiguration configuration, string environment)
    {
        var registrationType = typeof(VoiceDraftConsumer).Assembly.GetType(
            "AgroPlan.Worker.AppliedFarmIntelligenceWorkerRegistration");
        Assert.NotNull(registrationType);

        var registerMethod = registrationType!.GetMethod("Register", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(registerMethod);

        registerMethod!.Invoke(null, [services, configuration, environment]);
    }
}
