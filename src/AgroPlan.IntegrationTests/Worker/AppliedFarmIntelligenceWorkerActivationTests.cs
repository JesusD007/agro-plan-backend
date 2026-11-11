using System.Reflection;
using AgroPlan.Application.Abstractions;
using AgroPlan.Domain.Voice;
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
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ISpeechTranscriptionProvider));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IWeeklySummaryNarrativeProvider));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ISymptomObservationProvider));
    }

    [Fact]
    public async Task Development_registers_unconfigured_adapters_without_hosting_consumers_or_making_network_calls()
    {
        var services = new ServiceCollection();
        InvokeRegistration(services, new ConfigurationBuilder().Build(), "Development");
        using var provider = services.BuildServiceProvider();

        AssertNoPhaseFourConsumers(services);
        await Assert.ThrowsAsync<SpeechTranscriptionProviderException>(() =>
            provider.GetRequiredService<ISpeechTranscriptionProvider>().TranscribeAsync(
                new SpeechTranscriptionRequest([], SpeechLanguage.HaitianCreole), CancellationToken.None));
        await Assert.ThrowsAsync<WeeklySummaryNarrativeProviderException>(() =>
            provider.GetRequiredService<IWeeklySummaryNarrativeProvider>().GenerateAsync(
                new WeeklySummaryNarrativeInput(default, default, "America/Santo_Domingo", [], [], []), CancellationToken.None));
        await Assert.ThrowsAsync<SymptomObservationProviderException>(() =>
            provider.GetRequiredService<ISymptomObservationProvider>().ObserveAsync(
                new SymptomObservationRequest([], null, null), CancellationToken.None));
    }

    [Fact]
    public void Enabling_summary_requires_operation_specific_provider_approval_and_hosts_only_its_own_consumer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWeeklySummaryNarrativeProvider, FakeWeeklySummaryNarrativeProvider>();
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["AppliedFarmIntelligence:WeeklySummary:Enabled"] = "true"
        });

        InvokeRegistration(services, configuration, "Production");
        AssertNotHosted<WeeklySummaryConsumer>(services);

        services = new ServiceCollection();
        services.AddSingleton<IWeeklySummaryNarrativeProvider, FakeWeeklySummaryNarrativeProvider>();
        InvokeRegistration(services, Configuration(ApprovedProviderConfiguration("WeeklySummary", "narrative")), "Production");

        AssertHosted<WeeklySummaryConsumer>(services);
        AssertNotHosted<VoiceDraftConsumer>(services);
        AssertNotHosted<SymptomObservationConsumer>(services);
    }

    [Fact]
    public void Enabling_symptom_observation_requires_operation_specific_approval_and_private_storage()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISymptomObservationProvider, FakeSymptomObservationProvider>();
        var configuration = Configuration(new Dictionary<string, string?>
            { ["AppliedFarmIntelligence:SymptomObservation:Enabled"] = "true" });

        InvokeRegistration(services, configuration, "Production");
        AssertNotHosted<SymptomObservationConsumer>(services);

        services.AddSingleton<IFileStorage>(_ => throw new InvalidOperationException("Storage must not be resolved by registration."));
        InvokeRegistration(services, configuration, "Production");
        AssertNotHosted<SymptomObservationConsumer>(services);

        services = new ServiceCollection();
        services.AddSingleton<ISymptomObservationProvider, FakeSymptomObservationProvider>();
        services.AddSingleton<IFileStorage>(_ => throw new InvalidOperationException("Storage must not be resolved by registration."));
        InvokeRegistration(services, Configuration(ApprovedProviderConfiguration("SymptomObservation", "observation")), "Production");

        AssertHosted<SymptomObservationConsumer>(services);
        AssertNotHosted<VoiceDraftConsumer>(services);
        AssertNotHosted<WeeklySummaryConsumer>(services);
    }

    [Fact]
    public void Production_speech_requires_provider_both_languages_and_approved_bilingual_evidence()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISpeechTranscriptionProvider, FakeSpeechTranscriptionProvider>();
        services.AddSingleton<IFileStorage>(_ => throw new InvalidOperationException("Storage must not be resolved by registration."));
        var incomplete = SpeechConfiguration();
        incomplete["AppliedFarmIntelligence:Speech:SupportsHaitianCreole"] = "false";

        InvokeRegistration(services, Configuration(incomplete), "Production");
        AssertNotHosted<VoiceDraftConsumer>(services);

        services = new ServiceCollection();
        services.AddSingleton<ISpeechTranscriptionProvider, FakeSpeechTranscriptionProvider>();
        services.AddSingleton<IFileStorage>(_ => throw new InvalidOperationException("Storage must not be resolved by registration."));
        InvokeRegistration(services, Configuration(SpeechConfiguration()), "Production");

        AssertHosted<VoiceDraftConsumer>(services);
        AssertNotHosted<WeeklySummaryConsumer>(services);
        AssertNotHosted<SymptomObservationConsumer>(services);
    }

    [Fact]
    public void An_unconfigured_provider_cannot_activate_a_consumer_even_when_the_flag_is_enabled()
    {
        var services = new ServiceCollection();
        InvokeRegistration(services, Configuration(new Dictionary<string, string?>
        {
            ["AppliedFarmIntelligence:WeeklySummary:Enabled"] = "true",
            ["AppliedFarmIntelligence:SymptomObservation:Enabled"] = "true"
        }), "Test");

        AssertNotHosted<WeeklySummaryConsumer>(services);
        AssertNotHosted<SymptomObservationConsumer>(services);
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

    private static IConfiguration Configuration(IDictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static Dictionary<string, string?> SpeechConfiguration() => new()
    {
        ["AppliedFarmIntelligence:Speech:Enabled"] = "true",
        ["AppliedFarmIntelligence:Speech:Provider"] = "approved-transcription-provider",
        ["AppliedFarmIntelligence:Speech:VerifiedOperation"] = "transcription",
        ["AppliedFarmIntelligence:Speech:EvidenceApproved"] = "true",
        ["AppliedFarmIntelligence:Speech:SupportsDominicanSpanish"] = "true",
        ["AppliedFarmIntelligence:Speech:SupportsHaitianCreole"] = "true",
        ["AppliedFarmIntelligence:Speech:ReferenceCaseCount"] = "45",
        ["AppliedFarmIntelligence:Speech:VoiceReferenceCaseCount"] = "25",
        ["AppliedFarmIntelligence:Speech:SummaryReferenceCaseCount"] = "10",
        ["AppliedFarmIntelligence:Speech:SymptomReferenceCaseCount"] = "10",
        ["AppliedFarmIntelligence:Speech:DominicanSpanishConsentedRecordingCount"] = "20",
        ["AppliedFarmIntelligence:Speech:HaitianCreoleConsentedRecordingCount"] = "20",
        ["AppliedFarmIntelligence:Speech:CodeSwitchConsentedRecordingCount"] = "5",
        ["AppliedFarmIntelligence:Speech:DominicanSpanishFieldExactAccuracy"] = "0.95",
        ["AppliedFarmIntelligence:Speech:HaitianCreoleFieldExactAccuracy"] = "0.95"
    };

    private static Dictionary<string, string?> ApprovedProviderConfiguration(string feature, string operation) => new()
    {
        [$"AppliedFarmIntelligence:{feature}:Enabled"] = "true",
        [$"AppliedFarmIntelligence:{feature}:Provider"] = $"approved-{operation}-provider",
        [$"AppliedFarmIntelligence:{feature}:VerifiedOperation"] = operation,
        [$"AppliedFarmIntelligence:{feature}:EvidenceApproved"] = "true"
    };

    private static void AssertNoPhaseFourConsumers(IServiceCollection services)
    {
        AssertNotHosted<VoiceDraftConsumer>(services);
        AssertNotHosted<WeeklySummaryConsumer>(services);
        AssertNotHosted<SymptomObservationConsumer>(services);
    }

    private static void AssertHosted<TConsumer>(IServiceCollection services)
    {
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(TConsumer));
    }

    private static void AssertNotHosted<TConsumer>(IServiceCollection services)
    {
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(TConsumer));
    }

    private sealed class FakeWeeklySummaryNarrativeProvider : IWeeklySummaryNarrativeProvider
    {
        public Task<WeeklySummaryNarrativeDraft> GenerateAsync(WeeklySummaryNarrativeInput input, CancellationToken cancellationToken) =>
            Task.FromResult(new WeeklySummaryNarrativeDraft("A summary was generated.", []));
    }

    private sealed class FakeSpeechTranscriptionProvider : ISpeechTranscriptionProvider
    {
        public Task<string> TranscribeAsync(SpeechTranscriptionRequest request, CancellationToken cancellationToken) =>
            Task.FromResult("{}");
    }

    private sealed class FakeSymptomObservationProvider : ISymptomObservationProvider
    {
        public Task<SymptomObservationResult> ObserveAsync(SymptomObservationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new SymptomObservationResult("visible signs", "uncertainty", false, []));
    }
}
