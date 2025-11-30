namespace Heytom.MQ.DependencyInjection;

/// <summary>
/// 事件处理器注册信息
/// </summary>
public class EventHandlerRegistration
{
    public required Type EventType { get; init; }
    public required Type HandlerType { get; init; }
}
