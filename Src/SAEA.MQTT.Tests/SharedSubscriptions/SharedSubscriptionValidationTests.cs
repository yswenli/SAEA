using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Client.Subscribing;
using SAEA.MQTT.Formatter;
using SAEA.MQTT.Packets;
using SAEA.MQTT.Protocol;
using SAEA.MQTT.Server;
using SAEA.MQTT.Tests.TestInfrastructure;

namespace SAEA.MQTT.Tests.SharedSubscriptions
{
    /// <summary>
    /// 服务端对共享订阅的校验：SUBACK 授予结果、能力开关、非法过滤器与注册表联动
    /// </summary>
    [TestClass]
    public class SharedSubscriptionValidationTests
    {
        static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 验证合法的共享订阅返回 Granted QoS1，并在注册表中登记为 group/sport/# 的一个成员
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task V5_ValidSharedSubscription_ReturnsGrantedAndRegistersMember()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync().ConfigureAwait(false);
            await using var client = await ConnectRawClientAsync(server, "shared-valid", timeout.Token).ConfigureAwait(false);

            var subAck = await SubscribeAsync(
                client,
                CreateSubscribePacket("$share/group/sport/#", MqttQualityOfServiceLevel.AtLeastOnce),
                timeout.Token).ConfigureAwait(false);

            Assert.AreEqual(1, subAck.ReasonCodes.Count);
            Assert.AreEqual(MqttSubscribeReasonCode.GrantedQoS1, subAck.ReasonCodes[0]);

            var registry = server.ClientSessionsManager.SharedSubscriptionRegistry;
            Assert.AreEqual(1, registry.GetMemberCount("group", "sport/#"));
            Assert.AreEqual(1, registry.TotalMemberCount);
        }

        /// <summary>
        /// 验证服务端关闭共享订阅能力时，SUBACK 返回「不支持共享订阅」且不登记成员
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task V5_SharedSubscriptionDisabled_ReturnsSharedSubscriptionsNotSupported()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer
                .StartAsync(builder => builder.WithSharedSubscriptionAvailable(false))
                .ConfigureAwait(false);

            await using var client = await ConnectRawClientAsync(server, "shared-disabled", timeout.Token).ConfigureAwait(false);

            var subAck = await SubscribeAsync(
                client,
                CreateSubscribePacket("$share/group/sport/#", MqttQualityOfServiceLevel.AtMostOnce),
                timeout.Token).ConfigureAwait(false);

