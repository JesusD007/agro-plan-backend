using AgroPlan.Application.Abstractions;
using AgroPlan.Worker.AI;
using AgroPlan.Worker.Jobs;
using AgroPlan.Infrastructure.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgroPlan.Worker;

/// <summary>One fail-closed activation boundary for the optional Phase 4 AI consumers.</summary>
public static class AppliedFarmIntelligenceWorkerRegistration
{
    public static IServiceCollection Register(IServiceCollection services, IConfiguration configuration, string environmentName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var isDevelopmentOrTest = string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(environmentName, "Test", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);
        var activation = new ConfigurationAppliedFarmIntelligenceActivation(configuration);
        services.TryAddSingleton<IAppliedFarmIntelligenceActivation>(activation);

        var hasSpeechProvider = HasExplicitProvider<ISpeechTranscriptionProvider>(services,
            typeof(UnconfiguredSpeechTranscriptionProvider));
        var hasSummaryProvider = HasExplicitProvider<IWeeklySummaryNarrativeProvider>(services,
            typeof(UnconfiguredWeeklySummaryProvider));
        var hasSymptomProvider = HasExplicitProvider<ISymptomObservationProvider>(services,
            typeof(UnconfiguredSymptomObservationProvider));
        services.Configure<SymptomObservationRetryOptions>(options =>
        {
            options.MaxAttempts = configuration.GetValue<int?>("AppliedFarmIntelligence:SymptomObservation:MaxAttempts") ??
                                  SymptomObservationRetryOptions.DefaultMaximumAttempts;
        });

        if (isDevelopmentOrTest)
        {
            services.TryAddSingleton<ISpeechTranscriptionProvider, UnconfiguredSpeechTranscriptionProvider>();
            services.TryAddSingleton<IWeeklySummaryNarrativeProvider, UnconfiguredWeeklySummaryProvider>();
            services.TryAddSingleton<ISymptomObservationProvider, UnconfiguredSymptomObservationProvider>();
        }

        if (configuration.GetValue<bool>("AppliedFarmIntelligence:Speech:Enabled") &&
            hasSpeechProvider && HasService<IFileStorage>(services) && IsSpeechEvidenceApproved(configuration))
            services.TryAddEnumerable(ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, VoiceDraftConsumer>());

        if (activation.IsEnabled(AppliedFarmIntelligenceOperation.WeeklySummaryNarrative) && hasSummaryProvider)
            services.TryAddEnumerable(ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, WeeklySummaryConsumer>());

        if (activation.IsEnabled(AppliedFarmIntelligenceOperation.SymptomObservation) &&
            hasSymptomProvider && HasService<IFileStorage>(services))
            services.TryAddEnumerable(ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, SymptomObservationConsumer>());

        return services;
    }

    /// <summary>Checks provider-specific evidence; synthetic CI fixtures are never provider evidence.</summary>
    public static bool IsSpeechEvidenceApproved(IConfiguration configuration)
    {
        var section = configuration.GetSection("AppliedFarmIntelligence:Speech");
        var provider = section["Provider"];
        var operation = section["VerifiedOperation"];
        return !string.IsNullOrWhiteSpace(provider) &&
               string.Equals(operation, "transcription", StringComparison.Ordinal) &&
               section.GetValue<bool>("EvidenceApproved") &&
               section.GetValue<bool>("SupportsDominicanSpanish") &&
               section.GetValue<bool>("SupportsHaitianCreole") &&
               section.GetValue<int>("ReferenceCaseCount") == 45 &&
               section.GetValue<int>("VoiceReferenceCaseCount") == 25 &&
               section.GetValue<int>("SummaryReferenceCaseCount") == 10 &&
               section.GetValue<int>("SymptomReferenceCaseCount") == 10 &&
               section.GetValue<int>("DominicanSpanishConsentedRecordingCount") >= 20 &&
               section.GetValue<int>("HaitianCreoleConsentedRecordingCount") >= 20 &&
               section.GetValue<int>("CodeSwitchConsentedRecordingCount") >= 1 &&
               section.GetValue<double>("DominicanSpanishFieldExactAccuracy") >= 0.95 &&
               section.GetValue<double>("HaitianCreoleFieldExactAccuracy") >= 0.95;
    }

    private static bool HasExplicitProvider<TProvider>(IServiceCollection services, Type unconfiguredType)
    {
        foreach (var descriptor in services.Where(item => item.ServiceType == typeof(TProvider)))
        {
            if (descriptor.ImplementationType is { } implementationType)
            {
                if (!unconfiguredType.IsAssignableFrom(implementationType)) return true;
                continue;
            }
            if (descriptor.ImplementationInstance is { } instance)
            {
                if (instance.GetType() != unconfiguredType) return true;
                continue;
            }
            // A factory is an explicit composition decision. Provider correctness remains covered
            // by the production readiness/evidence checks and provider contract tests.
            if (descriptor.ImplementationFactory is not null) return true;
        }
        return false;
    }

    private static bool HasService<TService>(IServiceCollection services) =>
        services.Any(descriptor => descriptor.ServiceType == typeof(TService));
}
