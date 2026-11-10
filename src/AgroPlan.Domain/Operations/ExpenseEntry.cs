using System.Text.Json.Serialization;

namespace AgroPlan.Domain.Operations;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OperationCostCategory
{
    Expense,
    Labor,
    Material
}

/// <summary>An immutable farm operation cost. Corrections are appended as linked entries.</summary>
public sealed class ExpenseEntry
{
    private ExpenseEntry() { }

    public ExpenseEntry(Guid id, Guid farmId, Guid parcelId, Guid? cropCycleId,
        OperationCostCategory category, DateTimeOffset occurredAt, string description,
        decimal amount, string currencyCode, decimal? quantity, string? unit,
        string actorUserId, DateTimeOffset createdAt, Guid? correctsEntryId = null,
        string? correctionReason = null)
    {
        if (id == Guid.Empty || farmId == Guid.Empty || parcelId == Guid.Empty)
            throw new ArgumentException("Entry, farm and parcel ids are required.");
        if (!Enum.IsDefined(category)) throw new ArgumentException("Unsupported operation category.", nameof(category));
        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > 1000)
            throw new ArgumentException("Description is required and must not exceed 1000 characters.", nameof(description));
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Amount cannot be negative.");
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Trim().Length != 3 || !currencyCode.All(char.IsAsciiLetter))
            throw new ArgumentException("A three-letter currency code is required.", nameof(currencyCode));
        if (quantity is < 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity cannot be negative.");
        if (quantity.HasValue != !string.IsNullOrWhiteSpace(unit))
            throw new ArgumentException("Quantity and unit must be supplied together.");
        if (unit?.Trim().Length > 64) throw new ArgumentException("Unit must not exceed 64 characters.", nameof(unit));
        if (string.IsNullOrWhiteSpace(actorUserId)) throw new ArgumentException("Actor is required.", nameof(actorUserId));
        if (correctsEntryId.HasValue && string.IsNullOrWhiteSpace(correctionReason))
            throw new ArgumentException("A correction reason is required.", nameof(correctionReason));

        Id = id;
        FarmId = farmId;
        ParcelId = parcelId;
        CropCycleId = cropCycleId;
        Category = category;
        OccurredAt = occurredAt;
        Description = description.Trim();
        Amount = amount;
        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        Quantity = quantity;
        Unit = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim();
        ActorUserId = actorUserId;
        CreatedAt = createdAt;
        CorrectsEntryId = correctsEntryId;
        CorrectionReason = string.IsNullOrWhiteSpace(correctionReason) ? null : correctionReason.Trim();
    }

    public Guid Id { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid ParcelId { get; private set; }
    public Guid? CropCycleId { get; private set; }
    public OperationCostCategory Category { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string Description { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public string CurrencyCode { get; private set; } = null!;
    public decimal? Quantity { get; private set; }
    public string? Unit { get; private set; }
    public string ActorUserId { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CorrectsEntryId { get; private set; }
    public string? CorrectionReason { get; private set; }
}
