using Heytom.MQ.Abstractions;
using Heytom.MQ.RabbitMQ;

namespace Heytom.MQ.TestApi.Events;

[RabbitMQTopic(RoutingKey = "user.created", Exchange = "heytom.test.exchange")]
public class UserCreatedEvent : EventBase
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
