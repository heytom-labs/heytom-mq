# Heytom.MQ - 消息队列处理框架

基于 .NET 8 的统一消息队列处理框架，支持 RabbitMQ 和 Kafka。

## 项目结构

- **Heytom.MQ.Abstractions**: 抽象接口层，定义统一的消息生产者和消费者接口
- **Heytom.MQ.RabbitMQ**: RabbitMQ 实现
- **Heytom.MQ.Kafka**: Kafka 实现
- **Heytom.MQ.DependencyInjection**: 依赖注入扩展，简化服务注册和处理器配置

## 核心接口

### IMessageProducer
消息生产者接口，支持单条和批量发送消息。

### IMessageConsumer
消息消费者接口，支持订阅、取消订阅和消息处理。

## 使用示例

### 1. 定义事件消息

所有事件消息必须实现 `IEvent` 接口，推荐继承 `EventBase` 基类。

**Kafka 事件** - 使用 `[KafkaTopic]` 特性：

```csharp
using Heytom.MQ.Abstractions;
using Heytom.MQ.Kafka;

// 基础配置
[KafkaTopic(Topic = "user-events")]
public class UserCreatedEvent : EventBase
{
    public int UserId { get; set; }
    public string Name { get; set; }
}

// 高级配置（支持分区、压缩、消费组等）
[KafkaTopic(
    Topic = "order-events",
    GroupId = "order-service-group",
    PartitionKeyProperty = nameof(OrderCreatedEvent.OrderId),  // 使用OrderId作为分区键
    NumPartitions = 3,
    ReplicationFactor = 2,
    RetentionMs = 86400000,  // 保留1天
    CompressionType = "gzip",
    EnableIdempotence = true,
    Acks = "all",
    AutoOffsetReset = "earliest",
    EnableAutoCommit = false,
    SessionTimeoutMs = 10000,
    MaxPollRecords = 500)]
public class OrderCreatedEvent : EventBase
{
    public string OrderId { get; set; }
    public decimal Amount { get; set; }
}
```

**RabbitMQ 事件** - 使用 `[RabbitMQTopic]` 特性：

```csharp
using Heytom.MQ.Abstractions;
using Heytom.MQ.RabbitMQ;

// 基础配置
[RabbitMQTopic(RoutingKey = "user.created", Exchange = "heytom.exchange")]
public class UserRegisteredEvent : EventBase
{
    public int UserId { get; set; }
    public string Email { get; set; }
}

// 高级配置（支持死信队列、TTL、优先级等）
[RabbitMQTopic(
    RoutingKey = "order.created",
    Exchange = "order.exchange",
    ExchangeType = "topic",
    QueueName = "order.created.queue",
    Durable = true,
    MessageTTL = 60000,  // 消息60秒过期
    MaxLength = 10000,   // 队列最大长度
    MaxPriority = 10,    // 支持优先级0-10
    DeadLetterExchange = "order.dlx",
    DeadLetterRoutingKey = "order.dead")]
public class OrderCreatedEvent : EventBase
{
    public string OrderId { get; set; }
    public decimal Amount { get; set; }
}
```

### 2. 定义事件处理器

实现 `IEventHandler<TEvent>` 接口：

```csharp
using Heytom.MQ.Abstractions;

public class UserCreatedEventHandler : IEventHandler<UserCreatedEvent>
{
    public async Task<bool> HandleAsync(UserCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"处理用户创建事件: {event.Name}, EventId: {event.EventId}");
        // 处理业务逻辑
        await Task.CompletedTask;
        return true; // 返回 true 表示处理成功
    }
}
```

### 3. 手动使用 RabbitMQ

