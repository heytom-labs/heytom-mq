using Heytom.MQ.Abstractions;
using Heytom.MQ.DependencyInjection;
using Heytom.MQ.Kafka;
using Heytom.MQ.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 配置 RabbitMQ
builder.Services.AddRabbitMQ(options =>
{
    options.ConnectionString = "amqp://guest:guest@localhost:5672";
    options.Exchange = "heytom.test.exchange";
    options.ExchangeType = "topic";
});

// 配置 Kafka
builder.Services.AddKafka(options =>
{
    options.ConnectionString = "localhost:9092";
    options.GroupId = "heytom-test-group";
    options.AutoOffsetReset = "earliest";
});

// 注册命名服务，用于区分不同的 MQ 实现
builder.Services.AddSingleton<RabbitMQProducer>();
builder.Services.AddSingleton<KafkaProducer>();

// 批量注册所有事件处理器
builder.Services.AddEventHandlersFromAssembly(typeof(Program));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.Run();
