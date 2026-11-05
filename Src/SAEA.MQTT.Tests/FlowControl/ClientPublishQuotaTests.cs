using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Client.Publishing;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Protocol;
using SAEA.MQTT.Tests.TestInfrastructure;

namespace SAEA.MQTT.Tests.FlowControl
{
    [TestClass]
    public class ClientPublishQuotaTests
    {
        static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        [TestMethod]
        public async Task SecondQoS1Publish_WaitsForFirstPubAck_WhenReceiveMaximumIsOne()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var broker = await FakeMqttBroker.StartAsync(receiveMaximum: 1, autoAckQoS1: false);
            await using var client = await MqttTestClient.ConnectAsync(
                broker.Port,
                MqttProtocolVersion.V500,
                "client-quota",
                options => options.WithSessionExpiryInterval(60),
                timeout.Token);

            var firstPublish = client.Client.PublishAsync(
                CreateMessage("quota/one", MqttQualityOfServiceLevel.AtLeastOnce),
                timeout.Token);

            var firstPacketIdentifier = await broker
                .DequeuePendingPubAckAsync(WaitTimeout, timeout.Token)
                .ConfigureAwait(false);

            var secondPublish = client.Client.PublishAsync(
                CreateMessage("quota/two", MqttQualityOfServiceLevel.AtLeastOnce),
                timeout.Token);

            // 服务端 ReceiveMaximum=1 且第一条尚未 ACK：第二条必须停留在配额等待状态。
            await Task.Delay(300).ConfigureAwait(false);
            Assert.IsFalse(secondPublish.IsCompleted, "The second publish must wait for the publish quota.");
            Assert.AreEqual(1, broker.ReceivedPublishCount);

            await broker.SendPubAckAsync(firstPacketIdentifier).ConfigureAwait(false);

            var firstResult = await firstPublish.ConfigureAwait(false);
            Assert.AreEqual(MqttClientPublishReasonCode.Success, firstResult.ReasonCode);

            var secondPacketIdentifier = await broker
                .DequeuePendingPubAckAsync(WaitTimeout, timeout.Token)
                .ConfigureAwait(false);

            await broker.SendPubAckAsync(secondPacketIdentifier).ConfigureAwait(false);

            var secondResult = await secondPublish.ConfigureAwait(false);
            Assert.AreEqual(MqttClientPublishReasonCode.Success, secondResult.ReasonCode);
        }

        [TestMethod]
        public async Task ConnectionClosed_ReleasesPublishesWaitingForQuota()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var broker = await FakeMqttBroker.StartAsync(receiveMaximum: 1, autoAckQoS1: false);
            await using var client = await MqttTestClient.ConnectAsync(
                broker.Port,
                MqttProtocolVersion.V500,
                "client-quota-disconnect",
                options => options
                    .WithSessionExpiryInterval(60)
                    .WithCommunicationTimeout(TimeSpan.FromSeconds(2)),
                timeout.Token);

            var firstPublish = client.Client.PublishAsync(
                CreateMessage("quota/drop/one", MqttQualityOfServiceLevel.AtLeastOnce),
                timeout.Token);

            await broker.WaitForPendingPubAckAsync(WaitTimeout, timeout.Token).ConfigureAwait(false);

            var secondPublish = client.Client.PublishAsync(
                CreateMessage("quota/drop/two", MqttQualityOfServiceLevel.AtLeastOnce),
                timeout.Token);

            await Task.Delay(200).ConfigureAwait(false);
            Assert.IsFalse(secondPublish.IsCompleted, "The second publish must wait for the publish quota.");

            broker.DropConnection();

            var quotaWaiterCompleted = await Task.WhenAny(secondPublish, Task.Delay(WaitTimeout)).ConfigureAwait(false);
            Assert.AreSame(
                secondPublish,
                quotaWaiterCompleted,
                "The publish waiting for the quota must be released as soon as the connection is gone.");

            var both = Task.WhenAll(firstPublish, secondPublish);
            var completed = await Task.WhenAny(both, Task.Delay(TimeSpan.FromSeconds(8))).ConfigureAwait(false);

            Assert.AreSame(both, completed, "Publishes waiting for the quota must not block forever after a disconnect.");

            try
            {
                await both.ConfigureAwait(false);
            }
            catch
            {
                // 连接中断必然让发布失败，这里只关心它们能结束。
            }
        }

        static MqttApplicationMessage CreateMessage(string topic, MqttQualityOfServiceLevel qos)
        {
            return new MqttApplicationMessage
            {
                Topic = topic,
                Payload = System.Text.Encoding.UTF8.GetBytes(topic),
                QualityOfServiceLevel = qos
            };
        }
    }
}