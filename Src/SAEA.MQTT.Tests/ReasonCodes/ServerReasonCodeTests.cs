using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;
using SAEA.MQTT.Tests.TestInfrastructure;

namespace SAEA.MQTT.Tests.ReasonCodes
{
    [TestClass]
    public class ServerReasonCodeTests
    {
        static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        [TestMethod]
        public async Task RetainedPublish_WhenRetainUnavailable_DisconnectsWith0x9A()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync(builder => builder.WithRetainAvailable(false));
            await using var rawClient = await RawMqttTestClient.ConnectAsync(
                server.Port,
                "raw-retain-not-supported",
                null,
                timeout.Token);

            await rawClient.SendPublishAsync(
                "retain/not-supported",
                System.Text.Encoding.UTF8.GetBytes("payload"),
                MqttQualityOfServiceLevel.AtMostOnce,
                retain: true,
                packetIdentifier: null,
                timeout.Token);

            var disconnect = await rawClient.WaitForDisconnectAsync(WaitTimeout, timeout.Token);
            Assert.AreEqual(
                (MqttDisconnectReasonCode)0x9A,
                disconnect.ReasonCode.Value);
        }

        [TestMethod]
        public async Task QoSAboveServerMaximum_DisconnectsWith0x9B()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync(
                builder => builder.WithMaximumQoS(MqttQualityOfServiceLevel.AtLeastOnce));
            await using var rawClient = await RawMqttTestClient.ConnectAsync(
                server.Port,
                "raw-qos-not-supported",
                null,
                timeout.Token);

            await rawClient.SendQoS2PublishAsync("qos/not-supported", "payload", 1, timeout.Token);

            var disconnect = await rawClient.WaitForDisconnectAsync(WaitTimeout, timeout.Token);
            Assert.AreEqual(
                (MqttDisconnectReasonCode)0x9B,
                disconnect.ReasonCode.Value);
        }

        [TestMethod]
        public async Task MalformedRemainingLength_DisconnectsWith0x81()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync();
            await using var rawClient = await RawMqttTestClient.ConnectAsync(
                server.Port,
                "raw-malformed",
                null,
                timeout.Token);

            // 固定头 + 4 个 continuation 字节，超过 Remaining Length 的最大编码长度。
            await rawClient.SendRawBytesAsync(
                new byte[] { 0x30, 0xFF, 0xFF, 0xFF, 0xFF },
                timeout.Token);

            var disconnect = await rawClient.WaitForDisconnectAsync(WaitTimeout, timeout.Token);
            Assert.AreEqual(
                (MqttDisconnectReasonCode)0x81,
                disconnect.ReasonCode.Value);
        }

        [TestMethod]
        public async Task SubscriptionIdentifierZero_DisconnectsWithoutSubAck()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync();
            await using var rawClient = await RawMqttTestClient.ConnectAsync(
                server.Port,
                "raw-subscription-identifier-zero",
                null,
                timeout.Token);

            var subscribePacket = new MqttSubscribePacket
            {
                PacketIdentifier = 1,
                Properties = new MqttSubscribePacketProperties
                {
                    SubscriptionIdentifier = 0
                }
            };

            subscribePacket.TopicFilters.Add(new MqttTopicFilter
            {
                Topic = "subscription/identifier",
                QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce
            });

            await rawClient.SendPacketAsync(subscribePacket, timeout.Token);

            var disconnect = await rawClient.WaitForDisconnectAsync(WaitTimeout, timeout.Token);
            Assert.AreEqual(
                (MqttDisconnectReasonCode)0x82,
                disconnect.ReasonCode.Value);
        }

        [TestMethod]
        public async Task SubscribeAboveMaximumQoS_IsDowngradedInSubAck()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync(
                builder => builder.WithMaximumQoS(MqttQualityOfServiceLevel.AtLeastOnce));
            await using var rawClient = await RawMqttTestClient.ConnectAsync(
                server.Port,
                "raw-maximum-qos-downgrade",
                null,
                timeout.Token);

            var subAck = await rawClient.SubscribeAsync(
                "qos/downgrade",
                MqttQualityOfServiceLevel.ExactlyOnce,
                null,
                timeout.Token);

            Assert.IsNotNull(subAck);
            Assert.AreEqual(1, subAck.ReasonCodes.Count);
            Assert.AreEqual(MqttSubscribeReasonCode.GrantedQoS1, subAck.ReasonCodes[0]);
        }
    }
}