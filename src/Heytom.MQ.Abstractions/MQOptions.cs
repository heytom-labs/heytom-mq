namespace Heytom.MQ.Abstractions;

/// <summary>
/// MQ配置选项基类
/// </summary>
public abstract class MQOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public int RetryCount { get; set; } = 3;
    public int RetryDelayMilliseconds { get; set; } = 1000;
}
