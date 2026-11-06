using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Client.Publishing;
using SAEA.MQTT.Client.Subscribing;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Protocol;
using SAEA.MQTT.Tests.TestInfrastructure;

namespace SAEA.MQTT.Tests.FlowControl
{
    /// <summary>
    /// 客户端在 CONNECT 中声明接收上限为 1 时的收发行为
    /// </summary>
    [TestClass]
    public class ClientReceiveMaximumTests
    {
        static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 验证客户端声明接收上限为 1 时仍能连续发布三条 QoS 1 消息且全部成功
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task ClientDeclaringReceiveMaximumOne_PublishesThreeQoS1Successfully()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync();
            await using var client = await MqttTestClient.ConnectAsync(
                server.Port,
                MqttProtocolVersion.V500,
                "client-receive-maximum-publisher",
                options => options.WithReceiveMaximum(1),
                timeout.Token);

            for (var i = 0; i < 3; i++)
            {
                var result = await client.Client.PublishAsync(
                    new MqttApplicationMessage
                    {
                        Topic = "client-receive/publisher",
                        Payload = Encoding.UTF8.GetBytes("payload-" + i),
                        QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce
                    },
                    timeout.Token);

                Assert.AreEqual(MqttClientPublishReasonCode.Success, result.ReasonCode);
            }
        }

        /// <summary>
        /// 验证声明接收上限为 1 的订阅端能按序收全三条 QoS 1 消息
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task ClientDeclaringReceiveMaximumOne_ReceivesThreeQoS1Messages()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync();

            var collector = new TestMessageCollector();
            await using var subscriber = await MqttTestClient.ConnectAsync(
                server.Port,
                MqttProtocolVersion.V500,
                "client-receive-maximum-subscriber",
                options => options.WithReceiveMaximum(1),
                timeout.Token);

            subscriber.Client.ApplicationMessageReceivedHandler = collector;

            await subscriber.Client.SubscribeAsync(
                new MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter("client-receive/subscriber", MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build(),
                timeout.Token);

            await using var publisher = await MqttTestClient.ConnectAsync(
                server.Port,
                MqttProtocolVersion.V500,
                "client-receive-maximum-source",
                null,
                timeout.Token);

            for (var i = 0; i < 3; i++)
            {
                await publisher.Client.PublishAsync(
                    new MqttApplicationMessage
                    {
                        Topic = "client-receive/subscriber",
                        Payload = Encoding.UTF8.GetBytes("payload-" + i),
                        QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce
                    },
                    timeout.Token);
            }

            for (var i = 0; i < 3; i++)
            {
                var message = await collector.WaitAsync(WaitTimeout).ConfigureAwait(false);
                Assert.AreEqual("client-receive/subscriber", message.Topic);
                Assert.AreEqual("payload-" + i, Encoding.UTF8.GetString(message.Payload));
            }
        }
    }
}