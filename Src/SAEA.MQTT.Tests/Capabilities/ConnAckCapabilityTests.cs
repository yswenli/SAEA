using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Adapter;
using SAEA.MQTT.Client.Connecting;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Protocol;
using SAEA.MQTT.Server;
using SAEA.MQTT.Tests.TestInfrastructure;

namespace SAEA.MQTT.Tests.Capabilities
{
    [TestClass]
    public class ConnAckCapabilityTests
    {
        [TestMethod]
        public async Task V5_DefaultConfiguration_DoesNotWriteDefaultProperties()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync();
            await using var client = await MqttTestClient.ConnectAsync(
                server.Port,
                MqttProtocolVersion.V500,
                "v5-default",
                cancellationToken: timeout.Token);

            var result = client.ConnectResult;

            Assert.AreEqual(MqttClientConnectResultCode.Success, result.ResultCode);
            Assert.IsNull(result.TopicAliasMaximum);
            Assert.IsNull(result.ReceiveMaximum);
            Assert.IsNull(result.MaximumPacketSize);
            Assert.IsNull(result.RetainAvailable);
            Assert.IsNull(result.WildcardSubscriptionAvailable);
            Assert.IsNull(result.SubscriptionIdentifiersAvailable);
            Assert.IsNull(result.SharedSubscriptionAvailable);
            Assert.IsNull(result.ServerKeepAlive);
            Assert.IsNull(result.SessionExpiryInterval);
            Assert.IsNull(result.ReasonString);
        }

        [TestMethod]
        public async Task V5_NonDefaultCapabilities_AreAllWrittenToConnAck()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync(builder => builder
                .WithReceiveMaximum(10)
                .WithMaximumPacketSize(1024)
                .WithTopicAliasMaximum(5)
                .WithRetainAvailable(false)
                .WithWildcardSubscriptionAvailable(false)
                .WithSubscriptionIdentifiersAvailable(false)
                .WithSharedSubscriptionAvailable(false)
                .WithMaximumQoS(MqttQualityOfServiceLevel.AtMostOnce));

            await using var client = await MqttTestClient.ConnectAsync(
                server.Port,
                MqttProtocolVersion.V500,
                "v5-all-off",
                cancellationToken: timeout.Token);

            var result = client.ConnectResult;

            Assert.AreEqual(MqttClientConnectResultCode.Success, result.ResultCode);
            Assert.AreEqual((ushort)10, result.ReceiveMaximum);
            Assert.AreEqual(1024u, result.MaximumPacketSize);
            Assert.AreEqual((ushort)5, result.TopicAliasMaximum);
            Assert.AreEqual(false, result.RetainAvailable);
            Assert.AreEqual(false, result.WildcardSubscriptionAvailable);
            Assert.AreEqual(false, result.SubscriptionIdentifiersAvailable);
            Assert.AreEqual(false, result.SharedSubscriptionAvailable);
            Assert.AreEqual(MqttQualityOfServiceLevel.AtMostOnce, result.MaximumQoS);
        }

        [TestMethod]
        public async Task V5_TopicAliasMaximum_IsNotHardCodedTo65535()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using (var defaultServer = await MqttTestServer.StartAsync())
            {
                await using var defaultClient = await MqttTestClient.ConnectAsync(
                    defaultServer.Port,
                    MqttProtocolVersion.V500,
                    "v5-alias-default",
                    cancellationToken: timeout.Token);

                Assert.IsNull(defaultClient.ConnectResult.TopicAliasMaximum);
            }

            await using (var aliasServer = await MqttTestServer.StartAsync(builder => builder
                .WithTopicAliasMaximum(5)))
            {
                await using var aliasClient = await MqttTestClient.ConnectAsync(
                    aliasServer.Port,
                    MqttProtocolVersion.V500,
                    "v5-alias-five",
                    cancellationToken: timeout.Token);

                Assert.AreEqual((ushort)5, aliasClient.ConnectResult.TopicAliasMaximum);
            }
        }

        [TestMethod]
        public async Task V5_RequestProblemInformationZero_SuppressesReasonString()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync(builder => builder
                .WithConnectionValidator(context =>
                {
                    context.ReasonCode = MqttConnectReasonCode.NotAuthorized;
                    context.ReasonString = "not allowed";
                }));

            var exception = await Assert.ThrowsExceptionAsync<MqttConnectingFailedException>(async () =>
            {
                await using var client = await MqttTestClient.ConnectAsync(
                    server.Port,
                    MqttProtocolVersion.V500,
                    "v5-no-problem-info",
                    options => options.WithRequestProblemInformation(false),
                    timeout.Token);
            });

            Assert.AreEqual(MqttClientConnectResultCode.NotAuthorized, exception.ResultCode);
            Assert.IsNull(exception.Result.ReasonString);
        }

        [TestMethod]
        public async Task SamePort_AcceptsV310V311AndV500Sequentially()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync();

            var versions = new[]
            {
                MqttProtocolVersion.V310,
                MqttProtocolVersion.V311,
                MqttProtocolVersion.V500
            };

            foreach (var version in versions)
            {
                var client = await MqttTestClient.ConnectAsync(
                    server.Port,
                    version,
                    "compat-" + version,
                    cancellationToken: timeout.Token);

                try
                {
                    Assert.AreEqual(
                        MqttClientConnectResultCode.Success,
                        client.ConnectResult.ResultCode,
                        "Version " + version + " did not connect successfully.");
                }
                finally
                {
                    await client.DisposeAsync();
                }
            }
        }
    }
}