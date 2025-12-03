namespace Heytom.MQ.Abstractions;

/// <summary>
/// 本地消息表实体（同时支持 RabbitMQ 和 Kafka）
/// </summary>
public class LocalMessage
{
    /// <summary>
    /// 消息ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 消息队列类型 (RabbitMQ/Kafka)
    /// </summary>
    public string MQType { get; set; } = string.Empty;

    /// <summary>
    /// Topic/Exchange 名称
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// 路由键/分区键 (RabbitMQ: RoutingKey, Kafka: PartitionKey)
    /// </summary>
    public string? RoutingKey { get; set; }

    /// <summary>
    /// 消息类型
    /// </summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>
    /// 消息内容(JSON)
    /// </summary>
    public string MessageBody { get; set; } = string.Empty;

    /// <summary>
    /// 消息状态 (0: 待发送, 1: 已发送, 2: 发送失败)
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }
}
