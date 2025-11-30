using System.Reflection;
using Heytom.MQ.Abstractions;

namespace Heytom.MQ.RabbitMQ;

/// <summary>
/// RabbitMQ主题辅助类
/// </summary>
internal static class RabbitMQTopicHelper
{
    /// <summary>
    /// 从事件类型中获取RabbitMQTopicAttribute
    /// </summary>
    public static RabbitMQTopicAttribute GetTopicAttribute<T>() where T : class, IEvent
    {
        return GetTopicAttribute(typeof(T));
    }

    /// <summary>
    /// 从事件类型中获取RabbitMQTopicAttribute
    /// </summary>
    public static RabbitMQTopicAttribute GetTopicAttribute(Type eventType)
    {
        var attribute = eventType.GetCustomAttribute<RabbitMQTopicAttribute>();
        if (attribute == null)
        {
            throw new InvalidOperationException(
                $"事件类型 {eventType.Name} 必须标记 [RabbitMQTopic] 特性");
        }
        return attribute;
    }

    /// <summary>
    /// 获取RoutingKey
    /// </summary>
    public static string GetRoutingKey<T>() where T : class, IEvent
    {
        return GetTopicAttribute<T>().RoutingKey;
    }

    /// <summary>
    /// 获取Exchange名称（如果有）
    /// </summary>
    public static string? GetExchange<T>() where T : class, IEvent
    {
        return GetTopicAttribute<T>().Exchange;
    }
}
