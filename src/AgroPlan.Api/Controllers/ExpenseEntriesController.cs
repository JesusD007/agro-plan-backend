using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AgroPlan.Api.Authorization;
using AgroPlan.Application.UseCases;
using AgroPlan.Domain.Operations;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Api.Controllers;

[ApiController]
[Route("api/farms/{farmId:guid}/expenses")]
[Authorize(Policy = FarmMembershipRequirement.PolicyName)]
public sealed class ExpenseEntriesController(AgroPlanDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExpenseEntryResponse>>> List(Guid farmId, CancellationToken ct)
    {
        var rows = await db.ExpenseEntries.AsNoTracking().Where(x => x.FarmId == farmId)
            .OrderByDescending(x => x.OccurredAt).Take(500).ToListAsync(ct);
        return Ok(rows.Select(ToResponse).ToArray());
    }

    [HttpPost]
    public async Task<ActionResult<ExpenseEntryResponse>> Append(Guid farmId, ExpenseEntryRequest request, CancellationToken ct)
    {
        var actor = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actor is null) return Unauthorized();
        ExpenseEntry entry;
        try
        {
            entry = new AppendExpenseEntry().Create(farmId, request.ParcelId, request.CropCycleId,
                request.Category, request.OccurredAt, request.Description, request.Amount,
                request.CurrencyCode, request.Quantity, request.Unit, actor, DateTimeOffset.UtcNow);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        if (!await db.Parcels.AsNoTracking().AnyAsync(x => x.FarmId == farmId && x.Id == entry.ParcelId, ct))
            return BadRequest(new { message = "The parcel does not belong to this farm." });
        if (entry.CropCycleId.HasValue && !await db.CropCycles.AsNoTracking()
                .AnyAsync(x => x.FarmId == farmId && x.ParcelId == entry.ParcelId && x.Id == entry.CropCycleId, ct))
            return BadRequest(new { message = "The crop cycle does not belong to this farm and parcel." });

        db.ExpenseEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), new { farmId }, ToResponse(entry));
    }

    [HttpPost("{entryId:guid}/corrections")]
    public async Task<ActionResult<ExpenseEntryResponse>> Correct(Guid farmId, Guid entryId,
        CorrectExpenseEntryRequest request, CancellationToken ct)
    {
        var original = await db.ExpenseEntries.AsNoTracking()
            .SingleOrDefaultAsync(x => x.FarmId == farmId && x.Id == entryId, ct);
        if (original is null) return NotFound();
        var actor = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actor is null) return Unauthorized();
        ExpenseEntry correction;
        try
        {
            correction = new CorrectExpenseEntry().Create(original, request.Reason, request.OccurredAt,
                request.Description, request.Amount, request.CurrencyCode, request.Quantity, request.Unit,
                actor, DateTimeOffset.UtcNow);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        db.ExpenseEntries.Add(correction);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), new { farmId }, ToResponse(correction));
    }

    private static ExpenseEntryResponse ToResponse(ExpenseEntry x) => new(x.Id, x.FarmId, x.ParcelId,
        x.CropCycleId, x.Category, x.OccurredAt, x.Description, x.Amount, x.CurrencyCode,
        x.Quantity, x.Unit, x.ActorUserId, x.CreatedAt, x.CorrectsEntryId, x.CorrectionReason);
}

public sealed record ExpenseEntryRequest([param: Required] Guid ParcelId, Guid? CropCycleId,
    [param: Required] OperationCostCategory Category, [param: Required] DateTimeOffset OccurredAt,
    [param: Required, StringLength(1000, MinimumLength = 1)] string Description,
    [param: Required, Range(typeof(decimal), "0", "99999999999999")] decimal Amount,
    [param: Required, StringLength(3, MinimumLength = 3)] string CurrencyCode,
    decimal? Quantity, [StringLength(64)] string? Unit);

public sealed record CorrectExpenseEntryRequest([param: Required, StringLength(1000, MinimumLength = 1)] string Reason,
    [param: Required] DateTimeOffset OccurredAt,
    [param: Required, StringLength(1000, MinimumLength = 1)] string Description,
    [param: Required, Range(typeof(decimal), "0", "99999999999999")] decimal Amount,
    [param: Required, StringLength(3, MinimumLength = 3)] string CurrencyCode,
    decimal? Quantity, [StringLength(64)] string? Unit);

public sealed record ExpenseEntryResponse(Guid Id, Guid FarmId, Guid ParcelId, Guid? CropCycleId,
    OperationCostCategory Category, DateTimeOffset OccurredAt, string Description, decimal Amount,
    string CurrencyCode, decimal? Quantity, string? Unit, string ActorUserId, DateTimeOffset CreatedAt,
    Guid? CorrectsEntryId, string? CorrectionReason)
{
    public string AmountExact => Amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