```csharp
// 生产者
var options = new RabbitMQOptions
{
    ConnectionString = "amqp://guest:guest@localhost:5672",
    Exchange = "heytom.exchange"  // 默认 Exchange
};

using var producer = new RabbitMQProducer(options);
await producer.SendAsync(new UserRegisteredEvent 
{ 
    UserId = 123, 
    Email = "user@example.com" 
});

// 消费者 - 方式1：使用委托
using var consumer = new RabbitMQConsumer(options);
await consumer.StartAsync();
await consumer.SubscribeAsync<UserRegisteredEvent>(async (message) =>
{
    Console.WriteLine($"收到消息: {message.Email}");
    return true; // 返回 true 表示处理成功
});

// 消费者 - 方式2：使用事件处理器（推荐）
await consumer.SubscribeAsync<UserRegisteredEvent, UserRegisteredEventHandler>();
```

### 4. 使用依赖注入（推荐）

**安装包：**
```bash
dotnet add package Heytom.MQ.DependencyInjection
```

**Program.cs 配置：**

```csharp
using Heytom.MQ.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// 方式1：使用 RabbitMQ
builder.Services.AddRabbitMQ(options =>
{
    options.ConnectionString = "amqp://guest:guest@localhost:5672";
    options.Exchange = "heytom.exchange";
});

// 方式2：使用 Kafka
builder.Services.AddKafka(options =>
{
    options.ConnectionString = "localhost:9092";
    options.GroupId = "my-service-group";
});

// 注册单个事件处理器
builder.Services.AddEventHandler<UserCreatedEvent, UserCreatedEventHandler>();
builder.Services.AddEventHandler<OrderCreatedEvent, OrderCreatedEventHandler>();

// 或者批量注册程序集中的所有处理器（推荐）
builder.Services.AddEventHandlersFromAssembly(typeof(Program));

var app = builder.Build();
app.Run();
```

**定义事件和处理器：**

```csharp
// 事件定义
[RabbitMQTopic(RoutingKey = "user.created", Exchange = "heytom.exchange")]
public class UserCreatedEvent : EventBase
{
    public int UserId { get; set; }
    public string Name { get; set; }
}

// 处理器定义
public class UserCreatedEventHandler : IEventHandler<UserCreatedEvent>
{
    private readonly ILogger<UserCreatedEventHandler> _logger;

    public UserCreatedEventHandler(ILogger<UserCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    public async Task<bool> HandleAsync(UserCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("处理用户创建事件: UserId={UserId}, Name={Name}", @event.UserId, @event.Name);
        
        // 处理业务逻辑
        await Task.CompletedTask;
        
        return true; // 返回 true 表示处理成功
    }
}
```

**发送消息：**

```csharp
public class UserService
{
    private readonly IMessageProducer _producer;

    public UserService(IMessageProducer producer)
    {
        _producer = producer;
    }

    public async Task CreateUserAsync(string name)
    {
        var @event = new UserCreatedEvent
        {
            UserId = 123,
            Name = name
        };

        await _producer.SendAsync(@event);
    }
}
```

### 5. Kafka 使用

```csharp
// 生产者
var options = new KafkaOptions
{
    ConnectionString = "localhost:9092"
};

using var producer = new KafkaProducer(options);
await producer.SendAsync(new UserCreatedEvent 
{ 
    UserId = 123, 
    Name = "张三" 
});

// 消费者 - 方式1：使用委托
using var consumer = new KafkaConsumer(options);
await consumer.StartAsync();
await consumer.SubscribeAsync<UserCreatedEvent>(async (message) =>
{
    Console.WriteLine($"收到消息: {message.Name}");
    return true;
});

// 消费者 - 方式2：使用事件处理器（推荐）
await consumer.SubscribeAsync<UserCreatedEvent, UserCreatedEventHandler>();
```

## 本地消息表（事务性消息）

框架支持本地消息表模式，解决分布式事务中的消息可靠性问题。通过将消息先保存到本地数据库表中，与业务操作在同一个事务中提交，确保消息不会丢失。

### 核心特性

- ✅ **事务一致性**：业务数据和消息在同一事务中提交，保证原子性
- ✅ **自动重试**：后台服务定期扫描待发送消息，自动重试失败的消息
- ✅ **状态追踪**：实时查看消息状态（待发送、已发送、发送失败）
- ✅ **数据库无关**：支持任何 ADO.NET 兼容的数据库
- ✅ **高性能**：使用原生 SQL 操作，避免 EF Core 依赖

