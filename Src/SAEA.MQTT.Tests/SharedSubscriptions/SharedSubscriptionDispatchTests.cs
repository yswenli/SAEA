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

namespace SAEA.MQTT.Tests.SharedSubscriptions
{
    /// <summary>
    /// 共享订阅的投递分发：组内轮询、组间独立、取消订阅与成员退出后的选择变化
    /// </summary>
    [TestClass]
    public class SharedSubscriptionDispatchTests
    {
        static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);
        static readonly TimeSpan NoMessageTimeout = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// 验证同一共享组内两个成员按轮询各分得一半消息，组内每条消息只投递给一个成员
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task SharedSubscription_TwoMembers_RoundRobinsExactlyOnePerGroup()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync();
            var collectorA = new TestMessageCollector();
            var collectorB = new TestMessageCollector();

            await using var memberA = await ConnectSubscriberAsync(server, "shared-rr-a", collectorA, timeout.Token);
            await using var memberB = await ConnectSubscriberAsync(server, "shared-rr-b", collectorB, timeout.Token);

            await SubscribeAsync(memberA, "$share/g/sport/#", MqttQualityOfServiceLevel.AtMostOnce, timeout.Token);
            await SubscribeAsync(memberB, "$share/g/sport/#", MqttQualityOfServiceLevel.AtMostOnce, timeout.Token);

            await using var publisher = await ConnectClientAsync(server, "shared-rr-publisher", timeout.Token);

            var expectedPayloads = new List<string>();
            for (var i = 0; i < 4; i++)
            {
                var payload = "m" + i;
                expectedPayloads.Add(payload);
                await PublishAsync(publisher, "sport/tennis", payload, MqttQualityOfServiceLevel.AtMostOnce, false, timeout.Token);
            }

            await WaitUntilAsync(
                () => collectorA.Count == 2 && collectorB.Count == 2,
                timeout.Token);

            var receivedPayloads = new List<string>();
            for (var i = 0; i < 2; i++)
            {
                receivedPayloads.Add(Encoding.UTF8.GetString((await collectorA.WaitAsync(WaitTimeout).ConfigureAwait(false)).Payload));
                receivedPayloads.Add(Encoding.UTF8.GetString((await collectorB.WaitAsync(WaitTimeout).ConfigureAwait(false)).Payload));
            }

