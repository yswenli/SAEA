using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
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