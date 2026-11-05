using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;
using SAEA.MQTT.Tests.TestInfrastructure;

namespace SAEA.MQTT.Tests.FlowControl
{
    [TestClass]
    public class ServerReceiveMaximumTests
    {
        static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        [TestMethod]
        public async Task QoS2_SecondPublishWithoutPubComp_DisconnectsWith0x93()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync(builder => builder.WithReceiveMaximum(1));
            await using var rawClient = await RawMqttTestClient.ConnectAsync(
                server.Port,
                "raw-receive-maximum-exceeded",
                null,
                timeout.Token);

            await rawClient.SendQoS2PublishAsync("flow/qos2/one", "one", 1, timeout.Token);

            var pubRec = await ReadPacketAsync<MqttPubRecPacket>(rawClient, timeout.Token);
            Assert.AreEqual((ushort)1, pubRec.PacketIdentifier);

            await rawClient.SendQoS2PublishAsync("flow/qos2/two", "two", 2, timeout.Token);

            var disconnect = await rawClient.WaitForDisconnectAsync(WaitTimeout, timeout.Token);
            Assert.AreEqual((MqttDisconnectReasonCode)0x93, disconnect.ReasonCode.Value);
        }

        [TestMethod]
        public async Task QoS2_QuotaReleasedAfterPubComp_AllowsNextPublish()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync(builder => builder.WithReceiveMaximum(1));
            await using var rawClient = await RawMqttTestClient.ConnectAsync(
                server.Port,
                "raw-receive-maximum-release",
                null,
                timeout.Token);

            await rawClient.SendQoS2PublishAsync("flow/qos2/release", "one", 1, timeout.Token);
            await ReadPacketAsync<MqttPubRecPacket>(rawClient, timeout.Token);

            await rawClient.SendPacketAsync(
                new MqttPubRelPacket
                {
                    PacketIdentifier = 1,
                    ReasonCode = MqttPubRelReasonCode.Success
                },
                timeout.Token);

            var pubComp = await ReadPacketAsync<MqttPubCompPacket>(rawClient, timeout.Token);
            Assert.AreEqual((ushort)1, pubComp.PacketIdentifier);

            await rawClient.SendQoS2PublishAsync("flow/qos2/release", "two", 2, timeout.Token);
            var secondPubRec = await ReadPacketAsync<MqttPubRecPacket>(rawClient, timeout.Token);
            Assert.AreEqual((ushort)2, secondPubRec.PacketIdentifier);

            await rawClient.SendPacketAsync(MqttPingReqPacket.Instance, timeout.Token);
            await ReadPacketAsync<MqttPingRespPacket>(rawClient, timeout.Token);
        }

        [TestMethod]
        public async Task QoS1_PipelinedPublishes_DoNotLeakQuota()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync(builder => builder.WithReceiveMaximum(1));
            await using var rawClient = await RawMqttTestClient.ConnectAsync(
                server.Port,
                "raw-receive-maximum-qos1",
                null,
                timeout.Token);

            for (ushort packetId = 1; packetId <= 3; packetId++)
            {
                await rawClient.SendQoS1PublishAsync("flow/qos1", "payload-" + packetId, packetId, timeout.Token);

                var pubAck = await ReadPacketAsync<MqttPubAckPacket>(rawClient, timeout.Token);
                Assert.AreEqual(packetId, pubAck.PacketIdentifier);
            }

            await rawClient.SendPacketAsync(MqttPingReqPacket.Instance, timeout.Token);
            await ReadPacketAsync<MqttPingRespPacket>(rawClient, timeout.Token);
        }

        static async Task<TPacket> ReadPacketAsync<TPacket>(RawMqttTestClient client, CancellationToken cancellationToken)
            where TPacket : MqttBasePacket
        {
            var packet = await client.ReadPacketAsync(cancellationToken).ConfigureAwait(false);
            Assert.IsInstanceOfType(packet, typeof(TPacket), "Expected a " + typeof(TPacket).Name + " but received " + (packet == null ? "null" : packet.GetType().Name) + ".");
            return (TPacket)packet;
        }
    }
}