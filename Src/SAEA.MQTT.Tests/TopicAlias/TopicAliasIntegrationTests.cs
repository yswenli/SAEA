using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Client.Subscribing;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;
using SAEA.MQTT.Tests.TestInfrastructure;

namespace SAEA.MQTT.Tests.TopicAlias
{
    /// <summary>
    /// 主题别名在服务端入站/出站以及客户端双向通信中的端到端行为
    /// </summary>
    [TestClass]
    public class TopicAliasIntegrationTests
    {
        static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);
        static readonly TimeSpan NoMessageTimeout = TimeSpan.FromMilliseconds(300);

        /// <summary>
        /// 验证客户端使用主题别名 0（协议非法值）时，服务端以 Topic Alias Invalid 原因码断开且不投递该消息
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task ServerInbound_ZeroAlias_DisconnectsWithTopicAliasInvalid()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await StartServerAsync(2);
            var collector = new TestMessageCollector();
            await using var observer = await ConnectObserverAsync(server, "alias/inbound/#", collector, timeout.Token);

            await using var rawClient = await RawMqttTestClient.ConnectAsync(
                server.Port,
                "raw-zero-alias",
                null,
                timeout.Token);

            await rawClient.SendPublishAsync("alias/inbound/zero", "payload", 0, timeout.Token);

            var disconnect = await rawClient.WaitForDisconnectAsync(WaitTimeout, timeout.Token);