### 快速开始

#### 1. 创建本地消息表

**SQL Server:**
```sql
CREATE TABLE LocalMessages (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
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

**MySQL:**
```sql
CREATE TABLE LocalMessages (
    Id CHAR(36) PRIMARY KEY,
    MQType VARCHAR(50) NOT NULL,
    Topic VARCHAR(200) NOT NULL,
    RoutingKey VARCHAR(200),
    MessageType VARCHAR(500) NOT NULL,
    MessageBody TEXT NOT NULL,
    Status INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME,
    RetryCount INT NOT NULL DEFAULT 0,
    ErrorMessage TEXT
);

CREATE INDEX IX_LocalMessages_Status ON LocalMessages(Status);
CREATE INDEX IX_LocalMessages_CreatedAt ON LocalMessages(CreatedAt);
```

**PostgreSQL:**
```sql
CREATE TABLE LocalMessages (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    MQType VARCHAR(50) NOT NULL,
    Topic VARCHAR(200) NOT NULL,
    RoutingKey VARCHAR(200),
    MessageType VARCHAR(500) NOT NULL,
    MessageBody TEXT NOT NULL,
    Status INT NOT NULL DEFAULT 0,
    CreatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP,
    RetryCount INT NOT NULL DEFAULT 0,
    ErrorMessage TEXT
);

CREATE INDEX IX_LocalMessages_Status ON LocalMessages(Status);
CREATE INDEX IX_LocalMessages_CreatedAt ON LocalMessages(CreatedAt);
```

**SQLite:**
```sql
CREATE TABLE LocalMessages (
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
);

CREATE INDEX IX_LocalMessages_Status ON LocalMessages(Status);
CREATE INDEX IX_LocalMessages_CreatedAt ON LocalMessages(CreatedAt);
```

#### 2. 配置服务

```csharp
using System.Data.SqlClient;
using Heytom.MQ.Abstractions;
using Heytom.MQ.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// 配置数据库连接
builder.Services.AddScoped<IDbConnection>(_ => 
    new SqlConnection(builder.Configuration.GetConnectionString("DefaultConnection")));

// 配置 RabbitMQ（支持本地消息表）
builder.Services.AddRabbitMQ(options =>
{
    options.ConnectionString = "amqp://guest:guest@localhost:5672";
    options.Exchange = "my-exchange";
    options.LocalMessageTableName = "LocalMessages";  // 配置表名
});

// 配置本地消息重试服务
builder.Services.AddLocalMessageRetryService(options =>
{
    options.Enabled = true;
    options.ScanInterval = TimeSpan.FromSeconds(30);  // 每30秒扫描一次
    options.BatchSize = 100;                          // 每次处理100条消息
    options.MaxRetryCount = 5;                        // 最多重试5次
    options.TableName = "LocalMessages";
    
    // 配置数据库连接工厂
    options.DbConnectionFactory = serviceProvider =>
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("DefaultConnection")!;
        return new SqlConnection(connectionString);
    };
});
```

#### 3. 使用事务发送消息

```csharp
using System.Data;
using Dapper;
using Heytom.MQ.Abstractions;

