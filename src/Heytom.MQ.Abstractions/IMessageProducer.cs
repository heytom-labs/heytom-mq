using System.Data;

namespace Heytom.MQ.Abstractions;

/// <summary>
/// 消息生产者接口
/// </summary>
public interface IMessageProducer
{
    /// <summary>
    /// 发送消息（从消息类型的MessageTopicAttribute中获取Topic信息）
    /// </summary>
    Task SendAsync<T>(T message, CancellationToken cancellationToken = default) where T : class, IEvent;

    /// <summary>
    /// 批量发送消息（从消息类型的MessageTopicAttribute中获取Topic信息）
    /// </summary>
    Task SendBatchAsync<T>(IEnumerable<T> messages, CancellationToken cancellationToken = default) where T : class, IEvent;

    /// <summary>
    /// 在事务中发送消息到本地消息表（在委托中执行业务逻辑并返回消息，确保业务操作和消息写入在同一事务中提交）
    /// </summary>
    /// <param name="dbConnection">数据库连接对象</param>
    /// <param name="messageFunc">消息委托，在委托中执行业务逻辑并返回要发送的消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SendWithTransactionAsync<T>(IDbConnection dbConnection, Func<IDbTransaction, Task<T>> messageFunc, CancellationToken cancellationToken = default) where T : class, IEvent;

    /// <summary>
    /// 在事务中批量发送消息到本地消息表（在委托中执行业务逻辑并返回消息集合，确保业务操作和消息写入在同一事务中提交）
    /// </summary>
    /// <param name="dbConnection">数据库连接对象</param>
    /// <param name="messageFunc">消息委托，在委托中执行业务逻辑并返回要发送的消息集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SendBatchWithTransactionAsync<T>(IDbConnection dbConnection, Func<IDbTransaction, Task<IEnumerable<T>>> messageFunc, CancellationToken cancellationToken = default) where T : class, IEvent;
}
