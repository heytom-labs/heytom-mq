using System.Data;

namespace Heytom.MQ.Abstractions;

/// <summary>
/// 本地消息重试配置选项
/// </summary>
public class LocalMessageRetryOptions
{
    /// <summary>
    /// 扫描间隔（默认：30秒）
    /// </summary>
    public TimeSpan ScanInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 每次扫描的批量大小（默认：100）
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// 最大重试次数（默认：5次）
    /// </summary>
    public int MaxRetryCount { get; set; } = 5;

    /// <summary>
    /// 本地消息表名称（默认：LocalMessages）
    /// </summary>
    public string TableName { get; set; } = "LocalMessages";

    /// <summary>
    /// 是否启用重试服务（默认：true）
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 数据库连接工厂函数（用于创建 IDbConnection 实例）
    /// 如果未设置，将尝试从 DI 容器中获取 IDbConnection
    /// </summary>
    public Func<IServiceProvider, IDbConnection>? DbConnectionFactory { get; set; }
}
