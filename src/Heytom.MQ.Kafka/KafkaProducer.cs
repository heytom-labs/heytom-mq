using System.Text.Json;
using Confluent.Kafka;
using Heytom.MQ.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Heytom.MQ.Kafka;

/// <summary>
/// Kafka消息生产者
/// </summary>
public class KafkaProducer : IMessageProducer, IDisposable
{
    private readonly KafkaOptions _options;
    private readonly IProducer<string, string> _producer;
    private readonly ILocalMessageRepository _localMessageRepository;

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

            // 发送消息到 Kafka
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

            // 批量发送消息到 Kafka
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

        var localMessage = new LocalMessage
        {
            Id = Guid.NewGuid(),
            MQType = "Kafka",
            Topic = topic,
            RoutingKey = partitionKey,
            MessageType = typeof(T).FullName ?? string.Empty,
            MessageBody = JsonSerializer.Serialize(message),
            Status = 0, // 0: 待发送
            CreatedAt = DateTime.UtcNow,
            RetryCount = 0
        };

        await _localMessageRepository.SaveAsync(dbContext, localMessage, cancellationToken);
    }

    public void Dispose()
    {
        _producer?.Flush(TimeSpan.FromSeconds(10));
        _producer?.Dispose();
        GC.SuppressFinalize(this);
    }
}
