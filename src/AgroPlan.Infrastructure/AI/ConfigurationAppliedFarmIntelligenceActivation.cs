using AgroPlan.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace AgroPlan.Infrastructure.AI;

public sealed class ConfigurationAppliedFarmIntelligenceActivation(IConfiguration configuration)
    : IAppliedFarmIntelligenceActivation
{
    public bool IsEnabled(AppliedFarmIntelligenceOperation operation)
    {
        var (feature, verifiedOperation) = operation switch
        {
            AppliedFarmIntelligenceOperation.WeeklySummaryNarrative => ("WeeklySummary", "narrative"),
            AppliedFarmIntelligenceOperation.SymptomObservation => ("SymptomObservation", "observation"),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

        var section = configuration.GetSection($"AppliedFarmIntelligence:{feature}");
        return section.GetValue<bool>("Enabled") &&
               !string.IsNullOrWhiteSpace(section["Provider"]) &&
               string.Equals(section["VerifiedOperation"], verifiedOperation, StringComparison.Ordinal) &&
               section.GetValue<bool>("EvidenceApproved");
    }
}
