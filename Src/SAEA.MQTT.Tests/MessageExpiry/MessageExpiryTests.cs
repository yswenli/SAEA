/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                               
 *Copyright (c) yswenli All Rights Reserved.
 *CLR版本： netstandard2.0
 *机器名称：WENLI-PC
 *公司名称：yswenli
 *命名空间：SAEA.MQTT.Tests.MessageExpiry
 *文件名： MessageExpiryTests
 *版本号： v26.4.23.1
 *唯一标识：6f0f0f70-2a4b-4d4d-9a0e-2f0d7a0b1c09
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2021/03/11 16:46:45
 *描述：MessageExpiryTests类
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/05 00:00:00
 *修改人： yswenli
 *版本号： v26.4.23.1
 *描述：MessageExpiryTests类
 *
 *****************************************************************************/
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Client;
using SAEA.MQTT.Client.Disconnecting;
using SAEA.MQTT.Client.Options;
using SAEA.MQTT.Client.Receiving;
using SAEA.MQTT.Client.Subscribing;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Protocol;
using SAEA.MQTT.Tests.TestInfrastructure;

namespace SAEA.MQTT.Tests.MessageExpiry
{
    /// <summary>
    /// MQTT 5.0 Message Expiry Interval 与 retained 消息 TTL / 持久化语义测试。
    /// </summary>
    [TestClass]
    public class MessageExpiryTests
    {
        static readonly DateTimeOffset StartTime = new DateTimeOffset(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);
        static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);
        static readonly TimeSpan NoMessageTimeout = TimeSpan.FromMilliseconds(400);

        FakeMqttTimeProvider _time;
        TestClockScope _clockScope;

        [TestInitialize]
        public void Initialize()
        {
            _time = new FakeMqttTimeProvider(StartTime);
            _clockScope = new TestClockScope(_time);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (_clockScope != null)
            {
                _clockScope.Dispose();
            }
        }

        [TestMethod]
        public async Task QueuedMessageExpired_IsNotSent()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await StartServerAsync();

            const string clientId = "message-expiry-queued";
            const string topic = "expiry/queued";

            var subscriber = await ConnectAsync(
                server,
                MqttProtocolVersion.V500,
                clientId,
                options =>
                {
                    options.WithCleanSession(false);
                    options.WithSessionExpiryInterval(300);
                },
                null,
                timeout.Token);

            await SubscribeAsync(subscriber, topic, timeout.Token);
            await DisconnectAsync(subscriber, timeout.Token);

            await WaitUntilAsync(
                async () => await HasSessionAsync(server, clientId).ConfigureAwait(false),
                "The persistent session should exist after disconnecting.");

            await WaitUntilAsync(
                async () => !await HasConnectionAsync(server, clientId).ConfigureAwait(false),
                "The connection should be cleaned up after disconnecting.");

            var publisher = await ConnectAsync(
                server,
                MqttProtocolVersion.V500,
                "message-expiry-queued-publisher",
                null,
                null,
                timeout.Token);

            try
            {
                await PublishAsync(publisher, topic, "expire-me", 5, false, timeout.Token);
            }
            finally
            {
                await DisconnectAsync(publisher, timeout.Token);
            }

            await WaitUntilAsync(
                async () => await GetPendingMessagesCountAsync(server, clientId).ConfigureAwait(false) >= 1,
                "The message should be queued for the offline persistent session.");

            _time.Advance(TimeSpan.FromSeconds(6));

            var collector = new TestMessageCollector();
            var reconnected = await ConnectAsync(
                server,
                MqttProtocolVersion.V500,
                clientId,
                options =>
                {
                    options.WithCleanSession(false);
                    options.WithSessionExpiryInterval(300);
                },
                collector,
                timeout.Token);

