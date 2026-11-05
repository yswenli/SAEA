using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using SAEA.MQTT.Client.Receiving;

namespace SAEA.MQTT.Tests.TestInfrastructure
{
    /// <summary>
    /// 收集客户端收到的应用消息，供测试等待并断言发布结果。
    /// </summary>
    internal sealed class TestMessageCollector : IMqttApplicationMessageReceivedHandler
    {
        readonly ConcurrentQueue<MqttApplicationMessage> _messages = new ConcurrentQueue<MqttApplicationMessage>();
        readonly SemaphoreSlim _signal = new SemaphoreSlim(0, int.MaxValue);

        public int Count => _messages.Count;

        public Task HandleApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs eventArgs)
        {
            if (eventArgs == null) throw new ArgumentNullException(nameof(eventArgs));

            _messages.Enqueue(eventArgs.ApplicationMessage);
            _signal.Release();

            return Task.CompletedTask;
        }

        /// <summary>
        /// 等待下一条消息；超时抛出 <see cref="TimeoutException"/>。
        /// </summary>
        public async Task<MqttApplicationMessage> WaitAsync(TimeSpan timeout)
        {
            var message = await WaitAsyncOrNull(timeout).ConfigureAwait(false);
            if (message == null)
            {
                throw new TimeoutException("No application message was received within " + timeout + ".");
            }

            return message;
        }

        /// <summary>
        /// 等待下一条消息；超时返回 null。用于断言“没有消息”。
        /// </summary>
        public async Task<MqttApplicationMessage> WaitAsyncOrNull(TimeSpan timeout)
        {
            if (!await _signal.WaitAsync(timeout).ConfigureAwait(false))
            {
                return null;
            }

            MqttApplicationMessage message;
            _messages.TryDequeue(out message);
            return message;
        }
    }
}