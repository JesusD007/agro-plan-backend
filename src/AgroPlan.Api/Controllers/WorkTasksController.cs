using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AgroPlan.Api.Authorization;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Workforce;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/farms/{farmId:guid}/work-tasks")]
public sealed class WorkTasksController(AgroPlanDbContext db, AssignedWorkerAuthorizationService authorization) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkTaskResponse>>> List(Guid farmId, CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        var role = await authorization.GetActiveRoleAsync(farmId, userId, ct);
        if (role is null) return Forbid();
        var tasks = db.WorkTasks.AsNoTracking().Where(task => task.FarmId == farmId);
        if (role == FarmRole.Worker)
            tasks = tasks.Where(task => task.AssignedWorkerUserId == userId);
        else if (role is not (FarmRole.Owner or FarmRole.Administrator))
            return Forbid();
        return Ok(await tasks.OrderBy(task => task.DueDate).Select(task => ToResponse(task)).ToArrayAsync(ct));
    }

    [HttpGet("{taskId:guid}")]
    public async Task<ActionResult<WorkTaskResponse>> Get(Guid farmId, Guid taskId, CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        var role = await authorization.GetActiveRoleAsync(farmId, userId, ct);
        if (role is null) return Forbid();
        var task = await db.WorkTasks.AsNoTracking().SingleOrDefaultAsync(item => item.FarmId == farmId && item.Id == taskId, ct);
        if (task is null) return NotFound();
        if (role == FarmRole.Worker && task.AssignedWorkerUserId != userId) return NotFound();
        if (role is not (FarmRole.Owner or FarmRole.Administrator or FarmRole.Worker)) return Forbid();
        return Ok(ToResponse(task));
    }

    [HttpPost]
    public async Task<ActionResult<WorkTaskResponse>> Create(Guid farmId, CreateWorkTaskRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        if (!await IsManager(farmId, userId, ct)) return Forbid();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var worker = await db.FarmMemberships.AsNoTracking().AnyAsync(member => member.FarmId == farmId &&
            member.UserId == request.AssignedWorkerUserId && member.Role == FarmRole.Worker && member.IsActive, ct);
        if (!worker) return BadRequest(new { message = "The assignee must be an active Worker in this farm." });
        if (request.ParcelId.HasValue)
        {
            var sameFarmLink = await db.Parcels.AsNoTracking().AnyAsync(parcel => parcel.FarmId == farmId &&
                parcel.Id == request.ParcelId && db.CropCycles.Any(cycle => cycle.FarmId == farmId &&
                    cycle.Id == request.CropCycleId && cycle.ParcelId == request.ParcelId), ct);
            if (!sameFarmLink) return BadRequest(new { message = "The parcel and crop cycle must belong to this farm and match each other." });
        }

        WorkTask task;
        try
        {
            task = new WorkTask(Guid.NewGuid(), farmId, request.AssignedWorkerUserId, request.DueDate,
                request.Description, request.PhotoRequired, request.WageAmount, request.WageUnit,
                request.ParcelId, request.CropCycleId, request.OperationKind);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }

        db.WorkTasks.Add(task);
        await db.SaveChangesAsync(ct);
        var response = ToResponse(task);
        return CreatedAtAction(nameof(List), new { farmId }, response);
    }

    [HttpPost("{taskId:guid}/start")]
    public async Task<IActionResult> Start(Guid farmId, Guid taskId, CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        if (!await authorization.IsAssignedWorkerAsync(farmId, taskId, userId, ct)) return NotFound();
        var task = await db.WorkTasks.SingleOrDefaultAsync(item => item.FarmId == farmId && item.Id == taskId, ct);
        if (task is null) return NotFound();
        try { task.Start(); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
        await db.SaveChangesAsync(ct);
        return Ok(ToResponse(task));
    }

    [HttpPut("{taskId:guid}/assignee")]
    public async Task<IActionResult> Reassign(Guid farmId, Guid taskId, ReassignWorkTaskRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        if (!await IsManager(farmId, userId, ct)) return Forbid();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var worker = await db.FarmMemberships.AsNoTracking().AnyAsync(member => member.FarmId == farmId &&
            member.UserId == request.AssignedWorkerUserId && member.Role == FarmRole.Worker && member.IsActive, ct);
        if (!worker) return BadRequest(new { message = "The assignee must be an active Worker in this farm." });
        var task = await db.WorkTasks.SingleOrDefaultAsync(item => item.FarmId == farmId && item.Id == taskId, ct);
        if (task is null) return NotFound();
        try { task.Reassign(request.AssignedWorkerUserId); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
        await db.SaveChangesAsync(ct);
        return Ok(ToResponse(task));
    }

    private Task<bool> IsManager(Guid farmId, string userId, CancellationToken ct) =>
        db.FarmMemberships.AsNoTracking().AnyAsync(member => member.FarmId == farmId && member.UserId == userId &&
            member.IsActive && (member.Role == FarmRole.Owner || member.Role == FarmRole.Administrator), ct);

    private string? CurrentUserId() => User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

    private static WorkTaskResponse ToResponse(WorkTask task) => new(task.Id, task.FarmId, task.AssignedWorkerUserId,
        task.DueDate, task.Description, task.Status.ToString(), task.PhotoRequired, task.WageAmount, task.WageUnit,
        task.ParcelId, task.CropCycleId, task.OperationKind);
}

public sealed record CreateWorkTaskRequest(
    [Required, StringLength(450)] string AssignedWorkerUserId,
    DateOnly DueDate,
    [Required, StringLength(1000)] string Description,
    bool PhotoRequired,
    [Range(0, 1000000000)] decimal? WageAmount,
    [StringLength(64)] string? WageUnit,
    Guid? ParcelId,
    Guid? CropCycleId,
    [StringLength(64)] string? OperationKind);

public sealed record ReassignWorkTaskRequest([Required, StringLength(450)] string AssignedWorkerUserId);

public sealed record WorkTaskResponse(Guid Id, Guid FarmId, string AssignedWorkerUserId, DateOnly DueDate,
    string Description, string Status, bool PhotoRequired, decimal? WageAmount, string? WageUnit,
    Guid? ParcelId, Guid? CropCycleId, string? OperationKind);
