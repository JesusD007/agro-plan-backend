extern alias AgroApi;

using ApiIdentity = AgroApi::AgroPlan.Api.Identity.TokenIssuer;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using AgroPlan.Application.Abstractions;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Logbook;
using AgroPlan.Domain.Crops;
using AgroPlan.Infrastructure.Identity;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace AgroPlan.IntegrationTests;

internal static class TestSettings
{
    public const string SigningKey = "integration-test-signing-key-only-not-a-secret-0000000000000000";
    public const string Issuer = "AgroPlan.Api";

    public static string AccessToken(string userId, params string[] roles) =>
        new ApiIdentity(Encoding.UTF8.GetBytes(SigningKey), Issuer, ApiIdentity.Audience)
            .Issue(new ApplicationUser { Id = userId, Email = $"{userId}@example.test" }, DateTimeOffset.UtcNow, roles);
}

internal sealed class ApiTestFactory : WebApplicationFactory<AgroApi::Program>
{
    private readonly string _connectionString;
    private readonly string _environment;
    private readonly string? _rabbitMqUrl;
    private readonly bool _disableBackgroundServices;
    private readonly Dictionary<string, string?> _previousEnvironment = new(StringComparer.Ordinal);

    public ApiTestFactory(
        string connectionString,
        string environment = "Development",
        string? rabbitMqUrl = null,
        bool disableBackgroundServices = false)
    {
        _connectionString = connectionString;
        _environment = environment;
        _rabbitMqUrl = rabbitMqUrl;
        _disableBackgroundServices = disableBackgroundServices;

        // Minimal hosting reads its configuration before WebApplicationFactory's
        // ConfigureAppConfiguration callback runs. Set these early-read values
        // in the process environment; integration tests are serialized below.
        SetEnvironmentVariable("ConnectionStrings__AgroPlan", connectionString);
        SetEnvironmentVariable("Jwt__Issuer", TestSettings.Issuer);
        SetEnvironmentVariable("Jwt__Audience", ApiIdentity.Audience);
        SetEnvironmentVariable("Jwt__SigningKey", TestSettings.SigningKey);
        SetEnvironmentVariable("Recovery__CodeLifetimeMinutes", "15");
        SetEnvironmentVariable("Recovery__PasswordChangeTokenLifetimeMinutes", "15");
        SetEnvironmentVariable("RABBITMQ_URL", rabbitMqUrl);
    }

    public RecordingEmailSender Email { get; } = new();
    public RecordingFileStorage Storage { get; } = new();

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        foreach (var (name, value) in _previousEnvironment)
            Environment.SetEnvironmentVariable(name, value);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:AgroPlan"] = _connectionString,
                ["Jwt:Issuer"] = TestSettings.Issuer,
                ["Jwt:Audience"] = ApiIdentity.Audience,
                ["Jwt:SigningKey"] = TestSettings.SigningKey,
                ["Recovery:CodeLifetimeMinutes"] = "15",
                ["Recovery:PasswordChangeTokenLifetimeMinutes"] = "15",
                ["WorkerInvitations:AcceptanceUrl"] = "https://client.example.test/accept-worker-invitation",
                ["RABBITMQ_URL"] = _rabbitMqUrl
            }));

        builder.ConfigureTestServices(services =>
        {
            if (_disableBackgroundServices)
            {
                var backgroundServices = services.Where(descriptor =>
                    descriptor.ServiceType == typeof(IHostedService) &&
                    descriptor.ImplementationType is { } implementationType &&
                    (implementationType == typeof(AgroApi::AgroPlan.Api.Services.JobStatusNotificationService) ||
                     implementationType == typeof(AgroApi::AgroPlan.Api.Services.TaskScheduleNotificationConsumer) ||
                     implementationType == typeof(AgroPlan.Infrastructure.Messaging.OutboxPublisher))).ToArray();
                foreach (var descriptor in backgroundServices)
                    services.Remove(descriptor);
            }
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Email);
            services.RemoveAll<IFileStorage>();
            services.AddSingleton<IFileStorage>(Storage);
        });
    }

    private void SetEnvironmentVariable(string name, string? value)
    {
        if (!_previousEnvironment.ContainsKey(name))
            _previousEnvironment[name] = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }
}

internal sealed class RecordingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<SentEmail> _messages = new();
    public IReadOnlyList<SentEmail> Messages => _messages.ToArray();

    public Task SendAsync(string recipient, string subject, string textBody, CancellationToken cancellationToken)
    {
        _messages.Enqueue(new SentEmail(recipient, subject, textBody));
        return Task.CompletedTask;
    }
}

internal sealed record SentEmail(string Recipient, string Subject, string TextBody);

internal sealed class RecordingFileStorage : IFileStorage
{
    private const long FileSize = 512;
    private static readonly byte[] VoiceWave = CreateWave();
    private static readonly byte[] ImageBytes = CreatePng();
    private readonly ConcurrentQueue<string> _deletedObjectKeys = new();
    public IReadOnlyList<string> DeletedObjectKeys => _deletedObjectKeys.ToArray();

