using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Client.Disconnecting;
using SAEA.MQTT.Exceptions;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;
using SAEA.MQTT.Tests.TestInfrastructure;

namespace SAEA.MQTT.Tests.ReasonCodes
{
    [TestClass]
    public class ClientDisconnectTests
    {
        static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        [TestMethod]
        public async Task DisconnectAsync_PreservesUserReasonCodeAndProperties()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var broker = await FakeMqttBroker.StartAsync();
            await using var client = await MqttTestClient.ConnectAsync(
                broker.Port,
                MqttProtocolVersion.V500,
                "client-disconnect-properties",
                options => options.WithSessionExpiryInterval(60),
                timeout.Token);

            await client.Client.DisconnectAsync(
                new MqttClientDisconnectOptions
                {
                    ReasonCode = MqttClientDisconnectReason.AdministrativeAction,
                    ReasonString = "maintenance",
                    SessionExpiryInterval = 30,
                    UserProperties = new List<MqttUserProperty>
                    {
                        new MqttUserProperty("source", "test")
                    }
                },
                timeout.Token).ConfigureAwait(false);

            var disconnect = await broker.DisconnectReceived
                .WaitAsync(WaitTimeout, timeout.Token)
                .ConfigureAwait(false);

            Assert.AreEqual(
                MqttDisconnectReasonCode.AdministrativeAction,
                disconnect.ReasonCode.Value);
            Assert.IsNotNull(disconnect.Properties);
            Assert.AreEqual("maintenance", disconnect.Properties.ReasonString);
            Assert.AreEqual((uint?)30, disconnect.Properties.SessionExpiryInterval);
            Assert.IsNotNull(disconnect.Properties.UserProperties);
            Assert.AreEqual(1, disconnect.Properties.UserProperties.Count);
            Assert.AreEqual("source", disconnect.Properties.UserProperties[0].Name);
            Assert.AreEqual("test", disconnect.Properties.UserProperties[0].Value);
        }

        [TestMethod]
        public async Task DisconnectAsync_RaisesSessionExpiryAboveConnectValue_IsRejected()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var broker = await FakeMqttBroker.StartAsync();
            await using var client = await MqttTestClient.ConnectAsync(
                broker.Port,
                MqttProtocolVersion.V500,
                "client-disconnect-session-expiry",
                options => options.WithSessionExpiryInterval(0),
                timeout.Token);

            try
            {
                await client.Client.DisconnectAsync(
                    new MqttClientDisconnectOptions
                    {
                        SessionExpiryInterval = 10
                    },
                    timeout.Token).ConfigureAwait(false);

                Assert.Fail("Raising the session expiry interval above the CONNECT value must be rejected.");
            }
            catch (MqttProtocolViolationException)
            {
            }
        }
    }
}