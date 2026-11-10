using AgroPlan.Application.Reports;
using AgroPlan.Domain.Crops;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Logbook;
using AgroPlan.Domain.Operations;
using Xunit;

namespace AgroPlan.UnitTests.Reports;

public sealed class OperationsReportTests
{
    [Fact]
    public void Costs_keep_currency_and_unit_groups_trace_sources_and_mark_quintal_unavailable()
    {
        var farm = Guid.NewGuid(); var parcel = Guid.NewGuid(); var cycle = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var first = Expense(Guid.NewGuid(), farm, parcel, cycle, 25m, "DOP", 2m, "kg", now);
        var second = Expense(Guid.NewGuid(), farm, parcel, cycle, 4m, "USD", 3m, "lb", now);
        var unrelated = Expense(Guid.NewGuid(), Guid.NewGuid(), parcel, cycle, 900m, "DOP", null, null, now);
        var superseded = Expense(Guid.NewGuid(), farm, parcel, cycle, 10m, "DOP", 1m, "kg", now);
        var correction = Expense(Guid.NewGuid(), farm, parcel, cycle, 15m, "DOP", 1m, "kg", now, superseded.Id);

        var report = new OperationsReportReader().ReadCosts(farm, cycle, null, null,
            [first, second, unrelated, superseded, correction], []);

        Assert.Equal(2, report.CurrencyTotals.Count);
        Assert.Equal(40m, Assert.Single(report.CurrencyTotals, x => x.CurrencyCode == "DOP").Amount);
        Assert.Equal(4m, Assert.Single(report.CurrencyTotals, x => x.CurrencyCode == "USD").Amount);
        Assert.Equal(new[] { first.Id, correction.Id }.Order(), Assert.Single(report.CurrencyTotals, x => x.CurrencyCode == "DOP").SourceEntryIds);
        Assert.Equal(2, report.RecordedQuantities.Count);
        Assert.Equal("not_computable", report.CostPerQuintal.Status);
        Assert.Null(report.CostPerQuintal.Amount);
        Assert.Contains("validated_crop_specific_quintal_definition", report.CostPerQuintal.MissingInputs);
        Assert.Contains("single_currency_or_validated_exchange_rate", report.CostPerQuintal.MissingInputs);
    }

    [Fact]
    public void Production_keeps_harvest_units_and_excludes_superseded_rows_and_foreign_cycles()
    {
        var farm = Guid.NewGuid(); var parcelId = Guid.NewGuid(); var cycleId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var crop = new CropCatalogEntry(Guid.NewGuid(), "ARROZ", "Arroz", "catalog", CropCatalogValidationStatus.IdentityOnly, null, null, null);
        var parcel = new Parcel(parcelId, farm, "Parcel", null, 1.5m, now);
        var cycle = new CropCycle(cycleId, farm, parcelId, crop, null, null, null, now);
        var sowing = new LogbookEntry(Guid.NewGuid(), farm, parcelId, cycleId, LogbookActivityType.Sowing,
            now.AddDays(-20), "Actual planting", "producer", now.AddDays(-20));
        var replaced = Harvest(Guid.NewGuid(), farm, parcelId, cycleId, 100m, "kg", now);
        var correction = Harvest(Guid.NewGuid(), farm, parcelId, cycleId, 75m, "kg", now, replaced.Id);
        var sacks = Harvest(Guid.NewGuid(), farm, parcelId, cycleId, 2m, "sacks", now);

        var report = new OperationsReportReader().ReadProduction(farm, [(cycle, parcel)], [replaced, correction, sacks], [sowing]);
        var row = Assert.Single(report.CropCycles);

        Assert.Equal("ARROZ", row.CropCode);
        Assert.Equal(1.5m, row.AreaHectares);
        Assert.Equal(DateOnly.FromDateTime(now.AddDays(-20).UtcDateTime), row.PlantingDate);
        Assert.Equal(2, row.HarvestedQuantities.Count);
        Assert.Equal(75m, Assert.Single(row.HarvestedQuantities, x => x.Unit == "kg").Quantity);
        Assert.Equal(new[] { correction.Id }, Assert.Single(row.HarvestedQuantities, x => x.Unit == "kg").SourceHarvestIds);
    }

    [Fact]
    public void Production_uses_corrected_active_sowing_date()
    {
        var farm = Guid.NewGuid(); var parcelId = Guid.NewGuid(); var cycleId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var crop = new CropCatalogEntry(Guid.NewGuid(), "ARROZ", "Arroz", "catalog", CropCatalogValidationStatus.IdentityOnly, null, null, null);
        var parcel = new Parcel(parcelId, farm, "Parcel", null, 1.5m, now);
        var cycle = new CropCycle(cycleId, farm, parcelId, crop, null, null, null, now);
        var original = new LogbookEntry(Guid.NewGuid(), farm, parcelId, cycleId, LogbookActivityType.Sowing,
            now.AddDays(-30), "Original planting date", "producer", now.AddDays(-29));
        var correction = new LogbookEntry(Guid.NewGuid(), farm, parcelId, cycleId, LogbookActivityType.Sowing,
            now.AddDays(-12), "Corrected planting date", "producer", now, original.Id, "Planting date correction");

        var report = new OperationsReportReader().ReadProduction(farm, [(cycle, parcel)], [], [original, correction]);

        Assert.Equal(DateOnly.FromDateTime(now.AddDays(-12).UtcDateTime), Assert.Single(report.CropCycles).PlantingDate);
    }

    private static ExpenseEntry Expense(Guid id, Guid farm, Guid parcel, Guid? cycle, decimal amount,
        string currency, decimal? quantity, string? unit, DateTimeOffset at, Guid? corrects = null) =>
        new(id, farm, parcel, cycle, OperationCostCategory.Material, at, "Input", amount, currency,
            quantity, unit, "producer", at, corrects, corrects.HasValue ? "Correction" : null);

    private static HarvestEntry Harvest(Guid id, Guid farm, Guid parcel, Guid cycle, decimal quantity,
        string unit, DateTimeOffset at, Guid? corrects = null) =>
        new(id, farm, parcel, cycle, at, quantity, unit, "producer", at, "{}", corrects,
            corrects.HasValue ? "Correction" : null);
}
