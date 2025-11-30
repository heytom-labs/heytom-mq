using Heytom.MQ.Abstractions;

namespace Heytom.MQ.Kafka;

/// <summary>
/// Kafka配置选项
/// </summary>
public class KafkaOptions : MQOptions
{
    public string GroupId { get; set; } = "heytom-consumer-group";
    public bool EnableAutoCommit { get; set; } = false;
    public string AutoOffsetReset { get; set; } = "earliest";
    public int SessionTimeoutMs { get; set; } = 10000;
}
