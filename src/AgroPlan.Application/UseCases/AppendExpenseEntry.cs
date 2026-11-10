using AgroPlan.Domain.Operations;

namespace AgroPlan.Application.UseCases;

public sealed class AppendExpenseEntry
{
    public ExpenseEntry Create(Guid farmId, Guid parcelId, Guid? cropCycleId,
        OperationCostCategory category, DateTimeOffset occurredAt, string description,
        decimal amount, string currencyCode, decimal? quantity, string? unit,
        string actorUserId, DateTimeOffset createdAt) =>
        new(Guid.NewGuid(), farmId, parcelId, cropCycleId, category, occurredAt, description,
            amount, currencyCode, quantity, unit, actorUserId, createdAt);
}
