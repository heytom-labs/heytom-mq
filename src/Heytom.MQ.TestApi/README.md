# Heytom.MQ.TestApi - 测试项目

这是一个用于测试 Heytom.MQ 框架的 ASP.NET Core Web API 项目。

## 前置条件

### 1. 启动 RabbitMQ

```bash
# 使用 Docker 快速启动 RabbitMQ
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management

# 访问管理界面
# http://localhost:15672
# 默认用户名/密码: guest/guest
```

### 2. 启动 Kafka

```bash
# 使用 Docker Compose 启动 Kafka
# 创建 docker-compose.yml 文件
version: '3'
services:
  zookeeper:
    image: confluentinc/cp-zookeeper:latest
    environment:
      ZOOKEEPER_CLIENT_PORT: 2181
      ZOOKEEPER_TICK_TIME: 2000
    ports:
      - "2181:2181"

  kafka:
    image: confluentinc/cp-kafka:latest
    depends_on:
      - zookeeper
    ports:
      - "9092:9092"
    environment:
      KAFKA_BROKER_ID: 1
      KAFKA_ZOOKEEPER_CONNECT: zookeeper:2181
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://localhost:9092
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 1

# 启动
docker-compose up -d
```

## 运行项目

```bash
cd Heytom.MQ.TestApi
dotnet run
```

项目启动后访问 Swagger UI：http://localhost:5000/swagger

## 测试接口

### RabbitMQ 接口

#### 1. 发送单个用户创建事件

```bash
POST http://localhost:5000/api/message/rabbitmq/user
Content-Type: application/json

{
  "name": "张三",
  "email": "zhangsan@example.com"
}
```

#### 2. 发送单个订单创建事件

```bash
POST http://localhost:5000/api/message/rabbitmq/order
Content-Type: application/json

{
  "amount": 999.99,
  "customerName": "李四"
}
```

#### 3. 批量发送用户创建事件

```bash
POST http://localhost:5000/api/message/rabbitmq/user/batch
Content-Type: application/json

{
  "count": 10,
  "namePrefix": "TestUser"
}
```

### Kafka 接口

#### 4. 发送产品创建事件

```bash
POST http://localhost:5000/api/message/kafka/product
Content-Type: application/json

{
  "productName": "iPhone 15",
  "price": 5999.00
}
```

#### 5. 发送通知事件

```bash
POST http://localhost:5000/api/message/kafka/notification
Content-Type: application/json

{
  "userId": "user123",
  "title": "系统通知",
  "message": "您有新的订单",
  "type": "Info"
}
```

#### 6. 批量发送产品创建事件

```bash
POST http://localhost:5000/api/message/kafka/product/batch
Content-Type: application/json

{
  "count": 10,
  "productNamePrefix": "Product",
  "basePrice": 100.00
}
```

## 查看效果

### 1. 控制台日志

发送消息后，查看控制台日志，可以看到：
- `[RabbitMQ]` 标记的 RabbitMQ 消息发送和处理日志
- `[Kafka]` 标记的 Kafka 消息发送和处理日志
- EventId、时间戳等信息

### 2. RabbitMQ 管理界面

访问 http://localhost:15672 (guest/guest)：
- Exchange: `heytom.test.exchange`
- Queues: `heytom.queue.user.created`, `heytom.queue.order.created`
- 消息流转情况

### 3. Kafka 监控

可以使用 Kafka 命令行工具查看：

```bash
# 查看所有 Topic
docker exec -it <kafka-container-id> kafka-topics --list --bootstrap-server localhost:9092

# 查看消费者组
docker exec -it <kafka-container-id> kafka-consumer-groups --list --bootstrap-server localhost:9092

# 查看消费者组详情
docker exec -it <kafka-container-id> kafka-consumer-groups --describe --group heytom-test-group --bootstrap-server localhost:9092
```

## 项目结构

```
Heytom.MQ.TestApi/
├── Controllers/
│   └── MessageController.cs              # API 控制器
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
└── Program.cs                            # 启动配置
```

## 注意事项

- 确保 RabbitMQ 和 Kafka 服务都在运行
- RabbitMQ 默认连接：`amqp://guest:guest@localhost:5672`
- Kafka 默认连接：`localhost:9092`
- 如需修改连接配置，请编辑 `Program.cs` 中的配置
- 项目同时支持两种 MQ，可以对比测试不同场景下的表现
