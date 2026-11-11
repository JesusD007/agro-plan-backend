namespace AgroPlan.Application.Abstractions;

public sealed record UploadAuthorization(string ObjectKey, string UploadUrl, DateTimeOffset ExpiresAt, string RequiredContentType);
public sealed record StoredObjectMetadata(long SizeBytes, string ContentType);
public sealed record FinalizedStoredObject(string ObjectKey, StoredObjectMetadata Metadata);

public interface IFileStorage
{
    UploadAuthorization CreateUploadAuthorization(Guid farmId, Guid entryId, Guid attachmentId, string contentType, DateTimeOffset expiresAt);
    UploadAuthorization CreateTaskEvidenceUploadAuthorization(Guid farmId, Guid taskId, Guid submissionId, string contentType, DateTimeOffset expiresAt);
    UploadAuthorization CreateVoiceDraftUploadAuthorization(Guid farmId, Guid draftId, string contentType, DateTimeOffset expiresAt);
    UploadAuthorization CreateSymptomReportUploadAuthorization(Guid farmId, Guid reportId, Guid photoId, string contentType, DateTimeOffset expiresAt);
    UploadAuthorization CreateDownloadAuthorization(string objectKey, DateTimeOffset expiresAt);
    Task<StoredObjectMetadata?> GetMetadataAsync(string objectKey, CancellationToken cancellationToken);
    Task<byte[]?> ReadObjectAsync(string objectKey, long maximumSizeBytes, CancellationToken cancellationToken);
    Task<FinalizedStoredObject?> FinalizeUploadAsync(string stagingObjectKey, string finalObjectKey, CancellationToken cancellationToken);
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);
}
