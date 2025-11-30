namespace Heytom.MQ.Abstractions;

/// <summary>
/// 消息生产者接口
/// </summary>
public interface IMessageProducer
{
    /// <summary>
    /// 发送消息（从消息类型的MessageTopicAttribute中获取Topic信息）
    /// </summary>
    Task SendAsync<T>(T message, CancellationToken cancellationToken = default) where T : class, IEvent;

    /// <summary>
    /// 批量发送消息（从消息类型的MessageTopicAttribute中获取Topic信息）
    /// </summary>
    Task SendBatchAsync<T>(IEnumerable<T> messages, CancellationToken cancellationToken = default) where T : class, IEvent;
}
