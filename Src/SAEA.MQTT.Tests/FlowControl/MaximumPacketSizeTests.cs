using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Client.Subscribing;
using SAEA.MQTT.Exceptions;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Protocol;
using SAEA.MQTT.Tests.TestInfrastructure;

namespace SAEA.MQTT.Tests.FlowControl
{
    [TestClass]
    public class MaximumPacketSizeTests
    {
        static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);
        static readonly TimeSpan NoMessageTimeout = TimeSpan.FromMilliseconds(500);

        [TestMethod]
        public async Task InboundOversizedPacket_DisconnectsWith0x95()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync(builder => builder.WithMaximumPacketSize(64));
            await using var rawClient = await RawMqttTestClient.ConnectAsync(
                server.Port,
                "raw-oversized-inbound",
                null,
                timeout.Token);

            // 绕过 formatter 直接写 socket，构造一个超过服务端 64 字节上限的 PUBLISH。
            // 只发送固定头并声明 65 字节正文（总长 67 > 64），服务端在读固定头后即判定超限，
            // 不会读取正文。故意不补发正文：若服务端关闭时接收缓冲区仍有未读数据，
            // Windows 会以 RST 代替 FIN，可能丢弃刚发出的 DISCONNECT，让断言偶发失败。
            await rawClient.SendRawBytesAsync(new byte[] { 0x30, 0x41 }, timeout.Token);

            var disconnect = await rawClient.WaitForDisconnectAsync(WaitTimeout, timeout.Token);
            Assert.AreEqual((MqttDisconnectReasonCode)0x95, disconnect.ReasonCode.Value);
        }

        [TestMethod]
        public async Task OutboundOversizedPublish_IsDroppedAndConnectionSurvives()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync();

            var collector = new TestMessageCollector();
            await using var subscriber = await MqttTestClient.ConnectAsync(
                server.Port,
                MqttProtocolVersion.V500,
                "oversized-subscriber",
                options => options.WithMaximumPacketSize(64),
                timeout.Token);

            subscriber.Client.ApplicationMessageReceivedHandler = collector;

            await subscriber.Client.SubscribeAsync(
                new MqttClientSubscribeOptionsBuilder().WithTopicFilter("big/#").Build(),
                timeout.Token);

            await using var publisher = await MqttTestClient.ConnectAsync(
                server.Port,
                MqttProtocolVersion.V500,
                "oversized-publisher",
                null,
                timeout.Token);

            await PublishAsync(publisher.Client, "big/topic", new byte[512], timeout.Token);

            Assert.IsNull(
                await collector.WaitAsyncOrNull(NoMessageTimeout).ConfigureAwait(false),
                "An oversized PUBLISH must be dropped by the server.");

            await PublishAsync(publisher.Client, "big/small", Encoding.UTF8.GetBytes("small"), timeout.Token);

            var received = await collector.WaitAsync(WaitTimeout).ConfigureAwait(false);
            Assert.AreEqual("big/small", received.Topic);
            Assert.AreEqual("small", Encoding.UTF8.GetString(received.Payload));
        }

        [TestMethod]
        public async Task ClientOversizedPublish_ThrowsProtocolViolation()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync(builder => builder.WithMaximumPacketSize(64));
            await using var client = await MqttTestClient.ConnectAsync(
                server.Port,
                MqttProtocolVersion.V500,
                "oversized-client",
                null,
                timeout.Token);

            try
            {
                await client.Client.PublishAsync(
                    new MqttApplicationMessage
                    {
                        Topic = "big/topic",
                        Payload = new byte[512],
                        QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce
                    },
                    timeout.Token);

                Assert.Fail("Publishing a packet above the server maximum packet size must throw.");
            }
            catch (MqttProtocolViolationException exception)
            {
                Assert.IsInstanceOfType(exception, typeof(MqttProtocolViolationException));
            }
        }

        static Task PublishAsync(SAEA.MQTT.Client.IMqttClient client, string topic, byte[] payload, CancellationToken cancellationToken)
        {
            return client.PublishAsync(
                new MqttApplicationMessage
                {
                    Topic = topic,
                    Payload = payload,
                    QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce
                },
                cancellationToken);
        }
    }
}