# EventBus Channel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现基于 Channel 的轻量级事件总线，支持发布/订阅模式和重试机制

**Architecture:** 使用 System.Threading.Channels 作为底层队列，每个 Topic 独立 Channel，多订阅者广播分发，轻量级重试支持

**Tech Stack:** C#, System.Threading.Channels, Task, ConcurrentDictionary

---

## File Structure

```
SAEA.Common/EventBus/
├── EventBus.cs              (静态入口，发布/订阅/启动/停止)
├── TopicChannel.cs          (主题通道，Channel + 订阅者管理)
├── EventSubscriber.cs       (订阅者，处理器 + 重试配置)
├── EventMessage.cs          (消息实体)
└── EventBusHost.cs          (后台分发管理，可选)
```

---

### Task 1: EventMessage 实体类

**Files:**
- Create: `SAEA.Common/EventBus/EventMessage.cs`
- Test: `SAEA.Common.Tests/EventBus/EventMessageTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using SAEA.Common.EventBus;
using Xunit;

public class EventMessageTests
{
    [Fact]
    public void EventMessage_ShouldInitializeCorrectly()
    {
        var data = new byte[] { 1, 2, 3 };
        var context = new { Key = "value" };
        
        var msg = new EventMessage("test.topic", data, context);
        
        Assert.Equal("test.topic", msg.Topic);
        Assert.Equal(data, msg.Data);
        Assert.Equal(context, msg.Context);
        Assert.True(msg.Timestamp <= DateTime.UtcNow);
    }
    
    [Fact]
    public void EventMessage_ShouldAllowNullContext()
    {
        var msg = new EventMessage("test.topic", new byte[] { 1 });
        Assert.Null(msg.Context);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test SAEA.Common.Tests --filter "EventMessageTests" -v n`
Expected: FAIL (class not found)

- [ ] **Step 3: Write minimal implementation**

```csharp
using System;

namespace SAEA.Common.EventBus
{
    public class EventMessage
    {
        public string Topic { get; }
        public byte[] Data { get; }
        public object Context { get; }
        public DateTime Timestamp { get; }

        public EventMessage(string topic, byte[] data, object context = null)
        {
            Topic = topic ?? throw new ArgumentNullException(nameof(topic));
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Context = context;
            Timestamp = DateTime.UtcNow;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test SAEA.Common.Tests --filter "EventMessageTests" -v n`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add SAEA.Common/EventBus/EventMessage.cs SAEA.Common.Tests/EventBus/EventMessageTests.cs
