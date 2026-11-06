using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;
using SAEA.MQTT.Tests.TestInfrastructure;

namespace SAEA.MQTT.Tests.FlowControl
{
    /// <summary>
    /// 服务端接收上限（Receive Maximum）超限时的断开与配额释放
    /// </summary>
    [TestClass]
    public class ServerReceiveMaximumTests
    {
        static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 验证接收上限为 1 时，QoS 2 报文在未完成 PUBCOMP 前再发一条，服务端以 0x93（接收超限）原因码断开
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
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

        /// <summary>
        /// 验证 QoS 2 流程在收到 PUBREL 并完成 PUBCOMP 后释放配额，随后的新报文仍可正常处理且连接可用
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
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

        /// <summary>
        /// 验证接收上限为 1 时连续发送三条 QoS 1 报文不会泄漏配额，逐条收到 PUBACK 后连接仍然可用
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
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