    public UploadAuthorization CreateUploadAuthorization(Guid farmId, Guid entryId, Guid attachmentId,
        string contentType, DateTimeOffset expiresAt) => new(
        $"farms/{farmId:D}/logbook/{entryId:D}/{attachmentId:D}/staging",
        $"https://uploads.invalid/{attachmentId:D}", expiresAt, contentType);

    public UploadAuthorization CreateTaskEvidenceUploadAuthorization(Guid farmId, Guid taskId, Guid submissionId,
        string contentType, DateTimeOffset expiresAt) => new(
        $"farms/{farmId:D}/work-tasks/{taskId:D}/evidence/{submissionId:D}/staging",
        $"https://uploads.invalid/{submissionId:D}", expiresAt, contentType);

    public UploadAuthorization CreateVoiceDraftUploadAuthorization(Guid farmId, Guid draftId, string contentType,
        DateTimeOffset expiresAt) => new(
        $"farms/{farmId:D}/voice-drafts/{draftId:D}/staging",
        $"https://uploads.invalid/{draftId:D}", expiresAt, contentType);

    public UploadAuthorization CreateSymptomReportUploadAuthorization(Guid farmId, Guid reportId, Guid photoId,
        string contentType, DateTimeOffset expiresAt) => new(
        $"farms/{farmId:D}/symptom-reports/{reportId:D}/{photoId:D}/staging",
        $"https://uploads.invalid/{photoId:D}", expiresAt, contentType);

    public UploadAuthorization CreateDownloadAuthorization(string objectKey, DateTimeOffset expiresAt) => new(
        objectKey, $"https://downloads.invalid/{Uri.EscapeDataString(objectKey)}", expiresAt, "image/png");

    public Task<StoredObjectMetadata?> GetMetadataAsync(string objectKey, CancellationToken cancellationToken) =>
        Task.FromResult<StoredObjectMetadata?>(objectKey.Contains("/voice-drafts/", StringComparison.Ordinal)
            ? new StoredObjectMetadata(VoiceWave.Length, "audio/wav")
            : new StoredObjectMetadata(FileSize, "image/png"));

    public Task<byte[]?> ReadObjectAsync(string objectKey, long maximumSizeBytes, CancellationToken cancellationToken) =>
        Task.FromResult<byte[]?>(objectKey.Contains("/voice-drafts/", StringComparison.Ordinal)
            ? VoiceWave.Length <= maximumSizeBytes ? [.. VoiceWave] : null
            : ImageBytes.Length <= maximumSizeBytes ? [.. ImageBytes] : null);

    public Task<FinalizedStoredObject?> FinalizeUploadAsync(string stagingObjectKey, string finalObjectKey,
        CancellationToken cancellationToken)
    {
        var voice = stagingObjectKey.Contains("/voice-drafts/", StringComparison.Ordinal);
        return Task.FromResult<FinalizedStoredObject?>(new FinalizedStoredObject(finalObjectKey,
            voice ? new StoredObjectMetadata(VoiceWave.Length, "audio/wav") : new StoredObjectMetadata(FileSize, "image/png")));
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        _deletedObjectKeys.Enqueue(objectKey);
        return Task.CompletedTask;
    }

    private static byte[] CreateWave()
    {
        const int dataLength = 32000;
        var bytes = new byte[44 + dataLength];
        System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(bytes, 0);
        BitConverter.GetBytes(bytes.Length - 8).CopyTo(bytes, 4);
        System.Text.Encoding.ASCII.GetBytes("WAVEfmt ").CopyTo(bytes, 8);
        BitConverter.GetBytes(16).CopyTo(bytes, 16);
        BitConverter.GetBytes((short)1).CopyTo(bytes, 20);
        BitConverter.GetBytes((short)1).CopyTo(bytes, 22);
        BitConverter.GetBytes(16000).CopyTo(bytes, 24);
        BitConverter.GetBytes(32000).CopyTo(bytes, 28);
        BitConverter.GetBytes((short)2).CopyTo(bytes, 32);
        BitConverter.GetBytes((short)16).CopyTo(bytes, 34);
        System.Text.Encoding.ASCII.GetBytes("data").CopyTo(bytes, 36);
        BitConverter.GetBytes(dataLength).CopyTo(bytes, 40);
        return bytes;
    }

    private static byte[] CreatePng()
    {
        var bytes = new byte[FileSize];
        byte[] header = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82,
            0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0];
        header.CopyTo(bytes, 0);
        return bytes;
    }
}

internal static class DockerAvailability
{
    public static async Task<string?> GetUnavailableReasonAsync()
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add("version");
        process.StartInfo.ArgumentList.Add("--format");
        process.StartInfo.ArgumentList.Add("{{.Server.Version}}");