git commit -m "feat(eventbus): add EventMessage entity class"
```

---

### Task 2: EventSubscriber 订阅者类

**Files:**
- Create: `SAEA.Common/EventBus/EventSubscriber.cs`
- Test: `SAEA.Common.Tests/EventBus/EventSubscriberTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using SAEA.Common.EventBus;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public class EventSubscriberTests
{
    [Fact]
    public void EventSubscriber_ShouldInitializeCorrectly()
    {
        Func<EventMessage, CancellationToken, Task> handler = (msg, ct) => Task.CompletedTask;
        
        var sub = new EventSubscriber(handler, retryCount: 3);
        
        Assert.NotNull(sub.Id);
        Assert.Equal(3, sub.RetryCount);
        Assert.NotNull(sub.Handler);
    }
    
    [Fact]
    public void EventSubscriber_ShouldUseDefaultRetryCount()
    {
        var sub = new EventSubscriber((msg, ct) => Task.CompletedTask);
        Assert.Equal(3, sub.RetryCount);
    }
    
    [Fact]
    public void EventSubscriber_ShouldGenerateUniqueId()
    {
        var sub1 = new EventSubscriber((msg, ct) => Task.CompletedTask);
        var sub2 = new EventSubscriber((msg, ct) => Task.CompletedTask);
        
        Assert.NotEqual(sub1.Id, sub2.Id);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test SAEA.Common.Tests --filter "EventSubscriberTests" -v n`
Expected: FAIL (class not found)

- [ ] **Step 3: Write minimal implementation**

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Common.EventBus
{
    public class EventSubscriber
    {
        public string Id { get; }
        public Func<EventMessage, CancellationToken, Task> Handler { get; }
        public int RetryCount { get; }
        public TimeSpan? RetryDelay { get; }

        public EventSubscriber(
            Func<EventMessage, CancellationToken, Task> handler,
            int retryCount = 3,
            TimeSpan? retryDelay = null)
        {
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
            RetryCount = retryCount;
            RetryDelay = retryDelay;
            Id = Guid.NewGuid().ToString("N");
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test SAEA.Common.Tests --filter "EventSubscriberTests" -v n`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add SAEA.Common/EventBus/EventSubscriber.cs SAEA.Common.Tests/EventBus/EventSubscriberTests.cs
git commit -m "feat(eventbus): add EventSubscriber class with retry support"
```

---

### Task 3: TopicChannel 主题通道类

**Files:**
- Create: `SAEA.Common/EventBus/TopicChannel.cs`
- Test: `SAEA.Common.Tests/EventBus/TopicChannelTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using SAEA.Common.EventBus;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public class TopicChannelTests
{
    [Fact]
    public void TopicChannel_ShouldInitializeCorrectly()
    {
        var tc = new TopicChannel(capacity: 100);
        
        Assert.NotNull(tc);
    }
    
    [Fact]
    public async Task TopicChannel_ShouldPublishAndRead()
    {
        var tc = new TopicChannel(capacity: 10);
        var msg = new EventMessage("test", new byte[] { 1, 2, 3 });
        
        await tc.PublishAsync(msg);
        
        var readMsg = await tc.ReadAsync(CancellationToken.None);
        Assert.Equal(msg, readMsg);
    }
    
    [Fact]
    public void TopicChannel_ShouldAddSubscriber()
    {
        var tc = new TopicChannel();
        var sub = new EventSubscriber((msg, ct) => Task.CompletedTask);
        
        tc.AddSubscriber(sub);
        
        Assert.Single(tc.GetSubscribers());
    }
    
    [Fact]
    public void TopicChannel_ShouldRemoveSubscriber()
    {
        var tc = new TopicChannel();
        var sub = new EventSubscriber((msg, ct) => Task.CompletedTask);
        
        tc.AddSubscriber(sub);
        tc.RemoveSubscriber(sub.Id);
        
        Assert.Empty(tc.GetSubscribers());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test SAEA.Common.Tests --filter "TopicChannelTests" -v n`
Expected: FAIL (class not found)

- [ ] **Step 3: Write minimal implementation**

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SAEA.Common.EventBus
{
    internal class TopicChannel
    {
        private readonly Channel<EventMessage> _channel;
        private readonly List<EventSubscriber> _subscribers;
        private readonly object _subscriberLock = new object();

        public TopicChannel(int capacity = 0)
        {
            _subscribers = new List<EventSubscriber>();
            
            if (capacity > 0)
            {
                _channel = Channel.CreateBounded<EventMessage>(new BoundedChannelOptions(capacity)
                {
                    FullMode = BoundedChannelFullMode.Wait
                });
            }
            else
            {
                _channel = Channel.CreateUnbounded<EventMessage>();
            }
        }

        public async ValueTask PublishAsync(EventMessage message)
        {
            await _channel.Writer.WriteAsync(message);
        }

        public async ValueTask<EventMessage> ReadAsync(CancellationToken cancellationToken)
        {
            return await _channel.Reader.ReadAsync(cancellationToken);
        }

        public void AddSubscriber(EventSubscriber subscriber)
        {
            lock (_subscriberLock)
            {
                _subscribers.Add(subscriber);
            }
        }

        public void RemoveSubscriber(string subscriberId)
        {
            lock (_subscriberLock)
            {
                _subscribers.RemoveAll(s => s.Id == subscriberId);
            }
        }

        public IReadOnlyList<EventSubscriber> GetSubscribers()
        {
            lock (_subscriberLock)
            {
                return _subscribers.ToArray();
            }
        }

        public void Complete()
        {
            _channel.Writer.Complete();
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test SAEA.Common.Tests --filter "TopicChannelTests" -v n`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add SAEA.Common/EventBus/TopicChannel.cs SAEA.Common.Tests/EventBus/TopicChannelTests.cs
git commit -m "feat(eventbus): add TopicChannel with Channel and subscriber management"
```

---

### Task 4: EventBusHost 后台分发管理

**Files:**
- Create: `SAEA.Common/EventBus/EventBusHost.cs`
- Test: `SAEA.Common.Tests/EventBus/EventBusHostTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using SAEA.Common.EventBus;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public class EventBusHostTests
{
    [Fact]
    public void EventBusHost_ShouldInitializeCorrectly()
    {
        var topics = new Dictionary<string, TopicChannel>();
        var host = new EventBusHost(topics);
        
        Assert.NotNull(host);
        Assert.False(host.IsRunning);
    }
    
    [Fact]
    public async Task EventBusHost_ShouldStartAndStop()
    {
        var topics = new Dictionary<string, TopicChannel>();
        var host = new EventBusHost(topics);
        
        host.Start();
        Assert.True(host.IsRunning);
        
        await host.StopAsync(TimeSpan.FromSeconds(1));
        Assert.False(host.IsRunning);
    }
    
    [Fact]
    public async Task EventBusHost_ShouldDispatchMessageToSubscribers()
    {
        var tc = new TopicChannel();
        var received = 0;
        var sub = new EventSubscriber(async (msg, ct) => 
        {
            received++;
        }, retryCount: 0);
        
        tc.AddSubscriber(sub);
        
        var topics = new Dictionary<string, TopicChannel> { ["test"] = tc };
        var host = new EventBusHost(topics);
        
        host.Start();
        
        await tc.PublishAsync(new EventMessage("test", new byte[] { 1 }));
        
        await Task.Delay(100);
        
        Assert.Equal(1, received);
        
        await host.StopAsync();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test SAEA.Common.Tests --filter "EventBusHostTests" -v n`
Expected: FAIL (class not found)

- [ ] **Step 3: Write minimal implementation**

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Common.EventBus
{
    internal class EventBusHost : IDisposable
    {
        private readonly Dictionary<string, TopicChannel> _topics;
        private readonly Dictionary<string, Task> _dispatchTasks;
        private readonly CancellationTokenSource _cts;
        private bool _isRunning;
        private readonly object _lock = new object();

        public bool IsRunning => _isRunning;

        public EventBusHost(Dictionary<string, TopicChannel> topics)
        {
            _topics = topics ?? new Dictionary<string, TopicChannel>();
            _dispatchTasks = new Dictionary<string, Task>();
            _cts = new CancellationTokenSource();
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_isRunning) return;
                _isRunning = true;
                
                foreach (var topic in _topics)
                {
                    StartTopicDispatchLoop(topic.Value, topic.Key);
                }
            }
        }

        public void StartTopicDispatchLoop(TopicChannel topicChannel, string topicName)
        {
            var task = Task.Run(async () =>
            {
                try
                {
                    while (!_cts.IsCancellationRequested && topicChannel.GetSubscribers().Count > 0)
                    {
                        try
                        {
                            var msg = await topicChannel.ReadAsync(_cts.Token);
                            await DispatchToSubscribers(topicChannel, msg);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }
                catch (Exception)
                {
                }
            }, _cts.Token);
            
            _dispatchTasks[topicName] = task;
        }

        private async Task DispatchToSubscribers(TopicChannel topicChannel, EventMessage message)
        {
            var subscribers = topicChannel.GetSubscribers();
            
            foreach (var sub in subscribers)
            {
                await ExecuteWithRetry(sub, message);
            }
        }

        private async Task ExecuteWithRetry(EventSubscriber subscriber, EventMessage message)
        {
            var retryCount = subscriber.RetryCount;
            
            for (int attempt = 0; attempt <= retryCount; attempt++)
            {
                try
                {
                    await subscriber.Handler(message, _cts.Token);
                    return;
                }
                catch (Exception)
                {
                    if (attempt < retryCount)
                    {
                        var delay = subscriber.RetryDelay ?? TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt));
                        await Task.Delay(delay, _cts.Token);
                    }
                }
            }
        }

        public async Task StopAsync(TimeSpan? timeout = null)
        {
            lock (_lock)
            {
                if (!_isRunning) return;
                _isRunning = false;
                _cts.Cancel();
            }

            if (timeout != null)
            {
                var tasks = new List<Task>(_dispatchTasks.Values);
                await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(timeout.Value));
            }
            
            foreach (var tc in _topics.Values)
            {
                tc.Complete();
            }
        }

        public void Dispose()
        {
            StopAsync().GetAwaiter().GetResult();
            _cts?.Dispose();
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test SAEA.Common.Tests --filter "EventBusHostTests" -v n`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add SAEA.Common/EventBus/EventBusHost.cs SAEA.Common.Tests/EventBus/EventBusHostTests.cs
git commit -m "feat(eventbus): add EventBusHost for dispatch loop management"
```

---

### Task 5: EventBus 静态入口类

**Files:**
- Create: `SAEA.Common/EventBus/EventBus.cs`
- Test: `SAEA.Common.Tests/EventBus/EventBusTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using SAEA.Common.EventBus;
using System;
using System.Threading.Tasks;
using Xunit;

public class EventBusTests
{
    [Fact]
    public void EventBus_ShouldNotBeEnabledBeforeStart()
    {
        EventBus.Dispose();
        Assert.False(EventBus.Enabled);
    }
    
    [Fact]
    public void EventBus_ShouldRegisterTopic()
    {
        EventBus.Dispose();
        EventBus.RegisterTopic("test.topic", 100);
        
        EventBus.Start();
        Assert.True(EventBus.Enabled);
        
        EventBus.Dispose();
    }
    
    [Fact]
    public async Task EventBus_ShouldPublishAndSubscribe()
    {
        EventBus.Dispose();
        
        var received = false;
        var subId = EventBus.Subscribe("test.topic", async msg => 
        {
            received = true;
        }, retryCount: 0);
        
        EventBus.Start();
        
        EventBus.Publish("test.topic", new byte[] { 1, 2, 3 });
        
        await Task.Delay(100);
        
        Assert.True(received);
        
        EventBus.Unsubscribe("test.topic", subId);
        EventBus.Dispose();
    }
    
    [Fact]
    public async Task EventBus_ShouldSupportMultipleSubscribers()
    {
        EventBus.Dispose();
        
        var count = 0;
        var sub1 = EventBus.Subscribe("test.multi", async msg => count++);
        var sub2 = EventBus.Subscribe("test.multi", async msg => count++);
        
        EventBus.Start();
        
        EventBus.Publish("test.multi", new byte[] { 1 });
        
        await Task.Delay(100);
        
        Assert.Equal(2, count);
        
        EventBus.Unsubscribe("test.multi", sub1);
        EventBus.Unsubscribe("test.multi", sub2);
        EventBus.Dispose();
    }
    
    [Fact]
    public async Task EventBus_ShouldRetryOnFailure()
    {
        EventBus.Dispose();
        
        var attempts = 0;
        var subId = EventBus.Subscribe("test.retry", async msg => 
        {
            attempts++;
            if (attempts < 3) throw new Exception("retry");
        }, retryCount: 3);
        
        EventBus.Start();
        
        EventBus.Publish("test.retry", new byte[] { 1 });
        
        await Task.Delay(200);
        
        Assert.Equal(3, attempts);
        
        EventBus.Unsubscribe("test.retry", subId);
        EventBus.Dispose();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test SAEA.Common.Tests --filter "EventBusTests" -v n`
Expected: FAIL (class not found)

- [ ] **Step 3: Write minimal implementation**

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAEA.Common.EventBus
{
    public static class EventBus
    {
        private static readonly object _lock = new object();
        private static readonly ConcurrentDictionary<string, TopicChannel> _topics = new ConcurrentDictionary<string, TopicChannel>();
        private static EventBusHost _host;

        public static bool Enabled { get; private set; }

        public static void RegisterTopic(string topic, int capacity = 0)
        {
            _topics.GetOrAdd(topic, _ => new TopicChannel(capacity));
        }

        public static void Publish(string topic, byte[] data, object context = null)
        {
            if (!Enabled) return;

            var msg = new EventMessage(topic, data, context);
            if (_topics.TryGetValue(topic, out var tc))
            {
                tc.PublishAsync(msg).GetAwaiter().GetResult();
            }
        }

        public static async ValueTask PublishAsync(string topic, byte[] data, object context = null)
        {
            if (!Enabled) return;

            var msg = new EventMessage(topic, data, context);
            if (_topics.TryGetValue(topic, out var tc))
            {
                await tc.PublishAsync(msg);
            }
        }

        public static string Subscribe(
            string topic,
            Func<EventMessage, Task> handler,
            int retryCount = 3,
            TimeSpan? retryDelay = null)
        {
            var tc = _topics.GetOrAdd(topic, _ => new TopicChannel());
            var sub = new EventSubscriber(
                (msg, ct) => handler(msg),
                retryCount,
                retryDelay);
            
            tc.AddSubscriber(sub);

            if (_host != null && _host.IsRunning)
            {
                _host.StartTopicDispatchLoop(tc, topic);
            }

            return sub.Id;
        }

        public static string Subscribe(
            string topic,
            Func<EventMessage, CancellationToken, Task> handler,
            int retryCount = 3,
            TimeSpan? retryDelay = null)
        {
            var tc = _topics.GetOrAdd(topic, _ => new TopicChannel());
            var sub = new EventSubscriber(handler, retryCount, retryDelay);
            
            tc.AddSubscriber(sub);

            if (_host != null && _host.IsRunning)
            {
                _host.StartTopicDispatchLoop(tc, topic);
            }

            return sub.Id;
        }

        public static void Unsubscribe(string topic, string subscriberId)
        {
            if (_topics.TryGetValue(topic, out var tc))
            {
                tc.RemoveSubscriber(subscriberId);
            }
        }

        public static void Start()
        {
            lock (_lock)
            {
                if (_host != null && _host.IsRunning) return;
                _host = new EventBusHost(new Dictionary<string, TopicChannel>(_topics));
                _host.Start();
                Enabled = true;
            }
        }

        public static async Task StopAsync(TimeSpan? timeout = null)
        {
            lock (_lock)
            {
                Enabled = false;
            }
            
            if (_host != null)
            {
                await _host.StopAsync(timeout);
            }
        }

        public static void Dispose()
        {
            Enabled = false;
            try { _host?.Dispose(); } catch { }
            _host = null;
            _topics.Clear();
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test SAEA.Common.Tests --filter "EventBusTests" -v n`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add SAEA.Common/EventBus/EventBus.cs SAEA.Common.Tests/EventBus/EventBusTests.cs
git commit -m "feat(eventbus): add EventBus static entry point"
```

---

### Task 6: 集成测试和最终验证

**Files:**
- Modify: `SAEA.Common.Tests/EventBus/EventBusTests.cs`

- [ ] **Step 1: Run all EventBus tests**

Run: `dotnet test SAEA.Common.Tests --filter "EventBus" -v n`
Expected: All PASS

- [ ] **Step 2: Add integration test for concurrent scenario**

```csharp
[Fact]
public async Task EventBus_ShouldHandleConcurrentPublish()
{
    EventBus.Dispose();
    
    var count = 0;
    var subId = EventBus.Subscribe("test.concurrent", async msg => 
    {
        count++;
    }, retryCount: 0);
    
    EventBus.Start();
    
    var tasks = new List<Task>();
    for (int i = 0; i < 100; i++)
    {
        tasks.Add(Task.Run(() => EventBus.Publish("test.concurrent", new byte[] { (byte)i })));
    }
    
    await Task.WhenAll(tasks);
    await Task.Delay(200);
    
    Assert.Equal(100, count);
    
    EventBus.Unsubscribe("test.concurrent", subId);
    EventBus.Dispose();
}
```

- [ ] **Step 3: Run integration test**

Run: `dotnet test SAEA.Common.Tests --filter "EventBus_ShouldHandleConcurrentPublish" -v n`
Expected: PASS

- [ ] **Step 4: Final commit**

```bash
git add SAEA.Common.Tests/EventBus/EventBusTests.cs
git commit -m "feat(eventbus): add concurrent integration test"
```

---

## Self-Review Checklist

1. **Spec coverage**: 
   - Publish/Subscribe ✓ (Task 5)
   - Retry mechanism ✓ (Task 4, Task 5)
   - Multiple subscribers ✓ (Task 3, Task 5)
   - Topic management ✓ (Task 5)

2. **Placeholder scan**: No TBD/TODO found ✓

3. **Type consistency**: 
   - EventMessage signature consistent ✓
   - EventSubscriber.Handler signature consistent ✓
   - TopicChannel methods consistent ✓