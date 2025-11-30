using Heytom.MQ.Abstractions;
using Heytom.MQ.TestApi.Events;

namespace Heytom.MQ.TestApi.Handlers;

public class ProductCreatedEventHandler : IEventHandler<ProductCreatedEvent>
{
    private readonly ILogger<ProductCreatedEventHandler> _logger;

    public ProductCreatedEventHandler(ILogger<ProductCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    public async Task<bool> HandleAsync(ProductCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[Kafka] 处理产品创建事件 - EventId: {EventId}, ProductId: {ProductId}, ProductName: {ProductName}, Price: {Price}, OccurredAt: {OccurredAt}",
            @event.EventId,
            @event.ProductId,
            @event.ProductName,
            @event.Price,
            @event.OccurredAt);

        // 模拟业务处理
        await Task.Delay(150, cancellationToken);

        _logger.LogInformation("[Kafka] 产品创建事件处理完成 - ProductId: {ProductId}", @event.ProductId);

        return true;
    }
}
