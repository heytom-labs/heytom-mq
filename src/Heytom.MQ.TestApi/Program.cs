using System.Data;
using Heytom.MQ.Abstractions;
using Heytom.MQ.DependencyInjection;
using Heytom.MQ.Kafka;
using Heytom.MQ.RabbitMQ;
using Heytom.MQ.TestApi.Services;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Heytom.MQ Test API",
        Version = "v1",
        Description = "消息队列测试 API - 演示 RabbitMQ、Kafka 和本地消息表的使用"
    });
    
    // 启用 XML 注释
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// 配置数据库连接（使用 SQLite 作为示例）
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddScoped<IDbConnection>(_ => new SqliteConnection(connectionString));

// 配置 Kafka
builder.Services.AddKafka(options =>
{
    options.ConnectionString = "localhost:9092";
    options.GroupId = "heytom-test-group";
    options.AutoOffsetReset = "earliest";
});

// 配置 RabbitMQ（支持本地消息表）
builder.Services.AddRabbitMQ(options =>
{
    options.ConnectionString = "amqp://guest:guest@localhost:5672";
    options.Exchange = "heytom.test.exchange";
    options.ExchangeType = "topic";
    options.LocalMessageTableName = "LocalMessages"; // 配置本地消息表名
});


// 配置本地消息重试服务
builder.Services.AddLocalMessageRetryService(options =>
{
    options.Enabled = true;
    options.ScanInterval = TimeSpan.FromSeconds(30);  // 每30秒扫描一次
    options.BatchSize = 100;                          // 每次处理100条消息
    options.MaxRetryCount = 5;                        // 最多重试5次
    options.TableName = "LocalMessages";              // 本地消息表名
    
    // 配置数据库连接工厂
    options.DbConnectionFactory = serviceProvider =>
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var connString = configuration.GetConnectionString("DefaultConnection")!;
        return new SqliteConnection(connString);
    };
});

// 注册命名服务，用于区分不同的 MQ 实现
builder.Services.AddSingleton<RabbitMQProducer>();
builder.Services.AddSingleton<KafkaProducer>();

// 注册业务服务
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<DatabaseInitializer>();

// 批量注册所有事件处理器
builder.Services.AddEventHandlersFromAssembly(typeof(Program));

var app = builder.Build();

// 初始化数据库
using (var scope = app.Services.CreateScope())
{
    var dbInitializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await dbInitializer.InitializeAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Heytom.MQ Test API v1");
        options.RoutePrefix = string.Empty; // 设置 Swagger UI 为根路径
    });
}

app.UseAuthorization();
app.MapControllers();

app.Logger.LogInformation("===========================================");
app.Logger.LogInformation("Heytom.MQ Test API 已启动");
app.Logger.LogInformation("Swagger UI: http://localhost:5000");
app.Logger.LogInformation("===========================================");
app.Logger.LogInformation("功能演示：");
app.Logger.LogInformation("1. 基础消息发送: /api/message");
app.Logger.LogInformation("2. 本地消息表: /api/order");
app.Logger.LogInformation("3. 查看本地消息: /api/order/local-messages");
app.Logger.LogInformation("===========================================");

app.Run();
