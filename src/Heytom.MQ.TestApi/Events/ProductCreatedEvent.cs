using Heytom.MQ.Abstractions;
using Heytom.MQ.Kafka;

namespace Heytom.MQ.TestApi.Events;

[KafkaTopic(Topic = "product-events", GroupId = "product-service-group")]
public class ProductCreatedEvent : EventBase
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
