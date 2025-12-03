# 本地消息表使用指南

## 概述

本地消息表模式用于解决分布式事务中的消息可靠性问题。通过将消息先保存到本地数据库表中，与业务操作在同一个事务中提交，确保消息不会丢失。

## 快速开始

### 1. 创建本地消息表

```sql
-- SQL Server
CREATE TABLE LocalMessages (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    MQType NVARCHAR(50) NOT NULL,
    Topic NVARCHAR(200) NOT NULL,
    RoutingKey NVARCHAR(200) NULL,
    MessageType NVARCHAR(500) NOT NULL,
    MessageBody NVARCHAR(MAX) NOT NULL,
    Status INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    RetryCount INT NOT NULL DEFAULT 0,
    ErrorMessage NVARCHAR(MAX) NULL
);

CREATE INDEX IX_LocalMessages_Status ON LocalMessages(Status);
CREATE INDEX IX_LocalMessages_CreatedAt ON LocalMessages(CreatedAt);
```

### 2. 配置服务

```csharp
using System.Data.SqlClient;
using Heytom.MQ.Abstractions;
using Heytom.MQ.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// 配置消息队列（RabbitMQ 或 Kafka）
builder.Services.AddRabbitMQ(options =>
{
    options.ConnectionString = "amqp://guest:guest@localhost:5672";
    options.Exchange = "heytom.exchange";
    options.LocalMessageTableName = "LocalMessages"; // 本地消息表名
});

// 配置本地消息重试服务
builder.Services.AddLocalMessageRetryService(options =>
{
    options.Enabled = true;
    options.ScanInterval = TimeSpan.FromSeconds(30);
    options.BatchSize = 100;
    options.MaxRetryCount = 5;
    options.TableName = "LocalMessages";
    
    // 配置数据库连接工厂
    options.DbConnectionFactory = serviceProvider =>
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        return new SqlConnection(connectionString);
    };
});

var app = builder.Build();
app.Run();
```

### 3. 在业务代码中使用

#### 方式一：使用 IDbConnection（推荐）

```csharp
using System.Data;
using Dapper;
using Heytom.MQ.Abstractions;

public class OrderService
{
    private readonly IDbConnection _dbConnection;
    private readonly IMessageProducer _producer;

    public OrderService(IDbConnection dbConnection, IMessageProducer producer)
    {
        _dbConnection = dbConnection;
        _producer = producer;
    }

    public async Task CreateOrderAsync(CreateOrderRequest request)
    {
        await _producer.SendWithTransactionAsync(_dbConnection, async (transaction) =>
        {
            // 1. 执行业务逻辑（使用 Dapper）
            var orderId = Guid.NewGuid();
            await _dbConnection.ExecuteAsync(
                @"INSERT INTO Orders (Id, CustomerId, Amount, CreatedAt) 
                  VALUES (@Id, @CustomerId, @Amount, @CreatedAt)",
                new
                {
                    Id = orderId,
                    CustomerId = request.CustomerId,
                    Amount = request.Amount,
                    CreatedAt = DateTime.UtcNow
                },
                transaction);

            // 2. 返回要发送的消息
            return new OrderCreatedEvent
            {
                OrderId = orderId.ToString(),
                CustomerId = request.CustomerId,
                Amount = request.Amount
            };
        });
    }
}
```

#### 方式二：使用原生 ADO.NET

```csharp
using System.Data;
using System.Data.SqlClient;
using Heytom.MQ.Abstractions;

public class OrderService
{
    private readonly string _connectionString;
    private readonly IMessageProducer _producer;

    public OrderService(IConfiguration configuration, IMessageProducer producer)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
        _producer = producer;
    }

    public async Task CreateOrderAsync(CreateOrderRequest request)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await _producer.SendWithTransactionAsync(connection, async (transaction) =>
        {
            // 执行业务逻辑
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
                INSERT INTO Orders (Id, CustomerId, Amount, CreatedAt) 
                VALUES (@Id, @CustomerId, @Amount, @CreatedAt)";
            
            var orderId = Guid.NewGuid();
            command.Parameters.AddWithValue("@Id", orderId);
            command.Parameters.AddWithValue("@CustomerId", request.CustomerId);
            command.Parameters.AddWithValue("@Amount", request.Amount);
            command.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);
            
            await command.ExecuteNonQueryAsync();

            // 返回消息
            return new OrderCreatedEvent
            {
                OrderId = orderId.ToString(),
                Amount = request.Amount
            };
        });
    }
}
```

