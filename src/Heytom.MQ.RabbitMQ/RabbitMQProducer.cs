using System.Text;
using System.Text.Json;
using Heytom.MQ.Abstractions;
using RabbitMQ.Client;

namespace Heytom.MQ.RabbitMQ;

/// <summary>
/// RabbitMQ消息生产者
/// </summary>
public class RabbitMQProducer : IMessageProducer, IDisposable
{
    private readonly RabbitMQOptions _options;
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly HashSet<string> _declaredExchanges = new();

    public RabbitMQProducer(RabbitMQOptions options)
    {
        _options = options;
        var factory = new ConnectionFactory { Uri = new Uri(options.ConnectionString) };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
    }

    public Task SendAsync<T>(T message, CancellationToken cancellationToken = default) where T : class, IEvent
    {
        var attribute = RabbitMQTopicHelper.GetTopicAttribute<T>();
        var exchange = attribute.Exchange ?? _options.Exchange;
        var routingKey = attribute.RoutingKey;

        // 确保Exchange已声明
        EnsureExchangeDeclared(exchange, attribute.ExchangeType, attribute.Durable, attribute.AutoDelete);

        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";
        properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        _channel.BasicPublish(
            exchange: exchange,
            routingKey: routingKey,
            basicProperties: properties,
            body: body
        );

        return Task.CompletedTask;
    }

    public async Task SendBatchAsync<T>(IEnumerable<T> messages, CancellationToken cancellationToken = default) where T : class, IEvent
    {
        foreach (var message in messages)
        {
            await SendAsync(message, cancellationToken);
        }
    }

    private void EnsureExchangeDeclared(string exchange, string exchangeType, bool durable, bool autoDelete)
    {
        if (_declaredExchanges.Contains(exchange))
            return;

        _channel.ExchangeDeclare(
            exchange: exchange,
            type: exchangeType,
            durable: durable,
            autoDelete: autoDelete
        );

        _declaredExchanges.Add(exchange);
    }

    public void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();
        _connection?.Close();
        _connection?.Dispose();
    }
}
