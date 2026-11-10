using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgroPlan.Api.Authorization;
using AgroPlan.Application.Workforce;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Workforce;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/farms/{farmId:guid}/work-sync")]
public sealed class WorkSyncController(AgroPlanDbContext db, AssignedWorkerAuthorizationService authorization,
    TaskEvidenceFinalizationService evidenceFinalization) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<WorkSyncBatchResponse>> Sync(Guid farmId, WorkSyncBatchRequest request, CancellationToken ct)
    {
        var workerId = CurrentUserId();
        if (workerId is null) return Unauthorized();
        var role = await authorization.GetActiveRoleAsync(farmId, workerId, ct);
        if (role != FarmRole.Worker) return Forbid();
        if (!ModelState.IsValid || request.Operations.Count is < 1 or > 100) return ValidationProblem(ModelState);

        var outcomes = new List<WorkSyncOperationResponse>(request.Operations.Count);
        foreach (var operation in request.Operations)
        {
            try
            {
                outcomes.Add(await ApplyAsync(farmId, workerId, operation, ct));
            }
            catch (InvalidOperationException exception)
            {
                outcomes.Add(new(operation.OperationId, "Rejected", null, exception.Message, false));
            }
            catch (DbUpdateException)
            {
                outcomes.Add(new(operation.OperationId, "Retryable", null, "The operation could not be committed; retry with the same operation id.", true));
            }
        }
        return Ok(new WorkSyncBatchResponse(outcomes));
    }

    private async Task<WorkSyncOperationResponse> ApplyAsync(Guid farmId, string workerId,
        WorkSyncOperationRequest operation, CancellationToken ct)
    {
        if (operation.OperationId == Guid.Empty || operation.TaskId == Guid.Empty || operation.BaseVersion < 1)
            return new(operation.OperationId, "Rejected", null, "Operation, task, and base version are required.", false);

        var fingerprint = Fingerprint(operation);
        var lockMaterial = SHA256.HashData(Encoding.UTF8.GetBytes($"{farmId:N}:{workerId}:{operation.OperationId:N}"));
        var lockKey = BitConverter.ToInt64(lockMaterial, 0);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockKey})", ct);

        var existing = await db.SyncOperationReceipts.SingleOrDefaultAsync(receipt => receipt.FarmId == farmId &&
            receipt.WorkerUserId == workerId && receipt.ClientOperationId == operation.OperationId, ct);
        if (existing is not null)
        {
            await transaction.CommitAsync(ct);
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(existing.PayloadFingerprint), Convert.FromHexString(fingerprint)))
                return new(operation.OperationId, "Conflict", existing.Id, "This operation id was already used with a different payload.", false);
            var result = JsonSerializer.Deserialize<StoredOperationResult>(existing.ResultJson)!;
            return new(operation.OperationId, "Replayed", existing.Id, result.Message, false, result.TaskVersion, result.SubmissionId);
        }

        var workerIsActive = await db.FarmMemberships.AnyAsync(member => member.FarmId == farmId &&
            member.UserId == workerId && member.IsActive && member.Role == FarmRole.Worker, ct);
        if (!workerIsActive)
        {
            await transaction.RollbackAsync(ct);
            return new(operation.OperationId, "Rejected", null, "Active worker membership is required.", false);
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM agro.work_tasks WHERE \"FarmId\" = {farmId} AND \"Id\" = {operation.TaskId} FOR UPDATE", ct);
        var task = await db.WorkTasks.SingleOrDefaultAsync(task => task.FarmId == farmId && task.Id == operation.TaskId &&
            task.AssignedWorkerUserId == workerId, ct);
        if (task is null)
        {
            await transaction.RollbackAsync(ct);
            return new(operation.OperationId, "Rejected", null, "The task is not assigned to this worker.", false);
        }

        Guid? submissionId = null;
        string message;
        switch (operation.Kind.Trim().ToLowerInvariant())
        {
            case "task.start":
                if (task.Status != WorkTaskStatus.Pending)
                {
                    await transaction.RollbackAsync(ct);
                    return new(operation.OperationId, "Rejected", null, "Only pending tasks can be started.", false);
                }
                task.Start();
                message = "Task started.";
                break;
            case "evidence.finalize":
                if (!operation.SubmissionId.HasValue || operation.SubmissionId == Guid.Empty)
                {
                    await transaction.RollbackAsync(ct);
                    return new(operation.OperationId, "Rejected", null, "Evidence submission id is required.", false);
                }
                var finalized = await evidenceFinalization.FinalizeAsync(farmId, task.Id, operation.SubmissionId.Value, workerId, ct);
                if (finalized is null)
                {
                    await transaction.RollbackAsync(ct);
                    return new(operation.OperationId, "Rejected", null, "The staged evidence does not belong to this task and worker.", false);
                }
                submissionId = finalized.Id;
                message = finalized.FinalizedAt is null ? "Evidence is not finalized." : "Evidence finalized.";
                break;
            default:
                await transaction.RollbackAsync(ct);
                return new(operation.OperationId, "Rejected", null, "Unsupported operation kind.", false);
        }

        var receipt = new SyncOperationReceipt(Guid.NewGuid(), farmId, workerId, operation.OperationId,
            fingerprint, "Applied", JsonSerializer.Serialize(new StoredOperationResult(message, 1, submissionId)),
            DateTimeOffset.UtcNow);
        db.SyncOperationReceipts.Add(receipt);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(operation.OperationId, "Applied", receipt.Id, message, false, 1, submissionId);
    }

    private static string Fingerprint(WorkSyncOperationRequest operation)
    {
        var payload = operation.Payload.ValueKind == JsonValueKind.Undefined ? "null" : CanonicalJson(operation.Payload);
        var material = JsonSerializer.Serialize(new
        {
            kind = operation.Kind.Trim().ToLowerInvariant(), operation.TaskId, operation.BaseVersion,
            operation.SubmissionId, payload
        });
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    private static string CanonicalJson(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) WriteCanonical(writer, element);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private string? CurrentUserId() => User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

    private sealed record StoredOperationResult(string Message, int TaskVersion, Guid? SubmissionId);
}

public sealed record WorkSyncBatchRequest([Required, MinLength(1), MaxLength(100)] IReadOnlyList<WorkSyncOperationRequest> Operations);
public sealed record WorkSyncOperationRequest(Guid OperationId, Guid TaskId, [Required, StringLength(48)] string Kind,
    [Range(1, int.MaxValue)] int BaseVersion, JsonElement Payload, Guid? SubmissionId = null);
public sealed record WorkSyncBatchResponse(IReadOnlyList<WorkSyncOperationResponse> Operations);
public sealed record WorkSyncOperationResponse(Guid OperationId, string Status, Guid? ReceiptId, string Message,
    bool Retryable, int? TaskVersion = null, Guid? SubmissionId = null);
