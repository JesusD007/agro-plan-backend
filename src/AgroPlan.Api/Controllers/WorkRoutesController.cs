using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AgroPlan.Api.Authorization;
using AgroPlan.Application.Abstractions;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Workforce;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgroPlan.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/farms/{farmId:guid}/work-routes")]
public sealed class WorkRoutesController(
    AgroPlanDbContext db,
    AssignedWorkerAuthorizationService authorization,
    IWorkRouteProvider routeProvider,
    ILogger<WorkRoutesController> logger) : ControllerBase
{
    [HttpPost("generate")]
    public async Task<IActionResult> Generate(Guid farmId, GenerateWorkRouteRequest request, CancellationToken ct)
    {
        var requesterId = CurrentUserId();
        if (requesterId is null) return Unauthorized();
        var role = await authorization.GetActiveRoleAsync(farmId, requesterId, ct);
        if (role is not (FarmRole.Owner or FarmRole.Administrator)) return Forbid();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (request.TaskIds.Distinct().Count() != request.TaskIds.Length)
            return BadRequest(new { code = "duplicate_task", message = "Route task ids must be unique." });
        if (!await db.FarmMemberships.AsNoTracking().AnyAsync(member => member.FarmId == farmId &&
            member.UserId == request.WorkerUserId && member.Role == FarmRole.Worker && member.IsActive, ct))
            return BadRequest(new { code = "invalid_worker", message = "The worker must be an active member of this farm." });

        var tasks = await db.WorkTasks.AsNoTracking().Where(task => task.FarmId == farmId &&
            task.AssignedWorkerUserId == request.WorkerUserId && request.TaskIds.Contains(task.Id)).ToArrayAsync(ct);
        if (tasks.Length != request.TaskIds.Length || tasks.Any(task => task.Status is WorkTaskStatus.Verified or WorkTaskStatus.Rejected))
            return BadRequest(new { code = "invalid_tasks", message = "Every route task must be active and assigned to the selected worker in this farm." });

        var parcelIds = tasks.Where(task => task.ParcelId.HasValue).Select(task => task.ParcelId!.Value).Distinct().ToArray();
        var parcels = parcelIds.Length == 0 ? new Dictionary<Guid, AgroPlan.Domain.Farms.Parcel>() :
            await db.Parcels.AsNoTracking().Where(parcel => parcel.FarmId == farmId && parcelIds.Contains(parcel.Id))
                .ToDictionaryAsync(parcel => parcel.Id, ct);
        var stops = tasks.Select(task => new WorkRouteStop(task.Id, task.ParcelId, null, null)).ToArray();
        if (stops.Any(stop => !stop.Latitude.HasValue || !stop.Longitude.HasValue))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "unavailable",
                reason = "Validated coordinates are not available for every assigned route stop.",
                missingInputs = new[] { "parcel.coordinates" }
            });
        var providerRequest = new WorkRouteRequest(farmId, stops);
        WorkRouteProposal proposal;
        try
        {
            proposal = await routeProvider.GenerateAsync(providerRequest, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Work route provider failed for farm {FarmId}; returning unavailable.", farmId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "unavailable",
                reason = "The route provider could not generate a route."
            });
        }
        if (!proposal.Available)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "unavailable",
                reason = proposal.UnavailableReason ?? "No approved route provider is available."
            });
        if (proposal.OrderedTaskIds.Count != tasks.Length ||
            proposal.OrderedTaskIds.Distinct().Count() != tasks.Length ||
            !request.TaskIds.ToHashSet().SetEquals(proposal.OrderedTaskIds))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "unavailable",
                reason = "The route provider returned an invalid stop sequence."
            });

        var routeId = request.RouteId ?? Guid.NewGuid();
        var latest = await db.WorkRoutes.AsNoTracking().Where(route => route.FarmId == farmId && route.RouteId == routeId)
            .OrderByDescending(route => route.Version).FirstOrDefaultAsync(ct);
        if (request.RouteId.HasValue && (latest is null || latest.WorkerUserId != request.WorkerUserId)) return NotFound();
        var sourceVersions = tasks.Select(task => new WorkRouteTaskSource(task.Id, task.Version, task.ParcelId,
            task.ParcelId.HasValue && parcels.TryGetValue(task.ParcelId.Value, out var parcel) ? parcel.UpdatedAt : null)).ToArray();
        WorkRoute route;
        try
        {
            route = new WorkRoute(Guid.NewGuid(), routeId, farmId, request.WorkerUserId,
                (latest?.Version ?? 0) + 1, proposal.OrderedTaskIds, sourceVersions, proposal.GeometryGeoJson, DateTimeOffset.UtcNow);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { code = "invalid_provider_output", message = exception.Message });
        }
        db.WorkRoutes.Add(route);
        await db.SaveChangesAsync(ct);
        return AcceptedAtAction(nameof(DownloadCurrent), new { farmId, routeId }, ToResponse(route));
    }

    [HttpPost("{routeId:guid}/versions/{version:int}/confirm")]
    public async Task<IActionResult> Confirm(Guid farmId, Guid routeId, int version, CancellationToken ct)
    {
        var requesterId = CurrentUserId();
        if (requesterId is null) return Unauthorized();
        var role = await authorization.GetActiveRoleAsync(farmId, requesterId, ct);
        if (role is not (FarmRole.Owner or FarmRole.Administrator)) return Forbid();
        var route = await db.WorkRoutes.SingleOrDefaultAsync(item => item.FarmId == farmId &&
            item.RouteId == routeId && item.Version == version, ct);
        if (route is null) return NotFound();
        var newest = await db.WorkRoutes.Where(item => item.FarmId == farmId && item.RouteId == routeId)
            .MaxAsync(item => item.Version, ct);
        if (version != newest || route.Status != WorkRouteStatus.Proposed)
            return Conflict(new { code = "route_not_proposed", message = "Only the latest proposed route version can be confirmed." });
        if (!await IsSourceCurrent(route, ct))
        {
            route.MarkStale(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(ct);
            return Conflict(new { code = "route_stale", message = "The task or parcel source changed; generate and review a new route version." });
        }
        route.Confirm(requesterId, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);
        return Ok(ToResponse(route));
    }

    [HttpGet("{routeId:guid}/current")]
    public async Task<IActionResult> DownloadCurrent(Guid farmId, Guid routeId, CancellationToken ct)
    {
        var requesterId = CurrentUserId();
        if (requesterId is null) return Unauthorized();
        var role = await authorization.GetActiveRoleAsync(farmId, requesterId, ct);
        if (role != FarmRole.Worker) return Forbid();
        var route = await db.WorkRoutes.Where(item => item.FarmId == farmId && item.RouteId == routeId &&
            item.WorkerUserId == requesterId &&
            (item.Status == WorkRouteStatus.Confirmed || item.Status == WorkRouteStatus.Stale))
            .OrderByDescending(item => item.Version).FirstOrDefaultAsync(ct);
        if (route is null) return NotFound();
        if (route.Status == WorkRouteStatus.Stale)
            return Conflict(new { code = "route_stale", message = "This route is stale and is not available for download." });
        if (!await IsSourceCurrent(route, ct))
        {
            route.MarkStale(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(ct);
            return Conflict(new { code = "route_stale", message = "An assigned task or parcel changed; this route is no longer current." });
        }
        return Ok(ToResponse(route));
    }

    private async Task<bool> IsSourceCurrent(WorkRoute route, CancellationToken ct)
    {
        var sources = route.GetTaskSources();
        var taskIds = sources.Select(source => source.TaskId).ToArray();
        var tasks = await db.WorkTasks.AsNoTracking().Where(task => task.FarmId == route.FarmId &&
            task.AssignedWorkerUserId == route.WorkerUserId && taskIds.Contains(task.Id)).ToDictionaryAsync(task => task.Id, ct);
        if (tasks.Count != sources.Count) return false;
        var parcelIds = sources.Where(source => source.ParcelId.HasValue).Select(source => source.ParcelId!.Value).Distinct().ToArray();
        var parcels = parcelIds.Length == 0 ? new Dictionary<Guid, DateTimeOffset>() : await db.Parcels.AsNoTracking()
            .Where(parcel => parcel.FarmId == route.FarmId && parcelIds.Contains(parcel.Id))
            .ToDictionaryAsync(parcel => parcel.Id, parcel => parcel.UpdatedAt, ct);
        return sources.All(source => tasks[source.TaskId].Version == source.TaskVersion &&
            (!source.ParcelId.HasValue || parcels.TryGetValue(source.ParcelId.Value, out var updatedAt) &&
                updatedAt == source.ParcelUpdatedAt));
    }

    private string? CurrentUserId() => User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

    private static WorkRouteResponse ToResponse(WorkRoute route) => new(route.RouteId, route.Version, route.Status.ToString(),
        route.GetOrderedTaskIds(), route.GeometryGeoJson, route.ConfirmedAt, route.StaleAt);
}

public sealed record GenerateWorkRouteRequest(
    [Required, StringLength(450)] string WorkerUserId,
    [Required, MinLength(1), MaxLength(50)] Guid[] TaskIds,
    Guid? RouteId = null);

public sealed record WorkRouteResponse(Guid RouteId, int Version, string Status, IReadOnlyList<Guid> OrderedTaskIds,
    string? GeometryGeoJson, DateTimeOffset? ConfirmedAt, DateTimeOffset? StaleAt);
