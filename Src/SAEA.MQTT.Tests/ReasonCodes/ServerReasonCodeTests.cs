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
    /// <summary>
    /// 服务端在协议违规场景下返回的 DISCONNECT 原因码与 SUBACK 授予结果
    /// </summary>
    [TestClass]
    public class ServerReasonCodeTests
    {
        static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 验证服务端关闭 retain 能力后收到保留发布，以 0x9A（不支持保留消息）原因码断开
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
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

        /// <summary>
        /// 验证服务端最大 QoS 为 1 时收到 QoS 2 发布，以 0x9B（不支持该 QoS）原因码断开
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
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

        /// <summary>
        /// 验证 Remaining Length 使用 4 个以上延续字节编码（非法长度）时，服务端以 0x81（格式错误）原因码断开
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
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

        /// <summary>
        /// 验证订阅标识符为 0（非法值）时，服务端不回 SUBACK 而是直接以 0x82（协议错误）原因码断开
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
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

        /// <summary>
        /// 验证订阅请求的 QoS 高于服务端最大值时不断开，而是在 SUBACK 中降级为 QoS 1
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
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