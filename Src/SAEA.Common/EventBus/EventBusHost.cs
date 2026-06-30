using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Common.EventBus
{
    public class EventBusHost : IDisposable
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
            if (topicChannel.GetSubscribers().Count == 0) return;
            
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
                        catch
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