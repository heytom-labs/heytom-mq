using System.Text.Json;
using Confluent.Kafka;
using Heytom.MQ.Abstractions;

namespace Heytom.MQ.Kafka;

/// <summary>
/// Kafka消息生产者
/// </summary>
public class KafkaProducer : IMessageProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;

    public KafkaProducer(KafkaOptions options)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = options.ConnectionString,
            Acks = Acks.All,
            EnableIdempotence = true
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
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

    public void Dispose()
    {
        _producer?.Flush(TimeSpan.FromSeconds(10));
        _producer?.Dispose();
    }
}