            try
            {
                Assert.IsNull(
                    await collector.WaitAsyncOrNull(NoMessageTimeout).ConfigureAwait(false),
                    "A queued message whose expiry elapsed before reconnect must not be sent.");
            }
            finally
            {
                await DisconnectAsync(reconnected, timeout.Token);
            }
        }

        [TestMethod]
        public async Task ForwardedExpiry_IsRemainingSeconds()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await StartServerAsync();

            const string clientId = "message-expiry-forwarded";
            const string topic = "expiry/forwarded";
            const string payload = "remaining-seconds";

            var subscriber = await ConnectAsync(
                server,
                MqttProtocolVersion.V500,
                clientId,
                options =>
                {
                    options.WithCleanSession(false);
                    options.WithSessionExpiryInterval(300);
                },
                null,
                timeout.Token);

            await SubscribeAsync(subscriber, topic, timeout.Token);
            await DisconnectAsync(subscriber, timeout.Token);

            await WaitUntilAsync(
                async () => !await HasConnectionAsync(server, clientId).ConfigureAwait(false),
                "The connection should be cleaned up after disconnecting.");

            var publisher = await ConnectAsync(
                server,
                MqttProtocolVersion.V500,
                "message-expiry-forwarded-publisher",
                null,
                null,
                timeout.Token);

            try
            {
                await PublishAsync(publisher, topic, payload, 10, false, timeout.Token);
            }
            finally
            {
                await DisconnectAsync(publisher, timeout.Token);
            }

            await WaitUntilAsync(
                async () => await GetPendingMessagesCountAsync(server, clientId).ConfigureAwait(false) >= 1,
                "The message should be queued for the offline persistent session.");

            _time.Advance(TimeSpan.FromSeconds(4));

            var collector = new TestMessageCollector();
            var reconnected = await ConnectAsync(
                server,
                MqttProtocolVersion.V500,
                clientId,
                options =>
                {
                    options.WithCleanSession(false);
                    options.WithSessionExpiryInterval(300);
                },
                collector,
                timeout.Token);

            try
            {
                var message = await collector.WaitAsync(WaitTimeout).ConfigureAwait(false);

                Assert.AreEqual(topic, message.Topic, "The forwarded message must keep the original topic.");
                Assert.AreEqual(payload, Encoding.UTF8.GetString(message.Payload), "The forwarded payload must match.");
                Assert.IsTrue(message.MessageExpiryInterval.HasValue, "The V5 forwarded message must carry Message Expiry Interval.");
                Assert.AreEqual(
                    6u,
                    message.MessageExpiryInterval.Value,
                    "A message with 10s expiry that waited 4s must be forwarded with the 6 remaining seconds.");
            }
            finally
            {
                await DisconnectAsync(reconnected, timeout.Token);
            }
        }

        [TestMethod]
        public async Task RetainedMessageExpired_IsNotDelivered()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await StartServerAsync();

            const string topic = "expiry/retained";
            const string payload = "retained-expire";

            var publisher = await ConnectAsync(
                server,
                MqttProtocolVersion.V500,
                "message-expiry-retained-publisher",
                null,
                null,
                timeout.Token);

            try
            {
                await PublishAsync(publisher, topic, payload, 10, true, timeout.Token);
            }
            finally
            {
                await DisconnectAsync(publisher, timeout.Token);
            }

            await WaitUntilAsync(
                async () => await HasRetainedTopicAsync(server, topic).ConfigureAwait(false),
                "The retained message should be stored before it expires.");

            _time.Advance(TimeSpan.FromSeconds(11));

            var collector = new TestMessageCollector();
            var subscriber = await ConnectAsync(
                server,
                MqttProtocolVersion.V500,
                "message-expiry-retained-subscriber",
                null,
                collector,
                timeout.Token);

            try
            {
                await SubscribeAsync(subscriber, topic, timeout.Token);

                Assert.IsNull(
                    await collector.WaitAsyncOrNull(NoMessageTimeout).ConfigureAwait(false),
                    "An expired retained message must not be delivered to a new subscriber.");

                Assert.IsFalse(
                    await HasRetainedTopicAsync(server, topic).ConfigureAwait(false),
                    "An expired retained message must be removed from the retained store.");
            }
            finally
            {
                await DisconnectAsync(subscriber, timeout.Token);
            }
        }

        [TestMethod]
        public async Task RetainedMessageExpiry_SurvivesPersistenceReload()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            const string topic = "expiry/retained-persisted";
            const string payload = "retained-persisted";

            var storage = new InMemoryServerStorage();

            var serverA = await MqttTestServer.StartAsync(builder => builder
                .WithPersistentSessions()
                .WithStorage(storage));

            try
            {
                var publisher = await ConnectAsync(
                    serverA,
                    MqttProtocolVersion.V500,
                    "message-expiry-persist-publisher",
                    null,
                    null,
                    timeout.Token);

                try
                {
                    await PublishAsync(publisher, topic, payload, 100, true, timeout.Token);
                }
                finally
                {
                    await DisconnectAsync(publisher, timeout.Token);
                }

                await WaitUntilAsync(
                    () => storage.EntryCount >= 1,
                    "The retained message should be persisted before the server stops.");
            }
            finally
            {
                await serverA.DisposeAsync();
            }

            await using var serverB = await MqttTestServer.StartAsync(builder => builder
                .WithPersistentSessions()
                .WithStorage(storage));

            var collector = new TestMessageCollector();
            var firstSubscriber = await ConnectAsync(
                serverB,
                MqttProtocolVersion.V500,
                "message-expiry-persist-subscriber-1",
                null,
                collector,
                timeout.Token);

            try
            {
                await SubscribeAsync(firstSubscriber, topic, timeout.Token);

                var message = await collector.WaitAsync(WaitTimeout).ConfigureAwait(false);

                Assert.AreEqual(topic, message.Topic, "The persisted retained message must be restored after a restart.");
                Assert.AreEqual(payload, Encoding.UTF8.GetString(message.Payload), "The restored retained payload must match.");
                Assert.IsTrue(message.MessageExpiryInterval.HasValue, "The restored retained message must keep its expiry metadata.");
                Assert.IsTrue(
                    message.MessageExpiryInterval.Value >= 90 && message.MessageExpiryInterval.Value <= 100,
                    "The restored retained message must keep its original expiry interval (was " +
                    message.MessageExpiryInterval.Value + ").");
            }
            finally
            {
                await DisconnectAsync(firstSubscriber, timeout.Token);
            }

            _time.Advance(TimeSpan.FromSeconds(101));

            var secondCollector = new TestMessageCollector();
            var secondSubscriber = await ConnectAsync(
                serverB,
                MqttProtocolVersion.V500,
                "message-expiry-persist-subscriber-2",
                null,
                secondCollector,
                timeout.Token);

            try
            {
                await SubscribeAsync(secondSubscriber, topic, timeout.Token);

                Assert.IsNull(
                    await secondCollector.WaitAsyncOrNull(NoMessageTimeout).ConfigureAwait(false),
                    "A restored retained message must still expire after its original deadline.");
            }
            finally
            {
                await DisconnectAsync(secondSubscriber, timeout.Token);
            }
        }

        [TestMethod]
        public async Task V311_DoesNotReceiveMessageExpiryProperty()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await StartServerAsync();

            const string topic = "expiry/v311";

            var collector = new TestMessageCollector();
            var subscriber = await ConnectAsync(
                server,
                MqttProtocolVersion.V311,
                "message-expiry-v311-subscriber",
                null,
                collector,
                timeout.Token);

            try
            {
                await SubscribeAsync(subscriber, topic, timeout.Token);

                var publisher = await ConnectAsync(
                    server,
                    MqttProtocolVersion.V500,
                    "message-expiry-v311-publisher",
                    null,
                    null,
                    timeout.Token);

                try
                {
                    await PublishAsync(publisher, topic, "v311-payload", 30, false, timeout.Token);
                }
                finally
                {
                    await DisconnectAsync(publisher, timeout.Token);
                }

                var message = await collector.WaitAsync(WaitTimeout).ConfigureAwait(false);

                Assert.AreEqual(topic, message.Topic, "The V3.1.1 subscriber must receive the published message.");
                Assert.IsNull(
                    message.MessageExpiryInterval,
                    "A V3.1.1 subscriber must not receive the Message Expiry Interval property.");
            }
            finally
            {
                await DisconnectAsync(subscriber, timeout.Token);
            }
        }

        [TestMethod]
        public async Task RetainedExpiry_OverwriteRefreshesRetainedAt()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await StartServerAsync();

            const string topic = "expiry/retained-overwrite";

            var publisher = await ConnectAsync(
                server,
                MqttProtocolVersion.V500,
                "message-expiry-overwrite-publisher",
                null,
                null,
                timeout.Token);

            try
            {
                await PublishAsync(publisher, topic, "first", 10, true, timeout.Token);

                await WaitUntilAsync(
                    async () => await RetainedPayloadEqualsAsync(server, topic, "first").ConfigureAwait(false),
                    "The first retained message should be stored.");

                await PublishAsync(publisher, topic, "second", 20, true, timeout.Token);

                await WaitUntilAsync(
                    async () => await RetainedPayloadEqualsAsync(server, topic, "second").ConfigureAwait(false),
                    "The retained message overwrite should replace the stored payload.");
            }
            finally
            {
                await DisconnectAsync(publisher, timeout.Token);
            }

            var collector = new TestMessageCollector();
            var subscriber = await ConnectAsync(
                server,
                MqttProtocolVersion.V500,
                "message-expiry-overwrite-subscriber",
                null,
                collector,
                timeout.Token);

            try
            {
                await SubscribeAsync(subscriber, topic, timeout.Token);

                var message = await collector.WaitAsync(WaitTimeout).ConfigureAwait(false);

                Assert.AreEqual(topic, message.Topic, "The overwritten retained message must keep its topic.");
                Assert.AreEqual("second", Encoding.UTF8.GetString(message.Payload), "The newest retained payload must win.");
                Assert.IsTrue(message.MessageExpiryInterval.HasValue, "The overwritten retained message must carry Message Expiry Interval.");
                Assert.IsTrue(
                    message.MessageExpiryInterval.Value >= 19 && message.MessageExpiryInterval.Value <= 20,
                    "An overwrite must refresh the retained deadline (was " +
                    message.MessageExpiryInterval.Value + ").");
            }
            finally
            {
                await DisconnectAsync(subscriber, timeout.Token);
            }
        }

        static Task<MqttTestServer> StartServerAsync()
        {
            return MqttTestServer.StartAsync(builder => builder.WithPersistentSessions());
        }

        static async Task<IMqttClient> ConnectAsync(
            MqttTestServer server,
            MqttProtocolVersion protocolVersion,
            string clientId,
            Action<MqttClientOptionsBuilder> configure,
            IMqttApplicationMessageReceivedHandler handler,
            CancellationToken cancellationToken)
        {
            var client = new MqttFactory().CreateMqttClient();

            if (handler != null)
            {
                // 必须在 ConnectAsync 之前注册，避免离线队列消息在注册前就被分发。
                client.ApplicationMessageReceivedHandler = handler;
            }

            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer("127.0.0.1", server.Port)
                .WithProtocolVersion(protocolVersion)
                .WithClientId(clientId)
                .WithCleanSession()
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(5))
                .WithCommunicationTimeout(TimeSpan.FromSeconds(10));

            configure?.Invoke(optionsBuilder);

            try
            {
                await client.ConnectAsync(optionsBuilder.Build(), cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                client.Dispose();
                throw;
            }

            return client;
        }

        static Task<MqttClientSubscribeResult> SubscribeAsync(
            IMqttClient client,
            string topic,
            CancellationToken cancellationToken)
        {
            return client.SubscribeAsync(
                new MqttClientSubscribeOptionsBuilder().WithTopicFilter(topic).Build(),
                cancellationToken);
        }

        static Task PublishAsync(
            IMqttClient client,
            string topic,
            string payload,
            uint? messageExpiryInterval,
            bool retain,
            CancellationToken cancellationToken)
        {
            var applicationMessage = new MqttApplicationMessage
            {
                Topic = topic,
                Payload = Encoding.UTF8.GetBytes(payload),
                QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce,
                Retain = retain,
                MessageExpiryInterval = messageExpiryInterval
            };

            return client.PublishAsync(applicationMessage, cancellationToken);
        }

        static async Task DisconnectAsync(IMqttClient client, CancellationToken cancellationToken)
        {
            try
            {
                if (client.IsConnected)
                {
                    await client
                        .DisconnectAsync(new MqttClientDisconnectOptions(), cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            finally
            {
                client.Dispose();
            }
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

        static async Task<long> GetPendingMessagesCountAsync(MqttTestServer server, string clientId)
        {
            var sessions = await server.Server.GetSessionStatusAsync().ConfigureAwait(false);
            var session = sessions.FirstOrDefault(s => s.ClientId == clientId);
            return session == null ? 0 : session.PendingApplicationMessagesCount;
        }

        static async Task<bool> HasRetainedTopicAsync(MqttTestServer server, string topic)
        {
            var retainedMessages = await server.Server.GetRetainedApplicationMessagesAsync().ConfigureAwait(false);
            return retainedMessages.Any(message => message.Topic == topic);
        }

        static async Task<bool> RetainedPayloadEqualsAsync(MqttTestServer server, string topic, string payload)
        {
            var retainedMessages = await server.Server.GetRetainedApplicationMessagesAsync().ConfigureAwait(false);
            return retainedMessages.Any(message =>
                message.Topic == topic &&
                message.Payload != null &&
                Encoding.UTF8.GetString(message.Payload) == payload);
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