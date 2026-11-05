using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Protocol;
using SAEA.MQTT.Server;

namespace SAEA.MQTT.Tests.SharedSubscriptions
{
    [TestClass]
    public class MqttSharedSubscriptionRegistryTests
    {
        [TestMethod]
        public void Add_TwoClientsForSameKey_TracksBothMembers()
        {
            var registry = new MqttSharedSubscriptionRegistry();

            registry.Add("group", "sport/#", Member("client-a"));
            registry.Add("group", "sport/#", Member("client-b"));

            Assert.AreEqual(2, registry.GetMemberCount("group", "sport/#"));
            Assert.AreEqual(2, registry.TotalMemberCount);
        }

        [TestMethod]
        public void Add_SameClientForSameKey_ReplacesMemberInPlace()
        {
            var registry = new MqttSharedSubscriptionRegistry();

            registry.Add("group", "sport/#", Member("client-a", MqttQualityOfServiceLevel.AtMostOnce));
            registry.Add("group", "sport/#", Member("client-a", MqttQualityOfServiceLevel.ExactlyOnce));

            Assert.AreEqual(1, registry.GetMemberCount("group", "sport/#"));

            MqttSharedSubscriptionMember selected;
            Assert.IsTrue(registry.TrySelect("group", "sport/#", out selected));
            Assert.AreEqual("client-a", selected.ClientId);
            Assert.AreEqual(MqttQualityOfServiceLevel.ExactlyOnce, selected.QualityOfServiceLevel);
        }

        [TestMethod]
        public void TrySelect_RotatesMembersInInsertionOrder()
        {
            var registry = new MqttSharedSubscriptionRegistry();

            registry.Add("group", "sport/#", Member("client-a"));
            registry.Add("group", "sport/#", Member("client-b"));
            registry.Add("group", "sport/#", Member("client-c"));

            MqttSharedSubscriptionMember selected;

            Assert.IsTrue(registry.TrySelect("group", "sport/#", out selected));
            Assert.AreEqual("client-a", selected.ClientId);

            Assert.IsTrue(registry.TrySelect("group", "sport/#", out selected));
            Assert.AreEqual("client-b", selected.ClientId);

            Assert.IsTrue(registry.TrySelect("group", "sport/#", out selected));
            Assert.AreEqual("client-c", selected.ClientId);

            Assert.IsTrue(registry.TrySelect("group", "sport/#", out selected));
            Assert.AreEqual("client-a", selected.ClientId);
        }

        [TestMethod]
        public void Remove_OneMember_KeepsRemainingMembers()
        {
            var registry = new MqttSharedSubscriptionRegistry();

            registry.Add("group", "sport/#", Member("client-a"));
            registry.Add("group", "sport/#", Member("client-b"));

            registry.Remove("group", "sport/#", "client-a");

            Assert.AreEqual(1, registry.GetMemberCount("group", "sport/#"));
            Assert.AreEqual(1, registry.TotalMemberCount);

            MqttSharedSubscriptionMember selected;
            Assert.IsTrue(registry.TrySelect("group", "sport/#", out selected));
            Assert.AreEqual("client-b", selected.ClientId);
        }

        [TestMethod]
        public void Remove_LastMember_DeletesKey()
        {
            var registry = new MqttSharedSubscriptionRegistry();

            registry.Add("group", "sport/#", Member("client-a"));
            registry.Remove("group", "sport/#", "client-a");

            Assert.AreEqual(0, registry.GetMemberCount("group", "sport/#"));
            Assert.AreEqual(0, registry.TotalMemberCount);

            MqttSharedSubscriptionMember selected;
            Assert.IsFalse(registry.TrySelect("group", "sport/#", out selected));
            Assert.IsNull(selected);
        }

        [TestMethod]
        public void RemoveClient_RemovesMemberFromEveryKey()
        {
            var registry = new MqttSharedSubscriptionRegistry();

            registry.Add("group-a", "sport/#", Member("client-a"));
            registry.Add("group-a", "sport/#", Member("client-b"));
            registry.Add("group-b", "weather/#", Member("client-a"));
            registry.Add("group-b", "weather/#", Member("client-c"));

            registry.RemoveClient("client-a");

            Assert.AreEqual(1, registry.GetMemberCount("group-a", "sport/#"));
            Assert.AreEqual(1, registry.GetMemberCount("group-b", "weather/#"));
            Assert.AreEqual(2, registry.TotalMemberCount);
        }

        [TestMethod]
        public void GetMatchingKeys_ReturnsOnlyMatchingFilters()
        {
            var registry = new MqttSharedSubscriptionRegistry();

            registry.Add("group-a", "sport/#", Member("client-a"));
            registry.Add("group-b", "sport/tennis", Member("client-b"));
            registry.Add("group-c", "weather/#", Member("client-c"));

            var keys = registry.GetMatchingKeys("sport/tennis");

            Assert.AreEqual(2, keys.Count);
            CollectionAssert.AreEquivalent(
                new List<MqttSharedSubscriptionKey>
                {
                    new MqttSharedSubscriptionKey("group-a", "sport/#"),
                    new MqttSharedSubscriptionKey("group-b", "sport/tennis")
                },
                keys.ToList());
        }

        [TestMethod]
        public void TrySelect_UnknownKey_ReturnsFalse()
        {
            var registry = new MqttSharedSubscriptionRegistry();

            MqttSharedSubscriptionMember selected;
            Assert.IsFalse(registry.TrySelect("missing", "sport/#", out selected));
            Assert.IsNull(selected);
        }

        static MqttSharedSubscriptionMember Member(
            string clientId,
            MqttQualityOfServiceLevel qualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce)
        {
            return new MqttSharedSubscriptionMember
            {
                ClientId = clientId,
                QualityOfServiceLevel = qualityOfServiceLevel
            };
        }
    }
}