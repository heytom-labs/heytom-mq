using Heytom.MQ.Abstractions;
using Heytom.MQ.RabbitMQ;

namespace Heytom.MQ.TestApi.Events;

[RabbitMQTopic(RoutingKey = "order.created", Exchange = "heytom.test.exchange")]
public class OrderCreatedEvent : EventBase
{
    public string OrderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string CustomerName { get; set; } = string.Empty;
}