public class OrderService
{
    private readonly IDbConnection _dbConnection;
    private readonly IMessageProducer _producer;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IDbConnection dbConnection,
        IMessageProducer producer,
        ILogger<OrderService> logger)
    {
        _dbConnection = dbConnection;
        _producer = producer;
        _logger = logger;
    }

    /// <summary>
    /// 创建订单（使用本地消息表确保事务一致性）
    /// </summary>
    public async Task<string> CreateOrderAsync(CreateOrderRequest request)
    {
        _logger.LogInformation("开始创建订单");

        string orderId = string.Empty;

        // 在事务中发送消息
        await _producer.SendWithTransactionAsync(_dbConnection, async (transaction) =>
        {
            // 1. 生成订单ID
            orderId = Guid.NewGuid().ToString();

            // 2. 保存订单到数据库（在事务中）
            await _dbConnection.ExecuteAsync(
                @"INSERT INTO Orders (Id, CustomerId, CustomerName, Amount, Status, CreatedAt) 
                  VALUES (@Id, @CustomerId, @CustomerName, @Amount, @Status, @CreatedAt)",
                new
                {
                    Id = orderId,
                    CustomerId = request.CustomerId,
                    CustomerName = request.CustomerName,
                    Amount = request.Amount,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                },
                transaction);

            _logger.LogInformation("订单已保存 - OrderId: {OrderId}", orderId);

            // 3. 返回要发送的消息
            return new OrderCreatedEvent
            {
                OrderId = orderId,
                CustomerName = request.CustomerName,
                Amount = request.Amount
            };
        });

        _logger.LogInformation("订单创建完成 - OrderId: {OrderId}", orderId);
        return orderId;
    }

    /// <summary>
    /// 批量创建订单
    /// </summary>
    public async Task<List<string>> CreateOrdersBatchAsync(List<CreateOrderRequest> requests)
    {
        var orderIds = new List<string>();

        await _producer.SendBatchWithTransactionAsync<OrderCreatedEvent>(
            _dbConnection, 
            async (transaction) =>
        {
            var events = new List<OrderCreatedEvent>();

            foreach (var request in requests)
            {
                var orderId = Guid.NewGuid().ToString();
                orderIds.Add(orderId);

                // 保存订单
                await _dbConnection.ExecuteAsync(
                    @"INSERT INTO Orders (Id, CustomerId, CustomerName, Amount, Status, CreatedAt) 
                      VALUES (@Id, @CustomerId, @CustomerName, @Amount, @Status, @CreatedAt)",
                    new
                    {
                        Id = orderId,
                        CustomerId = request.CustomerId,
                        CustomerName = request.CustomerName,
                        Amount = request.Amount,
                        Status = "Pending",
                        CreatedAt = DateTime.UtcNow
                    },
                    transaction);

                // 添加事件
                events.Add(new OrderCreatedEvent
                {
                    OrderId = orderId,
                    CustomerName = request.CustomerName,
                    Amount = request.Amount
                });
            }

            return events;
        });

        return orderIds;
    }
}
```

### 工作流程

```mermaid
sequenceDiagram
    participant App as 应用程序
    participant DB as 数据库
    participant LocalMsg as 本地消息表
    participant MQ as 消息队列
    participant BgService as 后台服务

    App->>DB: 1. 开启事务
    App->>DB: 2. 保存业务数据
    App->>LocalMsg: 3. 保存消息(Status=0)
    App->>DB: 4. 提交事务
    
    alt 发送成功
        App->>MQ: 5. 发送消息
        App->>LocalMsg: 6. 更新状态(Status=1)
    else 发送失败
        App->>LocalMsg: 消息保持Status=0
        BgService->>LocalMsg: 7. 定期扫描Status=0
        BgService->>MQ: 8. 重试发送
        BgService->>LocalMsg: 9. 更新状态
    end
