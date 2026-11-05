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
    [TestClass]
    public class MqttServerCapabilitiesBuilderTests
    {
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
                await Assert.ThrowsExceptionAsync<MqttConfigurationException>(
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