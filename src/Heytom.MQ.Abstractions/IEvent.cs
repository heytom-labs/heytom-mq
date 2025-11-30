namespace Heytom.MQ.Abstractions;

/// <summary>
/// 事件消息接口，所有事件消息必须实现此接口
/// </summary>
public interface IEvent
{
    /// <summary>
    /// 事件ID
    /// </summary>
    string EventId { get; }

    /// <summary>
    /// 事件发生时间
    /// </summary>
    DateTime OccurredAt { get; }
}
