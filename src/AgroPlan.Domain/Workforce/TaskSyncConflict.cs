namespace AgroPlan.Domain.Workforce;

public sealed class TaskSyncConflict
{
    private TaskSyncConflict() { }

    public TaskSyncConflict(Guid id, Guid farmId, Guid taskId, string workerUserId, Guid clientOperationId,
        int baseVersion, string clientValuesJson, int serverVersion, string serverValuesJson, DateTimeOffset detectedAt)
    {
        if (id == Guid.Empty || farmId == Guid.Empty || taskId == Guid.Empty || clientOperationId == Guid.Empty)
            throw new ArgumentException("Conflict, farm, task, and operation ids are required.");
        if (string.IsNullOrWhiteSpace(workerUserId) || workerUserId.Length > 450)
            throw new ArgumentException("Worker is required.", nameof(workerUserId));
        if (baseVersion < 1 || serverVersion <= baseVersion)
            throw new ArgumentOutOfRangeException(nameof(serverVersion), "Conflict versions must retain an older client base and a newer server version.");
        if (string.IsNullOrWhiteSpace(clientValuesJson) || string.IsNullOrWhiteSpace(serverValuesJson) || detectedAt == default)
            throw new ArgumentException("Both task versions and the detection time are required.");
        Id = id; FarmId = farmId; WorkTaskId = taskId; WorkerUserId = workerUserId.Trim();
        ClientOperationId = clientOperationId; BaseVersion = baseVersion; ClientValuesJson = clientValuesJson;
        ServerVersion = serverVersion; ServerValuesJson = serverValuesJson; DetectedAt = detectedAt;
    }

    public Guid Id { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid WorkTaskId { get; private set; }
    public string WorkerUserId { get; private set; } = string.Empty;
    public Guid ClientOperationId { get; private set; }
    public int BaseVersion { get; private set; }
    public string ClientValuesJson { get; private set; } = string.Empty;
    public int ServerVersion { get; private set; }
    public string ServerValuesJson { get; private set; } = string.Empty;
    public DateTimeOffset DetectedAt { get; private set; }
}

public sealed class TaskSyncConflictResolution
{
    private TaskSyncConflictResolution() { }

    public TaskSyncConflictResolution(Guid id, Guid farmId, Guid conflictId, string decidedByUserId,
        string resolution, string selectedValuesJson, int resultTaskVersion, DateTimeOffset decidedAt)
    {
        if (id == Guid.Empty || farmId == Guid.Empty || conflictId == Guid.Empty)
            throw new ArgumentException("Resolution, farm, and conflict ids are required.");
        if (string.IsNullOrWhiteSpace(decidedByUserId) || decidedByUserId.Length > 450)
            throw new ArgumentException("Resolution actor is required.", nameof(decidedByUserId));
        if (resolution is not ("Server" or "Client" or "Edited")) throw new ArgumentException("Unsupported conflict resolution.", nameof(resolution));
        if (string.IsNullOrWhiteSpace(selectedValuesJson) || resultTaskVersion < 1 || decidedAt == default)
            throw new ArgumentException("Resolution values, resulting version, and timestamp are required.");
        Id = id; FarmId = farmId; ConflictId = conflictId; DecidedByUserId = decidedByUserId.Trim();
        Resolution = resolution; SelectedValuesJson = selectedValuesJson; ResultTaskVersion = resultTaskVersion; DecidedAt = decidedAt;
    }

    public Guid Id { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid ConflictId { get; private set; }
    public string DecidedByUserId { get; private set; } = string.Empty;
    public string Resolution { get; private set; } = string.Empty;
    public string SelectedValuesJson { get; private set; } = string.Empty;
    public int ResultTaskVersion { get; private set; }
    public DateTimeOffset DecidedAt { get; private set; }
}