            CollectionAssert.AreEquivalent(expectedPayloads, receivedPayloads);
        }

        /// <summary>
        /// 验证不同共享组名视为独立分组，各自都会收到同一条消息
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task SharedSubscription_DifferentShareNames_EachGroupGetsOne()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync();
            var collectorA = new TestMessageCollector();
            var collectorB = new TestMessageCollector();

            await using var memberA = await ConnectSubscriberAsync(server, "shared-g1-a", collectorA, timeout.Token);
            await using var memberB = await ConnectSubscriberAsync(server, "shared-g2-b", collectorB, timeout.Token);

            await SubscribeAsync(memberA, "$share/g1/sport/#", MqttQualityOfServiceLevel.AtMostOnce, timeout.Token);
            await SubscribeAsync(memberB, "$share/g2/sport/#", MqttQualityOfServiceLevel.AtMostOnce, timeout.Token);

            await using var publisher = await ConnectClientAsync(server, "shared-g-publisher", timeout.Token);

            await PublishAsync(publisher, "sport/tennis", "one", MqttQualityOfServiceLevel.AtMostOnce, false, timeout.Token);

            var messageA = await collectorA.WaitAsync(WaitTimeout).ConfigureAwait(false);
            var messageB = await collectorB.WaitAsync(WaitTimeout).ConfigureAwait(false);

            Assert.AreEqual("one", Encoding.UTF8.GetString(messageA.Payload));
            Assert.AreEqual("one", Encoding.UTF8.GetString(messageB.Payload));
        }

        /// <summary>
        /// 验证普通订阅与共享订阅并存时，普通订阅者收到一份，共享组内也恰好只有一个成员收到一份
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task SharedSubscription_AndNormalSubscription_BothDelivered()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync();
            var normalCollector = new TestMessageCollector();
            var collectorA = new TestMessageCollector();
            var collectorB = new TestMessageCollector();

            await using var normal = await ConnectSubscriberAsync(server, "shared-normal-n", normalCollector, timeout.Token);
            await using var memberA = await ConnectSubscriberAsync(server, "shared-normal-a", collectorA, timeout.Token);
            await using var memberB = await ConnectSubscriberAsync(server, "shared-normal-b", collectorB, timeout.Token);

            await SubscribeAsync(normal, "sport/#", MqttQualityOfServiceLevel.AtMostOnce, timeout.Token);
            await SubscribeAsync(memberA, "$share/g/sport/#", MqttQualityOfServiceLevel.AtMostOnce, timeout.Token);
            await SubscribeAsync(memberB, "$share/g/sport/#", MqttQualityOfServiceLevel.AtMostOnce, timeout.Token);

            await using var publisher = await ConnectClientAsync(server, "shared-normal-publisher", timeout.Token);

            await PublishAsync(publisher, "sport/tennis", "one", MqttQualityOfServiceLevel.AtMostOnce, false, timeout.Token);

            await WaitUntilAsync(
                () => normalCollector.Count == 1 && (collectorA.Count + collectorB.Count) == 1,
                timeout.Token);

            Assert.AreEqual("one", Encoding.UTF8.GetString((await normalCollector.WaitAsync(WaitTimeout).ConfigureAwait(false)).Payload));
            Assert.AreEqual(1, collectorA.Count + collectorB.Count);

            var sharedMessage = await WaitForAnyAsync(collectorA, collectorB).ConfigureAwait(false);
            Assert.IsNotNull(sharedMessage, "Exactly one shared member must receive the message.");
            Assert.AreEqual("one", Encoding.UTF8.GetString(sharedMessage.Payload));

            Assert.IsNull(
                await WaitForAnyAsync(collectorA, collectorB).ConfigureAwait(false),
                "The shared group must deliver the message only once.");
        }

        /// <summary>
        /// 验证共享组成员退订后不再被选中，剩余成员独享全部投递
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task SharedSubscription_UnsubscribedMember_IsNoLongerSelected()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync();
            var collectorA = new TestMessageCollector();
            var collectorB = new TestMessageCollector();

            await using var memberA = await ConnectSubscriberAsync(server, "shared-unsub-a", collectorA, timeout.Token);
            await using var memberB = await ConnectSubscriberAsync(server, "shared-unsub-b", collectorB, timeout.Token);

            await SubscribeAsync(memberA, "$share/g/sport/#", MqttQualityOfServiceLevel.AtMostOnce, timeout.Token);
            await SubscribeAsync(memberB, "$share/g/sport/#", MqttQualityOfServiceLevel.AtMostOnce, timeout.Token);

            await memberA.Client.UnsubscribeAsync("$share/g/sport/#");

            await using var publisher = await ConnectClientAsync(server, "shared-unsub-publisher", timeout.Token);

            for (var i = 0; i < 4; i++)
            {
                await PublishAsync(publisher, "sport/tennis", "m" + i, MqttQualityOfServiceLevel.AtMostOnce, false, timeout.Token);
            }

            await WaitUntilAsync(() => collectorB.Count == 4, timeout.Token);

            Assert.AreEqual(4, collectorB.Count);
            Assert.IsNull(
                await collectorA.WaitAsyncOrNull(NoMessageTimeout).ConfigureAwait(false),
                "An unsubscribed shared member must not receive any further messages.");
        }

        /// <summary>
        /// 验证共享组成员断开后从注册表移除，后续消息全部由剩余成员按序接收
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task SharedSubscription_SessionEndedMember_IsRemovedFromRegistry()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync();
            var collectorA = new TestMessageCollector();
            var collectorB = new TestMessageCollector();

            await using var memberA = await ConnectSubscriberAsync(server, "shared-ended-a", collectorA, timeout.Token);
            await using var memberB = await ConnectSubscriberAsync(server, "shared-ended-b", collectorB, timeout.Token);

            await SubscribeAsync(memberA, "$share/g/sport/#", MqttQualityOfServiceLevel.AtMostOnce, timeout.Token);
            await SubscribeAsync(memberB, "$share/g/sport/#", MqttQualityOfServiceLevel.AtMostOnce, timeout.Token);

            await memberA.DisposeAsync().ConfigureAwait(false);

            await WaitUntilAsync(
                () => server.ClientSessionsManager.SharedSubscriptionRegistry.GetMemberCount("g", "sport/#") == 1,
                timeout.Token);

            await using var publisher = await ConnectClientAsync(server, "shared-ended-publisher", timeout.Token);

            await PublishAsync(publisher, "sport/tennis", "one", MqttQualityOfServiceLevel.AtMostOnce, false, timeout.Token);
            await PublishAsync(publisher, "sport/tennis", "two", MqttQualityOfServiceLevel.AtMostOnce, false, timeout.Token);

            await WaitUntilAsync(() => collectorB.Count == 2, timeout.Token);

            Assert.AreEqual("one", Encoding.UTF8.GetString((await collectorB.WaitAsync(WaitTimeout).ConfigureAwait(false)).Payload));
            Assert.AreEqual("two", Encoding.UTF8.GetString((await collectorB.WaitAsync(WaitTimeout).ConfigureAwait(false)).Payload));
        }

        /// <summary>
        /// 验证新增的共享订阅成员不会收到保留消息，而普通订阅仍会重放保留消息并置 Retain 标记
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task SharedSubscription_NewMember_DoesNotReceiveRetained()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync();

            await using var publisher = await ConnectClientAsync(server, "shared-retained-publisher", timeout.Token);
            await PublishAsync(publisher, "sport/retained", "retained", MqttQualityOfServiceLevel.AtMostOnce, true, timeout.Token);

            await WaitUntilAsync(
                async () => (await server.Server.GetRetainedApplicationMessagesAsync().ConfigureAwait(false))
                    .Any(message => message.Topic == "sport/retained"),
                timeout.Token);

            var sharedCollector = new TestMessageCollector();
            await using var sharedMember = await ConnectSubscriberAsync(server, "shared-retained-member", sharedCollector, timeout.Token);
            await SubscribeAsync(sharedMember, "$share/g/sport/#", MqttQualityOfServiceLevel.AtMostOnce, timeout.Token);

            Assert.IsNull(
                await sharedCollector.WaitAsyncOrNull(NoMessageTimeout).ConfigureAwait(false),
                "A new shared subscription member must not receive retained messages.");

            var normalCollector = new TestMessageCollector();
            await using var normal = await ConnectSubscriberAsync(server, "shared-retained-normal", normalCollector, timeout.Token);
            await SubscribeAsync(normal, "sport/#", MqttQualityOfServiceLevel.AtMostOnce, timeout.Token);

            var replayed = await normalCollector.WaitAsync(WaitTimeout).ConfigureAwait(false);
            Assert.AreEqual("sport/retained", replayed.Topic);
            Assert.IsTrue(replayed.Retain, "A retained replay must set the Retain flag.");
        }

        /// <summary>
        /// 验证轮询选中某成员后按该成员自己的订阅 QoS 投递（QoS 0 成员收 0、QoS 1 成员收 1）
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task SharedSubscription_UsesMemberQualityOfService()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync();
            var collectorQos0 = new TestMessageCollector();
            var collectorQos1 = new TestMessageCollector();

            await using var qos0Member = await ConnectSubscriberAsync(server, "shared-qos0", collectorQos0, timeout.Token);
            await using var qos1Member = await ConnectSubscriberAsync(server, "shared-qos1", collectorQos1, timeout.Token);

            await SubscribeAsync(qos0Member, "$share/g/sport/#", MqttQualityOfServiceLevel.AtMostOnce, timeout.Token);
            await SubscribeAsync(qos1Member, "$share/g/sport/#", MqttQualityOfServiceLevel.AtLeastOnce, timeout.Token);

            await using var publisher = await ConnectClientAsync(server, "shared-qos-publisher", timeout.Token);

            for (var i = 0; i < 4; i++)
            {
                await PublishAsync(publisher, "sport/tennis", "m" + i, MqttQualityOfServiceLevel.AtLeastOnce, false, timeout.Token);
            }

            await WaitUntilAsync(
                () => collectorQos0.Count == 2 && collectorQos1.Count == 2,
                timeout.Token);

            for (var i = 0; i < 2; i++)
            {
                var message = await collectorQos0.WaitAsync(WaitTimeout).ConfigureAwait(false);
                Assert.AreEqual(MqttQualityOfServiceLevel.AtMostOnce, message.QualityOfServiceLevel);
            }

            for (var i = 0; i < 2; i++)
            {
                var message = await collectorQos1.WaitAsync(WaitTimeout).ConfigureAwait(false);
                Assert.AreEqual(MqttQualityOfServiceLevel.AtLeastOnce, message.QualityOfServiceLevel);
            }
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
            string topicFilter,
            MqttQualityOfServiceLevel qualityOfServiceLevel,
            CancellationToken cancellationToken)
        {
            return client.Client.SubscribeAsync(
                new MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter(topicFilter, qualityOfServiceLevel)
                    .Build(),
                cancellationToken);
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

        static async Task<MqttApplicationMessage> WaitForAnyAsync(
            TestMessageCollector first,
            TestMessageCollector second)
        {
            var deadline = DateTime.UtcNow + NoMessageTimeout;
            while (DateTime.UtcNow < deadline)
            {
                var message = await first.WaitAsyncOrNull(TimeSpan.FromMilliseconds(25)).ConfigureAwait(false);
                if (message != null)
                {
                    return message;
                }

                message = await second.WaitAsyncOrNull(TimeSpan.FromMilliseconds(25)).ConfigureAwait(false);
                if (message != null)
                {
                    return message;
                }
            }

            return null;
        }

        static async Task WaitUntilAsync(Func<bool> predicate, CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow + WaitTimeout;
            while (DateTime.UtcNow < deadline)
            {
                if (predicate())
                {
                    return;
                }

                await Task.Delay(25, cancellationToken).ConfigureAwait(false);
            }

            Assert.Fail("The expected condition was not met within " + WaitTimeout + ".");
        }

        static async Task WaitUntilAsync(Func<Task<bool>> predicate, CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow + WaitTimeout;
            while (DateTime.UtcNow < deadline)
            {
                if (await predicate().ConfigureAwait(false))
                {
                    return;
                }

                await Task.Delay(25, cancellationToken).ConfigureAwait(false);
            }

            Assert.Fail("The expected condition was not met within " + WaitTimeout + ".");
        }
    }
}