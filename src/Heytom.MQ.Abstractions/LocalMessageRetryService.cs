using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Heytom.MQ.Abstractions;

/// <summary>
/// 本地消息表重试后台服务
/// </summary>
public class LocalMessageRetryService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LocalMessageRetryService> _logger;
    private readonly LocalMessageRetryOptions _options;

    public LocalMessageRetryService(
        IServiceProvider serviceProvider,
        ILogger<LocalMessageRetryService> logger,
        LocalMessageRetryOptions options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("本地消息重试服务已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理待发送消息时发生错误");
            }

            await Task.Delay(_options.ScanInterval, stoppingToken);
        }

        _logger.LogInformation("本地消息重试服务已停止");
    }

    /// <summary>
    /// 根据类型名称获取类型（支持跨程序集）
    /// </summary>
    private static Type? GetTypeFromName(string typeName)
    {
        // 首先尝试直接获取（如果包含程序集信息）
        var type = Type.GetType(typeName);
        if (type != null)
        {
            return type;
        }

        // 如果失败，遍历所有已加载的程序集查找
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType(typeName);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        // 使用工厂函数创建数据库连接
        IDbConnection? dbConnection = null;
        if (_options.DbConnectionFactory != null)
        {
            dbConnection = _options.DbConnectionFactory(scope.ServiceProvider);
        }
        else
        {
            // 尝试从 DI 容器获取（向后兼容）
            dbConnection = scope.ServiceProvider.GetService<IDbConnection>();
        }

        if (dbConnection == null)
        {
            _logger.LogWarning("未找到 IDbConnection，请在 LocalMessageRetryOptions 中配置 DbConnectionFactory 或在 DI 容器中注册 IDbConnection");
            return;
        }

        var repository = new LocalMessageRepository(_options.TableName);
        var producer = scope.ServiceProvider.GetService<IMessageProducer>();

        if (producer == null)
        {
            _logger.LogWarning("未找到 IMessageProducer，跳过本次扫描");
            return;
        }

        try
        {
            // 获取待发送的消息
            var pendingMessages = await repository.GetPendingMessagesAsync(
                dbConnection,
                _options.BatchSize,
                cancellationToken);

            if (pendingMessages.Count == 0)
            {
                return;
            }

            _logger.LogInformation("发现 {Count} 条待发送消息", pendingMessages.Count);

            foreach (var message in pendingMessages)
            {
                // 检查重试次数
                if (message.RetryCount >= _options.MaxRetryCount)
                {
                    _logger.LogWarning(
                        "消息 {MessageId} 已达到最大重试次数 {MaxRetryCount}，跳过",
                        message.Id,
                        _options.MaxRetryCount);
                    continue;
                }

                try
                {
                    await RetryMessageAsync(producer, repository, dbConnection, message, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "重试消息 {MessageId} 失败: {ErrorMessage}",
                        message.Id,
                        ex.Message);

                    // 更新状态为发送失败
                    await repository.UpdateStatusAsync(
                            dbConnection,
                            message.Id,
                            2, // 发送失败
                            ex.Message,
                            cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "扫描待发送消息时发生错误");
        }
    }

    private async Task RetryMessageAsync(
        IMessageProducer producer,
        ILocalMessageRepository repository,
        IDbConnection dbConnection,
        LocalMessage message,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "正在重试消息 {MessageId}, MQType: {MQType}, Topic: {Topic}, RetryCount: {RetryCount}",
            message.Id,
            message.MQType,
            message.Topic,
            message.RetryCount);

        // 这里需要根据 MessageType 反序列化消息
        // 由于泛型限制，这里使用反射来调用 SendAsync 方法
        var messageType = GetTypeFromName(message.MessageType);
        if (messageType == null)
        {
            throw new InvalidOperationException($"无法找到消息类型: {message.MessageType}");
        }

        var deserializedMessage = System.Text.Json.JsonSerializer.Deserialize(
            message.MessageBody,
            messageType);

        if (deserializedMessage == null)
        {
            throw new InvalidOperationException($"消息反序列化失败: {message.MessageType}");
        }

        // 使用反射调用 SendAsync 方法
        var sendAsyncMethod = producer.GetType()
            .GetMethod(nameof(IMessageProducer.SendAsync))
            ?.MakeGenericMethod(messageType);

        if (sendAsyncMethod == null)
        {
            throw new InvalidOperationException("无法找到 SendAsync 方法");
        }

        var sendTask = (Task?)sendAsyncMethod.Invoke(
            producer,
            [deserializedMessage, cancellationToken]);

        if (sendTask != null)
        {
            await sendTask;
        }

        // 更新状态为已发送
        await repository.UpdateStatusAsync(
               dbConnection,
               message.Id,
               1, // 已发送
               null,
               cancellationToken);

        _logger.LogInformation("消息 {MessageId} 重试发送成功", message.Id);
    }
}
