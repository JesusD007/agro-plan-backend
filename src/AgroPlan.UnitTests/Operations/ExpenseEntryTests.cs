using AgroPlan.Application.UseCases;
using AgroPlan.Domain.Operations;
using Xunit;

namespace AgroPlan.UnitTests.Operations;

public sealed class ExpenseEntryTests
{
    [Theory]
    [InlineData(OperationCostCategory.Expense)]
    [InlineData(OperationCostCategory.Labor)]
    [InlineData(OperationCostCategory.Material)]
    public void Create_preserves_original_currency_and_unit_for_all_categories(OperationCostCategory category)
    {
        var entry = new AppendExpenseEntry().Create(Guid.NewGuid(), Guid.NewGuid(), null,
            category, DateTimeOffset.UtcNow, "Seed", 120.50m, "dop", 4.5m, "kg", "producer-1", DateTimeOffset.UtcNow);

        Assert.Equal(category, entry.Category);
        Assert.Equal(120.50m, entry.Amount);
        Assert.Equal("DOP", entry.CurrencyCode);
        Assert.Equal(4.5m, entry.Quantity);
        Assert.Equal("kg", entry.Unit);
    }

    [Fact]
    public void Create_requires_currency_code_and_quantity_unit_pair()
    {
        Assert.Throws<ArgumentException>(() => new AppendExpenseEntry().Create(Guid.NewGuid(), Guid.NewGuid(), null,
            OperationCostCategory.Expense, DateTimeOffset.UtcNow, "Seed", 1m, " ", null, null, "producer-1", DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => new AppendExpenseEntry().Create(Guid.NewGuid(), Guid.NewGuid(), null,
            OperationCostCategory.Material, DateTimeOffset.UtcNow, "Seed", 1m, "DOP", 2m, null, "producer-1", DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => new AppendExpenseEntry().Create(Guid.NewGuid(), Guid.NewGuid(), null,
            OperationCostCategory.Material, DateTimeOffset.UtcNow, "Seed", 1m, "DOP", null, "kg", "producer-1", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Create_rejects_negative_amount_or_quantity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AppendExpenseEntry().Create(Guid.NewGuid(), Guid.NewGuid(), null,
            OperationCostCategory.Expense, DateTimeOffset.UtcNow, "Seed", -1m, "DOP", null, null, "producer-1", DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AppendExpenseEntry().Create(Guid.NewGuid(), Guid.NewGuid(), null,
            OperationCostCategory.Material, DateTimeOffset.UtcNow, "Seed", 1m, "DOP", -1m, "kg", "producer-1", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Correction_appends_linked_entry_and_preserves_original_values()
    {
        var farmId = Guid.NewGuid();
        var parcelId = Guid.NewGuid();
        var cycleId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        var original = new AppendExpenseEntry().Create(farmId, parcelId, cycleId, OperationCostCategory.Material,
            createdAt, "Fertilizer", 150m, "DOP", 10m, "kg", "producer-1", createdAt);

        var correction = new CorrectExpenseEntry().Create(original, "Receipt amount corrected", createdAt.AddDays(-1),
            "Fertilizer", 175m, "DOP", 10m, "kg", "producer-2", createdAt.AddHours(1));

        Assert.Equal(original.Id, correction.CorrectsEntryId);
        Assert.Equal("Receipt amount corrected", correction.CorrectionReason);
        Assert.Equal(farmId, correction.FarmId);
        Assert.Equal(parcelId, correction.ParcelId);
        Assert.Equal(cycleId, correction.CropCycleId);
        Assert.Equal(OperationCostCategory.Material, correction.Category);
        Assert.Equal("DOP", original.CurrencyCode);
        Assert.Equal(10m, original.Quantity);
        Assert.Equal("kg", original.Unit);
        Assert.Equal(150m, original.Amount);
    }

    [Fact]
    public void Correction_requires_a_reason()
    {
        var original = new AppendExpenseEntry().Create(Guid.NewGuid(), Guid.NewGuid(), null,
            OperationCostCategory.Labor, DateTimeOffset.UtcNow, "Harvest work", 200m, "DOP", null, null,
            "producer-1", DateTimeOffset.UtcNow);
        Assert.Throws<ArgumentException>(() => new CorrectExpenseEntry().Create(original, " ", DateTimeOffset.UtcNow,
            "Corrected", 210m, "DOP", null, null, "producer-1", DateTimeOffset.UtcNow));
    }
}
