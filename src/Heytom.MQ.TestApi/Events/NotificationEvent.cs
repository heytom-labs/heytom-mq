using Heytom.MQ.Abstractions;
using Heytom.MQ.Kafka;

namespace Heytom.MQ.TestApi.Events;

[KafkaTopic(Topic = "notification-events")]
public class NotificationEvent : EventBase
{
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "Info";
}
