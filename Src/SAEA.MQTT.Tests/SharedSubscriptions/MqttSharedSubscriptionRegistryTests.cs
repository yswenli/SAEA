using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Protocol;
using SAEA.MQTT.Server;

namespace SAEA.MQTT.Tests.SharedSubscriptions
{
    /// <summary>
    /// 共享订阅注册表的成员增删、轮询选择与匹配查询
    /// </summary>
    [TestClass]
    public class MqttSharedSubscriptionRegistryTests
    {
        /// <summary>
        /// 验证同一共享组下两个客户端各占一个成员名额，总成员数累加
        /// </summary>
        [TestMethod]
        public void Add_TwoClientsForSameKey_TracksBothMembers()
        {
            var registry = new MqttSharedSubscriptionRegistry();

            registry.Add("group", "sport/#", Member("client-a"));
            registry.Add("group", "sport/#", Member("client-b"));

            Assert.AreEqual(2, registry.GetMemberCount("group", "sport/#"));
            Assert.AreEqual(2, registry.TotalMemberCount);
        }

        /// <summary>
        /// 验证同一客户端重复加入同一共享组时不新增成员，而是原位替换，QoS 更新为新值
        /// </summary>
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

        /// <summary>
        /// 验证每次选取按插入顺序轮转，选完一轮后回到第一个成员
        /// </summary>
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

        /// <summary>
        /// 验证移除其中一个成员后另一成员保留，选取只会命中剩余成员
        /// </summary>
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

        /// <summary>
        /// 验证移除最后一个成员后该共享组键被删除，选取返回 false 且输出参数为 null
        /// </summary>
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

        /// <summary>
        /// 验证按客户端移除时跨越所有共享组键，仅保留其他客户端成员
        /// </summary>
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

        /// <summary>
        /// 验证按主题查询只返回过滤器能匹配该主题的共享组键，通配符与精确过滤器都命中
        /// </summary>
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

        /// <summary>
        /// 验证选取不存在的共享组键时返回 false 且输出参数为 null
        /// </summary>
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