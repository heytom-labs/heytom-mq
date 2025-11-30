using System.Text;
using System.Text.Json;
using Heytom.MQ.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Heytom.MQ.RabbitMQ;

/// <summary>
/// RabbitMQ消息消费者
/// </summary>
public class RabbitMQConsumer : IMessageConsumer, IDisposable
{
    private readonly RabbitMQOptions _options;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly Dictionary<string, string> _queueTopicMap = new();
    private readonly HashSet<string> _declaredExchanges = new();

    public RabbitMQConsumer(RabbitMQOptions options)
    {
        _options = options;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        var factory = new ConnectionFactory { Uri = new Uri(_options.ConnectionString) };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        return Task.CompletedTask;
    }

    public Task SubscribeAsync<T>(Func<T, Task<bool>> handler, CancellationToken cancellationToken = default) where T : class, IEvent
    {
        if (_channel == null)
            throw new InvalidOperationException("Consumer not started. Call StartAsync first.");

        var attribute = RabbitMQTopicHelper.GetTopicAttribute<T>();
        var exchange = attribute.Exchange ?? _options.Exchange;
        var exchangeType = attribute.ExchangeType;
        var routingKey = attribute.RoutingKey;
        var queueName = attribute.QueueName ?? $"heytom.queue.{routingKey}";

        // 确保Exchange已声明
        EnsureExchangeDeclared(exchange, exchangeType, attribute.Durable, attribute.AutoDelete);

        // 声明队列，支持高级配置
        var arguments = new Dictionary<string, object>();
        if (attribute.MessageTTL.HasValue)
            arguments["x-message-ttl"] = attribute.MessageTTL.Value;
        if (attribute.MaxLength.HasValue)
            arguments["x-max-length"] = attribute.MaxLength.Value;
        if (attribute.MaxPriority.HasValue)
            arguments["x-max-priority"] = attribute.MaxPriority.Value;
        if (!string.IsNullOrEmpty(attribute.DeadLetterExchange))
            arguments["x-dead-letter-exchange"] = attribute.DeadLetterExchange;
        if (!string.IsNullOrEmpty(attribute.DeadLetterRoutingKey))
            arguments["x-dead-letter-routing-key"] = attribute.DeadLetterRoutingKey;

        _channel.QueueDeclare(
            queue: queueName,
            durable: attribute.Durable,
            exclusive: attribute.Exclusive,
            autoDelete: attribute.AutoDelete,
            arguments: arguments.Count > 0 ? arguments : null
        );

        _channel.QueueBind(queue: queueName, exchange: exchange, routingKey: routingKey);
        
        _queueTopicMap[queueName] = routingKey;

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);
                var message = JsonSerializer.Deserialize<T>(json);

                if (message != null)
                {
                    var success = await handler(message);
                    if (success)
                    {
                        _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    }
                    else
                    {
                        _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                    }
                }
            }
            catch
            {
                _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);

        return Task.CompletedTask;
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
        var attribute = RabbitMQTopicHelper.GetTopicAttribute<T>();
        var exchange = attribute.Exchange ?? _options.Exchange;
        var routingKey = attribute.RoutingKey;
        var queueName = attribute.QueueName ?? $"heytom.queue.{routingKey}";
        
        _channel?.QueueUnbind(queue: queueName, exchange: exchange, routingKey: routingKey);
        return Task.CompletedTask;
    }

    private void EnsureExchangeDeclared(string exchange, string exchangeType, bool durable, bool autoDelete)
    {
        if (_declaredExchanges.Contains(exchange))
            return;

        _channel!.ExchangeDeclare(
            exchange: exchange,
            type: exchangeType,
            durable: durable,
            autoDelete: autoDelete
        );

        _declaredExchanges.Add(exchange);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _channel?.Close();
        _connection?.Close();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
