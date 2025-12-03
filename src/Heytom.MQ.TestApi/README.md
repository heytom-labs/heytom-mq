# Heytom.MQ.TestApi - 测试项目

这是一个用于测试 Heytom.MQ 框架的 ASP.NET Core Web API 项目，演示了基础消息发送和本地消息表的使用。

## 功能特性

### 1. 基础消息发送 (`/api/message`)
- ✅ RabbitMQ 消息发送（单条/批量）
- ✅ Kafka 消息发送（单条/批量）
- ✅ 事件处理器自动消费

### 2. 本地消息表 (`/api/order`)
- ✅ 事务性消息发送
- ✅ 订单创建与消息发送的原子性保证
- ✅ 自动重试机制
- ✅ 消息状态查询

## 前置条件

### 1. 启动 RabbitMQ

```bash
# 使用 Docker 快速启动 RabbitMQ
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management

# 访问管理界面: http://localhost:15672
# 默认用户名/密码: guest/guest
```

### 2. 启动 Kafka (可选)

```bash
# 使用 Docker 启动 Kafka
docker run -d --name kafka -p 9092:9092 \
  -e KAFKA_ADVERTISED_LISTENERS=PLAINTEXT://localhost:9092 \
  apache/kafka:latest
```

## 运行项目

```bash
cd src/Heytom.MQ.TestApi
dotnet run
```

项目启动后访问 Swagger UI: <http://localhost:5000>

## API 接口说明

### 基础消息发送

#### POST /api/message/rabbitmq/user
发送用户创建事件到 RabbitMQ

```json
{
  "name": "张三",
  "email": "zhangsan@example.com"
}
```

#### POST /api/message/rabbitmq/order
发送订单创建事件到 RabbitMQ

```json
{
  "amount": 199.99,
  "customerName": "李四"
}
```

#### POST /api/message/kafka/product
发送产品创建事件到 Kafka

```json
{
  "productName": "iPhone 15",
  "price": 5999.00
}
```

### 本地消息表（事务性消息）

#### POST /api/order
创建订单（使用本地消息表）

```json
{
  "customerId": "CUST001",
  "customerName": "王五",
  "amount": 299.99
}
```

**工作流程：**

1. 开启数据库事务
2. 保存订单到 `Orders` 表
3. 保存消息到 `LocalMessages` 表
4. 提交事务（确保订单和消息同时成功）
5. 发送消息到 RabbitMQ
6. 如果发送失败，后台服务会自动重试

#### POST /api/order/batch
批量创建订单

```json
{
  "count": 5,
  "customerNamePrefix": "客户",
  "baseAmount": 100.00
}
```

#### GET /api/order
获取所有订单

#### GET /api/order/{orderId}
获取订单详情

#### GET /api/order/local-messages
查看本地消息表

**消息状态：**

- `0`: 待发送
- `1`: 已发送
- `2`: 发送失败（会继续重试）

## 数据库

项目使用 SQLite 作为示例数据库，数据文件：`heytom_mq_test.db`

### 表结构

**Orders 表：**

```sql
CREATE TABLE Orders (
    Id TEXT PRIMARY KEY,
    CustomerId TEXT NOT NULL,
    CustomerName TEXT NOT NULL,
    Amount REAL NOT NULL,
    Status TEXT NOT NULL DEFAULT 'Pending',
    CreatedAt TEXT NOT NULL
);
```

**LocalMessages 表：**

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
```

## 配置说明

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=heytom_mq_test.db"
  }
}
```

### Program.cs 配置

```csharp
// 配置 RabbitMQ（支持本地消息表）
builder.Services.AddRabbitMQ(options =>
{
    options.ConnectionString = "amqp://guest:guest@localhost:5672";
    options.Exchange = "heytom.test.exchange";
    options.LocalMessageTableName = "LocalMessages";
});

// 配置本地消息重试服务
builder.Services.AddLocalMessageRetryService(options =>
{
    options.Enabled = true;
    options.ScanInterval = TimeSpan.FromSeconds(30);
    options.BatchSize = 100;
    options.MaxRetryCount = 5;
    options.TableName = "LocalMessages";
    
    options.DbConnectionFactory = serviceProvider =>
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("DefaultConnection")!;
        return new SqliteConnection(connectionString);
    };
});
```

