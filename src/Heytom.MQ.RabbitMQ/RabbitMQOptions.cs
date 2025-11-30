using Heytom.MQ.Abstractions;

namespace Heytom.MQ.RabbitMQ;

/// <summary>
/// RabbitMQ配置选项
/// </summary>
public class RabbitMQOptions : MQOptions
{
    public string VirtualHost { get; set; } = "/";
    public string Exchange { get; set; } = "heytom.exchange";
    public string ExchangeType { get; set; } = "topic";
    public bool Durable { get; set; } = true;
    public bool AutoDelete { get; set; } = false;
}
