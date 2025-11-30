using System.Reflection;
using Heytom.MQ.Abstractions;
using Heytom.MQ.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Heytom.MQ.DependencyInjection;

/// <summary>
/// RabbitMQ后台服务，自动启动消费者并注册处理器
/// </summary>
public class RabbitMQHostedService : IHostedService
{
    private readonly IMessageConsumer _consumer;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RabbitMQHostedService> _logger;
    private readonly IEnumerable<EventHandlerRegistration> _registrations;

    public RabbitMQHostedService(
        RabbitMQConsumer consumer,
        IServiceProvider serviceProvider,
        ILogger<RabbitMQHostedService> logger,
        IEnumerable<EventHandlerRegistration> registrations)
    {
        _consumer = consumer;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _registrations = registrations;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("启动 RabbitMQ 消费者服务");

        await _consumer.StartAsync(cancellationToken);

        var registeredCount = 0;
        var skippedCount = 0;

        // 注册所有事件处理器
        foreach (var registration in _registrations)
        {
            // 检查 Event 是否有 RabbitMQTopicAttribute
            var hasRabbitMQAttribute = registration.EventType
                .GetCustomAttribute<RabbitMQTopicAttribute>() != null;

            if (!hasRabbitMQAttribute)
            {
                _logger.LogDebug(
                    "跳过事件 {EventType}，因为它没有 [RabbitMQTopic] 特性",
                    registration.EventType.Name);
                skippedCount++;
                continue;
            }

            try
            {
                var subscribeMethod = typeof(IMessageConsumer)
                    .GetMethods()
                    .First(m => m.Name == nameof(IMessageConsumer.SubscribeAsync) &&
                               m.GetGenericArguments().Length == 2);

                var genericMethod = subscribeMethod.MakeGenericMethod(
                    registration.EventType,
                    registration.HandlerType);

                var task = (Task?)genericMethod.Invoke(_consumer, new object[] { _serviceProvider, cancellationToken });
                if (task != null)
                {
                    await task;
                }

                _logger.LogInformation(
                    "已注册 RabbitMQ 事件处理器: {EventType} -> {HandlerType}",
                    registration.EventType.Name,
                    registration.HandlerType.Name);
                registeredCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "注册 RabbitMQ 事件处理器失败: {EventType} -> {HandlerType}",
                    registration.EventType.Name,
                    registration.HandlerType.Name);
            }
        }

        _logger.LogInformation(
            "RabbitMQ 消费者服务启动完成，已注册 {RegisteredCount} 个处理器，跳过 {SkippedCount} 个",
            registeredCount,
            skippedCount);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("停止 RabbitMQ 消费者服务");
        await _consumer.StopAsync(cancellationToken);
    }
}
