using System.Reflection;
using Heytom.MQ.Abstractions;

namespace Heytom.MQ.Kafka;

/// <summary>
/// Kafka主题辅助类
/// </summary>
internal static class KafkaTopicHelper
{
    /// <summary>
    /// 从事件类型中获取KafkaTopicAttribute
    /// </summary>
    public static KafkaTopicAttribute GetTopicAttribute<T>() where T : class, IEvent
    {
        return GetTopicAttribute(typeof(T));
    }

    /// <summary>
    /// 从事件类型中获取KafkaTopicAttribute
    /// </summary>
    public static KafkaTopicAttribute GetTopicAttribute(Type eventType)
    {
        var attribute = eventType.GetCustomAttribute<KafkaTopicAttribute>();
        if (attribute == null)
        {
            throw new InvalidOperationException(
                $"事件类型 {eventType.Name} 必须标记 [KafkaTopic] 特性");
        }
        return attribute;
    }

    /// <summary>
    /// 获取Topic名称
    /// </summary>
    public static string GetTopic<T>() where T : class, IEvent
    {
        return GetTopicAttribute<T>().Topic;
    }

    /// <summary>
    /// 获取分区键（如果有）
    /// </summary>
    public static string? GetPartitionKey<T>() where T : class, IEvent
    {
        return GetTopicAttribute<T>().PartitionKeyProperty;
    }
}
