# 快速开始指南

## 1. 启动 RabbitMQ

```bash
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

## 2. 运行项目

```bash
cd src/Heytom.MQ.TestApi
dotnet run
```

## 3. 打开浏览器

访问: <http://localhost:5000>

## 4. 测试本地消息表功能

### 步骤 1: 创建订单

在 Swagger UI 中找到 `POST /api/order`，发送请求：

```json
{
  "customerId": "CUST001",
  "customerName": "张三",
  "amount": 299.99
}
```

### 步骤 2: 查看本地消息表

访问 `GET /api/order/local-messages`，你会看到：

```json
{
  "success": true,
  "count": 1,
  "messages": [
    {
      "id": "...",
      "mqType": "RabbitMQ",
      "topic": "heytom.test.exchange",
      "routingKey": "order.created",
      "messageType": "Heytom.MQ.TestApi.Events.OrderCreatedEvent",
      "status": 1,  // 1 = 已发送
      "createdAt": "2024-01-01T00:00:00Z",
      "retryCount": 0,
      "statusText": "已发送"
    }
  ]
}
```

### 步骤 3: 测试失败重试

1. 停止 RabbitMQ: `docker stop rabbitmq`
2. 创建新订单（会失败但订单已保存）
3. 查看本地消息表，状态为 `0`（待发送）
4. 启动 RabbitMQ: `docker start rabbitmq`
5. 等待 30 秒
6. 再次查看本地消息表，状态变为 `1`（已发送）

## 5. 查看订单

访问 `GET /api/order` 查看所有订单

## 6. 查看日志

控制台会输出详细的日志信息：

```text
[Information] 开始创建订单 - CustomerId: CUST001, Amount: 299.99
[Information] 订单已保存到数据库 - OrderId: abc123...
[Information] 订单创建完成 - OrderId: abc123...
[Information] [RabbitMQ] 已发送订单创建事件 - OrderId: abc123...
[Information] [RabbitMQ] 处理订单创建事件 - OrderId: abc123...
```

## 常见问题

### Q: RabbitMQ 连接失败？

A: 确保 RabbitMQ 已启动：`docker ps | grep rabbitmq`

### Q: 消息没有被重试？

A: 检查后台服务日志，确认 `LocalMessageRetryService` 已启动

### Q: 数据库文件在哪里？

A: 在项目根目录下的 `heytom_mq_test.db`
