using Microsoft.EntityFrameworkCore;

namespace Heytom.MQ.Abstractions;

/// <summary>
/// 本地消息表仓储接口
/// </summary>
public interface ILocalMessageRepository
{
    /// <summary>
    /// 保存消息到本地表
    /// </summary>
    Task SaveAsync(DbContext dbContext, LocalMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量保存消息到本地表
    /// </summary>
    Task SaveBatchAsync(DbContext dbContext, IEnumerable<LocalMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新消息状态
    /// </summary>
    Task UpdateStatusAsync(DbContext dbContext, Guid messageId, int status, string? errorMessage = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取待发送的消息
    /// </summary>
    Task<List<LocalMessage>> GetPendingMessagesAsync(DbContext dbContext, int batchSize = 100, CancellationToken cancellationToken = default);
}