## 测试场景

### 场景 1: 正常流程

1. 调用 `POST /api/order` 创建订单
2. 订单和消息同时保存成功
3. 消息立即发送到 RabbitMQ
4. 查看 `GET /api/order/local-messages`，消息状态为 `1`（已发送）

### 场景 2: MQ 发送失败

1. 停止 RabbitMQ 服务
2. 调用 `POST /api/order` 创建订单
3. 订单保存成功，消息保存到本地表
4. 消息发送失败，状态为 `0`（待发送）
5. 启动 RabbitMQ 服务
6. 等待 30 秒，后台服务自动重试
7. 查看消息状态变为 `1`（已发送）

### 场景 3: 批量操作

1. 调用 `POST /api/order/batch` 批量创建订单
2. 所有订单和消息在同一个事务中提交
3. 确保全部成功或全部失败

## 查看效果

### 1. 控制台日志

发送消息后，查看控制台日志，可以看到：

- `[RabbitMQ]` 标记的 RabbitMQ 消息发送和处理日志
- `[Kafka]` 标记的 Kafka 消息发送和处理日志
- EventId、时间戳等信息

### 2. RabbitMQ 管理界面

访问 <http://localhost:15672> (guest/guest)：

- Exchange: `heytom.test.exchange`
- Queues: `heytom.queue.user.created`, `heytom.queue.order.created`
- 消息流转情况

## 项目结构

```text
Heytom.MQ.TestApi/
├── Controllers/
│   ├── MessageController.cs              # 基础消息发送 API
│   └── OrderController.cs                # 本地消息表示例 API
├── Events/
│   ├── UserCreatedEvent.cs               # 用户创建事件 (RabbitMQ)
│   ├── OrderCreatedEvent.cs              # 订单创建事件 (RabbitMQ)
│   ├── ProductCreatedEvent.cs            # 产品创建事件 (Kafka)
│   └── NotificationEvent.cs              # 通知事件 (Kafka)
├── Handlers/
│   ├── UserCreatedEventHandler.cs        # 用户事件处理器
│   ├── OrderCreatedEventHandler.cs       # 订单事件处理器
│   ├── ProductCreatedEventHandler.cs     # 产品事件处理器
│   └── NotificationEventHandler.cs       # 通知事件处理器
├── Services/
│   ├── DatabaseInitializer.cs            # 数据库初始化
│   └── OrderService.cs                   # 订单服务（本地消息表示例）
└── Program.cs                            # 启动配置
```

## 故障排查

### 问题 1: RabbitMQ 连接失败

- 确认 RabbitMQ 服务已启动
- 检查连接字符串是否正确
- 查看日志中的错误信息

### 问题 2: 消息未被消费

- 确认事件处理器已注册
- 检查 RabbitMQ 管理界面的队列状态
- 查看应用程序日志

### 问题 3: 本地消息表重试不工作

- 确认 `LocalMessageRetryService` 已启用
- 检查 `DbConnectionFactory` 配置是否正确
- 查看后台服务日志

## 扩展示例

### 使用其他数据库

**SQL Server:**

```csharp
builder.Services.AddScoped<IDbConnection>(_ => 
    new SqlConnection(connectionString));

options.DbConnectionFactory = serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("DefaultConnection")!;
    return new SqlConnection(connectionString);
};
```

**MySQL:**

```csharp
builder.Services.AddScoped<IDbConnection>(_ => 
    new MySqlConnection(connectionString));

options.DbConnectionFactory = serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("DefaultConnection")!;
    return new MySqlConnection(connectionString);
};
```

## 相关文档

- [主文档](../../README.md)
- [本地消息表使用指南](../../LOCAL_MESSAGE_TABLE_USAGE.md)
