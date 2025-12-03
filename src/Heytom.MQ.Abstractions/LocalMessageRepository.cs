using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Heytom.MQ.Abstractions;

/// <summary>
/// 本地消息表仓储实现（使用原生 SQL）
/// </summary>
public class LocalMessageRepository : ILocalMessageRepository
{
    private readonly string _tableName;

    public LocalMessageRepository(string tableName = "LocalMessages")
    {
        _tableName = tableName;
    }

    public async Task SaveAsync(DbContext dbContext, LocalMessage message, CancellationToken cancellationToken = default)
    {
        var sql = $@"
            INSERT INTO {_tableName} 
            (Id, MQType, Topic, RoutingKey, MessageType, MessageBody, Status, CreatedAt, UpdatedAt, RetryCount, ErrorMessage)
            VALUES 
            (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10)";

        await dbContext.Database.ExecuteSqlRawAsync(
            sql,
            new object[]
            {
                message.Id,
                message.MQType,
                message.Topic,
                message.RoutingKey ?? (object)DBNull.Value,
                message.MessageType,
                message.MessageBody,
                message.Status,
                message.CreatedAt,
                message.UpdatedAt ?? (object)DBNull.Value,
                message.RetryCount,
                message.ErrorMessage ?? (object)DBNull.Value
            },
            cancellationToken);
    }

    public async Task SaveBatchAsync(DbContext dbContext, IEnumerable<LocalMessage> messages, CancellationToken cancellationToken = default)
    {
        foreach (var message in messages)
        {
            await SaveAsync(dbContext, message, cancellationToken);
        }
    }

    public async Task UpdateStatusAsync(DbContext dbContext, Guid messageId, int status, string? errorMessage = null, CancellationToken cancellationToken = default)
    {
        var sql = $@"
            UPDATE {_tableName} 
            SET Status = @p0, UpdatedAt = @p1, ErrorMessage = @p2, RetryCount = RetryCount + 1
            WHERE Id = @p3";

        await dbContext.Database.ExecuteSqlRawAsync(
            sql,
            new object[]
            {
                status,
                DateTime.UtcNow,
                errorMessage ?? (object)DBNull.Value,
                messageId
            },
            cancellationToken);
    }

    public async Task<List<LocalMessage>> GetPendingMessagesAsync(DbContext dbContext, int batchSize = 100, CancellationToken cancellationToken = default)
    {
        var sql = $@"
            SELECT TOP {batchSize} 
                Id, MQType, Topic, RoutingKey, MessageType, MessageBody, 
                Status, CreatedAt, UpdatedAt, RetryCount, ErrorMessage
            FROM {_tableName}
            WHERE Status IN (0, 2)
            ORDER BY CreatedAt ASC";

        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var messages = new List<LocalMessage>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            messages.Add(new LocalMessage
            {
                Id = reader.GetGuid(0),
                MQType = reader.GetString(1),
                Topic = reader.GetString(2),
                RoutingKey = reader.IsDBNull(3) ? null : reader.GetString(3),
                MessageType = reader.GetString(4),
                MessageBody = reader.GetString(5),
                Status = reader.GetInt32(6),
                CreatedAt = reader.GetDateTime(7),
                UpdatedAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                RetryCount = reader.GetInt32(9),
                ErrorMessage = reader.IsDBNull(10) ? null : reader.GetString(10)
            });
        }

        return messages;
    }
}