            Assert.AreEqual(1, subAck.ReasonCodes.Count);
            Assert.AreEqual(MqttSubscribeReasonCode.SharedSubscriptionsNotSupported, subAck.ReasonCodes[0]);
            Assert.AreEqual(0, server.ClientSessionsManager.SharedSubscriptionRegistry.TotalMemberCount);
        }

        /// <summary>
        /// 验证共享组名为空的非法共享订阅返回「主题过滤器无效」且不登记成员
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task V5_MalformedSharedSubscription_ReturnsTopicFilterInvalid()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync().ConfigureAwait(false);
            await using var client = await ConnectRawClientAsync(server, "shared-malformed", timeout.Token).ConfigureAwait(false);

            var subAck = await SubscribeAsync(
                client,
                CreateSubscribePacket("$share//sport/#", MqttQualityOfServiceLevel.AtMostOnce),
                timeout.Token).ConfigureAwait(false);

            Assert.AreEqual(1, subAck.ReasonCodes.Count);
            Assert.AreEqual(MqttSubscribeReasonCode.TopicFilterInvalid, subAck.ReasonCodes[0]);
            Assert.AreEqual(0, server.ClientSessionsManager.SharedSubscriptionRegistry.TotalMemberCount);
        }

        /// <summary>
        /// 验证共享订阅同时设置 No Local 属协议错误：服务端不回 SUBACK，直接以协议错误原因码断开且不登记成员
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task V5_SharedSubscriptionWithNoLocal_DisconnectsWithProtocolError()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync().ConfigureAwait(false);
            await using var client = await ConnectRawClientAsync(server, "shared-no-local", timeout.Token).ConfigureAwait(false);

            var subscribePacket = CreateSubscribePacket("$share/group/sport/#", MqttQualityOfServiceLevel.AtMostOnce);
            subscribePacket.TopicFilters[0].NoLocal = true;

            await client.SendPacketAsync(subscribePacket, timeout.Token).ConfigureAwait(false);

            var firstPacket = await client.ReadPacketAsync(timeout.Token).ConfigureAwait(false);
            var disconnect = firstPacket as MqttDisconnectPacket;

            Assert.IsNotNull(disconnect, "A shared subscription with NoLocal must not receive a SUBACK packet.");
            Assert.AreEqual(MqttDisconnectReasonCode.ProtocolError, disconnect.ReasonCode.Value);
            Assert.AreEqual(0, server.ClientSessionsManager.SharedSubscriptionRegistry.TotalMemberCount);
        }

        /// <summary>
        /// 验证服务端关闭订阅标识符能力时，带订阅标识符的订阅不回 SUBACK，而以「不支持订阅标识符」原因码断开
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task V5_SubscriptionIdentifierDisabled_DisconnectsWithSubscriptionIdentifiersNotSupported()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer
                .StartAsync(builder => builder.WithSubscriptionIdentifiersAvailable(false))
                .ConfigureAwait(false);

            await using var client = await ConnectRawClientAsync(server, "subscription-id-disabled", timeout.Token).ConfigureAwait(false);

            var subscribePacket = CreateSubscribePacket("sport/#", MqttQualityOfServiceLevel.AtMostOnce);
            subscribePacket.Properties.SubscriptionIdentifier = 1;

            await client.SendPacketAsync(subscribePacket, timeout.Token).ConfigureAwait(false);

            var firstPacket = await client.ReadPacketAsync(timeout.Token).ConfigureAwait(false);
            var disconnect = firstPacket as MqttDisconnectPacket;

            Assert.IsNotNull(disconnect, "An unsupported Subscription Identifier must not receive a SUBACK packet.");
            Assert.AreEqual(
                MqttDisconnectReasonCode.SubscriptionIdentifiersNotSupported,
                disconnect.ReasonCode.Value);
        }

        /// <summary>
        /// 验证服务端关闭通配符订阅能力时，含 # 的过滤器返回「不支持通配符订阅」
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task V5_WildcardSubscriptionDisabled_ReturnsWildcardSubscriptionsNotSupported()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer
                .StartAsync(builder => builder.WithWildcardSubscriptionAvailable(false))
                .ConfigureAwait(false);

            await using var client = await ConnectRawClientAsync(server, "wildcard-disabled", timeout.Token).ConfigureAwait(false);

            var subAck = await SubscribeAsync(
                client,
                CreateSubscribePacket("sport/#", MqttQualityOfServiceLevel.AtMostOnce),
                timeout.Token).ConfigureAwait(false);

            Assert.AreEqual(1, subAck.ReasonCodes.Count);
            Assert.AreEqual(MqttSubscribeReasonCode.WildcardSubscriptionsNotSupported, subAck.ReasonCodes[0]);
        }

        /// <summary>
        /// 验证关闭通配符能力时，含通配符的共享订阅同样返回「不支持通配符订阅」且不登记成员
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task V5_SharedWildcardWhenWildcardDisabled_ReturnsWildcardSubscriptionsNotSupported()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer
                .StartAsync(builder => builder.WithWildcardSubscriptionAvailable(false))
                .ConfigureAwait(false);

            await using var client = await ConnectRawClientAsync(server, "shared-wildcard-disabled", timeout.Token).ConfigureAwait(false);

            var subAck = await SubscribeAsync(
                client,
                CreateSubscribePacket("$share/group/sport/#", MqttQualityOfServiceLevel.AtMostOnce),
                timeout.Token).ConfigureAwait(false);

            Assert.AreEqual(1, subAck.ReasonCodes.Count);
            Assert.AreEqual(MqttSubscribeReasonCode.WildcardSubscriptionsNotSupported, subAck.ReasonCodes[0]);
            Assert.AreEqual(0, server.ClientSessionsManager.SharedSubscriptionRegistry.TotalMemberCount);
        }

        /// <summary>
        /// 验证 V3.1.1 下 $share/ 前缀按普通字面量处理，授予 QoS0 且不登记共享订阅成员
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task V311_SharedPrefixRemainsLiteral_AndDoesNotRegisterMember()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync().ConfigureAwait(false);
            await using var client = await MqttTestClient
                .ConnectAsync(server.Port, MqttProtocolVersion.V311, "v311-shared-literal", cancellationToken: timeout.Token)
                .ConfigureAwait(false);

            var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter("$share/group/sport/#", MqttQualityOfServiceLevel.AtMostOnce)
                .Build();

            var result = await client.Client.SubscribeAsync(subscribeOptions, timeout.Token).ConfigureAwait(false);

            Assert.AreEqual(1, result.Items.Count);
            Assert.AreEqual(MqttClientSubscribeResultCode.GrantedQoS0, result.Items[0].ResultCode);
            Assert.AreEqual(0, server.ClientSessionsManager.SharedSubscriptionRegistry.TotalMemberCount);
        }

        /// <summary>
        /// 验证删除会话后共享订阅注册表中该客户端的成员被移除
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task V5_DeleteSession_RemovesRegistryMember()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync().ConfigureAwait(false);
            await using var client = await ConnectRawClientAsync(server, "shared-delete-session", timeout.Token).ConfigureAwait(false);

            await SubscribeAsync(
                client,
                CreateSubscribePacket("$share/group/sport/#", MqttQualityOfServiceLevel.AtMostOnce),
                timeout.Token).ConfigureAwait(false);

            var registry = server.ClientSessionsManager.SharedSubscriptionRegistry;
            Assert.AreEqual(1, registry.TotalMemberCount);

            await server.ClientSessionsManager
                .DeleteSessionAsync("shared-delete-session")
                .ConfigureAwait(false);

            Assert.AreEqual(0, registry.TotalMemberCount);
        }

        /// <summary>
        /// 验证取消订阅共享过滤器后返回成功原因码，并从注册表移除该成员
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task V5_Unsubscribe_RemovesRegistryMember()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await using var server = await MqttTestServer.StartAsync().ConfigureAwait(false);
            await using var client = await ConnectRawClientAsync(server, "shared-unsubscribe", timeout.Token).ConfigureAwait(false);

            await SubscribeAsync(
                client,
                CreateSubscribePacket("$share/group/sport/#", MqttQualityOfServiceLevel.AtMostOnce),
                timeout.Token).ConfigureAwait(false);

            var registry = server.ClientSessionsManager.SharedSubscriptionRegistry;
            Assert.AreEqual(1, registry.TotalMemberCount);

            var unsubscribePacket = new MqttUnsubscribePacket
            {
                PacketIdentifier = 2,
                Properties = new MqttUnsubscribePacketProperties()
            };
            unsubscribePacket.TopicFilters.Add("$share/group/sport/#");

            await client.SendPacketAsync(unsubscribePacket, timeout.Token).ConfigureAwait(false);

            var unsubAck = await client.ReadPacketAsync(timeout.Token).ConfigureAwait(false) as MqttUnsubAckPacket;

            Assert.IsNotNull(unsubAck);
            Assert.AreEqual(1, unsubAck.ReasonCodes.Count);
            Assert.AreEqual(MqttUnsubscribeReasonCode.Success, unsubAck.ReasonCodes[0]);
            Assert.AreEqual(0, registry.TotalMemberCount);
        }

        static async Task<RawMqttTestClient> ConnectRawClientAsync(
            MqttTestServer server,
            string clientId,
            CancellationToken cancellationToken)
        {
            return await RawMqttTestClient
                .ConnectAsync(server.Port, clientId, null, cancellationToken)
                .ConfigureAwait(false);
        }

        static MqttSubscribePacket CreateSubscribePacket(
            string topic,
            MqttQualityOfServiceLevel qualityOfServiceLevel)
        {
            var packet = new MqttSubscribePacket
            {
                PacketIdentifier = 1,
                Properties = new MqttSubscribePacketProperties()
            };

            packet.TopicFilters.Add(new MqttTopicFilter
            {
                Topic = topic,
                QualityOfServiceLevel = qualityOfServiceLevel
            });

            return packet;
        }

        static async Task<MqttSubAckPacket> SubscribeAsync(
            RawMqttTestClient client,
            MqttSubscribePacket subscribePacket,
            CancellationToken cancellationToken)
        {
            await client.SendPacketAsync(subscribePacket, cancellationToken).ConfigureAwait(false);

            var subAck = await client.ReadPacketAsync(cancellationToken).ConfigureAwait(false) as MqttSubAckPacket;
            if (subAck == null)
            {
                throw new InvalidOperationException("The raw test client did not receive a SUBACK packet.");
            }

            return subAck;
        }
    }
}