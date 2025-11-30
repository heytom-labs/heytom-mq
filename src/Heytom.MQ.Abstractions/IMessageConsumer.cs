namespace Heytom.MQ.Abstractions;

/// <summary>
/// 消息消费者接口
/// </summary>
public interface IMessageConsumer
{
    /// <summary>
    /// 订阅消息（从消息类型的MessageTopicAttribute中获取Topic信息）
    /// </summary>
    Task SubscribeAsync<T>(Func<T, Task<bool>> handler, CancellationToken cancellationToken = default) where T : class, IEvent;

    /// <summary>
    /// 订阅消息并使用事件处理器（需要提供 ServiceProvider 用于依赖注入）
    /// </summary>
    Task SubscribeAsync<TEvent, THandler>(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
        where THandler : IEventHandler<TEvent>;

    /// <summary>
    /// 取消订阅（从消息类型的MessageTopicAttribute中获取Topic信息）
    /// </summary>
    Task UnsubscribeAsync<T>(CancellationToken cancellationToken = default) where T : class, IEvent;

    /// <summary>
    /// 启动消费者
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止消费者
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
