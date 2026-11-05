using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Formatter.V3;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Tests.Compatibility
{
    [TestClass]
    public class V3GoldenBytesTests
    {
        [TestMethod]
        public void V311_Connect_EncodesToGoldenBytes()
        {
            var packet = new MqttConnectPacket
            {
                ClientId = "client",
                CleanSession = true,
                KeepAlivePeriod = 60
            };

            AssertBytes(
                new byte[]
                {
                    0x10, 0x12,
                    0x00, 0x04, 0x4D, 0x51, 0x54, 0x54,
                    0x04,
                    0x02,
                    0x00, 0x3C,
                    0x00, 0x06, 0x63, 0x6C, 0x69, 0x65, 0x6E, 0x74
                },
                Encode(packet));
        }

        [TestMethod]
        public void V311_ConnAck_EncodesToGoldenBytes()
        {
            var packet = new MqttConnAckPacket
            {
                ReturnCode = MqttConnectReturnCode.ConnectionAccepted,
                IsSessionPresent = false
            };

            AssertBytes(
                new byte[]
                {
                    0x20, 0x02,
                    0x00, 0x00
                },
                Encode(packet));
        }

        [TestMethod]
        public void V311_Subscribe_EncodesToGoldenBytes()
        {
            var packet = new MqttSubscribePacket
            {
                PacketIdentifier = 1
            };

            packet.TopicFilters.Add(new MqttTopicFilter
            {
                Topic = "a/b",
                QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce
            });

            AssertBytes(
                new byte[]
                {
                    0x82, 0x08,
                    0x00, 0x01,
                    0x00, 0x03, 0x61, 0x2F, 0x62,
                    0x01
                },
                Encode(packet));
        }

        [TestMethod]
        public void V311_SubAck_EncodesToGoldenBytes()
        {
            var packet = new MqttSubAckPacket
            {
                PacketIdentifier = 1
            };

            packet.ReturnCodes.Add(MqttSubscribeReturnCode.SuccessMaximumQoS1);

            AssertBytes(
                new byte[]
                {
                    0x90, 0x03,
                    0x00, 0x01,
                    0x01
                },
                Encode(packet));
        }

        [TestMethod]
        public void V311_PublishQoS0_EncodesToGoldenBytes()
        {
            var packet = new MqttPublishPacket
            {
                Topic = "a/b",
                Payload = Encoding.ASCII.GetBytes("hi"),
                QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce
            };

            AssertBytes(
                new byte[]
                {
                    0x30, 0x07,
                    0x00, 0x03, 0x61, 0x2F, 0x62,
                    0x68, 0x69
                },
                Encode(packet));
        }

        [TestMethod]
        public void V311_PublishQoS1_EncodesToGoldenBytes()
        {
            var packet = new MqttPublishPacket
            {
                Topic = "a/b",
                Payload = Encoding.ASCII.GetBytes("hi"),
                QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce,
                PacketIdentifier = 10
            };

            AssertBytes(
                new byte[]
                {
                    0x32, 0x09,
                    0x00, 0x03, 0x61, 0x2F, 0x62,
                    0x00, 0x0A,
                    0x68, 0x69
                },
                Encode(packet));
        }

        [TestMethod]
        public void V311_PublishQoS2_EncodesToGoldenBytes()
        {
            var packet = new MqttPublishPacket
            {
                Topic = "a/b",
                Payload = Encoding.ASCII.GetBytes("hi"),
                QualityOfServiceLevel = MqttQualityOfServiceLevel.ExactlyOnce,
                PacketIdentifier = 10
            };

            AssertBytes(
                new byte[]
                {
                    0x34, 0x09,
                    0x00, 0x03, 0x61, 0x2F, 0x62,
                    0x00, 0x0A,
                    0x68, 0x69
                },
                Encode(packet));
        }

        [TestMethod]
        public void V311_Ping_EncodesToGoldenBytes()
        {
            AssertBytes(new byte[] { 0xC0, 0x00 }, Encode(new MqttPingReqPacket()));
            AssertBytes(new byte[] { 0xD0, 0x00 }, Encode(new MqttPingRespPacket()));
        }

        private static byte[] Encode(MqttBasePacket packet)
        {
            var formatter = new MqttV311PacketFormatter(new MqttPacketWriter());
            return formatter.Encode(packet).ToArray();
        }

        private static void AssertBytes(byte[] expected, byte[] actual)
        {
            Assert.AreEqual(expected.Length, actual.Length, "Encoded packet length differs from the V3 baseline.");
            CollectionAssert.AreEqual(expected, actual, "Encoded packet bytes differ from the V3 baseline.");
        }
    }
}