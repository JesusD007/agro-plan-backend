using AgroPlan.Application.Abstractions;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;

namespace AgroPlan.Infrastructure.Storage;

public sealed class SupabaseFileStorage(IConfiguration configuration) : IFileStorage, IDisposable
{
    private readonly string _bucket = configuration["SUPABASE_STORAGE_BUCKET"] ?? configuration["Supabase:Storage:Bucket"]
        ?? throw new InvalidOperationException("Configure Supabase:Storage:Bucket.");
    private readonly Lazy<IAmazonS3> _client = new(() =>
    {
        var accessKey = configuration["S3_ACCESS_KEY_ID"];
        var secretKey = configuration["S3_SECRET_ACCESS_KEY"];
        var endpoint = configuration["S3_ENDPOINT"];
        var region = configuration["S3_REGION"];
        if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey) ||
            string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(region))
            throw new InvalidOperationException("Configure server-side S3_ACCESS_KEY_ID, S3_SECRET_ACCESS_KEY, S3_ENDPOINT, and S3_REGION.");
        return new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), new AmazonS3Config
        {
            ServiceURL = endpoint,
            AuthenticationRegion = region,
            ForcePathStyle = true
        });
    });

    public UploadAuthorization CreateUploadAuthorization(Guid farmId, Guid entryId, Guid attachmentId,
        string contentType, DateTimeOffset expiresAt)
    {
        var key = $"farms/{farmId:D}/logbook/{entryId:D}/{attachmentId:D}/staging";
        return CreateUploadAuthorization(key, contentType, expiresAt);
    }

    public UploadAuthorization CreateTaskEvidenceUploadAuthorization(Guid farmId, Guid taskId, Guid submissionId,
        string contentType, DateTimeOffset expiresAt)
    {
        var key = $"farms/{farmId:D}/work-tasks/{taskId:D}/evidence/{submissionId:D}/staging";
        return CreateUploadAuthorization(key, contentType, expiresAt);
    }

    private UploadAuthorization CreateUploadAuthorization(string key, string contentType, DateTimeOffset expiresAt)
    {
        var url = _client.Value.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Expires = expiresAt.UtcDateTime
        });
        return new UploadAuthorization(key, url, expiresAt, contentType);
    }

    public UploadAuthorization CreateDownloadAuthorization(string key, DateTimeOffset expiresAt)
    {
        var url = _client.Value.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = expiresAt.UtcDateTime
        });
        return new UploadAuthorization(key, url, expiresAt, string.Empty);
    }

    public async Task<FinalizedStoredObject?> FinalizeUploadAsync(string stagingObjectKey, string finalObjectKey, CancellationToken cancellationToken)
    {
        var finalMetadata = await GetMetadataAsync(finalObjectKey, cancellationToken);
        if (finalMetadata is not null) return new FinalizedStoredObject(finalObjectKey, finalMetadata);

        var stagedMetadata = await GetMetadataAsync(stagingObjectKey, cancellationToken);
        if (stagedMetadata is null) return null;
        await _client.Value.CopyObjectAsync(new CopyObjectRequest
        {
            SourceBucket = _bucket,
            SourceKey = stagingObjectKey,
            DestinationBucket = _bucket,
            DestinationKey = finalObjectKey,
            MetadataDirective = S3MetadataDirective.REPLACE,
            ContentType = stagedMetadata.ContentType
        }, cancellationToken);

        finalMetadata = await GetMetadataAsync(finalObjectKey, cancellationToken);
        if (finalMetadata is null) return null;
        await _client.Value.DeleteObjectAsync(new DeleteObjectRequest { BucketName = _bucket, Key = stagingObjectKey }, cancellationToken);
        return new FinalizedStoredObject(finalObjectKey, finalMetadata);
    }

    public async Task<StoredObjectMetadata?> GetMetadataAsync(string objectKey, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.Value.GetObjectMetadataAsync(new GetObjectMetadataRequest { BucketName = _bucket, Key = objectKey }, cancellationToken);
            return new StoredObjectMetadata(response.Headers.ContentLength, response.Headers.ContentType);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public void Dispose() { if (_client.IsValueCreated) _client.Value.Dispose(); }
}
