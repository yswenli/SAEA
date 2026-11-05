using System;
using System.Collections.Generic;
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
    [TestClass]
    public class SubscriptionIdentifierTests
    {
        static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);
        static readonly TimeSpan NoMessageTimeout = TimeSpan.FromMilliseconds(500);

        [TestMethod]
        public async Task SubscriptionIdentifier_IsForwardedToSubscriber()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync();
            var collector = new TestMessageCollector();

            await using var subscriber = await ConnectSubscriberAsync(server, MqttProtocolVersion.V500, "identifier-single-a", collector, timeout.Token);
            await subscriber.Client.SubscribeAsync(
                new MqttClientSubscribeOptionsBuilder()
                    .WithSubscriptionIdentifier(7)
                    .WithTopicFilter("sport/#")
                    .Build(),
                timeout.Token);

            await using var publisher = await ConnectClientAsync(server, MqttProtocolVersion.V500, "identifier-single-b", timeout.Token);
            await PublishAsync(publisher, "sport/tennis", "one", timeout.Token);

            var received = await collector.WaitAsync(WaitTimeout).ConfigureAwait(false);
            Assert.IsNotNull(received.SubscriptionIdentifiers);
            CollectionAssert.AreEqual(new List<uint> { 7 }, received.SubscriptionIdentifiers);
        }

        [TestMethod]
        public async Task MultipleSubscriptionIdentifiers_AreMergedIntoOnePublish()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync();
            var collector = new TestMessageCollector();

            await using var subscriber = await ConnectSubscriberAsync(server, MqttProtocolVersion.V500, "identifier-merge-a", collector, timeout.Token);

            await subscriber.Client.SubscribeAsync(
                new MqttClientSubscribeOptionsBuilder()
                    .WithSubscriptionIdentifier(7)
                    .WithTopicFilter("sport/#")
                    .Build(),
                timeout.Token);

            await subscriber.Client.SubscribeAsync(
                new MqttClientSubscribeOptionsBuilder()
                    .WithSubscriptionIdentifier(9)
                    .WithTopicFilter("sport/tennis")
                    .Build(),
                timeout.Token);

            await using var publisher = await ConnectClientAsync(server, MqttProtocolVersion.V500, "identifier-merge-b", timeout.Token);
            await PublishAsync(publisher, "sport/tennis", "one", timeout.Token);

            var received = await collector.WaitAsync(WaitTimeout).ConfigureAwait(false);
            Assert.IsNotNull(received.SubscriptionIdentifiers);
            CollectionAssert.AreEquivalent(
                new List<uint> { 7, 9 },
                received.SubscriptionIdentifiers.ToList());

            Assert.IsNull(
                await collector.WaitAsyncOrNull(NoMessageTimeout).ConfigureAwait(false),
                "Multiple matching subscriptions must still result in a single PUBLISH.");
        }

        [TestMethod]
        public async Task V311_Subscriber_DoesNotReceiveSubscriptionIdentifiers()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync();
            var collector = new TestMessageCollector();

            await using var subscriber = await ConnectSubscriberAsync(server, MqttProtocolVersion.V311, "identifier-v3-a", collector, timeout.Token);
            await subscriber.Client.SubscribeAsync(
                new MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter("sport/#")
                    .Build(),
                timeout.Token);

            await using var publisher = await ConnectClientAsync(server, MqttProtocolVersion.V500, "identifier-v3-b", timeout.Token);
            await PublishAsync(publisher, "sport/tennis", "one", timeout.Token);

            var received = await collector.WaitAsync(WaitTimeout).ConfigureAwait(false);
            Assert.IsTrue(
                received.SubscriptionIdentifiers == null || received.SubscriptionIdentifiers.Count == 0,
                "A V3 subscriber must not receive subscription identifiers.");
        }

        static async Task<MqttTestClient> ConnectClientAsync(
            MqttTestServer server,
            MqttProtocolVersion protocolVersion,
            string clientId,
            CancellationToken cancellationToken)
        {
            return await MqttTestClient.ConnectAsync(
                server.Port,
                protocolVersion,
                clientId,
                null,
                cancellationToken).ConfigureAwait(false);
        }

        static async Task<MqttTestClient> ConnectSubscriberAsync(
            MqttTestServer server,
            MqttProtocolVersion protocolVersion,
            string clientId,
            TestMessageCollector collector,
            CancellationToken cancellationToken)
        {
            var client = await ConnectClientAsync(server, protocolVersion, clientId, cancellationToken).ConfigureAwait(false);
            client.Client.ApplicationMessageReceivedHandler = collector;
            return client;
        }

        static Task PublishAsync(MqttTestClient client, string topic, string payload, CancellationToken cancellationToken)
        {
            return client.Client.PublishAsync(
                new MqttApplicationMessage
                {
                    Topic = topic,
                    Payload = Encoding.UTF8.GetBytes(payload),
                    QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce
                },
                cancellationToken);
        }
    }
}