            Assert.AreEqual(MqttDisconnectReasonCode.TopicAliasInvalid, disconnect.ReasonCode.Value);
            Assert.IsNull(
                await collector.WaitAsyncOrNull(NoMessageTimeout).ConfigureAwait(false),
                "A PUBLISH with Topic Alias 0 must not be dispatched.");
        }

        /// <summary>
        /// 验证别名超过服务端宣告的上限（上限 2 时用 3）时，服务端以 Topic Alias Invalid 原因码断开且不投递
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task ServerInbound_AliasAboveMaximum_DisconnectsWithTopicAliasInvalid()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await StartServerAsync(2);
            var collector = new TestMessageCollector();
            await using var observer = await ConnectObserverAsync(server, "alias/inbound/#", collector, timeout.Token);

            await using var rawClient = await RawMqttTestClient.ConnectAsync(
                server.Port,
                "raw-exceeds-alias",
                null,
                timeout.Token);

            await rawClient.SendPublishAsync("alias/inbound/exceeds", "payload", 3, timeout.Token);

            var disconnect = await rawClient.WaitForDisconnectAsync(WaitTimeout, timeout.Token);

            Assert.AreEqual(MqttDisconnectReasonCode.TopicAliasInvalid, disconnect.ReasonCode.Value);
            Assert.IsNull(
                await collector.WaitAsyncOrNull(NoMessageTimeout).ConfigureAwait(false),
                "A PUBLISH with a Topic Alias above the advertised maximum must not be dispatched.");
        }

        /// <summary>
        /// 验证主题为空且别名尚未建立映射时，服务端以 Topic Alias Invalid 原因码断开且不投递
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task ServerInbound_UnknownAlias_DisconnectsWithTopicAliasInvalid()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await StartServerAsync(2);
            var collector = new TestMessageCollector();
            await using var observer = await ConnectObserverAsync(server, "alias/inbound/#", collector, timeout.Token);

            await using var rawClient = await RawMqttTestClient.ConnectAsync(
                server.Port,
                "raw-unknown-alias",
                null,
                timeout.Token);

            await rawClient.SendPublishAsync(string.Empty, "payload", 1, timeout.Token);

            var disconnect = await rawClient.WaitForDisconnectAsync(WaitTimeout, timeout.Token);

            Assert.AreEqual(MqttDisconnectReasonCode.TopicAliasInvalid, disconnect.ReasonCode.Value);
            Assert.IsNull(
                await collector.WaitAsyncOrNull(NoMessageTimeout).ConfigureAwait(false),
                "An empty topic with an unknown Topic Alias must not be dispatched.");
        }

        /// <summary>
        /// 验证入站别名先建立映射、空主题复用该映射、再用新主题覆盖映射后按解析结果路由消息
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task ServerInbound_MappingAndOverwrite_RoutesResolvedTopic()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await StartServerAsync(2);
            var collector = new TestMessageCollector();
            await using var observer = await ConnectObserverAsync(server, "alias/inbound/#", collector, timeout.Token);

            await using var rawClient = await RawMqttTestClient.ConnectAsync(
                server.Port,
                "raw-mapping",
                null,
                timeout.Token);

            await rawClient.SendPublishAsync("alias/inbound/one", "one", 1, timeout.Token);
            var first = await collector.WaitAsync(WaitTimeout).ConfigureAwait(false);
            Assert.AreEqual("alias/inbound/one", first.Topic);
            Assert.AreEqual("one", Encoding.UTF8.GetString(first.Payload));

            await rawClient.SendPublishAsync(string.Empty, "two", 1, timeout.Token);
            var second = await collector.WaitAsync(WaitTimeout).ConfigureAwait(false);
            Assert.AreEqual("alias/inbound/one", second.Topic, "The known alias must resolve to the established topic.");
            Assert.AreEqual("two", Encoding.UTF8.GetString(second.Payload));

            await rawClient.SendPublishAsync("alias/inbound/two", "three", 1, timeout.Token);
            var third = await collector.WaitAsync(WaitTimeout).ConfigureAwait(false);
            Assert.AreEqual("alias/inbound/two", third.Topic);
            Assert.AreEqual("three", Encoding.UTF8.GetString(third.Payload));

            await rawClient.SendPublishAsync(string.Empty, "four", 1, timeout.Token);
            var fourth = await collector.WaitAsync(WaitTimeout).ConfigureAwait(false);
            Assert.AreEqual("alias/inbound/two", fourth.Topic, "A non-empty topic must overwrite the existing alias mapping.");
            Assert.AreEqual("four", Encoding.UTF8.GetString(fourth.Payload));
        }

        /// <summary>
        /// 验证客户端宣告的主题别名上限为 0 时，服务端出站 PUBLISH 始终携带完整主题、不带别名属性
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task ServerOutbound_ClientMaximumZero_NeverSendsAlias()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await StartServerAsync(2);
            await using var rawSubscriber = await RawMqttTestClient.ConnectAsync(
                server.Port,
                "raw-subscriber-zero",
                0,
                timeout.Token);

            await rawSubscriber.SubscribeAsync("alias/outbound/zero", timeout.Token);

            await using var publisher = await MqttTestClient.ConnectAsync(
                server.Port,
                MqttProtocolVersion.V500,
                "outbound-publisher-zero",
                null,
                timeout.Token);

            await PublishAsync(publisher.Client, "alias/outbound/zero", "one", timeout.Token);
            await PublishAsync(publisher.Client, "alias/outbound/zero", "two", timeout.Token);

            var first = await ReadPublishAsync(rawSubscriber, timeout.Token);
            Assert.AreEqual("alias/outbound/zero", first.Topic);
            Assert.IsTrue(first.Properties == null || first.Properties.TopicAlias == null);

            var second = await ReadPublishAsync(rawSubscriber, timeout.Token);
            Assert.AreEqual("alias/outbound/zero", second.Topic);
            Assert.IsTrue(second.Properties == null || second.Properties.TopicAlias == null);
        }

        /// <summary>
        /// 验证客户端宣告非零别名上限时，服务端首条 PUBLISH 带完整主题与别名 1，第二条省略主题只带别名
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task ServerOutbound_ClientMaximumPositive_SendsAliasAndOmitsTopicAfterFirst()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await StartServerAsync(2);
            await using var rawSubscriber = await RawMqttTestClient.ConnectAsync(
                server.Port,
                "raw-subscriber-positive",
                5,
                timeout.Token);

            const string topic = "alias/outbound/positive";
            await rawSubscriber.SubscribeAsync(topic, timeout.Token);

            await using var publisher = await MqttTestClient.ConnectAsync(
                server.Port,
                MqttProtocolVersion.V500,
                "outbound-publisher-positive",
                null,
                timeout.Token);

            await PublishAsync(publisher.Client, topic, "one", timeout.Token);
            await PublishAsync(publisher.Client, topic, "two", timeout.Token);

            var first = await ReadPublishAsync(rawSubscriber, timeout.Token);
            Assert.AreEqual(topic, first.Topic, "The first PUBLISH must include the full topic.");
            Assert.IsNotNull(first.Properties);
            Assert.AreEqual((ushort)1, first.Properties.TopicAlias.Value);
            Assert.AreEqual("one", Encoding.UTF8.GetString(first.Payload));

            var second = await ReadPublishAsync(rawSubscriber, timeout.Token);
            Assert.IsTrue(string.IsNullOrEmpty(second.Topic), "The second PUBLISH must omit the topic.");
            Assert.IsNotNull(second.Properties);
            Assert.AreEqual((ushort)1, second.Properties.TopicAlias.Value);
            Assert.AreEqual("two", Encoding.UTF8.GetString(second.Payload));
        }

        /// <summary>
        /// 验证订阅端与发布端都开启主题别名后，出站与入站双向使用别名且客户端最终解析回原主题
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task ClientBidirectional_UsesAliasesInBothDirections()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await StartServerAsync(10);

            const string topic = "alias/bidirectional";

            var collector = new TestMessageCollector();
            await using var subscriber = await MqttTestClient.ConnectAsync(
                server.Port,
                MqttProtocolVersion.V500,
                "bidirectional-subscriber",
                options => options.WithTopicAliasMaximum(10),
                timeout.Token);

            subscriber.Client.ApplicationMessageReceivedHandler = collector;

            await subscriber.Client.SubscribeAsync(
                new MqttClientSubscribeOptionsBuilder().WithTopicFilter(topic).Build(),
                timeout.Token);

            await using var publisher = await MqttTestClient.ConnectAsync(
                server.Port,
                MqttProtocolVersion.V500,
                "bidirectional-publisher",
                options => options.WithTopicAliasMaximum(10),
                timeout.Token);

            await PublishAsync(publisher.Client, topic, "one", timeout.Token);
            await PublishAsync(publisher.Client, topic, "two", timeout.Token);

            var first = await collector.WaitAsync(WaitTimeout).ConfigureAwait(false);
            Assert.AreEqual(topic, first.Topic);
            Assert.AreEqual("one", Encoding.UTF8.GetString(first.Payload));

            var second = await collector.WaitAsync(WaitTimeout).ConfigureAwait(false);
            Assert.AreEqual(topic, second.Topic, "The client must resolve the inbound alias to the original topic.");
            Assert.AreEqual("two", Encoding.UTF8.GetString(second.Payload));
        }

        /// <summary>
        /// 验证主题别名映射按连接隔离：新连接沿用旧别名的空主题发布会以 Topic Alias Invalid 原因码断开
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task TopicAlias_IsNotReusedAcrossConnections()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await StartServerAsync(2);
            var collector = new TestMessageCollector();
            await using var observer = await ConnectObserverAsync(server, "alias/reuse/#", collector, timeout.Token);

            var firstClient = await RawMqttTestClient.ConnectAsync(
                server.Port,
                "raw-reuse-first",
                null,
                timeout.Token);

            await firstClient.SendPublishAsync("alias/reuse/first", "first", 1, timeout.Token);

            var firstMessage = await collector.WaitAsync(WaitTimeout).ConfigureAwait(false);
            Assert.AreEqual("alias/reuse/first", firstMessage.Topic);

            await firstClient.DisposeAsync().ConfigureAwait(false);

            await using var secondClient = await RawMqttTestClient.ConnectAsync(
                server.Port,
                "raw-reuse-second",
                null,
                timeout.Token);

            await secondClient.SendPublishAsync(string.Empty, "second", 1, timeout.Token);

            var disconnect = await secondClient.WaitForDisconnectAsync(WaitTimeout, timeout.Token);
            Assert.AreEqual(
                MqttDisconnectReasonCode.TopicAliasInvalid,
                disconnect.ReasonCode.Value,
                "A new connection must not reuse the previous connection's Topic Alias mapping.");

            Assert.IsNull(
                await collector.WaitAsyncOrNull(NoMessageTimeout).ConfigureAwait(false),
                "The second connection's unknown alias must not be dispatched.");
        }

        static Task<MqttTestServer> StartServerAsync(ushort topicAliasMaximum)
        {
            return MqttTestServer.StartAsync(builder => builder.WithTopicAliasMaximum(topicAliasMaximum));
        }

        static async Task<MqttTestClient> ConnectObserverAsync(
            MqttTestServer server,
            string topicFilter,
            TestMessageCollector collector,
            CancellationToken cancellationToken)
        {
            var observer = await MqttTestClient.ConnectAsync(
                server.Port,
                MqttProtocolVersion.V500,
                "alias-observer-" + Guid.NewGuid().ToString("N"),
                options => options.WithCleanSession(),
                cancellationToken).ConfigureAwait(false);

            observer.Client.ApplicationMessageReceivedHandler = collector;

            await observer.Client.SubscribeAsync(
                new MqttClientSubscribeOptionsBuilder().WithTopicFilter(topicFilter).Build(),
                cancellationToken).ConfigureAwait(false);

            return observer;
        }

        static Task PublishAsync(SAEA.MQTT.Client.IMqttClient client, string topic, string payload, CancellationToken cancellationToken)
        {
            return client.PublishAsync(
                new MqttApplicationMessage
                {
                    Topic = topic,
                    Payload = Encoding.UTF8.GetBytes(payload),
                    QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce
                },
                cancellationToken);
        }

        static async Task<MqttPublishPacket> ReadPublishAsync(RawMqttTestClient client, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < WaitTimeout)
            {
                var packet = await client.ReadPacketAsync(cancellationToken).ConfigureAwait(false);
                var publishPacket = packet as MqttPublishPacket;
                if (publishPacket != null)
                {
                    return publishPacket;
                }
            }

            throw new TimeoutException("No PUBLISH packet was received within " + WaitTimeout + ".");
        }
    }
}