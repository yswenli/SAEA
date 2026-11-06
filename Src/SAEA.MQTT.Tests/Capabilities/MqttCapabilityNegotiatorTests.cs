using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Exceptions;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Internal;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;
using SAEA.MQTT.Server;

namespace SAEA.MQTT.Tests.Capabilities
{
    /// <summary>
    /// 服务端能力协商器对 V5 / V3 连接报文的解析与校验
    /// </summary>
    [TestClass]
    public class MqttCapabilityNegotiatorTests
    {
        /// <summary>
        /// 验证 V5 连接报文不带属性时各项能力取协议默认值，服务端 Keep Alive 继承客户端值
        /// </summary>
        [TestMethod]
        public void Negotiate_V5_WithoutProperties_UsesProtocolDefaults()
        {
            var connectPacket = new MqttConnectPacket
            {
                ClientId = "client-1",
                KeepAlivePeriod = 60
            };

            var result = MqttCapabilityNegotiator.Negotiate(
                new MqttServerCapabilities(),
                connectPacket,
                MqttProtocolVersion.V500,
                "client-1");

            Assert.AreEqual(MqttProtocolVersion.V500, result.ProtocolVersion);
            Assert.AreEqual("client-1", result.AssignedClientIdentifier);

            Assert.AreEqual(ushort.MaxValue, result.ReceiveMaximum);
            Assert.AreEqual(268435455u, result.MaximumPacketSize);
            Assert.AreEqual((ushort)0, result.TopicAliasMaximum);
            Assert.AreEqual(MqttQualityOfServiceLevel.ExactlyOnce, result.MaximumQoS);
            Assert.IsTrue(result.RetainAvailable);
            Assert.IsTrue(result.WildcardSubscriptionAvailable);
            Assert.IsTrue(result.SubscriptionIdentifiersAvailable);
            Assert.IsTrue(result.SharedSubscriptionAvailable);
            Assert.AreEqual((ushort)60, result.ServerKeepAlive);
            Assert.AreEqual(uint.MaxValue, result.MaximumSessionExpiryInterval);

            Assert.AreEqual(ushort.MaxValue, result.ClientReceiveMaximum);
            Assert.AreEqual(268435455u, result.ClientMaximumPacketSize);
            Assert.AreEqual((ushort)0, result.ClientTopicAliasMaximum);
            Assert.IsTrue(result.RequestProblemInformation);
            Assert.AreEqual(0u, result.SessionExpiryInterval);
        }

        /// <summary>
        /// 验证 V5 连接报文中的接收上限、最大报文长度、主题别名上限、会话过期与问题信息开关被读取到协商结果
        /// </summary>
        [TestMethod]
        public void Negotiate_V5_ReadsClientProperties()
        {
            var connectPacket = new MqttConnectPacket
            {
                ClientId = "client-2",
                KeepAlivePeriod = 30,
                Properties = new MqttConnectPacketProperties
                {
                    ReceiveMaximum = 10,
                    MaximumPacketSize = 1024,
                    TopicAliasMaximum = 5,
                    SessionExpiryInterval = 30,
                    RequestProblemInformation = false
                }
            };

            var result = MqttCapabilityNegotiator.Negotiate(
                new MqttServerCapabilities(),
                connectPacket,
                MqttProtocolVersion.V500,
                null);

            Assert.AreEqual((ushort)10, result.ClientReceiveMaximum);
            Assert.AreEqual(1024u, result.ClientMaximumPacketSize);
            Assert.AreEqual((ushort)5, result.ClientTopicAliasMaximum);
            Assert.AreEqual(30u, result.SessionExpiryInterval);
            Assert.IsFalse(result.RequestProblemInformation);
            Assert.IsNull(result.AssignedClientIdentifier);
        }

        /// <summary>
        /// 验证客户端会话过期时间超过服务端上限时被下调到上限，未超限时保持不变
        /// </summary>
        [TestMethod]
        public void Negotiate_V5_ClampsSessionExpiryToServerMaximum()
        {
            var serverCapabilities = new MqttServerCapabilities
            {
                MaximumSessionExpiryInterval = 10
            };

            var longExpiryPacket = new MqttConnectPacket
            {
                ClientId = "client-3",
                Properties = new MqttConnectPacketProperties
                {
                    SessionExpiryInterval = 30
                }
            };

            var shortExpiryPacket = new MqttConnectPacket
            {
                ClientId = "client-4",
                Properties = new MqttConnectPacketProperties
                {
                    SessionExpiryInterval = 5
                }
            };

            var clamped = MqttCapabilityNegotiator.Negotiate(
                serverCapabilities,
                longExpiryPacket,
                MqttProtocolVersion.V500,
                null);

            var unchanged = MqttCapabilityNegotiator.Negotiate(
                serverCapabilities,
                shortExpiryPacket,
                MqttProtocolVersion.V500,
                null);

            Assert.AreEqual(10u, clamped.SessionExpiryInterval);
            Assert.AreEqual(5u, unchanged.SessionExpiryInterval);
        }

