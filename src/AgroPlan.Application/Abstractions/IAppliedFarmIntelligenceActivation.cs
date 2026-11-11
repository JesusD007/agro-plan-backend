namespace AgroPlan.Application.Abstractions;

public enum AppliedFarmIntelligenceOperation
{
    WeeklySummaryNarrative,
    SymptomObservation
}

/// <summary>Shared durable-work activation contract consumed by the API and Worker hosts.</summary>
public interface IAppliedFarmIntelligenceActivation
{
    bool IsEnabled(AppliedFarmIntelligenceOperation operation);
}
