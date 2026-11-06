using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Client.Disconnecting;
using SAEA.MQTT.Client.Options;
using SAEA.MQTT.Client.Subscribing;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;
using SAEA.MQTT.Server;
using SAEA.MQTT.Tests.TestInfrastructure;

namespace SAEA.MQTT.Tests.Will
{
    /// <summary>
    /// 遗嘱消息的发布时机：正常断开、Will Delay 与连接接管场景
    /// </summary>
    [TestClass]
    public class WillDelayTests
    {
        static readonly DateTimeOffset StartTime = new DateTimeOffset(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);
        static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);
        static readonly TimeSpan NoMessageTimeout = TimeSpan.FromMilliseconds(300);

        FakeMqttTimeProvider _time;
        TestClockScope _clockScope;

        /// <summary>
        /// 安装假时钟，让测试可以手动推进遗嘱延迟时间
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            _time = new FakeMqttTimeProvider(StartTime);
            _clockScope = new TestClockScope(_time);
        }

        /// <summary>
        /// 释放时钟作用域并恢复进入测试前的全局时间提供者
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            if (_clockScope != null)
            {
                _clockScope.Dispose();
            }
        }

        /// <summary>
        /// 验证原因码为 0x00 的正常断开不登记遗嘱任务也不发布遗嘱
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task NormalDisconnect_DoesNotPublishWill()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await StartServerAsync();

            const string clientId = "will-normal";
            const string topic = "will/normal";

            var collector = new TestMessageCollector();
            await using var observer = await ConnectObserverAsync(server, topic, collector, timeout.Token);

            var owner = await ConnectV5Async(
                server,
                clientId,
                false,
                300,
                CreateWill(topic, "normal-disconnect"),
                null,
                timeout.Token);

            await DisconnectAsync(owner, MqttClientDisconnectReason.NormalDisconnection, timeout.Token);

            await WaitUntilAsync(
                async () => !await HasConnectionAsync(server, clientId).ConfigureAwait(false),
                "The connection should be cleaned up after a normal DISCONNECT.");

            Assert.IsFalse(
                server.ClientSessionsManager.SessionExpiryManager.IsWillScheduled(clientId),
                "A normal DISCONNECT must never schedule the will.");

            Assert.IsNull(
                await collector.WaitAsyncOrNull(NoMessageTimeout).ConfigureAwait(false),
                "A normal DISCONNECT (0x00) must not publish the will.");
        }

        /// <summary>
        /// 验证客户端以 0x04（带遗嘱断开）断连时，服务端立即按遗嘱主题与载荷发布遗嘱
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task DisconnectWithWillMessage_PublishesWill()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await StartServerAsync();

            const string clientId = "will-disconnect-0x04";
            const string topic = "will/disconnect-0x04";
            const string payload = "publish-me";

            var collector = new TestMessageCollector();
            await using var observer = await ConnectObserverAsync(server, topic, collector, timeout.Token);

            var owner = await ConnectV5Async(
                server,
                clientId,
                false,
                300,
                CreateWill(topic, payload),
                null,
                timeout.Token);

            await DisconnectAsync(owner, MqttClientDisconnectReason.DisconnectWithWillMessage, timeout.Token);

            var message = await collector.WaitAsync(WaitTimeout).ConfigureAwait(false);
            Assert.AreEqual(topic, message.Topic, "The published will must use the will topic.");
            Assert.AreEqual(payload, Encoding.UTF8.GetString(message.Payload), "The published will must use the will payload.");
        }

        /// <summary>
        /// 验证因协议错误被服务端断开时仍发布遗嘱，且协商会话过期时间为 0 时会话被移除
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task ProtocolError_PublishesWill()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await StartServerAsync();

            const string clientId = "will-protocol-error";
            const string topic = "will/protocol-error";
            const string payload = "protocol-error";

            var collector = new TestMessageCollector();
            await using var observer = await ConnectObserverAsync(server, topic, collector, timeout.Token);

            // CONNECT 声明 Session Expiry=0，而 DISCONNECT 试图改大，服务端必须按协议错误断开并发布 will。
            await SendRawProtocolErrorConnectionAsync(
                server.Port,
                clientId,
                CreateWill(topic, payload),
                timeout.Token).ConfigureAwait(false);

            var message = await collector.WaitAsync(WaitTimeout).ConfigureAwait(false);
            Assert.AreEqual(topic, message.Topic, "A protocol error must publish the will.");
            Assert.AreEqual(payload, Encoding.UTF8.GetString(message.Payload), "The protocol error will payload must match.");

            await WaitUntilAsync(
                async () => !await HasSessionAsync(server, clientId).ConfigureAwait(false),
                "The session must be removed because the negotiated session expiry interval was 0.");
        }

        /// <summary>
        /// 验证异常断开后遗嘱延迟 5 秒才到期，期间恢复会话会取消遗嘱，且原到期时刻过后也不再发布
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task WillDelay_ReconnectBeforeDelay_DoesNotPublishWill()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await StartServerAsync();

            const string clientId = "will-delay-reconnect-before";
            const string topic = "will/delay-reconnect-before";

            var collector = new TestMessageCollector();
            await using var observer = await ConnectObserverAsync(server, topic, collector, timeout.Token);

            var owner = await ConnectV5Async(
                server,
                clientId,
                false,
                300,
                CreateWill(topic, "should-not-be-published"),
                5,
                timeout.Token);

            CloseAbnormally(owner);

            await WaitUntilAsync(
                () => server.ClientSessionsManager.SessionExpiryManager.IsWillScheduled(clientId),
                "The will publication should be scheduled after an abnormal disconnect.");

            _time.Advance(TimeSpan.FromSeconds(4));
            server.ClientSessionsManager.SessionExpiryManager.RunDueTasks();

            Assert.IsTrue(
                server.ClientSessionsManager.SessionExpiryManager.IsWillScheduled(clientId),
                "The will must still be pending 4 seconds into a 5 second delay.");

            var reconnected = await ConnectV5Async(
                server,
                clientId,
                false,
                300,
                null,
                null,
                timeout.Token);

            try
            {
                await WaitUntilAsync(
                    () => !server.ClientSessionsManager.SessionExpiryManager.IsWillScheduled(clientId),
                    "Reconnecting within the will delay must cancel the pending will.");

                Assert.IsNull(
                    await collector.WaitAsyncOrNull(NoMessageTimeout).ConfigureAwait(false),
                    "A reconnect within the will delay interval must not publish the will.");

                _time.Advance(TimeSpan.FromSeconds(10));
                server.ClientSessionsManager.SessionExpiryManager.RunDueTasks();

                Assert.IsNull(
                    await collector.WaitAsyncOrNull(NoMessageTimeout).ConfigureAwait(false),
                    "The cancelled will must stay cancelled after the original delay elapsed.");
            }
            finally
            {
                await DisconnectAsync(reconnected, MqttClientDisconnectReason.NormalDisconnection, timeout.Token);
            }
        }

        /// <summary>
        /// 验证异常断开后遗嘱延迟届满（5 秒延迟推进 6 秒）时发布遗嘱，并清除该遗嘱的调度记录
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task WillDelay_Elapsed_PublishesWill()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await StartServerAsync();

            const string clientId = "will-delay-elapsed";
            const string topic = "will/delay-elapsed";
            const string payload = "delayed-will";

            var collector = new TestMessageCollector();
            await using var observer = await ConnectObserverAsync(server, topic, collector, timeout.Token);

            var owner = await ConnectV5Async(
                server,
                clientId,
                false,
                300,
                CreateWill(topic, payload),
                5,
                timeout.Token);

            CloseAbnormally(owner);

            await WaitUntilAsync(
                () => server.ClientSessionsManager.SessionExpiryManager.IsWillScheduled(clientId),
                "The will publication should be scheduled after an abnormal disconnect.");

            _time.Advance(TimeSpan.FromSeconds(6));
            server.ClientSessionsManager.SessionExpiryManager.RunDueTasks();

            var message = await collector.WaitAsync(WaitTimeout).ConfigureAwait(false);
            Assert.AreEqual(topic, message.Topic, "The will must be published after the delay elapsed.");
            Assert.AreEqual(payload, Encoding.UTF8.GetString(message.Payload), "The delayed will payload must match.");

            await WaitUntilAsync(
                () => !server.ClientSessionsManager.SessionExpiryManager.IsWillScheduled(clientId),
                "The will schedule must be removed after publishing.");
        }

        /// <summary>
        /// 验证 Clean Start 为 0 且遗嘱延迟为 0 的连接接管时复用会话并立即发布旧连接的遗嘱
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Takeover_CleanStartFalse_DelayZero_PublishesImmediately()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await StartServerAsync();

            const string clientId = "will-takeover-cs0-delay0";
            const string topic = "will/takeover-cs0-delay0";
            const string payload = "immediate-takeover";

            var collector = new TestMessageCollector();
            await using var observer = await ConnectObserverAsync(server, topic, collector, timeout.Token);

            var oldClient = await ConnectV5Async(
                server,
                clientId,
                false,
                300,
                CreateWill(topic, payload),
                0,
                timeout.Token);

            var newClient = await ConnectV5Async(server, clientId, false, 300, null, null, timeout.Token);

            try
            {
                Assert.IsTrue(
                    newClient.ConnectResult.IsSessionPresent,
                    "CleanStart=0 must reuse the existing session.");

                var message = await collector.WaitAsync(WaitTimeout).ConfigureAwait(false);
                Assert.AreEqual(topic, message.Topic, "Takeover with CleanStart=0 and WillDelay=0 must publish the will immediately.");
                Assert.AreEqual(payload, Encoding.UTF8.GetString(message.Payload), "The takeover will payload must match.");

                await WaitUntilAsync(
                    () => !oldClient.Client.IsConnected,
                    "The old connection must be stopped after the takeover.");
            }
            finally
            {
                await DisconnectAsync(newClient, MqttClientDisconnectReason.NormalDisconnection, timeout.Token);
            }
        }

        /// <summary>
        /// 验证 Clean Start 为 0 且遗嘱延迟大于 0 的连接接管会取消待发的遗嘱，延迟届满后也不发布
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Takeover_CleanStartFalse_DelayPositive_CancelsWill()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await StartServerAsync();

            const string clientId = "will-takeover-cs0-delay5";
            const string topic = "will/takeover-cs0-delay5";

            var collector = new TestMessageCollector();
            await using var observer = await ConnectObserverAsync(server, topic, collector, timeout.Token);

            var oldClient = await ConnectV5Async(
                server,
                clientId,
                false,
                300,
                CreateWill(topic, "must-be-cancelled"),
                5,
                timeout.Token);

            var newClient = await ConnectV5Async(server, clientId, false, 300, null, null, timeout.Token);

            try
            {
                Assert.IsTrue(
                    newClient.ConnectResult.IsSessionPresent,
                    "CleanStart=0 must reuse the existing session.");

                await WaitUntilAsync(
                    () => !oldClient.Client.IsConnected,
                    "The old connection must be stopped after the takeover.");

                Assert.IsFalse(
                    server.ClientSessionsManager.SessionExpiryManager.IsWillScheduled(clientId),
                    "Takeover with CleanStart=0 and WillDelay>0 must cancel the will.");

                _time.Advance(TimeSpan.FromSeconds(6));
                server.ClientSessionsManager.SessionExpiryManager.RunDueTasks();

                Assert.IsNull(
                    await collector.WaitAsyncOrNull(NoMessageTimeout).ConfigureAwait(false),
                    "The cancelled will must not be published after the original delay elapsed.");
            }
            finally
            {
                await DisconnectAsync(newClient, MqttClientDisconnectReason.NormalDisconnection, timeout.Token);
            }
        }

        /// <summary>
        /// 验证 Clean Start=1 接管时丢弃旧会话并立即发布旧连接的遗嘱，主题与载荷均与遗嘱一致
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Takeover_CleanStartTrue_PublishesWillImmediately()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await StartServerAsync();

            const string clientId = "will-takeover-cs1-delay5";
            const string topic = "will/takeover-cs1-delay5";
            const string payload = "clean-start-takeover";

            var collector = new TestMessageCollector();
            await using var observer = await ConnectObserverAsync(server, topic, collector, timeout.Token);

            var oldClient = await ConnectV5Async(
                server,
                clientId,
                false,
                300,
                CreateWill(topic, payload),
                5,
                timeout.Token);

            var newClient = await ConnectV5Async(server, clientId, true, 300, null, null, timeout.Token);

            try
            {
                Assert.IsFalse(
                    newClient.ConnectResult.IsSessionPresent,
                    "CleanStart=1 must discard the existing session.");

                var message = await collector.WaitAsync(WaitTimeout).ConfigureAwait(false);
                Assert.AreEqual(topic, message.Topic, "Takeover with CleanStart=1 must publish the old will immediately.");
                Assert.AreEqual(payload, Encoding.UTF8.GetString(message.Payload), "The CleanStart=1 takeover will payload must match.");

                await WaitUntilAsync(
                    () => !oldClient.Client.IsConnected,
                    "The old connection must be stopped after the takeover.");
            }
            finally
            {
                await DisconnectAsync(newClient, MqttClientDisconnectReason.NormalDisconnection, timeout.Token);
            }
        }

        /// <summary>
        /// 验证 Retain=1 的遗嘱在异常断开后写入 retained 存储，新订阅者能收到该遗嘱的重放
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task RetainedWill_UpdatesRetainedStorage()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await StartServerAsync();

            const string clientId = "will-retained";
            const string topic = "will/retained";
            const string payload = "retained-will";

            var owner = await ConnectV5Async(
                server,
                clientId,
                false,
                300,
                CreateWill(topic, payload, true),
                0,
                timeout.Token);

            CloseAbnormally(owner);

            await WaitUntilAsync(
                async () => await HasRetainedMessageAsync(server, topic, payload).ConfigureAwait(false),
                "A will with Retain=1 must update the retained message store.");

            var collector = new TestMessageCollector();
            await using var observer = await ConnectObserverAsync(server, topic, collector, timeout.Token);

            var retainedMessage = await collector.WaitAsync(WaitTimeout).ConfigureAwait(false);
            Assert.AreEqual(topic, retainedMessage.Topic, "A new subscriber must receive the retained will.");
            Assert.AreEqual(payload, Encoding.UTF8.GetString(retainedMessage.Payload), "The retained will payload must match.");
        }

        static Task<MqttTestServer> StartServerAsync()
        {
            return MqttTestServer.StartAsync(builder => builder.WithPersistentSessions());
        }

        static MqttApplicationMessage CreateWill(string topic, string payload, bool retain = false)
        {
            return new MqttApplicationMessage
            {
                Topic = topic,
                Payload = Encoding.UTF8.GetBytes(payload),
                QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce,
                Retain = retain
            };
        }

        static async Task<MqttTestClient> ConnectV5Async(
            MqttTestServer server,
            string clientId,
            bool cleanSession,
            uint? sessionExpiryInterval,
            MqttApplicationMessage willMessage,
            uint? willDelayInterval,
            CancellationToken cancellationToken)
        {
            return await MqttTestClient.ConnectAsync(
                server.Port,
                MqttProtocolVersion.V500,
                clientId,
                options =>
                {
                    options.WithCleanSession(cleanSession);

                    if (sessionExpiryInterval.HasValue)
                    {
                        options.WithSessionExpiryInterval(sessionExpiryInterval.Value);
                    }

                    if (willMessage != null)
                    {
                        options.WithWillMessage(willMessage);
                    }

                    if (willDelayInterval.HasValue)
                    {
                        options.WithWillDelayInterval(willDelayInterval.Value);
                    }
                },
                cancellationToken).ConfigureAwait(false);
        }

        static async Task<MqttTestClient> ConnectObserverAsync(
            MqttTestServer server,
            string topic,
            TestMessageCollector collector,
            CancellationToken cancellationToken)
        {
            var observer = await MqttTestClient.ConnectAsync(
                server.Port,
                MqttProtocolVersion.V500,
                "will-observer-" + Guid.NewGuid().ToString("N"),
                options => options.WithCleanSession(),
                cancellationToken).ConfigureAwait(false);

            observer.Client.ApplicationMessageReceivedHandler = collector;

            await observer.Client.SubscribeAsync(
                new MqttClientSubscribeOptionsBuilder().WithTopicFilter(topic).Build(),
                cancellationToken).ConfigureAwait(false);

            return observer;
        }

        static async Task DisconnectAsync(
            MqttTestClient client,
            MqttClientDisconnectReason reason,
            CancellationToken cancellationToken)
        {
            try
            {
                if (client.Client.IsConnected)
                {
                    await client.Client
                        .DisconnectAsync(
                            new MqttClientDisconnectOptions { ReasonCode = reason },
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            finally
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }
        }

        static void CloseAbnormally(MqttTestClient client)
        {
            // Dispose() 走 Cleanup 路径，不发送 DISCONNECT，用于模拟异常关闭。
            client.Client.Dispose();
        }

        static async Task<bool> HasSessionAsync(MqttTestServer server, string clientId)
        {
            var sessions = await server.Server.GetSessionStatusAsync().ConfigureAwait(false);
            return sessions.Any(s => s.ClientId == clientId);
        }

        static async Task<bool> HasConnectionAsync(MqttTestServer server, string clientId)
        {
            var connections = await server.Server.GetClientStatusAsync().ConfigureAwait(false);
            return connections.Any(c => c.ClientId == clientId);
        }

        static async Task<bool> HasRetainedMessageAsync(MqttTestServer server, string topic, string payload)
        {
            var retainedMessages = await server.Server.GetRetainedApplicationMessagesAsync().ConfigureAwait(false);
            return retainedMessages.Any(message =>
                message.Topic == topic &&
                Encoding.UTF8.GetString(message.Payload) == payload);
        }

        /// <summary>
        /// 用原始 TCP 连接完成一次 V5 CONNECT，然后发送一个把 Session Expiry 从 0 改大的
        /// DISCONNECT，触发服务端协议错误路径。
        /// </summary>
        static async Task SendRawProtocolErrorConnectionAsync(
            int port,
            string clientId,
            MqttApplicationMessage willMessage,
            CancellationToken cancellationToken)
        {
            var formatterAdapter = new MqttPacketFormatterAdapter(MqttProtocolVersion.V500);

            var clientOptions = new MqttClientOptions
            {
                ClientId = clientId,
                CleanSession = true,
                KeepAlivePeriod = TimeSpan.FromSeconds(30),
                ProtocolVersion = MqttProtocolVersion.V500,
                SessionExpiryInterval = 0
            };

            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync("127.0.0.1", port).ConfigureAwait(false);

            using var stream = tcpClient.GetStream();

            var connectPacket = formatterAdapter.DataConverter.CreateConnectPacket(willMessage, clientOptions);
            await WritePacketAsync(stream, formatterAdapter.Encode(connectPacket), cancellationToken).ConfigureAwait(false);
            await ReadPacketAsync(stream, cancellationToken).ConfigureAwait(false);

            var disconnectPacket = new MqttDisconnectPacket
            {
                ReasonCode = MqttDisconnectReasonCode.NormalDisconnection,
                Properties = new MqttDisconnectPacketProperties
                {
                    SessionExpiryInterval = 10
                }
            };

            await WritePacketAsync(stream, formatterAdapter.Encode(disconnectPacket), cancellationToken).ConfigureAwait(false);
        }

        static async Task WritePacketAsync(
            NetworkStream stream,
            ArraySegment<byte> buffer,
            CancellationToken cancellationToken)
        {
            await stream
                .WriteAsync(buffer.Array, buffer.Offset, buffer.Count, cancellationToken)
                .ConfigureAwait(false);
        }

        static async Task ReadPacketAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            var fixedHeader = new byte[1];
            await ReadExactAsync(stream, fixedHeader, 0, 1, cancellationToken).ConfigureAwait(false);

            var remainingLength = 0;
            var multiplier = 1;

            while (true)
            {
                var digit = new byte[1];
                await ReadExactAsync(stream, digit, 0, 1, cancellationToken).ConfigureAwait(false);

                remainingLength += (digit[0] & 0x7F) * multiplier;
                if ((digit[0] & 0x80) == 0)
                {
                    break;
                }

                multiplier *= 128;
            }

            if (remainingLength > 0)
            {
                var body = new byte[remainingLength];
                await ReadExactAsync(stream, body, 0, remainingLength, cancellationToken).ConfigureAwait(false);
            }
        }

        static async Task ReadExactAsync(
            NetworkStream stream,
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            var read = 0;
            while (read < count)
            {
                var chunk = await stream
                    .ReadAsync(buffer, offset + read, count - read, cancellationToken)
                    .ConfigureAwait(false);

                if (chunk == 0)
                {
                    throw new IOException("The connection was closed before a full packet was received.");
                }

                read += chunk;
            }
        }

        static async Task WaitUntilAsync(Func<bool> condition, string message)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < WaitTimeout)
            {
                if (condition())
                {
                    return;
                }

                await Task.Delay(10).ConfigureAwait(false);
            }

            Assert.Fail(message);
        }

        static async Task WaitUntilAsync(Func<Task<bool>> condition, string message)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < WaitTimeout)
            {
                if (await condition().ConfigureAwait(false))
                {
                    return;
                }

                await Task.Delay(10).ConfigureAwait(false);
            }

            Assert.Fail(message);
        }
    }
}