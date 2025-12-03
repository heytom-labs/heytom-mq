using System.Data;
using System.Data.Common;

namespace Heytom.MQ.Abstractions;

/// <summary>
/// 本地消息表仓储实现（使用原生 SQL）
/// </summary>
public class LocalMessageRepository(string tableName = "LocalMessages") : ILocalMessageRepository
{
    private readonly string _tableName = tableName;

    public async Task SaveAsync(IDbTransaction transaction, LocalMessage message, CancellationToken cancellationToken = default)
    {
        var sql = $@"
            INSERT INTO {_tableName} 
            (Id, MQType, Topic, RoutingKey, MessageType, MessageBody, Status, CreatedAt, UpdatedAt, RetryCount, ErrorMessage)
            VALUES 
            (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10)";

        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;

        AddParameter(command, "@p0", message.Id);
        AddParameter(command, "@p1", message.MQType);
        AddParameter(command, "@p2", message.Topic);
        AddParameter(command, "@p3", message.RoutingKey ?? (object)DBNull.Value);
        AddParameter(command, "@p4", message.MessageType);
        AddParameter(command, "@p5", message.MessageBody);
        AddParameter(command, "@p6", message.Status);
        AddParameter(command, "@p7", message.CreatedAt);
        AddParameter(command, "@p8", message.UpdatedAt ?? (object)DBNull.Value);
        AddParameter(command, "@p9", message.RetryCount);
        AddParameter(command, "@p10", message.ErrorMessage ?? (object)DBNull.Value);

        if (command is DbCommand dbCommand)
        {
            await dbCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            command.ExecuteNonQuery();
        }
    }

    public async Task SaveBatchAsync(IDbTransaction transaction, IEnumerable<LocalMessage> messages, CancellationToken cancellationToken = default)
    {
        foreach (var message in messages)
        {
            await SaveAsync(transaction, message, cancellationToken);
        }
    }

    public async Task UpdateStatusAsync(IDbConnection connection, Guid messageId, int status, string? errorMessage = null, CancellationToken cancellationToken = default)
    {
        var sql = $@"
            UPDATE {_tableName} 
            SET Status = @p0, UpdatedAt = @p1, ErrorMessage = @p2
            WHERE Id = @p3";

        if (connection.State != ConnectionState.Open)
        {
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken);
            }
            else
            {
                connection.Open();
            }
        }

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        AddParameter(command, "@p0", status);
        AddParameter(command, "@p1", DateTime.UtcNow);
        AddParameter(command, "@p2", errorMessage ?? (object)DBNull.Value);
        AddParameter(command, "@p3", messageId);

        if (command is DbCommand dbCommand)
        {
            await dbCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            command.ExecuteNonQuery();
        }
    }

    public async Task<List<LocalMessage>> GetPendingMessagesAsync(IDbConnection connection, int batchSize = 100, CancellationToken cancellationToken = default)
    {
        var sql = $@"
            SELECT 
                Id, MQType, Topic, RoutingKey, MessageType, MessageBody, 
                Status, CreatedAt, UpdatedAt, RetryCount, ErrorMessage
            FROM {_tableName}
            WHERE Status IN (0, 2)
            ORDER BY CreatedAt ASC limit {batchSize} ";

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        if (connection.State != ConnectionState.Open)
        {
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken);
            }
            else
            {
                connection.Open();
            }
        }

        var messages = new List<LocalMessage>();
        
        if (command is DbCommand dbCommand)
        {
            await using var reader = await dbCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                messages.Add(ReadMessage(reader));
            }
        }
        else
        {
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                messages.Add(ReadMessage(reader));
            }
        }

        return messages;
    }

    private static LocalMessage ReadMessage(IDataReader reader)
    {
        return new LocalMessage
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
        };
    }

    private static void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