```

**详细步骤：**

1. **开启数据库事务**：确保后续操作的原子性
2. **执行业务逻辑**：保存订单等业务数据
3. **保存消息到本地表**：消息状态为 0（待发送），包含完整的消息类型和内容
4. **提交事务**：业务数据和消息记录同时提交，保证一致性
5. **发送消息到 MQ**：事务提交后立即尝试发送
6. **更新消息状态**：
   - 发送成功 → 状态更新为 1（已发送）
   - 发送失败 → 状态保持为 0（待发送）
7. **后台服务重试**：定期扫描状态为 0 或 2 的消息，自动重试

### 消息状态说明

| 状态值 | 状态名称 | 说明 |
|--------|---------|------|
| 0 | 待发送 | 消息已保存，等待发送或重试 |
| 1 | 已发送 | 消息已成功发送到 MQ |
| 2 | 发送失败 | 消息发送失败，会继续重试 |

### 配置选项

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

### 最佳实践

1. **定期清理历史消息**
   ```sql
   -- 清理30天前已发送的消息
   DELETE FROM LocalMessages 
   WHERE Status = 1 
   AND CreatedAt < DATEADD(DAY, -30, GETUTCDATE());
   ```

2. **监控失败消息**
   ```sql
   -- 查询重试次数过多的消息
   SELECT * FROM LocalMessages 
   WHERE Status IN (0, 2) 
   AND RetryCount >= 5
   ORDER BY CreatedAt DESC;
   ```

3. **设置合理的重试间隔**
   - 高频业务：10-30秒
   - 一般业务：30-60秒
   - 低频业务：1-5分钟

4. **实现幂等性**
   - 消息处理器应该实现幂等性
   - 使用消息ID或业务ID去重

### 注意事项

- ⚠️ 必须配置 `DbConnectionFactory` 或在 DI 容器中注册 `IDbConnection`
- ⚠️ 消息类型使用 `AssemblyQualifiedName` 保存，确保跨程序集反序列化
- ⚠️ 定期清理已发送的历史消息，避免表过大影响性能
- ⚠️ 消息处理器应实现幂等性，因为消息可能被重复发送
- ⚠️ 建议在生产环境监控失败消息数量，及时发现问题

### 示例项目

完整的使用示例请参考：
- [TestApi 项目](src/Heytom.MQ.TestApi) - 包含完整的订单创建示例
- [使用指南](LOCAL_MESSAGE_TABLE_USAGE.md) - 详细的使用文档
- [快速开始](src/Heytom.MQ.TestApi/QUICKSTART.md) - 5分钟快速体验

## 特性

- 统一的抽象接口，方便切换不同的 MQ 实现
- 支持异步操作
- 支持批量发送
- 支持消息重试机制
- **支持本地消息表（事务性消息）**
- **使用原生 SQL 操作，性能更高**
- **支持自定义表名**
- 类型安全的消息处理
- 自动序列化/反序列化（JSON）
- 依赖注入支持，自动注册和启动消费者
- 支持批量扫描注册事件处理器
- 后台服务自动管理消费者生命周期

## API 参考

### IMessageProducer 接口

```csharp
public interface IMessageProducer
{
    // 发送单条消息
    Task SendAsync<T>(T message, CancellationToken cancellationToken = default) where T : class, IEvent;
    
    // 批量发送消息
    Task SendBatchAsync<T>(IEnumerable<T> messages, CancellationToken cancellationToken = default) where T : class, IEvent;
    
    // 在事务中发送消息到本地消息表
    Task SendWithTransactionAsync<T>(IDbConnection dbConnection, Func<IDbTransaction, Task<T>> messageFunc, CancellationToken cancellationToken = default) where T : class, IEvent;
    
    // 在事务中批量发送消息到本地消息表
    Task SendBatchWithTransactionAsync<T>(IDbConnection dbConnection, Func<IDbTransaction, Task<IEnumerable<T>>> messageFunc, CancellationToken cancellationToken = default) where T : class, IEvent;
}
```

### ILocalMessageRepository 接口

```csharp
public interface ILocalMessageRepository
{
    // 保存消息到本地表
    Task SaveAsync(IDbTransaction transaction, LocalMessage message, CancellationToken cancellationToken = default);
    
    // 批量保存消息
    Task SaveBatchAsync(IDbTransaction transaction, IEnumerable<LocalMessage> messages, CancellationToken cancellationToken = default);
    
    // 更新消息状态
    Task UpdateStatusAsync(IDbTransaction transaction, Guid messageId, int status, string? errorMessage = null, CancellationToken cancellationToken = default);
    
    // 获取待发送的消息
    Task<List<LocalMessage>> GetPendingMessagesAsync(IDbConnection connection, int batchSize = 100, CancellationToken cancellationToken = default);
}
```

## 依赖

- .NET 8.0
- RabbitMQ.Client 6.8.1
- Confluent.Kafka 2.3.0
- Microsoft.EntityFrameworkCore 8.0.22
- Microsoft.EntityFrameworkCore.Relational 8.0.22

## 许可证

MIT License
