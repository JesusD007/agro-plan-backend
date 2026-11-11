using System.Text;
using AgroPlan.Domain.Planning;
using RabbitMQ.Client;

namespace AgroPlan.Infrastructure.Messaging;

public sealed class RabbitMqPublisher(RabbitMqConnection connection)
{
    public const string QueueName = "agroplan.sowing-plans.v1";
    public const string PlantingFramesQueueName = "agroplan.planting-frames.v1";
    public const string ScheduleChangesQueueName = "agroplan.schedule-changes.v1";
    public const string VerifiedWorkQueueName = "agroplan.verified-work.v1";
    public const string ConfirmedRecordsQueueName = "agroplan.confirmed-records.v1";
    public const string TaskCalendarQueueName = "agroplan.task-calendar-sync.v1";
    public const string TaskScheduleNotificationsQueueName = "agroplan.task-schedule-notifications.v1";
    public const string VoiceDraftsQueueName = "agroplan.voice-drafts.v1";
    public const string WeeklySummaryNarrativesQueueName = "agroplan.weekly-summary-narratives.v1";
    public const string SymptomObservationsQueueName = "agroplan.symptom-observations.v1";

    public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var brokerConnection = await connection.GetAsync(cancellationToken);
        var options = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);
        await using var channel = await brokerConnection.CreateChannelAsync(options, cancellationToken);
        var queueName = message.EventType switch
        {
            "planting-frame.requested" => PlantingFramesQueueName,
            "schedule-change.requested" => ScheduleChangesQueueName,
            "work-task.verified" => VerifiedWorkQueueName,
            "farm-record.confirmed" => ConfirmedRecordsQueueName,
            "task-calendar.sync" => TaskCalendarQueueName,
            "task-schedule.changed" => TaskScheduleNotificationsQueueName,
            "voice-draft.requested" => VoiceDraftsQueueName,
            "weekly-summary.narrative.requested" => WeeklySummaryNarrativesQueueName,
            "symptom-observation.requested" => SymptomObservationsQueueName,
            _ => QueueName
        };
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

    public static Task EnsureScheduleChangesQueueAsync(IChannel channel, CancellationToken cancellationToken) =>
        channel.QueueDeclareAsync(ScheduleChangesQueueName, durable: true, exclusive: false, autoDelete: false,
            arguments: null, cancellationToken: cancellationToken);

    public static Task EnsureVerifiedWorkQueueAsync(IChannel channel, CancellationToken cancellationToken) =>
        channel.QueueDeclareAsync(VerifiedWorkQueueName, durable: true, exclusive: false, autoDelete: false,
            arguments: null, cancellationToken: cancellationToken);

    public static Task EnsureConfirmedRecordsQueueAsync(IChannel channel, CancellationToken cancellationToken) =>
        channel.QueueDeclareAsync(ConfirmedRecordsQueueName, durable: true, exclusive: false, autoDelete: false,
            arguments: null, cancellationToken: cancellationToken);

    public static Task EnsureTaskCalendarQueueAsync(IChannel channel, CancellationToken cancellationToken) =>
        channel.QueueDeclareAsync(TaskCalendarQueueName, durable: true, exclusive: false, autoDelete: false,
            arguments: null, cancellationToken: cancellationToken);

    public static Task EnsureVoiceDraftsQueueAsync(IChannel channel, CancellationToken cancellationToken) =>
        channel.QueueDeclareAsync(VoiceDraftsQueueName, durable: true, exclusive: false, autoDelete: false,
            arguments: null, cancellationToken: cancellationToken);
}
