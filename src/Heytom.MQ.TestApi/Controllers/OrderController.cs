using Heytom.MQ.TestApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace Heytom.MQ.TestApi.Controllers;

/// <summary>
/// 订单控制器 - 演示本地消息表的使用
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class OrderController : ControllerBase
{
    private readonly OrderService _orderService;
    private readonly ILogger<OrderController> _logger;

    public OrderController(OrderService orderService, ILogger<OrderController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    /// <summary>
    /// 创建订单（使用本地消息表）
    /// </summary>
    /// <remarks>
    /// 此接口演示如何使用本地消息表确保订单创建和消息发送的事务一致性。
    /// 
    /// 工作流程：
    /// 1. 开启数据库事务
    /// 2. 保存订单到 Orders 表
    /// 3. 保存消息到 LocalMessages 表
    /// 4. 提交事务（确保订单和消息同时成功或失败）
    /// 5. 发送消息到 RabbitMQ
    /// 6. 如果发送失败，后台服务会自动重试
    /// </remarks>
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderWithTransactionRequest request)
    {
        try
        {
            var orderId = await _orderService.CreateOrderWithTransactionAsync(
                request.CustomerId,
                request.CustomerName,
                request.Amount);

            return Ok(new
            {
                Success = true,
                Message = "订单创建成功（已使用本地消息表）",
                OrderId = orderId,
                Note = "消息已保存到本地消息表，即使 MQ 发送失败也会自动重试"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建订单失败");
            return StatusCode(500, new
            {
                Success = false,
                Message = "订单创建失败",
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// 批量创建订单（使用本地消息表）
    /// </summary>
    [HttpPost("batch")]
    public async Task<IActionResult> CreateOrdersBatch([FromBody] BatchCreateOrderWithTransactionRequest request)
    {
        try
        {
            var orders = Enumerable.Range(1, request.Count)
                .Select(i => (
                    CustomerId: $"CUST{Random.Shared.Next(1000, 9999)}",
                    CustomerName: $"{request.CustomerNamePrefix}_{i}",
                    Amount: request.BaseAmount + i * 10
                ))
                .ToList();

            var orderIds = await _orderService.CreateOrdersBatchWithTransactionAsync(orders);

            return Ok(new
            {
                Success = true,
                Message = $"批量创建 {orderIds.Count} 个订单成功",
                OrderIds = orderIds,
                Note = "所有消息已保存到本地消息表"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量创建订单失败");
            return StatusCode(500, new
            {
                Success = false,
                Message = "批量创建订单失败",
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// 获取所有订单
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllOrders()
    {
        var orders = await _orderService.GetAllOrdersAsync();
        return Ok(new
        {
            Success = true,
            Count = orders.Count,
            Orders = orders
        });
    }

    /// <summary>
    /// 获取订单详情
    /// </summary>
    [HttpGet("{orderId}")]
    public async Task<IActionResult> GetOrder(string orderId)
    {
        var order = await _orderService.GetOrderByIdAsync(orderId);
        
        if (order == null)
        {
            return NotFound(new
            {
                Success = false,
                Message = "订单不存在"
            });
        }

        return Ok(new
        {
            Success = true,
            Order = order
        });
    }

    /// <summary>
    /// 查看本地消息表
    /// </summary>
    /// <remarks>
    /// 查看本地消息表中的消息状态：
    /// - Status 0: 待发送
    /// - Status 1: 已发送
    /// - Status 2: 发送失败（会继续重试）
    /// </remarks>
    [HttpGet("local-messages")]
    public async Task<IActionResult> GetLocalMessages([FromQuery] int limit = 50)
    {
        var messages = await _orderService.GetLocalMessagesAsync(limit);
        return Ok(new
        {
            Success = true,
            Count = messages.Count,
            Messages = messages,
            StatusDescription = new
            {
                Pending = "0 - 待发送",
                Sent = "1 - 已发送",
                Failed = "2 - 发送失败（会重试）"
            }
        });
    }
}

public record CreateOrderWithTransactionRequest(string CustomerId, string CustomerName, decimal Amount);
public record BatchCreateOrderWithTransactionRequest(int Count, string CustomerNamePrefix, decimal BaseAmount);
