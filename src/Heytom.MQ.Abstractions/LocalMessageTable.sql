-- 本地消息表 SQL 脚本
-- 支持 RabbitMQ 和 Kafka
-- 表名可自定义，默认为 LocalMessages

CREATE TABLE LocalMessages (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    MQType NVARCHAR(50) NOT NULL,                    -- 消息队列类型 (RabbitMQ/Kafka)
    Topic NVARCHAR(200) NOT NULL,                    -- Topic/Exchange 名称
    RoutingKey NVARCHAR(200) NULL,                   -- 路由键/分区键
    MessageType NVARCHAR(500) NOT NULL,              -- 消息类型
    MessageBody NVARCHAR(MAX) NOT NULL,              -- 消息内容(JSON)
    Status INT NOT NULL DEFAULT 0,                   -- 消息状态 (0: 待发送, 1: 已发送, 2: 发送失败)
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),  -- 创建时间
    UpdatedAt DATETIME2 NULL,                        -- 更新时间
    RetryCount INT NOT NULL DEFAULT 0,               -- 重试次数
    ErrorMessage NVARCHAR(MAX) NULL                  -- 错误信息
);

-- 创建索引以提高查询性能
CREATE INDEX IX_LocalMessages_Status_CreatedAt ON LocalMessages(Status, CreatedAt);
CREATE INDEX IX_LocalMessages_MQType ON LocalMessages(MQType);
CREATE INDEX IX_LocalMessages_CreatedAt ON LocalMessages(CreatedAt);

-- 如果需要自定义表名，请将上述脚本中的 LocalMessages 替换为你的表名
-- 例如：CREATE TABLE MyCustomMessageTable (...)
