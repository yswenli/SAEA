using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Exceptions;
using SAEA.MQTT.Protocol;
using SAEA.MQTT.Server;
using SAEA.MQTT.Tests.TestInfrastructure;

namespace SAEA.MQTT.Tests.Capabilities
{
    /// <summary>
    /// 服务端选项构建器的能力配置项是否如实流入 <see cref="MqttServerCapabilities"/>
    /// </summary>
    [TestClass]
    public class MqttServerCapabilitiesBuilderTests
    {
        /// <summary>
        /// 验证构建器上设置的接收上限、报文长度、主题别名、各能力开关与超时参数全部写入服务端能力对象
        /// </summary>
        [TestMethod]
        public void Builder_FlowsConfiguredValuesIntoServerCapabilities()
        {
            var options = new MqttServerOptionsBuilder()
                .WithReceiveMaximum(10)
                .WithMaximumPacketSize(1024)
                .WithTopicAliasMaximum(5)
                .WithRetainAvailable(false)
                .WithWildcardSubscriptionAvailable(false)
                .WithSubscriptionIdentifiersAvailable(false)
                .WithSharedSubscriptionAvailable(false)
                .WithMaximumQoS(MqttQualityOfServiceLevel.AtMostOnce)
                .WithServerKeepAlive(30)
                .WithMaximumSessionExpiryInterval(60)
                .Build();

            var capabilities = options.Capabilities;

            Assert.AreEqual((ushort)10, capabilities.ReceiveMaximum);
            Assert.AreEqual(1024u, capabilities.MaximumPacketSize);
            Assert.AreEqual((ushort)5, capabilities.TopicAliasMaximum);
            Assert.IsFalse(capabilities.RetainAvailable);
            Assert.IsFalse(capabilities.WildcardSubscriptionAvailable);
            Assert.IsFalse(capabilities.SubscriptionIdentifiersAvailable);
            Assert.IsFalse(capabilities.SharedSubscriptionAvailable);
            Assert.AreEqual(MqttQualityOfServiceLevel.AtMostOnce, capabilities.MaximumQoS);
            Assert.AreEqual((ushort)30, capabilities.ServerKeepAlive);
            Assert.AreEqual(60u, capabilities.MaximumSessionExpiryInterval);
        }

        /// <summary>
        /// 验证把接收上限配置为 0 时服务端启动抛出配置异常
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task StartAsync_RejectsZeroReceiveMaximum()
        {
            var options = new MqttServerOptionsBuilder()
                .WithDefaultEndpoint()
                .WithDefaultEndpointPort(FreePort.GetFreePort())
                .WithDefaultEndpointBoundIPAddress(IPAddress.Loopback)
                .WithDefaultEndpointBoundIPV6Address(IPAddress.None)
                .WithReceiveMaximum(0)
                .Build();

            var server = new MqttFactory().CreateMqttServer();

            try
            {
                await Assert.ThrowsExactlyAsync<MqttConfigurationException>(
                    () => server.StartAsync(options));
            }
            finally
            {
                try
                {
                    server.Dispose();
                }
                catch
                {
                    // Dispose also stops the partially started server and may fail on this path.
                }
            }
        }
    }
}