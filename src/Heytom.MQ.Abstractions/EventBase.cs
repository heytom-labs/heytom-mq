namespace Heytom.MQ.Abstractions;

/// <summary>
/// 事件基类，提供默认实现
/// </summary>
public abstract class EventBase : IEvent
{
    /// <summary>
    /// 事件ID
    /// </summary>
    public string EventId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 事件发生时间
    /// </summary>
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
