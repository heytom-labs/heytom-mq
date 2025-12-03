using System.Text;
using System.Text.Json;
using Heytom.MQ.Abstractions;
using Microsoft.EntityFrameworkCore;
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
    private readonly HashSet<string> _declaredExchanges = [];
    private readonly ILocalMessageRepository _localMessageRepository;

    public RabbitMQProducer(RabbitMQOptions options)
    {
        _options = options;
        var factory = new ConnectionFactory { Uri = new Uri(options.ConnectionString) };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _localMessageRepository = new LocalMessageRepository(options.LocalMessageTableName);
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

    public async Task SendWithTransactionAsync<T>(DbContext dbContext, Func<Task<T>> messageFunc, CancellationToken cancellationToken = default) where T : class, IEvent
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // 执行业务逻辑并获取消息
            var message = await messageFunc();

            // 将消息保存到本地消息表
            await SaveMessageToLocalTable(dbContext, message, cancellationToken);

            // 提交事务
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // 发送消息到 RabbitMQ
            await SendAsync(message, cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task SendBatchWithTransactionAsync<T>(DbContext dbContext, Func<Task<IEnumerable<T>>> messageFunc, CancellationToken cancellationToken = default) where T : class, IEvent
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // 执行业务逻辑并获取消息集合
            var messages = await messageFunc();

            // 将消息批量保存到本地消息表
            foreach (var message in messages)
            {
                await SaveMessageToLocalTable(dbContext, message, cancellationToken);
            }

            // 提交事务
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // 批量发送消息到 RabbitMQ
            await SendBatchAsync(messages, cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task SaveMessageToLocalTable<T>(DbContext dbContext, T message, CancellationToken cancellationToken) where T : class, IEvent
    {
        var attribute = RabbitMQTopicHelper.GetTopicAttribute<T>();
        var exchange = attribute.Exchange ?? _options.Exchange;
        var routingKey = attribute.RoutingKey;

        var localMessage = new LocalMessage
        {
            Id = Guid.NewGuid(),
            MQType = "RabbitMQ",
            Topic = exchange,
            RoutingKey = routingKey,
            MessageType = typeof(T).FullName ?? string.Empty,
            MessageBody = JsonSerializer.Serialize(message),
            Status = 0, // 0: 待发送
            CreatedAt = DateTime.UtcNow,
            RetryCount = 0
        };

        await _localMessageRepository.SaveAsync(dbContext, localMessage, cancellationToken);
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
        GC.SuppressFinalize(this);
    }
}
