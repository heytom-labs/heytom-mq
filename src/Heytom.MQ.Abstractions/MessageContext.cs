namespace Heytom.MQ.Abstractions;

/// <summary>
/// 消息上下文
/// </summary>
public class MessageContext<T> where T : class
{
    public required T Message { get; init; }
    public required string Topic { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public Dictionary<string, string> Headers { get; init; } = new();
}
