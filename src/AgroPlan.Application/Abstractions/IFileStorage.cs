namespace AgroPlan.Application.Abstractions;

public sealed record UploadAuthorization(string ObjectKey, string UploadUrl, DateTimeOffset ExpiresAt, string RequiredContentType);
public sealed record StoredObjectMetadata(long SizeBytes, string ContentType);

public interface IFileStorage
{
    UploadAuthorization CreateUploadAuthorization(Guid farmId, Guid entryId, Guid attachmentId, string contentType, DateTimeOffset expiresAt);
    UploadAuthorization CreateDownloadAuthorization(Guid farmId, Guid entryId, Guid attachmentId, DateTimeOffset expiresAt);
    Task<StoredObjectMetadata?> GetMetadataAsync(string objectKey, CancellationToken cancellationToken);
}
