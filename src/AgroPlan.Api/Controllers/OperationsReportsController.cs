using AgroPlan.Api.Authorization;
using AgroPlan.Api.Reports;
using AgroPlan.Application.Reports;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Api.Controllers;

[ApiController]
[Route("api/farms/{farmId:guid}/reports")]
[Authorize(Policy = FarmMembershipRequirement.PolicyName)]
public sealed class OperationsReportsController(AgroPlanDbContext db) : ControllerBase
{
    private readonly OperationsReportReader _reader = new();
    private readonly CsvReportWriter _csv = new();

    [HttpGet("costs")]
    public async Task<ActionResult<CostReport>> Costs(Guid farmId, Guid? cropCycleId,
        DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)
    {
        if (from.HasValue && to.HasValue && to < from) return BadRequest(new { message = "End date must not precede start date." });
        if (cropCycleId.HasValue && !await db.CropCycles.AsNoTracking().AnyAsync(x => x.FarmId == farmId && x.Id == cropCycleId, ct))
            return NotFound();
        var expenses = await db.ExpenseEntries.AsNoTracking().Where(x => x.FarmId == farmId).ToListAsync(ct);
        var harvests = await db.HarvestEntries.AsNoTracking().Where(x => x.FarmId == farmId).ToListAsync(ct);
        return Ok(_reader.ReadCosts(farmId, cropCycleId, from, to, expenses, harvests));
    }

    [HttpGet("production")]
    public async Task<ActionResult<ProductionReport>> Production(Guid farmId, CancellationToken ct)
    {
        var cycles = await db.CropCycles.AsNoTracking().Where(x => x.FarmId == farmId)
            .Include(x => x.CropCatalogEntry).Join(db.Parcels.AsNoTracking().Where(p => p.FarmId == farmId),
                cycle => new { cycle.FarmId, cycle.ParcelId }, parcel => new { parcel.FarmId, ParcelId = parcel.Id },
                (cycle, parcel) => new { Cycle = cycle, Parcel = parcel }).ToListAsync(ct);
        var harvests = await db.HarvestEntries.AsNoTracking().Where(x => x.FarmId == farmId).ToListAsync(ct);
        var plantingRows = await db.LogbookEntries.AsNoTracking().Where(x => x.FarmId == farmId).ToListAsync(ct);
        var report = _reader.ReadProduction(farmId, cycles.Select(x => (x.Cycle, x.Parcel)), harvests, plantingRows);
        return Ok(report);
    }

    [HttpGet("costs.csv")]
    public async Task<IActionResult> CostsCsv(Guid farmId, Guid? cropCycleId,
        DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)
    {
        if (from.HasValue && to.HasValue && to < from) return BadRequest(new { message = "End date must not precede start date." });
        if (cropCycleId.HasValue && !await db.CropCycles.AsNoTracking().AnyAsync(x => x.FarmId == farmId && x.Id == cropCycleId, ct))
            return NotFound();
        var allRowsQuery = db.ExpenseEntries.AsNoTracking().Where(x => x.FarmId == farmId);
        if (cropCycleId.HasValue) allRowsQuery = allRowsQuery.Where(x => x.CropCycleId == cropCycleId);
        var allRows = await allRowsQuery.ToListAsync(ct);
        var correctedIds = allRows.Where(x => x.CorrectsEntryId.HasValue).Select(x => x.CorrectsEntryId!.Value).ToHashSet();
        var rows = allRows.Where(x => (!from.HasValue || x.OccurredAt >= from) && (!to.HasValue || x.OccurredAt <= to))
            .OrderBy(x => x.OccurredAt).ToArray();
        var data = new List<IEnumerable<string?>>
        {
            new string?[] { "entry_id", "corrects_entry_id", "active", "occurred_at", "category", "description", "amount", "currency", "quantity", "unit" }
        };
        data.AddRange(rows.Select(x => (IEnumerable<string?>)new string?[] {
            x.Id.ToString(), x.CorrectsEntryId?.ToString(), (!correctedIds.Contains(x.Id)).ToString().ToLowerInvariant(),
            x.OccurredAt.ToString("O"), x.Category.ToString(), x.Description, CsvReportWriter.Format(x.Amount), x.CurrencyCode,
            x.Quantity.HasValue ? CsvReportWriter.Format(x.Quantity.Value) : null, x.Unit }));
        return File(_csv.Write(data), "text/csv; charset=utf-8", "costs.csv");
    }

    [HttpGet("production.csv")]
    public async Task<IActionResult> ProductionCsv(Guid farmId, CancellationToken ct)
    {
        var cycles = await db.CropCycles.AsNoTracking().Where(x => x.FarmId == farmId)
            .Include(x => x.CropCatalogEntry).Join(db.Parcels.AsNoTracking().Where(p => p.FarmId == farmId),
                cycle => new { cycle.FarmId, cycle.ParcelId }, parcel => new { parcel.FarmId, ParcelId = parcel.Id },
                (cycle, parcel) => new { Cycle = cycle, Parcel = parcel }).ToListAsync(ct);
        var harvests = await db.HarvestEntries.AsNoTracking().Where(x => x.FarmId == farmId).ToListAsync(ct);
        var plantingRows = await db.LogbookEntries.AsNoTracking().Where(x => x.FarmId == farmId).ToListAsync(ct);
        var report = _reader.ReadProduction(farmId, cycles.Select(x => (x.Cycle, x.Parcel)), harvests, plantingRows);
        var rows = new List<IEnumerable<string?>> { new string?[] { "crop_cycle_id", "crop_code", "crop_name", "area_hectares", "planting_date", "harvest_id", "quantity", "unit" } };
        foreach (var cycle in report.CropCycles)
        {
            if (cycle.HarvestedQuantities.Count == 0)
                rows.Add([cycle.CropCycleId.ToString(), cycle.CropCode, cycle.CropName, CsvReportWriter.Format(cycle.AreaHectares), cycle.PlantingDate?.ToString("yyyy-MM-dd"), null, null, null]);
            foreach (var total in cycle.HarvestedQuantities)
                rows.Add([cycle.CropCycleId.ToString(), cycle.CropCode, cycle.CropName, CsvReportWriter.Format(cycle.AreaHectares), cycle.PlantingDate?.ToString("yyyy-MM-dd"), string.Join(';', total.SourceHarvestIds), CsvReportWriter.Format(total.Quantity), total.Unit]);
        }
        return File(_csv.Write(rows), "text/csv; charset=utf-8", "production.csv");
    }
}
