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
        var key = $"farms/{farmId:D}/logbook/{entryId:D}/{attachmentId:D}";
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

    public UploadAuthorization CreateDownloadAuthorization(Guid farmId, Guid entryId, Guid attachmentId, DateTimeOffset expiresAt)
    {
        var key = $"farms/{farmId:D}/logbook/{entryId:D}/{attachmentId:D}";
        var url = _client.Value.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = expiresAt.UtcDateTime
        });
        return new UploadAuthorization(key, url, expiresAt, string.Empty);
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
