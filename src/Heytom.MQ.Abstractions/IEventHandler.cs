namespace Heytom.MQ.Abstractions;

/// <summary>
/// 事件处理器接口
/// </summary>
/// <typeparam name="TEvent">事件类型</typeparam>
public interface IEventHandler<in TEvent> where TEvent : class, IEvent
{
    /// <summary>
    /// 处理事件
    /// </summary>
    /// <param name="event">事件对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>返回 true 表示处理成功，false 表示处理失败需要重试</returns>
    Task<bool> HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
