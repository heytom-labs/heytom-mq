using Heytom.MQ.Abstractions;
using Heytom.MQ.Kafka;
using Heytom.MQ.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Heytom.MQ.DependencyInjection;

/// <summary>
/// 服务集合扩展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加RabbitMQ消息队列服务
    /// </summary>
    public static IServiceCollection AddRabbitMQ(
        this IServiceCollection services,
        Action<RabbitMQOptions> configureOptions)
    {
        if (configureOptions == null)
            throw new ArgumentNullException(nameof(configureOptions));

        var options = new RabbitMQOptions
        {
            ConnectionString = string.Empty
        };
        configureOptions(options);

        // 验证必填配置
        if (string.IsNullOrEmpty(options.ConnectionString))
            throw new InvalidOperationException("RabbitMQ ConnectionString 不能为空");

        services.AddSingleton(options);
        services.AddSingleton<IMessageProducer, RabbitMQProducer>();
        services.AddSingleton<RabbitMQConsumer>();
        services.AddSingleton<IMessageConsumer, RabbitMQConsumer>();
        services.AddHostedService<RabbitMQHostedService>();

        return services;
    }

    /// <summary>
    /// 添加Kafka消息队列服务
    /// </summary>
    public static IServiceCollection AddKafka(
        this IServiceCollection services,
        Action<KafkaOptions> configureOptions)
    {
        if (configureOptions == null)
            throw new ArgumentNullException(nameof(configureOptions));

        var options = new KafkaOptions
        {
            ConnectionString = string.Empty
        };
        configureOptions(options);

        // 验证必填配置
        if (string.IsNullOrEmpty(options.ConnectionString))
            throw new InvalidOperationException("Kafka ConnectionString 不能为空");

        services.AddSingleton(options);
        services.AddSingleton<IMessageProducer, KafkaProducer>();
        services.AddSingleton<KafkaConsumer>();
        services.AddSingleton<IMessageConsumer, KafkaConsumer>();
        services.AddHostedService<KafkaHostedService>();

        return services;
    }

    /// <summary>
    /// 注册事件处理器
    /// </summary>
    public static IServiceCollection AddEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : class, IEvent
        where THandler : class, IEventHandler<TEvent>
    {
        services.TryAddTransient<THandler>();
        services.AddSingleton<EventHandlerRegistration>(sp => new EventHandlerRegistration
        {
            EventType = typeof(TEvent),
            HandlerType = typeof(THandler)
        });

        return services;
    }

    /// <summary>
    /// 批量注册程序集中的所有事件处理器
    /// </summary>
    public static IServiceCollection AddEventHandlersFromAssembly(
        this IServiceCollection services,
        params Type[] assemblyMarkerTypes)
    {
        foreach (var markerType in assemblyMarkerTypes)
        {
            var assembly = markerType.Assembly;
            var handlerTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.GetInterfaces()
                    .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>)));

            foreach (var handlerType in handlerTypes)
            {
                var eventHandlerInterface = handlerType.GetInterfaces()
                    .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>));
                var eventType = eventHandlerInterface.GetGenericArguments()[0];

                services.TryAddTransient(handlerType);
                services.AddSingleton(new EventHandlerRegistration
                {
                    EventType = eventType,
                    HandlerType = handlerType
                });
            }
        }

        return services;
    }
}
