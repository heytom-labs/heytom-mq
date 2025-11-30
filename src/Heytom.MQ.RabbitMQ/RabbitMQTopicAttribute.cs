namespace Heytom.MQ.RabbitMQ;

/// <summary>
/// RabbitMQ主题特性，用于标记消息类型的Exchange、Queue和RoutingKey信息
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class RabbitMQTopicAttribute : Attribute
{
    /// <summary>
    /// RoutingKey（路由键）- 必填
    /// </summary>
    public string RoutingKey { get; set; } = string.Empty;

    /// <summary>
    /// Exchange名称（可选，不指定则使用配置中的默认Exchange）
    /// </summary>
    public string? Exchange { get; set; }

    /// <summary>
    /// Exchange类型（可选，默认为 topic）
    /// 可选值：direct, topic, fanout, headers
    /// </summary>
    public string ExchangeType { get; set; } = "topic";

    /// <summary>
    /// Queue名称（可选，不指定则自动生成：heytom.queue.{RoutingKey}）
    /// </summary>
    public string? QueueName { get; set; }

    /// <summary>
    /// 是否持久化（默认 true）
    /// </summary>
    public bool Durable { get; set; } = true;

    /// <summary>
    /// 是否自动删除（默认 false）
    /// </summary>
    public bool AutoDelete { get; set; } = false;

    /// <summary>
    /// 是否排他队列（默认 false）
    /// 排他队列只能被声明它的连接使用，连接断开时自动删除
    /// </summary>
    public bool Exclusive { get; set; } = false;

    /// <summary>
    /// 消息TTL（毫秒，可选）
    /// 消息在队列中的存活时间
    /// </summary>
    public int? MessageTTL { get; set; }

    /// <summary>
    /// 队列最大长度（可选）
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// 队列优先级（0-255，可选）
    /// </summary>
    public byte? MaxPriority { get; set; }

    /// <summary>
    /// 死信Exchange（可选）
    /// 消息被拒绝或过期时发送到的Exchange
    /// </summary>
    public string? DeadLetterExchange { get; set; }

    /// <summary>
    /// 死信RoutingKey（可选）
    /// </summary>
    public string? DeadLetterRoutingKey { get; set; }

    public RabbitMQTopicAttribute()
    {
    }
}

