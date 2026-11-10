using System.Security.Claims;
using AgroPlan.Api.Authorization;
using AgroPlan.Api.Reports;
using AgroPlan.Application.Reports;
using AgroPlan.Domain.AdvisoryAccess;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Api.Controllers;

[ApiController]
[Route("api/farms/{farmId:guid}/advisor-reports")]
[Authorize]
public sealed class AdvisorReportsController(AgroPlanDbContext db, IAuthorizationService authorization) : ControllerBase
{
    private readonly OperationsReportReader reader = new();
    private readonly CsvReportWriter csv = new();

    [HttpGet("costs")]
    public async Task<IActionResult> Costs(Guid farmId, Guid? cropCycleId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)
    {
        if (!await Allowed(farmId, AdvisorReportScope.Costs)) return Forbid();
        if (from.HasValue && to.HasValue && to < from) return BadRequest(new { message = "End date must not precede start date." });
        if (cropCycleId.HasValue && !await db.CropCycles.AsNoTracking().AnyAsync(x => x.FarmId == farmId && x.Id == cropCycleId, ct)) return NotFound();
        var expenses = await db.ExpenseEntries.AsNoTracking().Where(x => x.FarmId == farmId).ToListAsync(ct);
        var harvests = await db.HarvestEntries.AsNoTracking().Where(x => x.FarmId == farmId).ToListAsync(ct);
        return Ok(reader.ReadCosts(farmId, cropCycleId, from, to, expenses, harvests));
    }

    [HttpGet("costs.csv")]
    public async Task<IActionResult> CostsCsv(Guid farmId, Guid? cropCycleId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)
    {
        if (!await Allowed(farmId, AdvisorReportScope.Costs)) return Forbid();
        if (from.HasValue && to.HasValue && to < from) return BadRequest(new { message = "End date must not precede start date." });
        var query = db.ExpenseEntries.AsNoTracking().Where(x => x.FarmId == farmId);
        if (cropCycleId.HasValue) query = query.Where(x => x.CropCycleId == cropCycleId);
        if (from.HasValue) query = query.Where(x => x.OccurredAt >= from);
        if (to.HasValue) query = query.Where(x => x.OccurredAt <= to);
        var rows = await query.OrderBy(x => x.OccurredAt).ToListAsync(ct);
        var corrected = rows.Where(x => x.CorrectsEntryId.HasValue).Select(x => x.CorrectsEntryId!.Value).ToHashSet();
        var data = new List<IEnumerable<string?>> { new string?[] { "entry_id", "active", "occurred_at", "category", "amount", "currency", "quantity", "unit" } };
        data.AddRange(rows.Select(x => (IEnumerable<string?>)new string?[] { x.Id.ToString(), (!corrected.Contains(x.Id)).ToString().ToLowerInvariant(),
            x.OccurredAt.ToString("O"), x.Category.ToString(), CsvReportWriter.Format(x.Amount), x.CurrencyCode,
            x.Quantity.HasValue ? CsvReportWriter.Format(x.Quantity.Value) : null, x.Unit }));
        return File(csv.Write(data), "text/csv; charset=utf-8", "advisor-costs.csv");
    }

    [HttpGet("production")]
    public async Task<IActionResult> Production(Guid farmId, CancellationToken ct)
    {
        if (!await Allowed(farmId, AdvisorReportScope.Production)) return Forbid();
        var cycles = await db.CropCycles.AsNoTracking().Where(x => x.FarmId == farmId).Include(x => x.CropCatalogEntry)
            .Join(db.Parcels.AsNoTracking().Where(x => x.FarmId == farmId), cycle => new { cycle.FarmId, cycle.ParcelId },
                parcel => new { parcel.FarmId, ParcelId = parcel.Id }, (cycle, parcel) => new { Cycle = cycle, Parcel = parcel }).ToListAsync(ct);
        var harvests = await db.HarvestEntries.AsNoTracking().Where(x => x.FarmId == farmId).ToListAsync(ct);
        var planting = await db.LogbookEntries.AsNoTracking().Where(x => x.FarmId == farmId).ToListAsync(ct);
        return Ok(reader.ReadProduction(farmId, cycles.Select(x => (x.Cycle, x.Parcel)), harvests, planting));
    }

    [HttpGet("production.csv")]
    public async Task<IActionResult> ProductionCsv(Guid farmId, CancellationToken ct)
    {
        if (!await Allowed(farmId, AdvisorReportScope.Production)) return Forbid();
        var cycles = await db.CropCycles.AsNoTracking().Where(x => x.FarmId == farmId).Include(x => x.CropCatalogEntry)
            .Join(db.Parcels.AsNoTracking().Where(x => x.FarmId == farmId), cycle => new { cycle.FarmId, cycle.ParcelId },
                parcel => new { parcel.FarmId, ParcelId = parcel.Id }, (cycle, parcel) => new { Cycle = cycle, Parcel = parcel }).ToListAsync(ct);
        var harvests = await db.HarvestEntries.AsNoTracking().Where(x => x.FarmId == farmId).ToListAsync(ct);
        var planting = await db.LogbookEntries.AsNoTracking().Where(x => x.FarmId == farmId).ToListAsync(ct);
        var report = reader.ReadProduction(farmId, cycles.Select(x => (x.Cycle, x.Parcel)), harvests, planting);
        var rows = new List<IEnumerable<string?>> { new string?[] { "crop_cycle_id", "crop_code", "crop_name", "area_hectares", "planting_date", "harvest_id", "quantity", "unit" } };
        foreach (var cycle in report.CropCycles)
        {
            if (cycle.HarvestedQuantities.Count == 0)
                rows.Add([cycle.CropCycleId.ToString(), cycle.CropCode, cycle.CropName, CsvReportWriter.Format(cycle.AreaHectares), cycle.PlantingDate?.ToString("yyyy-MM-dd"), null, null, null]);
            foreach (var total in cycle.HarvestedQuantities)
                rows.Add([cycle.CropCycleId.ToString(), cycle.CropCode, cycle.CropName, CsvReportWriter.Format(cycle.AreaHectares), cycle.PlantingDate?.ToString("yyyy-MM-dd"), string.Join(';', total.SourceHarvestIds), CsvReportWriter.Format(total.Quantity), total.Unit]);
        }
        return File(csv.Write(rows), "text/csv; charset=utf-8", "advisor-production.csv");
    }

    private async Task<bool> Allowed(Guid farmId, AdvisorReportScope scope)
    {
        var resource = new AdvisorReportResource(farmId, scope);
        var result = await authorization.AuthorizeAsync(User, resource, new AdvisorReportRequirement(scope));
        return result.Succeeded;
    }
}
