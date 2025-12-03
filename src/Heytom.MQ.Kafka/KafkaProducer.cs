using System.Data;
using System.Text.Json;
using Confluent.Kafka;
using Heytom.MQ.Abstractions;

namespace Heytom.MQ.Kafka;

/// <summary>
/// Kafka消息生产者
/// </summary>
public class KafkaProducer : IMessageProducer, IDisposable
{
    private readonly KafkaOptions _options;
    private readonly IProducer<string, string> _producer;
    private readonly LocalMessageRepository _localMessageRepository;

    public KafkaProducer(KafkaOptions options)
    {
        _options = options;
        var config = new ProducerConfig
        {
            BootstrapServers = options.ConnectionString,
            Acks = Acks.All,
            EnableIdempotence = true
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
        _localMessageRepository = new LocalMessageRepository(options.LocalMessageTableName);
    }

    public async Task SendAsync<T>(T message, CancellationToken cancellationToken = default) where T : class, IEvent
    {
        var attribute = KafkaTopicHelper.GetTopicAttribute<T>();
        var topic = attribute.Topic;

        // 获取分区键
        string? partitionKey = null;
        if (!string.IsNullOrEmpty(attribute.PartitionKeyProperty))
        {
            var property = typeof(T).GetProperty(attribute.PartitionKeyProperty);
            if (property != null)
            {
                var value = property.GetValue(message);
                partitionKey = value?.ToString();
            }
        }

        var json = JsonSerializer.Serialize(message);
        var kafkaMessage = new Message<string, string>
        {
            Key = partitionKey ?? Guid.NewGuid().ToString(),
            Value = json,
            Timestamp = new Timestamp(DateTime.UtcNow)
        };

        await _producer.ProduceAsync(topic, kafkaMessage, cancellationToken);
    }

    public async Task SendBatchAsync<T>(IEnumerable<T> messages, CancellationToken cancellationToken = default) where T : class, IEvent
    {
        var tasks = messages.Select(message => SendAsync(message, cancellationToken));
        await Task.WhenAll(tasks);
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

        // 事务提交后，发送消息到 Kafka
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

        // 事务提交后，批量发送消息到 Kafka
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
        var attribute = KafkaTopicHelper.GetTopicAttribute<T>();
        var topic = attribute.Topic;

        // 获取分区键
        string? partitionKey = null;
        if (!string.IsNullOrEmpty(attribute.PartitionKeyProperty))
        {
            var property = typeof(T).GetProperty(attribute.PartitionKeyProperty);
            if (property != null)
            {
                var value = property.GetValue(message);
                partitionKey = value?.ToString();
            }
        }

        var messageId = Guid.NewGuid();
        var localMessage = new LocalMessage
        {
            Id = messageId,
            MQType = "Kafka",
            Topic = topic,
            RoutingKey = partitionKey,
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

    public void Dispose()
    {
        _producer?.Flush(TimeSpan.FromSeconds(10));
        _producer?.Dispose();
        GC.SuppressFinalize(this);
    }
}
