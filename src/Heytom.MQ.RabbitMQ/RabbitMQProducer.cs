using System.Data;
using System.Data.Common;
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
    private readonly HashSet<string> _declaredExchanges = [];
    private readonly LocalMessageRepository _localMessageRepository;

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

    public async Task SendWithTransactionAsync<T>(IDbConnection dbConnection, Func<IDbTransaction, Task<T>> messageFunc, CancellationToken cancellationToken = default) where T : class, IEvent
    {
        if (dbConnection.State != System.Data.ConnectionState.Open)
        {
            dbConnection.Open();
        }

        using var transaction = dbConnection.BeginTransaction();
        Guid messageId;
        T message;

        try
        {
            // 执行业务逻辑并获取消息
            message = await messageFunc(transaction);

            // 将消息保存到本地消息表，并获取消息ID
            messageId = await SaveMessageToLocalTable(transaction, message, cancellationToken);

            // 提交事务
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        // 事务提交后，发送消息到 RabbitMQ
        try
        {
            await SendAsync(message, cancellationToken);

            // 发送成功，更新消息状态为已发送
            await UpdateMessageStatusToSent(dbConnection, messageId, cancellationToken);
        }
        catch
        {
            // 发送失败，消息保持待发送状态（Status=0），后台服务会重试
            // 不抛出异常，因为业务数据已经保存成功
        }
    }

    public async Task SendBatchWithTransactionAsync<T>(IDbConnection dbConnection, Func<IDbTransaction, Task<IEnumerable<T>>> messageFunc, CancellationToken cancellationToken = default) where T : class, IEvent
    {
        if (dbConnection.State != System.Data.ConnectionState.Open)
        {
            dbConnection.Open();
        }

        using var transaction = dbConnection.BeginTransaction();
        var messageIds = new List<Guid>();
        IEnumerable<T> messages;

        try
        {
            // 执行业务逻辑并获取消息集合
            messages = await messageFunc(transaction);

            // 将消息批量保存到本地消息表
            foreach (var message in messages)
            {
                var messageId = await SaveMessageToLocalTable(transaction, message, cancellationToken);
                messageIds.Add(messageId);
            }

            // 提交事务
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        // 事务提交后，批量发送消息到 RabbitMQ
        try
        {
            await SendBatchAsync(messages, cancellationToken);

            // 发送成功，批量更新消息状态为已发送
            foreach (var messageId in messageIds)
            {
                await UpdateMessageStatusToSent(dbConnection, messageId, cancellationToken);
            }
        }
        catch
        {
            // 发送失败，消息保持待发送状态（Status=0），后台服务会重试
            // 不抛出异常，因为业务数据已经保存成功
        }
    }

    private async Task<Guid> SaveMessageToLocalTable<T>(IDbTransaction transaction, T message, CancellationToken cancellationToken) where T : class, IEvent
    {
        var attribute = RabbitMQTopicHelper.GetTopicAttribute<T>();
        var exchange = attribute.Exchange ?? _options.Exchange;
        var routingKey = attribute.RoutingKey;

        var messageId = Guid.NewGuid();
        var localMessage = new LocalMessage
        {
            Id = messageId,
            MQType = "RabbitMQ",
            Topic = exchange,
            RoutingKey = routingKey,
            MessageType = typeof(T).AssemblyQualifiedName ?? typeof(T).FullName ?? string.Empty,
            MessageBody = JsonSerializer.Serialize(message),
            Status = 0, // 0: 待发送
            CreatedAt = DateTime.UtcNow,
            RetryCount = 0
        };

        await _localMessageRepository.SaveAsync(transaction, localMessage, cancellationToken);
        return messageId;
    }

    private async Task UpdateMessageStatusToSent(IDbConnection dbConnection, Guid messageId, CancellationToken cancellationToken)
    {
        try
        {
            await _localMessageRepository.UpdateStatusAsync(
                dbConnection,
                messageId,
                1, // 1: 已发送
                null,
                cancellationToken);
        }
        catch
        {
            // 更新状态失败不影响主流程，后台服务会处理
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
        GC.SuppressFinalize(this);
    }
}
