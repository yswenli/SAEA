using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Diagnostics;
using SAEA.MQTT.Protocol;
using SAEA.MQTT.Server;

namespace SAEA.MQTT.Tests.SharedSubscriptions
{
    [TestClass]
    public class SubscriptionMatchingTests
    {
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