        /// <summary>
        /// 验证未配置服务端 Keep Alive 时沿用客户端值，配置后以配置值为准
        /// </summary>
        [TestMethod]
        public void Negotiate_V5_ServerKeepAliveDefaultsToClientKeepAliveUnlessOverridden()
        {
            var connectPacket = new MqttConnectPacket
            {
                ClientId = "client-5",
                KeepAlivePeriod = 45
            };

            var defaultResult = MqttCapabilityNegotiator.Negotiate(
                new MqttServerCapabilities(),
                connectPacket,
                MqttProtocolVersion.V500,
                null);

            var overriddenResult = MqttCapabilityNegotiator.Negotiate(
                new MqttServerCapabilities { ServerKeepAlive = 12 },
                connectPacket,
                MqttProtocolVersion.V500,
                null);

            Assert.AreEqual((ushort)45, defaultResult.ServerKeepAlive);
            Assert.AreEqual((ushort)12, overriddenResult.ServerKeepAlive);
        }

        /// <summary>
        /// 验证 V3.1.1 与 V3.1.0 忽略报文中的 V5 属性，客户端能力保持协议默认且会话过期时间为 0
        /// </summary>
        [TestMethod]
        public void Negotiate_V3_IgnoresV5PropertiesAndKeepsClientDefaults()
        {
            var connectPacket = new MqttConnectPacket
            {
                ClientId = "v3-client",
                KeepAlivePeriod = 20,
                Properties = new MqttConnectPacketProperties
                {
                    ReceiveMaximum = 10,
                    MaximumPacketSize = 1024,
                    TopicAliasMaximum = 5,
                    SessionExpiryInterval = 30,
                    RequestProblemInformation = false
                }
            };

            var result311 = MqttCapabilityNegotiator.Negotiate(
                new MqttServerCapabilities(),
                connectPacket,
                MqttProtocolVersion.V311,
                null);

            var result310 = MqttCapabilityNegotiator.Negotiate(
                new MqttServerCapabilities(),
                connectPacket,
                MqttProtocolVersion.V310,
                null);

            Assert.AreEqual(MqttProtocolVersion.V311, result311.ProtocolVersion);
            Assert.AreEqual(ushort.MaxValue, result311.ClientReceiveMaximum);
            Assert.AreEqual(268435455u, result311.ClientMaximumPacketSize);
            Assert.AreEqual((ushort)0, result311.ClientTopicAliasMaximum);
            Assert.AreEqual(0u, result311.SessionExpiryInterval);
            Assert.IsTrue(result311.RequestProblemInformation);

            Assert.AreEqual(MqttProtocolVersion.V310, result310.ProtocolVersion);
            Assert.AreEqual(0u, result310.SessionExpiryInterval);
        }

        /// <summary>
        /// 验证 V5 客户端把接收上限声明为 0 时协商抛出协议违规异常
        /// </summary>
        [TestMethod]
        public void Negotiate_V5_RejectsZeroClientReceiveMaximum()
        {
            var connectPacket = new MqttConnectPacket
            {
                ClientId = "invalid-1",
                Properties = new MqttConnectPacketProperties
                {
                    ReceiveMaximum = 0
                }
            };

            Assert.ThrowsException<MqttProtocolViolationException>(() =>
                MqttCapabilityNegotiator.Negotiate(
                    new MqttServerCapabilities(),
                    connectPacket,
                    MqttProtocolVersion.V500,
                    null));
        }

        /// <summary>
        /// 验证 V5 客户端把最大报文长度声明为 0 时协商抛出协议违规异常
        /// </summary>
        [TestMethod]
        public void Negotiate_V5_RejectsZeroClientMaximumPacketSize()
        {
            var connectPacket = new MqttConnectPacket
            {
                ClientId = "invalid-2",
                Properties = new MqttConnectPacketProperties
                {
                    MaximumPacketSize = 0
                }
            };

            Assert.ThrowsException<MqttProtocolViolationException>(() =>
                MqttCapabilityNegotiator.Negotiate(
                    new MqttServerCapabilities(),
                    connectPacket,
                    MqttProtocolVersion.V500,
                    null));
        }
    }
}