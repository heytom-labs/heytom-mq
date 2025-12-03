using Microsoft.Extensions.DependencyInjection;

namespace Heytom.MQ.Abstractions;

/// <summary>
/// 本地消息重试服务扩展方法
/// </summary>
public static class LocalMessageRetryServiceExtensions
{
    /// <summary>
    /// 添加本地消息重试服务
    /// </summary>
    public static IServiceCollection AddLocalMessageRetryService(
        this IServiceCollection services,
        Action<LocalMessageRetryOptions>? configure = null)
    {
        var options = new LocalMessageRetryOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);

        if (options.Enabled)
        {
            services.AddHostedService<LocalMessageRetryService>();
        }

        return services;
    }
}
