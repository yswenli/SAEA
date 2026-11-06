using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Client;
using SAEA.MQTT.Client.Options;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Protocol;
using SAEA.MQTT.Server;
using SAEA.MQTT.Tests.TestInfrastructure;

namespace SAEA.MQTT.Tests.Capabilities
{
    /// <summary>
    /// 客户端协议版本默认值与空 ClientId 分配行为
    /// </summary>
    [TestClass]
    public class DefaultProtocolVersionTests
    {
        /// <summary>
        /// 验证未显式指定版本时客户端选项的协议版本默认为 MQTT 3.1.1
        /// </summary>
        [TestMethod]
        public void ClientOptions_DefaultProtocolVersion_IsV311()
        {
            var options = new MqttClientOptions();

            Assert.AreEqual(MqttProtocolVersion.V311, options.ProtocolVersion);
        }

        /// <summary>
        /// 验证不调用 WithProtocolVersion 时服务端在连接校验回调中观察到的版本为 MQTT 3.1.1
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task ClientWithoutExplicitVersion_ConnectsAsV311()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            MqttProtocolVersion observedVersion = MqttProtocolVersion.Unknown;

            await using var server = await MqttTestServer.StartAsync(
                builder => builder.WithConnectionValidator(context =>
                {
                    observedVersion = context.ProtocolVersion;
                    context.ReasonCode = MqttConnectReasonCode.Success;
                }));

            var client = new MqttFactory().CreateMqttClient();
            try
            {
                await client.ConnectAsync(
                    new MqttClientOptionsBuilder()
                        .WithTcpServer("127.0.0.1", server.Port)
                        .WithClientId("default-version-client")
                        .WithCleanSession()
                        .WithCommunicationTimeout(TimeSpan.FromSeconds(10))
                        .Build(),
                    timeout.Token).ConfigureAwait(false);

                Assert.AreEqual(MqttProtocolVersion.V311, observedVersion);
            }
            finally
            {
                try
                {
                    if (client.IsConnected)
                    {
                        await client.DisconnectAsync().ConfigureAwait(false);
                    }
                }
                catch
                {
                }

                client.Dispose();
            }
        }

        /// <summary>
        /// 验证 V5.0 下客户端发送空 ClientId 时，服务端在 CONNACK 中回传以 saea- 开头的分配标识
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task EmptyClientId_V500_GetsAssignedIdentifier()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync();
            await using var client = await MqttTestClient.ConnectAsync(
                server.Port,
                MqttProtocolVersion.V500,
                "placeholder",
                options => options.WithClientId(string.Empty),
                timeout.Token);

            Assert.IsNotNull(client.ConnectResult);
            Assert.IsFalse(string.IsNullOrEmpty(client.ConnectResult.AssignedClientIdentifier));
            Assert.IsTrue(client.ConnectResult.AssignedClientIdentifier.StartsWith("saea-"));
        }
    }
}