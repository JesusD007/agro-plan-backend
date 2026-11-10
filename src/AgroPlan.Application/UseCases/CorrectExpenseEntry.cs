using AgroPlan.Domain.Operations;

namespace AgroPlan.Application.UseCases;

public sealed class CorrectExpenseEntry
{
    public ExpenseEntry Create(ExpenseEntry original, string reason, DateTimeOffset occurredAt,
        string description, decimal amount, string currencyCode, decimal? quantity, string? unit,
        string actorUserId, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000)
            throw new ArgumentException("Correction reason is required and must not exceed 1000 characters.", nameof(reason));
        return new ExpenseEntry(Guid.NewGuid(), original.FarmId, original.ParcelId, original.CropCycleId,
            original.Category, occurredAt, description, amount, currencyCode, quantity, unit,
            actorUserId, createdAt, original.Id, reason);
    }
}
