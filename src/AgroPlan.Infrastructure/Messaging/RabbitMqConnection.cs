using RabbitMQ.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgroPlan.Infrastructure.Messaging;

public sealed class RabbitMqConnection(IConfiguration configuration, ILogger<RabbitMqConnection> logger) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(configuration["RABBITMQ_URL"]);

    public async Task<IConnection> GetAsync(CancellationToken cancellationToken)
    {
        var connectionString = configuration["RABBITMQ_URL"];
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Configure RABBITMQ_URL to enable asynchronous proposal processing.");

        if (_connection is { IsOpen: true }) return _connection;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true }) return _connection;
            if (_connection is not null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }

            var factory = new ConnectionFactory
            {
                Uri = new Uri(connectionString, UriKind.Absolute),
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
            };
            _connection = await factory.CreateConnectionAsync("AgroPlan", cancellationToken);
            logger.LogInformation("Connected to the configured RabbitMQ broker.");
            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null) await _connection.DisposeAsync();
        _gate.Dispose();
    }
}
