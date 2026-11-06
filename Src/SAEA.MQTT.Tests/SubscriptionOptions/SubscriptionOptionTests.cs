using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Client;
using SAEA.MQTT.Client.Subscribing;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Protocol;
using SAEA.MQTT.Tests.TestInfrastructure;

namespace SAEA.MQTT.Tests.SubscriptionOptions
{
    /// <summary>
    /// 订阅选项（No Local、Retain As Published、Retain Handling）在投递时的生效情况
    /// </summary>
    [TestClass]
    public class SubscriptionOptionTests
    {
        static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);
        static readonly TimeSpan NoMessageTimeout = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// 验证 No Local 订阅下发布者收不到自己发布的消息，但能收到其他客户端发布的消息
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task NoLocal_PublisherDoesNotReceiveOwnMessage()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync();
            var collector = new TestMessageCollector();

            await using var subscriber = await ConnectSubscriberAsync(server, "options-nolocal-a", collector, timeout.Token);
            await SubscribeAsync(
                subscriber,
                new MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter("sport/#", MqttQualityOfServiceLevel.AtMostOnce, noLocal: true),
                timeout.Token);

            await PublishAsync(subscriber, "sport/tennis", "own", MqttQualityOfServiceLevel.AtMostOnce, false, timeout.Token);

            Assert.IsNull(
                await collector.WaitAsyncOrNull(NoMessageTimeout).ConfigureAwait(false),
                "A publisher must not receive its own message for a NoLocal subscription.");

            await using var publisher = await ConnectClientAsync(server, "options-nolocal-b", timeout.Token);
            await PublishAsync(publisher, "sport/tennis", "other", MqttQualityOfServiceLevel.AtMostOnce, false, timeout.Token);

