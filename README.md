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

框架支持本地消息表功能，确保业务操作和消息发送的事务一致性。消息会先保存到本地数据库，然后异步发送到消息队列。

### 快速开始

**1. 创建本地消息表**

执行 SQL 脚本（位于 `src/Heytom.MQ.Abstractions/LocalMessageTable.sql`）：

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

CREATE INDEX IX_LocalMessages_Status_CreatedAt ON LocalMessages(Status, CreatedAt);
```

**2. 配置自定义表名（可选）**

```csharp
builder.Services.AddRabbitMQ(options =>
{
    options.ConnectionString = "amqp://localhost";
    options.Exchange = "my-exchange";
    options.LocalMessageTableName = "MyCustomMessageTable";  // 自定义表名
});
```

**3. 使用事务发送消息**

```csharp
public class OrderService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IMessageProducer _producer;

    public async Task CreateOrderAsync(CreateOrderRequest request)
    {
        // 在事务中发送消息
        await _producer.SendWithTransactionAsync(_dbContext, async () =>
        {
            // 执行业务逻辑
            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = request.CustomerId,
                Amount = request.Amount
            };
            
            _dbContext.Orders.Add(order);
            await _dbContext.SaveChangesAsync();
            
            // 返回要发送的消息
            return new OrderCreatedEvent
            {
                OrderId = order.Id,
                Amount = order.Amount
            };
        });
    }
}
```

**工作流程：**
1. 开启数据库事务
2. 执行业务逻辑（如保存订单）
3. 保存消息到本地消息表
4. 提交事务（确保业务数据和消息记录一致性）
5. 发送消息到 MQ
6. 如果发送失败，消息已在本地表中，可通过重试机制处理

**详细文档：**
- [使用指南](LOCAL_MESSAGE_TABLE_USAGE.md)
- [架构说明](LOCAL_MESSAGE_TABLE_ARCHITECTURE.md)

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
    Task SendWithTransactionAsync<T>(DbContext dbContext, Func<Task<T>> messageFunc, CancellationToken cancellationToken = default) where T : class, IEvent;
    
    // 在事务中批量发送消息到本地消息表
    Task SendBatchWithTransactionAsync<T>(DbContext dbContext, Func<Task<IEnumerable<T>>> messageFunc, CancellationToken cancellationToken = default) where T : class, IEvent;
}
```

### ILocalMessageRepository 接口

```csharp
public interface ILocalMessageRepository
{
    // 保存消息到本地表
    Task SaveAsync(DbContext dbContext, LocalMessage message, CancellationToken cancellationToken = default);
    
    // 批量保存消息
    Task SaveBatchAsync(DbContext dbContext, IEnumerable<LocalMessage> messages, CancellationToken cancellationToken = default);
    
    // 更新消息状态
    Task UpdateStatusAsync(DbContext dbContext, Guid messageId, int status, string? errorMessage = null, CancellationToken cancellationToken = default);
    
    // 获取待发送的消息
    Task<List<LocalMessage>> GetPendingMessagesAsync(DbContext dbContext, int batchSize = 100, CancellationToken cancellationToken = default);
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
