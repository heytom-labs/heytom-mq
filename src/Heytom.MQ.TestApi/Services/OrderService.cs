using System.Data;
using System.Data.Common;
using Dapper;
using Heytom.MQ.Abstractions;
using Heytom.MQ.TestApi.Events;

namespace Heytom.MQ.TestApi.Services;

/// <summary>
/// 订单服务 - 演示本地消息表的使用
/// </summary>
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
    public async Task<string> CreateOrderWithTransactionAsync(
        string customerId,
        string customerName,
        decimal amount)
    {
        _logger.LogInformation("开始创建订单 - CustomerId: {CustomerId}, Amount: {Amount}", customerId, amount);

        string orderId = string.Empty;
        
        await _producer.SendWithTransactionAsync(_dbConnection, async (transaction) =>
        {
            // 1. 生成订单ID
            orderId = Guid.NewGuid().ToString("N");

            // 2. 保存订单到数据库（在事务中）
            await _dbConnection.ExecuteAsync(
                @"INSERT INTO Orders (Id, CustomerId, CustomerName, Amount, Status, CreatedAt) 
                  VALUES (@Id, @CustomerId, @CustomerName, @Amount, @Status, @CreatedAt)",
                new
                {
                    Id = orderId,
                    CustomerId = customerId,
                    CustomerName = customerName,
                    Amount = amount,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow.ToString("O")
                },
                transaction);

            _logger.LogInformation("订单已保存到数据库 - OrderId: {OrderId}", orderId);

            // 3. 返回要发送的消息
            return new OrderCreatedEvent
            {
                OrderId = orderId,
                Amount = amount,
                CustomerName = customerName
            };
        });

        _logger.LogInformation("订单创建完成 - OrderId: {OrderId}", orderId);

        return orderId;
    }

    /// <summary>
    /// 批量创建订单（使用本地消息表）
    /// </summary>
    public async Task<List<string>> CreateOrdersBatchWithTransactionAsync(
        List<(string CustomerId, string CustomerName, decimal Amount)> orders)
    {
        _logger.LogInformation("开始批量创建 {Count} 个订单", orders.Count);

        var orderIds = new List<string>();

        await _producer.SendBatchWithTransactionAsync<OrderCreatedEvent>(_dbConnection, async (transaction) =>
        {
            var orderEvents = new List<OrderCreatedEvent>();

            foreach (var (customerId, customerName, amount) in orders)
            {
                var orderId = Guid.NewGuid().ToString("N");
                orderIds.Add(orderId);

                // 保存订单
                await _dbConnection.ExecuteAsync(
                    @"INSERT INTO Orders (Id, CustomerId, CustomerName, Amount, Status, CreatedAt) 
                      VALUES (@Id, @CustomerId, @CustomerName, @Amount, @Status, @CreatedAt)",
                    new
                    {
                        Id = orderId,
                        CustomerId = customerId,
                        CustomerName = customerName,
                        Amount = amount,
                        Status = "Pending",
                        CreatedAt = DateTime.UtcNow.ToString("O")
                    },
                    transaction);

                // 添加事件
                orderEvents.Add(new OrderCreatedEvent
                {
                    OrderId = orderId,
                    Amount = amount,
                    CustomerName = customerName
                });
            }

            return orderEvents;
        });

        _logger.LogInformation("批量订单创建完成 - 共 {Count} 个订单", orderIds.Count);

        return orderIds;
    }

    /// <summary>
    /// 获取所有订单
    /// </summary>
    public async Task<List<Order>> GetAllOrdersAsync()
    {
        var orders = await _dbConnection.QueryAsync<Order>(
            "SELECT * FROM Orders ORDER BY CreatedAt DESC");
        
        return orders.ToList();
    }

    /// <summary>
    /// 获取订单详情
    /// </summary>
    public async Task<Order?> GetOrderByIdAsync(string orderId)
    {
        return await _dbConnection.QueryFirstOrDefaultAsync<Order>(
            "SELECT * FROM Orders WHERE Id = @Id",
            new { Id = orderId });
    }

    /// <summary>
    /// 获取本地消息表中的消息
    /// </summary>
    public async Task<List<LocalMessageInfo>> GetLocalMessagesAsync(int limit = 50)
    {
        var messages = await _dbConnection.QueryAsync<LocalMessageInfo>(
            @"SELECT Id, MQType, Topic, RoutingKey, MessageType, Status, 
                     CreatedAt, UpdatedAt, RetryCount, ErrorMessage
              FROM LocalMessages 
              ORDER BY CreatedAt DESC 
              LIMIT @Limit",
            new { Limit = limit });

        return messages.ToList();
    }
}

/// <summary>
/// 订单实体
/// </summary>
public class Order
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}

/// <summary>
/// 本地消息信息
/// </summary>
public class LocalMessageInfo
{
    public string Id { get; set; } = string.Empty;
    public string MQType { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string? RoutingKey { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public int Status { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string? UpdatedAt { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }

    public string StatusText => Status switch
    {
        0 => "待发送",
        1 => "已发送",
        2 => "发送失败",
        _ => "未知"
    };
}
