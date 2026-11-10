using AgroPlan.Domain.Crops;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Logbook;
using AgroPlan.Domain.Operations;

namespace AgroPlan.Application.Reports;

/// <summary>Pure reporting projections. Queries and tenant filtering remain in the API/data access layer.</summary>
public sealed class OperationsReportReader
{
    public CostReport ReadCosts(Guid farmId, Guid? cropCycleId, DateTimeOffset? from, DateTimeOffset? to,
        IEnumerable<ExpenseEntry> expenseRows, IEnumerable<HarvestEntry> harvestRows)
    {
        if (farmId == Guid.Empty) throw new ArgumentException("Farm id is required.", nameof(farmId));
        if (from.HasValue && to.HasValue && to < from) throw new ArgumentException("End date must not precede start date.", nameof(to));

        var expenses = Latest(expenseRows, x => x.FarmId == farmId && (!cropCycleId.HasValue || x.CropCycleId == cropCycleId)
            && (!from.HasValue || x.OccurredAt >= from) && (!to.HasValue || x.OccurredAt <= to));
        var harvests = Latest(harvestRows, x => x.FarmId == farmId && (!cropCycleId.HasValue || x.CropCycleId == cropCycleId)
            && (!from.HasValue || x.HarvestedAt >= from) && (!to.HasValue || x.HarvestedAt <= to));

        var currencyTotals = expenses.GroupBy(x => x.CurrencyCode, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new CostCurrencyTotal(group.Key, group.Sum(x => x.Amount), group.Select(x => x.Id).Order().ToArray()))
            .ToArray();
        var quantities = expenses.Where(x => x.Quantity.HasValue && x.Unit is not null)
            .GroupBy(x => x.Unit!, StringComparer.Ordinal)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(group => new SourceQuantityTotal(group.Key, group.Sum(x => x.Quantity!.Value), group.Select(x => x.Id).Order().ToArray()))
            .ToArray();
        var productionUnits = harvests.GroupBy(x => x.Unit, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity), StringComparer.Ordinal);
        var missing = new List<string>();
        if (currencyTotals.Length == 0) missing.Add("confirmed_cost_entries");
        else if (currencyTotals.Length > 1) missing.Add("single_currency_or_validated_exchange_rate");
        if (productionUnits.Count == 0) missing.Add("confirmed_harvest_quantity");
        else if (productionUnits.Count > 1) missing.Add("compatible_harvest_unit_or_validated_conversion");
        missing.Add("validated_crop_specific_quintal_definition");
        missing.Add("validated_source_backed_quantity_conversion");

        return new CostReport(farmId, cropCycleId, from, to, currencyTotals, quantities,
            new CostPerQuintalResult("not_computable", null, null, missing.Distinct().ToArray()));
    }

    public ProductionReport ReadProduction(Guid farmId, IEnumerable<(CropCycle Cycle, Parcel Parcel)> cycles,
        IEnumerable<HarvestEntry> harvestRows, IEnumerable<LogbookEntry> plantingRows)
    {
        if (farmId == Guid.Empty) throw new ArgumentException("Farm id is required.", nameof(farmId));
        var latestHarvests = Latest(harvestRows, x => x.FarmId == farmId);
        var byCycle = latestHarvests.GroupBy(x => x.CropCycleId).ToDictionary(x => x.Key, x => x.ToArray());
        var sowingRows = plantingRows.Where(x => x.FarmId == farmId && x.CropCycleId.HasValue && x.ActivityType == LogbookActivityType.Sowing).ToArray();
        var supersededSowingIds = sowingRows.Where(x => x.CorrectsEntryId.HasValue)
            .Select(x => x.CorrectsEntryId!.Value).ToHashSet();
        var plantingDates = sowingRows.Where(x => !supersededSowingIds.Contains(x.Id))
            .GroupBy(x => x.CropCycleId!.Value)
            .ToDictionary(x => x.Key, x => (DateOnly?)DateOnly.FromDateTime(x.Min(y => y.OccurredAt).UtcDateTime));
        var rows = cycles.Where(x => x.Cycle.FarmId == farmId && x.Parcel.FarmId == farmId)
            .OrderBy(x => x.Cycle.Id)
            .Select(x =>
            {
                var harvests = byCycle.GetValueOrDefault(x.Cycle.Id) ?? [];
                var totals = harvests.GroupBy(h => h.Unit, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal)
                    .Select(g => new ProductionQuantity(g.Key, g.Sum(h => h.Quantity), g.Select(h => h.Id).Order().ToArray())).ToArray();
                return new ProductionCropCycleReport(x.Cycle.Id, x.Parcel.Id, x.Cycle.CropCatalogEntry.Code,
                    x.Cycle.CropCatalogEntry.DisplayName, x.Parcel.AreaHectares, plantingDates.GetValueOrDefault(x.Cycle.Id), totals);
            }).ToArray();
        return new ProductionReport(farmId, rows);
    }

    private static T[] Latest<T>(IEnumerable<T> rows, Func<T, bool> scoped) where T : class
    {
        var all = rows.ToArray();
        var superseded = all.Select(row => row switch
        {
            ExpenseEntry expense => expense.CorrectsEntryId,
            HarvestEntry harvest => harvest.CorrectsEntryId,
            _ => null
        }).Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();
        return all.Where(scoped).Where(row => row switch
        {
            ExpenseEntry expense => !superseded.Contains(expense.Id),
            HarvestEntry harvest => !superseded.Contains(harvest.Id),
            _ => false
        }).ToArray();
    }
}