### 4. 批量发送消息

```csharp
public async Task CreateMultipleOrdersAsync(List<CreateOrderRequest> requests)
{
    await _producer.SendBatchWithTransactionAsync(_dbConnection, async (transaction) =>
    {
        var events = new List<OrderCreatedEvent>();

        foreach (var request in requests)
        {
            var orderId = Guid.NewGuid();
            
            // 保存订单
            await _dbConnection.ExecuteAsync(
                @"INSERT INTO Orders (Id, CustomerId, Amount, CreatedAt) 
                  VALUES (@Id, @CustomerId, @Amount, @CreatedAt)",
                new
                {
                    Id = orderId,
                    CustomerId = request.CustomerId,
                    Amount = request.Amount,
                    CreatedAt = DateTime.UtcNow
                },
                transaction);

            // 添加事件
            events.Add(new OrderCreatedEvent
            {
                OrderId = orderId.ToString(),
                Amount = request.Amount
            });
        }

        return events;
    });
}
```

## 工作原理

1. **开启事务**：调用 `SendWithTransactionAsync` 时，框架会自动开启数据库事务
2. **执行业务逻辑**：在委托函数中执行业务操作（如保存订单）
3. **保存消息到本地表**：框架自动将消息序列化并保存到本地消息表
4. **提交事务**：如果一切正常，提交事务（业务数据和消息记录同时提交）
5. **发送消息**：事务提交后，立即尝试发送消息到 MQ
6. **重试机制**：如果发送失败，后台服务会定期扫描本地消息表并重试

## 消息状态

- `0`: 待发送
- `1`: 已发送
- `2`: 发送失败（会继续重试）

## 配置选项

```csharp
public class LocalMessageRetryOptions
{
    // 扫描间隔（默认：30秒）
    public TimeSpan ScanInterval { get; set; } = TimeSpan.FromSeconds(30);

    // 每次扫描的批量大小（默认：100）
    public int BatchSize { get; set; } = 100;

    // 最大重试次数（默认：5次）
    public int MaxRetryCount { get; set; } = 5;

    // 本地消息表名称（默认：LocalMessages）
    public string TableName { get; set; } = "LocalMessages";

    // 是否启用重试服务（默认：true）
    public bool Enabled { get; set; } = true;

    // 数据库连接工厂（必需）
    public Func<IServiceProvider, IDbConnection>? DbConnectionFactory { get; set; }
}
```

## 注意事项

1. **必须配置数据库连接**：需要通过 `DbConnectionFactory` 配置数据库连接工厂
2. **事务范围**：业务操作和消息保存必须在同一个事务中
3. **幂等性**：消息处理器应该实现幂等性，因为消息可能会被重复发送
4. **性能考虑**：定期清理已发送的历史消息，避免表过大影响性能
5. **监控告警**：建议监控失败消息数量，及时发现问题

## 数据库支持

支持任何 ADO.NET 兼容的数据库：

- SQL Server
- MySQL
- PostgreSQL
- SQLite
- Oracle
- 等等

只需提供相应的 `IDbConnection` 实现即可。

## 示例：MySQL

```csharp
using MySql.Data.MySqlClient;

builder.Services.AddLocalMessageRetryService(options =>
{
    options.DbConnectionFactory = serviceProvider =>
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        return new MySqlConnection(connectionString);
    };
});
```

## 示例：PostgreSQL

```csharp
using Npgsql;

builder.Services.AddLocalMessageRetryService(options =>
{
    options.DbConnectionFactory = serviceProvider =>
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        return new NpgsqlConnection(connectionString);
    };
});
```
