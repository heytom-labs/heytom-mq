using Heytom.MQ.Abstractions;
using Heytom.MQ.TestApi.Events;

namespace Heytom.MQ.TestApi.Handlers;

public class OrderCreatedEventHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly ILogger<OrderCreatedEventHandler> _logger;

    public OrderCreatedEventHandler(ILogger<OrderCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    public async Task<bool> HandleAsync(OrderCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[RabbitMQ] 处理订单创建事件 - EventId: {EventId}, OrderId: {OrderId}, Amount: {Amount}, Customer: {CustomerName}, OccurredAt: {OccurredAt}",
            @event.EventId,
            @event.OrderId,
            @event.Amount,
            @event.CustomerName,
            @event.OccurredAt);

        // 模拟业务处理
        await Task.Delay(200, cancellationToken);

        _logger.LogInformation("[RabbitMQ] 订单创建事件处理完成 - OrderId: {OrderId}", @event.OrderId);

        return true;
    }
}
