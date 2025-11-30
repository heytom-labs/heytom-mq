using Heytom.MQ.Abstractions;
using Heytom.MQ.TestApi.Events;

namespace Heytom.MQ.TestApi.Handlers;

public class UserCreatedEventHandler : IEventHandler<UserCreatedEvent>
{
    private readonly ILogger<UserCreatedEventHandler> _logger;

    public UserCreatedEventHandler(ILogger<UserCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    public async Task<bool> HandleAsync(UserCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[RabbitMQ] 处理用户创建事件 - EventId: {EventId}, UserId: {UserId}, Name: {Name}, Email: {Email}, OccurredAt: {OccurredAt}",
            @event.EventId,
            @event.UserId,
            @event.Name,
            @event.Email,
            @event.OccurredAt);

        // 模拟业务处理
        await Task.Delay(100, cancellationToken);

        _logger.LogInformation("[RabbitMQ] 用户创建事件处理完成 - UserId: {UserId}", @event.UserId);

        return true; // 返回 true 表示处理成功
    }
}
