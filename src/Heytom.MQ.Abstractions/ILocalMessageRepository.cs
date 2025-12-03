using System.Data;

namespace Heytom.MQ.Abstractions;

/// <summary>
/// 本地消息表仓储接口
/// </summary>
public interface ILocalMessageRepository
{
    /// <summary>
    /// 保存消息到本地表
    /// </summary>
    Task SaveAsync(IDbTransaction transaction, LocalMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量保存消息到本地表
    /// </summary>
    Task SaveBatchAsync(IDbTransaction transaction, IEnumerable<LocalMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新消息状态（不使用事务）
    /// </summary>
    Task UpdateStatusAsync(IDbConnection connection, Guid messageId, int status, string? errorMessage = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取待发送的消息
    /// </summary>
    Task<List<LocalMessage>> GetPendingMessagesAsync(IDbConnection connection, int batchSize = 100, CancellationToken cancellationToken = default);
}
