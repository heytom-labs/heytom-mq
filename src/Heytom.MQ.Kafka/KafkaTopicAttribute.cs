namespace Heytom.MQ.Kafka;

/// <summary>
/// Kafka主题特性，用于标记消息类型的Topic和消费组信息
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class KafkaTopicAttribute : Attribute
{
    /// <summary>
    /// Topic名称 - 必填
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// 消费组ID（可选，不指定则使用配置中的默认GroupId）
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>
    /// 分区数量（可选，用于创建Topic时）
    /// </summary>
    public int? NumPartitions { get; set; }

    /// <summary>
    /// 副本因子（可选，用于创建Topic时）
    /// </summary>
    public short? ReplicationFactor { get; set; }

    /// <summary>
    /// 分区键属性名（可选）
    /// 指定消息对象中哪个属性作为分区键
    /// </summary>
    public string? PartitionKeyProperty { get; set; }

    /// <summary>
    /// 消息保留时间（毫秒，可选）
    /// </summary>
    public long? RetentionMs { get; set; }

    /// <summary>
    /// 压缩类型（可选）
    /// 可选值：none, gzip, snappy, lz4, zstd
    /// </summary>
    public string? CompressionType { get; set; }

    /// <summary>
    /// 是否启用幂等性（默认 true）
    /// </summary>
    public bool EnableIdempotence { get; set; } = true;

    /// <summary>
    /// 确认级别（可选）
    /// 0: 不等待确认
    /// 1: 等待Leader确认
    /// -1/all: 等待所有副本确认
    /// </summary>
    public string? Acks { get; set; }

    /// <summary>
    /// 消费起始位置（可选）
    /// 可选值：earliest, latest
    /// </summary>
    public string? AutoOffsetReset { get; set; }

    /// <summary>
    /// 是否启用自动提交（可选）
    /// </summary>
    public bool? EnableAutoCommit { get; set; }

    /// <summary>
    /// 自动提交间隔（毫秒，可选）
    /// </summary>
    public int? AutoCommitIntervalMs { get; set; }

    /// <summary>
    /// 会话超时时间（毫秒，可选）
    /// </summary>
    public int? SessionTimeoutMs { get; set; }

    /// <summary>
    /// 最大拉取记录数（可选）
    /// </summary>
    public int? MaxPollRecords { get; set; }

    public KafkaTopicAttribute()
    {
    }
}

