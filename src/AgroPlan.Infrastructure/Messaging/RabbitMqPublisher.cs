using System.Text;
using AgroPlan.Domain.Planning;
using RabbitMQ.Client;

namespace AgroPlan.Infrastructure.Messaging;

public sealed class RabbitMqPublisher(RabbitMqConnection connection)
{
    public const string QueueName = "agroplan.sowing-plans.v1";
    public const string PlantingFramesQueueName = "agroplan.planting-frames.v1";

    public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var brokerConnection = await connection.GetAsync(cancellationToken);
        var options = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);
        await using var channel = await brokerConnection.CreateChannelAsync(options, cancellationToken);
        var queueName = message.EventType == "planting-frame.requested" ? PlantingFramesQueueName : QueueName;
        await channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: cancellationToken);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = message.Id.ToString("N"),
            Type = message.EventType,
            CorrelationId = message.AggregateId.ToString("N")
        };
        var body = Encoding.UTF8.GetBytes(message.PayloadJson);
        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }

    public static Task EnsureQueueAsync(IChannel channel, CancellationToken cancellationToken) =>
        channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

    public static Task EnsurePlantingFramesQueueAsync(IChannel channel, CancellationToken cancellationToken) =>
        channel.QueueDeclareAsync(PlantingFramesQueueName, durable: true, exclusive: false, autoDelete: false,
            arguments: null, cancellationToken: cancellationToken);
}
