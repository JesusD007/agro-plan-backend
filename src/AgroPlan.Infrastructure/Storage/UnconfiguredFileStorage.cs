using AgroPlan.Application.Abstractions;

namespace AgroPlan.Infrastructure.Storage;

public sealed class StorageUnavailableException() : InvalidOperationException("Private file storage is not configured.");

/// <summary>Read-only workflows remain usable without optional object storage.</summary>
public sealed class UnconfiguredFileStorage : IFileStorage
{
    private static StorageUnavailableException Unavailable() => new();
    public UploadAuthorization CreateUploadAuthorization(Guid farmId, Guid entryId, Guid attachmentId, string contentType, DateTimeOffset expiresAt) => throw Unavailable();
    public UploadAuthorization CreateTaskEvidenceUploadAuthorization(Guid farmId, Guid taskId, Guid submissionId, string contentType, DateTimeOffset expiresAt) => throw Unavailable();
    public UploadAuthorization CreateVoiceDraftUploadAuthorization(Guid farmId, Guid draftId, string contentType, DateTimeOffset expiresAt) => throw Unavailable();
    public UploadAuthorization CreateSymptomReportUploadAuthorization(Guid farmId, Guid reportId, Guid photoId, string contentType, DateTimeOffset expiresAt) => throw Unavailable();
    public UploadAuthorization CreateDownloadAuthorization(string objectKey, DateTimeOffset expiresAt) => throw Unavailable();
    public Task<StoredObjectMetadata?> GetMetadataAsync(string objectKey, CancellationToken ct) => throw Unavailable();
    public Task<byte[]?> ReadObjectAsync(string objectKey, long maximumSizeBytes, CancellationToken ct) => throw Unavailable();
    public Task<FinalizedStoredObject?> FinalizeUploadAsync(string stagingObjectKey, string finalObjectKey, CancellationToken ct) => throw Unavailable();
    public Task DeleteAsync(string objectKey, CancellationToken ct) => throw Unavailable();
}
