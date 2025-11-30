using Heytom.MQ.Abstractions;
using Heytom.MQ.TestApi.Events;

namespace Heytom.MQ.TestApi.Handlers;

public class NotificationEventHandler : IEventHandler<NotificationEvent>
{
    private readonly ILogger<NotificationEventHandler> _logger;

    public NotificationEventHandler(ILogger<NotificationEventHandler> logger)
    {
        _logger = logger;
    }

    public async Task<bool> HandleAsync(NotificationEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[Kafka] 处理通知事件 - EventId: {EventId}, UserId: {UserId}, Type: {Type}, Title: {Title}, Message: {Message}, OccurredAt: {OccurredAt}",
            @event.EventId,
            @event.UserId,
            @event.Type,
            @event.Title,
            @event.Message,
            @event.OccurredAt);

        // 模拟发送通知
        await Task.Delay(100, cancellationToken);

        _logger.LogInformation("[Kafka] 通知事件处理完成 - UserId: {UserId}", @event.UserId);

        return true;
    }
}
