# EventBus Channel Implementation Design

## Overview

实现基于 `System.Threading.Channels` 的轻量级事件总线，用于组件间解耦和异步消息传递。

## Architecture

```
EventBus (静态入口)
├── Topics: ConcurrentDictionary<string, TopicChannel>
├── Enabled: bool
└── Start/Stop/Dispose

TopicChannel (每个主题的通道)
├── Channel<EventMessage> (核心队列)
├── Subscribers: List<EventSubscriber>
└── DispatchLoop (后台分发任务)

EventSubscriber (订阅者)
├── Handler: Func<EventMessage, CancellationToken, Task>
├── RetryCount: int
├── RetryDelay: TimeSpan?
└── Id: string

EventMessage (消息)
├── Topic: string
├── Data: byte[]
├── Context: object (可选)
└── Timestamp: DateTime
```

## Components

### EventBus (静态类)
- `RegisterTopic(string topic, int capacity)` - 注册主题（可选）
- `Publish(string topic, byte[] data)` - 同步发布
- `PublishAsync(string topic, byte[] data)` - 异步发布
- `Subscribe(string topic, Func<EventMessage, Task> handler, int retryCount = 3)` - 订阅
- `Unsubscribe(string topic, string subscriberId)` - 取消订阅
- `Start()` - 启动分发
- `StopAsync()` - 停止

### TopicChannel
- 使用 `Channel.CreateBounded` 或 `Channel.CreateUnbounded`
- 持有订阅者列表
- 运行分发循环：从 Channel 读取，分发给所有订阅者

### EventSubscriber
- 消息处理函数
- 重试次数配置
- 重试延迟（指数退避：100ms, 200ms, 400ms...）

### EventMessage
- 主题名称
- 消息数据
- 可选上下文对象

## Data Flow

1. Publisher 调用 `Publish/PublishAsync`
2. 消息写入 Topic 的 Channel
3. DispatchLoop 从 Channel 读取消息
4. 分发给该 Topic 的所有订阅者
5. 每个订阅者独立处理：
   - 成功：完成
   - 失败：重试（最多 retryCount 次）
   - 最终失败：记录日志，丢弃

## Retry Strategy

- 默认重试 3 次
- 延迟策略：指数退避 (100ms * 2^attempt)
- 仅对异常重试，不捕获正常业务错误

## Error Handling

- Channel 写入失败：返回 false
- 订阅者异常：重试后丢弃
- 不实现死信队列（轻量级设计）

## Usage Example

```csharp
// 启动
EventBus.Start();

// 订阅
var subId = EventBus.Subscribe("user.created", async msg => {
    var user = Deserialize<User>(msg.Data);
    await ProcessUser(user);
}, retryCount: 3);

// 发布
EventBus.Publish("user.created", Serialize(user));

// 取消订阅
EventBus.Unsubscribe("user.created", subId);

// 停止
await EventBus.StopAsync();
```

## Implementation Notes

- 使用 `Task.Run` 启动分发循环
- 每个订阅者独立处理，不阻塞其他订阅者
- `CancellationToken` 支持停止操作
- 无锁订阅者列表管理（使用锁保护列表修改）