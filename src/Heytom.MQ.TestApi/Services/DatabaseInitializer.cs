using System.Data;
using Dapper;

namespace Heytom.MQ.TestApi.Services;

/// <summary>
/// 数据库初始化服务
/// </summary>
public class DatabaseInitializer
{
    private readonly IDbConnection _connection;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(IDbConnection connection, ILogger<DatabaseInitializer> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("开始初始化数据库...");

        // 创建订单表
        await _connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS Orders (
                Id TEXT PRIMARY KEY,
                CustomerId TEXT NOT NULL,
                CustomerName TEXT NOT NULL,
                Amount REAL NOT NULL,
                Status TEXT NOT NULL DEFAULT 'Pending',
                CreatedAt TEXT NOT NULL
            )");

        // 创建本地消息表
        await _connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS LocalMessages (
                Id TEXT PRIMARY KEY,
                MQType TEXT NOT NULL,
                Topic TEXT NOT NULL,
                RoutingKey TEXT,
                MessageType TEXT NOT NULL,
                MessageBody TEXT NOT NULL,
                Status INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT,
                RetryCount INTEGER NOT NULL DEFAULT 0,
                ErrorMessage TEXT
            )");

        // 创建索引
        await _connection.ExecuteAsync(@"
            CREATE INDEX IF NOT EXISTS IX_LocalMessages_Status 
            ON LocalMessages(Status)");

        await _connection.ExecuteAsync(@"
            CREATE INDEX IF NOT EXISTS IX_LocalMessages_CreatedAt 
            ON LocalMessages(CreatedAt)");

        _logger.LogInformation("数据库初始化完成");
    }
}
