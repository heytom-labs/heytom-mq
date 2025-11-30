using System.Text.Json;
using Confluent.Kafka;
using Heytom.MQ.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Heytom.MQ.Kafka;

/// <summary>
/// Kafka消息消费者
/// </summary>
public class KafkaConsumer : IMessageConsumer, IDisposable
{
    private readonly KafkaOptions _options;
    private IConsumer<string, string>? _consumer;
    private readonly Dictionary<string, (Type MessageType, Func<object, Task<bool>> Handler)> _handlers = new();
    private CancellationTokenSource? _cts;

    public KafkaConsumer(KafkaOptions options)
    {
        _options = options;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        // 基础配置从 Options 获取
        var config = new ConsumerConfig
        {
            BootstrapServers = _options.ConnectionString,
            GroupId = _options.GroupId,
            EnableAutoCommit = _options.EnableAutoCommit,
            AutoOffsetReset = Enum.Parse<AutoOffsetReset>(_options.AutoOffsetReset, true),
            SessionTimeoutMs = _options.SessionTimeoutMs
        };

        Console.WriteLine($"[Kafka] 启动消费者 - BootstrapServers: {config.BootstrapServers}, GroupId: {config.GroupId}");

        _consumer = new ConsumerBuilder<string, string>(config).Build();
        _cts = new CancellationTokenSource();

        Task.Run(() => ConsumeLoop(_cts.Token), cancellationToken);

        Console.WriteLine("[Kafka] 消费循环已启动");

        return Task.CompletedTask;
    }

    public Task SubscribeAsync<T>(Func<T, Task<bool>> handler, CancellationToken cancellationToken = default) where T : class, IEvent
    {
        if (_consumer == null)
            throw new InvalidOperationException("Consumer not started. Call StartAsync first.");

        var attribute = KafkaTopicHelper.GetTopicAttribute<T>();
        var topic = attribute.Topic;

        // 注意：Attribute中的消费者配置（如GroupId、AutoOffsetReset等）
        // 需要在StartAsync之前设置，这里只能使用已启动的Consumer
        // 如果需要不同的消费者配置，应该创建不同的Consumer实例

        _handlers[topic] = (typeof(T), async (obj) => await handler((T)obj));
        _consumer.Subscribe(topic);

        Console.WriteLine($"[Kafka] 已订阅 Topic: {topic}");

        return Task.CompletedTask;
    }

    private async Task ConsumeLoop(CancellationToken cancellationToken)
    {
        if (_consumer == null) return;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer.Consume(TimeSpan.FromMilliseconds(100));
                    
                    if (consumeResult?.Message != null)
                    {
                        if (_handlers.TryGetValue(consumeResult.Topic, out var handlerInfo))
                        {
                            try
                            {
                                var message = JsonSerializer.Deserialize(consumeResult.Message.Value, handlerInfo.MessageType);

                                if (message != null)
                                {
                                    var success = await handlerInfo.Handler(message);
                                    if (success && !_options.EnableAutoCommit)
                                    {
                                        _consumer.Commit(consumeResult);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[Kafka] 处理消息失败: Topic={consumeResult.Topic}, Error={ex.Message}");
                                // 处理失败，可以记录日志或重试
                            }
                        }
                        else
                        {
                            Console.WriteLine($"[Kafka] 收到消息但没有找到处理器: Topic={consumeResult.Topic}");
                        }
                    }
                }
                catch (ConsumeException ex)
                {
                    Console.WriteLine($"[Kafka] 消费异常: {ex.Error.Reason}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[Kafka] 消费循环正常取消");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Kafka] 消费循环异常: {ex.Message}");
        }
    }

    public Task SubscribeAsync<TEvent, THandler>(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
        where THandler : IEventHandler<TEvent>
    {
        return SubscribeAsync<TEvent>(async (@event) =>
        {
            using var scope = serviceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<THandler>();
            return await handler.HandleAsync(@event, cancellationToken);
        }, cancellationToken);
    }

    public Task UnsubscribeAsync<T>(CancellationToken cancellationToken = default) where T : class, IEvent
    {
        var topic = KafkaTopicHelper.GetTopic<T>();
        _handlers.Remove(topic);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _cts?.Cancel();
        _consumer?.Close();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _cts?.Dispose();
        _consumer?.Dispose();
    }
}