        try
        {
            if (!process.Start()) return "Docker CLI could not be started.";
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return "Docker CLI is not installed; Testcontainers integration tests need Docker Desktop or another Docker-compatible engine.";
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            return process.ExitCode == 0
                ? null
                : "Docker CLI cannot reach a running Docker-compatible container engine.";
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            return "Docker CLI did not respond within five seconds; container-backed tests cannot run here.";
        }
    }
}

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("agroplan_integration")
        .WithUsername("agroplan_test")
        .WithPassword("agroplan_test")
        .Build();

    public string? SkipReason { get; private set; }
    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        var dockerUnavailable = await DockerAvailability.GetUnavailableReasonAsync();
        if (dockerUnavailable is not null)
        {
            SkipReason = dockerUnavailable;
            return;
        }
        try
        {
            await _container.StartAsync();
        }
        catch (Exception exception) when (IsDockerUnavailable(exception))
        {
            SkipReason = "Integration tests require a reachable Docker-compatible container engine. " + exception.Message;
            return;
        }

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public AgroPlanDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AgroPlanDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new AgroPlanDbContext(options);
    }

    public void EnsureAvailable()
    {
        if (SkipReason is not null) Assert.Skip(SkipReason);
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    internal static bool IsDockerUnavailable(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current.GetType().Name == "DockerUnavailableException") return true;
        }
        return false;
    }
}

public sealed class PostgreSqlRabbitFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("agroplan_worker_integration")
        .WithUsername("agroplan_test")
        .WithPassword("agroplan_test")
        .Build();
    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:4-alpine")
        .WithUsername("agroplan_test")
        .WithPassword("agroplan_test")
        .Build();

    public string? SkipReason { get; private set; }
    public string ConnectionString => _postgres.GetConnectionString();
    public string RabbitMqUrl => _rabbit.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        var dockerUnavailable = await DockerAvailability.GetUnavailableReasonAsync();
        if (dockerUnavailable is not null)
        {
            SkipReason = dockerUnavailable;
            return;
        }
        try
        {
            await _postgres.StartAsync();
            await _rabbit.StartAsync();
        }
        catch (Exception exception) when (PostgreSqlFixture.IsDockerUnavailable(exception))
        {
            SkipReason = "Worker integration tests require a reachable Docker-compatible container engine. " + exception.Message;
            return;
        }

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public AgroPlanDbContext CreateDbContext() => new(new DbContextOptionsBuilder<AgroPlanDbContext>()
        .UseNpgsql(ConnectionString)
        .Options);

    public void EnsureAvailable()
    {
        if (SkipReason is not null) Assert.Skip(SkipReason);
    }

    public async ValueTask DisposeAsync()
    {
        await _rabbit.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "PostgreSQL integration";
}

[CollectionDefinition(Name)]
public sealed class PostgreSqlRabbitCollection : ICollectionFixture<PostgreSqlRabbitFixture>
{
    public const string Name = "PostgreSQL and RabbitMQ integration";
}

internal static class TestData
{
    public static async Task<(Guid FarmId, Guid ParcelId)> SeedFarmAsync(
        AgroPlanDbContext db,
        string ownerId,
        string? additionalUserId = null,
        bool withParcel = true)
    {
        var now = DateTimeOffset.UtcNow;
        var farmId = Guid.NewGuid();
        var farm = new Farm(farmId, $"Farm-{farmId:N}"[..13], "Test location", now);
        var owner = new ApplicationUser
        {
            Id = ownerId,
            UserName = $"{ownerId}@example.test",
            NormalizedUserName = $"{ownerId}@EXAMPLE.TEST",
            Email = $"{ownerId}@example.test",
            NormalizedEmail = $"{ownerId}@EXAMPLE.TEST",
            EmailConfirmed = true
        };
        db.Users.Add(owner);
        db.Farms.Add(farm);
        db.FarmMemberships.Add(new FarmMembership(farmId, ownerId, FarmRole.Owner, now));
        if (!string.IsNullOrWhiteSpace(additionalUserId))
        {
            db.Users.Add(new ApplicationUser
            {
                Id = additionalUserId,
                UserName = $"{additionalUserId}@example.test",
                NormalizedUserName = $"{additionalUserId}@EXAMPLE.TEST",
                Email = $"{additionalUserId}@example.test",
                NormalizedEmail = $"{additionalUserId}@EXAMPLE.TEST",
                EmailConfirmed = true
            });
            db.FarmMemberships.Add(new FarmMembership(farmId, additionalUserId, FarmRole.Administrator, now));
        }

        var parcelId = Guid.NewGuid();
        if (withParcel)
            db.Parcels.Add(new Parcel(parcelId, farmId, "Parcel A", "Test location", 1.5m, now));
        else
            parcelId = Guid.Empty;

        await db.SaveChangesAsync();
        return (farmId, parcelId);
    }

    public static void SetBearer(HttpClient client, string userId, params string[] roles) =>
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", TestSettings.AccessToken(userId, roles));
}
