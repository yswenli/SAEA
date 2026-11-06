using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Diagnostics;
using SAEA.MQTT.Protocol;
using SAEA.MQTT.Server;

namespace SAEA.MQTT.Tests.SharedSubscriptions
{
    /// <summary>
    /// 会话级订阅匹配：共享订阅不参与本地匹配、No Local 与多过滤器 QoS 取值
    /// </summary>
    [TestClass]
    public class SubscriptionMatchingTests
    {
        /// <summary>
        /// 验证会话订阅 $share/group/sport/# 时本地匹配不命中，但注册表里记录了该共享组成员
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task CheckSubscriptions_SharedFilter_IsNotMatchedLocally()
        {
            var registry = new MqttSharedSubscriptionRegistry();
            var session = CreateSession(registry);
            session.IsV5 = true;

            await session.SubscriptionsManager.SubscribeAsync(
                new[]
                {
                    new MqttTopicFilter
                    {
                        Topic = "$share/group/sport/#",
                        QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce
                    }
                }).ConfigureAwait(false);

            var result = session.SubscriptionsManager.CheckSubscriptions(
                "sport/tennis",
                MqttQualityOfServiceLevel.AtLeastOnce,
                "publisher");

            Assert.IsFalse(result.IsSubscribed);
            Assert.AreEqual(0, result.Matches.Count);
            Assert.AreEqual(1, registry.GetMemberCount("group", "sport/#"));
        }

        /// <summary>
        /// 验证非共享订阅的投递 QoS 取订阅 QoS 与发布 QoS 的较小值（订阅 1、发布 2 得 1）
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task CheckSubscriptions_NonSharedFilter_UsesMinimumQualityOfService()
        {
            var session = CreateSession(new MqttSharedSubscriptionRegistry());

            await session.SubscriptionsManager.SubscribeAsync(
                new[]
                {
                    new MqttTopicFilter
                    {
                        Topic = "sport/#",
                        QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce
                    }
                }).ConfigureAwait(false);

            var result = session.SubscriptionsManager.CheckSubscriptions(
                "sport/tennis",
                MqttQualityOfServiceLevel.ExactlyOnce,
                "publisher");

            Assert.IsTrue(result.IsSubscribed);
            Assert.AreEqual(MqttQualityOfServiceLevel.AtLeastOnce, result.QualityOfServiceLevel);
            Assert.AreEqual(1, result.Matches.Count);
            Assert.AreEqual("sport/#", result.Matches[0].Filter);
            Assert.AreEqual(MqttQualityOfServiceLevel.AtLeastOnce, result.Matches[0].QualityOfServiceLevel);
            Assert.IsFalse(result.Matches[0].IsShared);
        }

        /// <summary>
        /// 验证 No Local 订阅只过滤发布者自身，其他客户端的消息正常入队
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task CheckSubscriptions_NoLocal_SkipsOnlyThePublishingClient()
        {
            var session = CreateSession(new MqttSharedSubscriptionRegistry());

            await session.SubscriptionsManager.SubscribeAsync(
                new[]
                {
                    new MqttTopicFilter
                    {
                        Topic = "sport/tennis",
                        QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce,
                        NoLocal = true
                    }
                }).ConfigureAwait(false);

            var sameClientResult = session.SubscriptionsManager.CheckSubscriptions(
                "sport/tennis",
                MqttQualityOfServiceLevel.AtMostOnce,
                session.ClientId);

            var otherClientResult = session.SubscriptionsManager.CheckSubscriptions(
                "sport/tennis",
                MqttQualityOfServiceLevel.AtMostOnce,
                "publisher");

            Assert.IsFalse(sameClientResult.IsSubscribed);
            Assert.IsTrue(otherClientResult.IsSubscribed);
            Assert.IsTrue(otherClientResult.Matches[0].NoLocal);

            var message = new MqttApplicationMessage
            {
                Topic = "sport/tennis",
                Payload = new byte[] { 1 }
            };

            Assert.IsFalse(session.EnqueueApplicationMessage(message, session.ClientId, false));
            Assert.AreEqual(0, session.ApplicationMessagesQueue.Count);

            Assert.IsTrue(session.EnqueueApplicationMessage(message, "publisher", false));
            Assert.AreEqual(1, session.ApplicationMessagesQueue.Count);
        }

        /// <summary>
        /// 验证多条过滤器同时命中时返回全部匹配项及其订阅标识符，最终 QoS 取最高订阅 QoS 与发布 QoS 的较小值
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task CheckSubscriptions_MultipleFilters_ReturnsEveryMatchAndHighestQualityOfService()
        {
            var session = CreateSession(new MqttSharedSubscriptionRegistry());
            session.IsV5 = true;

            await session.SubscriptionsManager.SubscribeAsync(
                new[]
                {
                    new MqttTopicFilter
                    {
                        Topic = "sport/#",
                        QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce,
                        SubscriptionIdentifier = 11
                    },
                    new MqttTopicFilter
                    {
                        Topic = "sport/tennis",
                        QualityOfServiceLevel = MqttQualityOfServiceLevel.ExactlyOnce,
                        SubscriptionIdentifier = 22
                    }
                }).ConfigureAwait(false);

            var result = session.SubscriptionsManager.CheckSubscriptions(
                "sport/tennis",
                MqttQualityOfServiceLevel.AtLeastOnce,
                "publisher");

            Assert.IsTrue(result.IsSubscribed);
            Assert.AreEqual(2, result.Matches.Count);
            Assert.AreEqual(MqttQualityOfServiceLevel.AtLeastOnce, result.QualityOfServiceLevel);
            CollectionAssert.AreEquivalent(
                new List<uint?> { 11, 22 },
                result.Matches.Select(m => m.SubscriptionIdentifier).ToList());
        }

        static MqttClientSession CreateSession(MqttSharedSubscriptionRegistry registry)
        {
            var logger = new MqttNetLogger();

            return new MqttClientSession(
                "client-a",
                new Dictionary<object, object>(),
                new MqttServerEventDispatcher(logger),
                new MqttServerOptions(),
                new MqttRetainedMessagesManager(),
                logger,
                registry);
        }
    }
}