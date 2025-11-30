using Heytom.MQ.Abstractions;
using Heytom.MQ.Kafka;
using Heytom.MQ.RabbitMQ;
using Heytom.MQ.TestApi.Events;
using Microsoft.AspNetCore.Mvc;

namespace Heytom.MQ.TestApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessageController : ControllerBase
{
    private readonly RabbitMQProducer _rabbitMQProducer;
    private readonly KafkaProducer _kafkaProducer;
    private readonly ILogger<MessageController> _logger;

    public MessageController(
        RabbitMQProducer rabbitMQProducer,
        KafkaProducer kafkaProducer,
        ILogger<MessageController> logger)
    {
        _rabbitMQProducer = rabbitMQProducer;
        _kafkaProducer = kafkaProducer;
        _logger = logger;
    }

    /// <summary>
    /// 发送用户创建事件 (RabbitMQ)
    /// </summary>
    [HttpPost("rabbitmq/user")]
    public async Task<IActionResult> SendUserCreatedEvent([FromBody] CreateUserRequest request)
    {
        var @event = new UserCreatedEvent
        {
            UserId = Random.Shared.Next(1000, 9999),
            Name = request.Name,
            Email = request.Email
        };

        await _rabbitMQProducer.SendAsync(@event);

        _logger.LogInformation("[RabbitMQ] 已发送用户创建事件 - UserId: {UserId}, EventId: {EventId}", @event.UserId, @event.EventId);

        return Ok(new
        {
            MQ = "RabbitMQ",
            Message = "用户创建事件已发送",
            EventId = @event.EventId,
            UserId = @event.UserId
        });
    }

    /// <summary>
    /// 发送订单创建事件 (RabbitMQ)
    /// </summary>
    [HttpPost("rabbitmq/order")]
    public async Task<IActionResult> SendOrderCreatedEvent([FromBody] CreateOrderRequest request)
    {
        var @event = new OrderCreatedEvent
        {
            OrderId = Guid.NewGuid().ToString("N"),
            Amount = request.Amount,
            CustomerName = request.CustomerName
        };

        await _rabbitMQProducer.SendAsync(@event);

        _logger.LogInformation("[RabbitMQ] 已发送订单创建事件 - OrderId: {OrderId}, EventId: {EventId}", @event.OrderId, @event.EventId);

        return Ok(new
        {
            MQ = "RabbitMQ",
            Message = "订单创建事件已发送",
            EventId = @event.EventId,
            OrderId = @event.OrderId
        });
    }

    /// <summary>
    /// 批量发送用户创建事件 (RabbitMQ)
    /// </summary>
    [HttpPost("rabbitmq/user/batch")]
    public async Task<IActionResult> SendBatchUserCreatedEvents([FromBody] BatchCreateUserRequest request)
    {
        var events = Enumerable.Range(1, request.Count).Select(i => new UserCreatedEvent
        {
            UserId = Random.Shared.Next(1000, 9999),
            Name = $"{request.NamePrefix}_{i}",
            Email = $"{request.NamePrefix}_{i}@example.com"
        }).ToList();

        await _rabbitMQProducer.SendBatchAsync(events);

        _logger.LogInformation("[RabbitMQ] 已批量发送 {Count} 个用户创建事件", events.Count);

        return Ok(new
        {
            MQ = "RabbitMQ",
            Message = $"已批量发送 {events.Count} 个用户创建事件",
            EventIds = events.Select(e => e.EventId).ToList()
        });
    }

    /// <summary>
    /// 发送产品创建事件 (Kafka)
    /// </summary>
    [HttpPost("kafka/product")]
    public async Task<IActionResult> SendProductCreatedEvent([FromBody] CreateProductRequest request)
    {
        var @event = new ProductCreatedEvent
        {
            ProductId = Guid.NewGuid().ToString("N"),
            ProductName = request.ProductName,
            Price = request.Price
        };

        await _kafkaProducer.SendAsync(@event);

        _logger.LogInformation("[Kafka] 已发送产品创建事件 - ProductId: {ProductId}, EventId: {EventId}", @event.ProductId, @event.EventId);

        return Ok(new
        {
            MQ = "Kafka",
            Message = "产品创建事件已发送",
            EventId = @event.EventId,
            ProductId = @event.ProductId
        });
    }

    /// <summary>
    /// 发送通知事件 (Kafka)
    /// </summary>
    [HttpPost("kafka/notification")]
    public async Task<IActionResult> SendNotificationEvent([FromBody] CreateNotificationRequest request)
    {
        var @event = new NotificationEvent
        {
            UserId = request.UserId,
            Title = request.Title,
            Message = request.Message,
            Type = request.Type
        };

        await _kafkaProducer.SendAsync(@event);

        _logger.LogInformation("[Kafka] 已发送通知事件 - UserId: {UserId}, EventId: {EventId}", @event.UserId, @event.EventId);

        return Ok(new
        {
            MQ = "Kafka",
            Message = "通知事件已发送",
            EventId = @event.EventId,
            UserId = @event.UserId
        });
    }

    /// <summary>
    /// 批量发送产品创建事件 (Kafka)
    /// </summary>
    [HttpPost("kafka/product/batch")]
    public async Task<IActionResult> SendBatchProductCreatedEvents([FromBody] BatchCreateProductRequest request)
    {
        var events = Enumerable.Range(1, request.Count).Select(i => new ProductCreatedEvent
        {
            ProductId = Guid.NewGuid().ToString("N"),
            ProductName = $"{request.ProductNamePrefix}_{i}",
            Price = request.BasePrice + i
        }).ToList();

        await _kafkaProducer.SendBatchAsync(events);

        _logger.LogInformation("[Kafka] 已批量发送 {Count} 个产品创建事件", events.Count);

        return Ok(new
        {
            MQ = "Kafka",
            Message = $"已批量发送 {events.Count} 个产品创建事件",
            EventIds = events.Select(e => e.EventId).ToList()
        });
    }
}

public record CreateUserRequest(string Name, string Email);
public record CreateOrderRequest(decimal Amount, string CustomerName);
public record BatchCreateUserRequest(int Count, string NamePrefix);
public record CreateProductRequest(string ProductName, decimal Price);
public record CreateNotificationRequest(string UserId, string Title, string Message, string Type = "Info");
public record BatchCreateProductRequest(int Count, string ProductNamePrefix, decimal BasePrice);