            var received = await collector.WaitAsync(WaitTimeout).ConfigureAwait(false);
            Assert.AreEqual("other", Encoding.UTF8.GetString(received.Payload));
        }

        /// <summary>
        /// 验证 Retain As Published 为 false 时，转发实时消息清空 Retain 标记
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task RetainAsPublishedFalse_ClearsRetainFlag()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync();
            var collector = new TestMessageCollector();

            await using var subscriber = await ConnectSubscriberAsync(server, "options-rap-false-a", collector, timeout.Token);
            await SubscribeAsync(
                subscriber,
                new MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter(
                        "sport/#",
                        MqttQualityOfServiceLevel.AtMostOnce,
                        retainAsPublished: false,
                        retainHandling: MqttRetainHandling.DoNotSendOnSubscribe),
                timeout.Token);

            await using var publisher = await ConnectClientAsync(server, "options-rap-false-b", timeout.Token);
            await PublishAsync(publisher, "sport/tennis", "one", MqttQualityOfServiceLevel.AtMostOnce, true, timeout.Token);

            var received = await collector.WaitAsync(WaitTimeout).ConfigureAwait(false);
            Assert.IsFalse(received.Retain, "RetainAsPublished=false must clear the Retain flag.");
        }

        /// <summary>
        /// 验证 Retain As Published 为 true 时，转发实时消息保留原始的 Retain 标记
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task RetainAsPublishedTrue_KeepsRetainFlag()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync();
            var collector = new TestMessageCollector();

            await using var subscriber = await ConnectSubscriberAsync(server, "options-rap-true-a", collector, timeout.Token);
            await SubscribeAsync(
                subscriber,
                new MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter(
                        "sport/#",
                        MqttQualityOfServiceLevel.AtMostOnce,
                        retainAsPublished: true,
                        retainHandling: MqttRetainHandling.DoNotSendOnSubscribe),
                timeout.Token);

            await using var publisher = await ConnectClientAsync(server, "options-rap-true-b", timeout.Token);
            await PublishAsync(publisher, "sport/tennis", "one", MqttQualityOfServiceLevel.AtMostOnce, true, timeout.Token);

            var received = await collector.WaitAsync(WaitTimeout).ConfigureAwait(false);
            Assert.IsTrue(received.Retain, "RetainAsPublished=true must keep the original Retain flag.");
        }

        /// <summary>
        /// 验证 Retain Handling 为 0 时订阅后立即重放保留消息且置 Retain 标记
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task RetainHandlingSendAtSubscribe_ReplaysRetained()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync();

            await using var publisher = await ConnectClientAsync(server, "options-rh0-publisher", timeout.Token);
            await PublishAsync(publisher, "sport/retained", "retained", MqttQualityOfServiceLevel.AtMostOnce, true, timeout.Token);
            await WaitForRetainedAsync(server, "sport/retained", timeout.Token);

            var collector = new TestMessageCollector();
            await using var subscriber = await ConnectSubscriberAsync(server, "options-rh0-a", collector, timeout.Token);
            await SubscribeAsync(
                subscriber,
                new MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter(
                        "sport/#",
                        MqttQualityOfServiceLevel.AtMostOnce,
                        retainHandling: MqttRetainHandling.SendAtSubscribe),
                timeout.Token);

            var replayed = await collector.WaitAsync(WaitTimeout).ConfigureAwait(false);
            Assert.AreEqual("sport/retained", replayed.Topic);
            Assert.IsTrue(replayed.Retain, "Retained replays must set the Retain flag.");
        }

        /// <summary>
        /// 验证 Retain Handling 为 1 时只在首次订阅时重放保留消息，重复订阅同一过滤器不再重放
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task RetainHandlingSendIfNew_ReplaysOnlyOnFirstSubscription()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync();

            await using var publisher = await ConnectClientAsync(server, "options-rh1-publisher", timeout.Token);
            await PublishAsync(publisher, "sport/retained", "retained", MqttQualityOfServiceLevel.AtMostOnce, true, timeout.Token);
            await WaitForRetainedAsync(server, "sport/retained", timeout.Token);

            var collector = new TestMessageCollector();
            await using var subscriber = await ConnectSubscriberAsync(server, "options-rh1-a", collector, timeout.Token);

            await SubscribeAsync(
                subscriber,
                new MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter(
                        "sport/#",
                        MqttQualityOfServiceLevel.AtMostOnce,
                        retainHandling: MqttRetainHandling.SendAtSubscribeIfNewSubscriptionOnly),
                timeout.Token);

            var replayed = await collector.WaitAsync(WaitTimeout).ConfigureAwait(false);
            Assert.AreEqual("sport/retained", replayed.Topic);

            await SubscribeAsync(
                subscriber,
                new MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter(
                        "sport/#",
                        MqttQualityOfServiceLevel.AtMostOnce,
                        retainHandling: MqttRetainHandling.SendAtSubscribeIfNewSubscriptionOnly),
                timeout.Token);

            Assert.IsNull(
                await collector.WaitAsyncOrNull(NoMessageTimeout).ConfigureAwait(false),
                "RetainHandling=1 must not replay on an existing subscription.");
        }

        /// <summary>
        /// 验证 Retain Handling 为 2 时订阅不重放保留消息，但后续实时发布仍能正常收到
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task RetainHandlingDoNotSend_DoesNotReplay()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync();

            await using var publisher = await ConnectClientAsync(server, "options-rh2-publisher", timeout.Token);
            await PublishAsync(publisher, "sport/retained", "retained", MqttQualityOfServiceLevel.AtMostOnce, true, timeout.Token);
            await WaitForRetainedAsync(server, "sport/retained", timeout.Token);

            var collector = new TestMessageCollector();
            await using var subscriber = await ConnectSubscriberAsync(server, "options-rh2-a", collector, timeout.Token);
            await SubscribeAsync(
                subscriber,
                new MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter(
                        "sport/#",
                        MqttQualityOfServiceLevel.AtMostOnce,
                        retainHandling: MqttRetainHandling.DoNotSendOnSubscribe),
                timeout.Token);

            Assert.IsNull(
                await collector.WaitAsyncOrNull(NoMessageTimeout).ConfigureAwait(false),
                "RetainHandling=2 must not replay retained messages.");

            await PublishAsync(publisher, "sport/live", "live", MqttQualityOfServiceLevel.AtMostOnce, true, timeout.Token);

            var live = await collector.WaitAsync(WaitTimeout).ConfigureAwait(false);
            Assert.AreEqual("sport/live", live.Topic);
            Assert.AreEqual("live", Encoding.UTF8.GetString(live.Payload));
        }

        /// <summary>
        /// 验证多条过滤器同时命中时按最高订阅 QoS 只投递一条消息
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task MultipleMatchingFilters_OneMessageHighestQualityOfService()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync();
            var collector = new TestMessageCollector();

            await using var subscriber = await ConnectSubscriberAsync(server, "options-multi-a", collector, timeout.Token);
            await SubscribeAsync(
                subscriber,
                new MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter("sport/#", MqttQualityOfServiceLevel.AtMostOnce)
                    .WithTopicFilter("sport/tennis", MqttQualityOfServiceLevel.AtLeastOnce),
                timeout.Token);

            await using var publisher = await ConnectClientAsync(server, "options-multi-b", timeout.Token);
            await PublishAsync(publisher, "sport/tennis", "one", MqttQualityOfServiceLevel.AtLeastOnce, false, timeout.Token);

            var received = await collector.WaitAsync(WaitTimeout).ConfigureAwait(false);
            Assert.AreEqual(MqttQualityOfServiceLevel.AtLeastOnce, received.QualityOfServiceLevel);

            Assert.IsNull(
                await collector.WaitAsyncOrNull(NoMessageTimeout).ConfigureAwait(false),
                "Multiple matching filters must be merged into a single PUBLISH.");
        }

        static async Task<MqttTestClient> ConnectClientAsync(MqttTestServer server, string clientId, CancellationToken cancellationToken)
        {
            return await MqttTestClient.ConnectAsync(
                server.Port,
                MqttProtocolVersion.V500,
                clientId,
                null,
                cancellationToken).ConfigureAwait(false);
        }

        static async Task<MqttTestClient> ConnectSubscriberAsync(
            MqttTestServer server,
            string clientId,
            TestMessageCollector collector,
            CancellationToken cancellationToken)
        {
            var client = await ConnectClientAsync(server, clientId, cancellationToken).ConfigureAwait(false);
            client.Client.ApplicationMessageReceivedHandler = collector;
            return client;
        }

        static Task SubscribeAsync(
            MqttTestClient client,
            MqttClientSubscribeOptionsBuilder builder,
            CancellationToken cancellationToken)
        {
            return client.Client.SubscribeAsync(builder.Build(), cancellationToken);
        }

        static Task PublishAsync(
            MqttTestClient client,
            string topic,
            string payload,
            MqttQualityOfServiceLevel qualityOfServiceLevel,
            bool retain,
            CancellationToken cancellationToken)
        {
            return client.Client.PublishAsync(
                new MqttApplicationMessage
                {
                    Topic = topic,
                    Payload = Encoding.UTF8.GetBytes(payload),
                    QualityOfServiceLevel = qualityOfServiceLevel,
                    Retain = retain
                },
                cancellationToken);
        }

        static async Task WaitForRetainedAsync(MqttTestServer server, string topic, CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow + WaitTimeout;
            while (DateTime.UtcNow < deadline)
            {
                var retained = await server.Server.GetRetainedApplicationMessagesAsync().ConfigureAwait(false);
                if (retained.Any(message => message.Topic == topic))
                {
                    return;
                }

                await Task.Delay(25, cancellationToken).ConfigureAwait(false);
            }

            Assert.Fail("The retained message for topic '" + topic + "' was not stored within " + WaitTimeout + ".");
        }
    }
}