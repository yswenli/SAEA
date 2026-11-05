using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT;

namespace SAEA.MQTT.Tests.SubscriptionParsing
{
    [TestClass]
    public class MqttTopicFilterSharedTests
    {
        [TestMethod]
        public void TryParseShared_ValidFilter_ReturnsShareNameAndFilter()
        {
            string shareName;
            string filter;

            var result = MqttTopicFilter.TryParseShared("$share/group/sport/tennis", out shareName, out filter);

            Assert.IsTrue(result);
            Assert.AreEqual("group", shareName);
            Assert.AreEqual("sport/tennis", filter);
        }

        [TestMethod]
        public void TryParseShared_WildcardFilter_ReturnsParsedFilter()
        {
            string shareName;
            string filter;

            var result = MqttTopicFilter.TryParseShared("$share/farm/+/temperature/#", out shareName, out filter);

            Assert.IsTrue(result);
            Assert.AreEqual("farm", shareName);
            Assert.AreEqual("+/temperature/#", filter);
        }

        [DataTestMethod]
        [DataRow("$share//sport/tennis")]
        [DataRow("$share/group")]
        [DataRow("$share/group/")]
        [DataRow("$share/")]
        [DataRow("$share")]
        public void TryParseShared_EmptyShareOrFilter_ReturnsFalse(string topic)
        {
            string shareName;
            string filter;

            Assert.IsFalse(MqttTopicFilter.TryParseShared(topic, out shareName, out filter));
            Assert.IsNull(shareName);
            Assert.IsNull(filter);
        }

        [DataTestMethod]
        [DataRow("$share/gr+oup/sport")]
        [DataRow("$share/gr#oup/sport")]
        public void TryParseShared_WildcardInShareName_ReturnsFalse(string topic)
        {
            string shareName;
            string filter;

            Assert.IsFalse(MqttTopicFilter.TryParseShared(topic, out shareName, out filter));
        }

        [DataTestMethod]
        [DataRow("$share/group/sport/#/ranking")]
        [DataRow("$share/group/sp+ort")]
        [DataRow("$share/group/sport/tennis#")]
        public void TryParseShared_InvalidInnerFilter_ReturnsFalse(string topic)
        {
            string shareName;
            string filter;

            Assert.IsFalse(MqttTopicFilter.TryParseShared(topic, out shareName, out filter));
        }

        [DataTestMethod]
        [DataRow("sport/tennis")]
        [DataRow("$sharing/group/sport")]
        [DataRow("$SHARE/group/sport")]
        [DataRow("")]
        [DataRow(null)]
        public void TryParseShared_NonSharedFilter_ReturnsFalse(string topic)
        {
            string shareName;
            string filter;

            Assert.IsFalse(MqttTopicFilter.TryParseShared(topic, out shareName, out filter));
            Assert.IsNull(shareName);
            Assert.IsNull(filter);
        }

        [TestMethod]
        public void TryParseShared_NonSharedFilter_DoesNotSetSubscriptionMetadata()
        {
            var topicFilter = new MqttTopicFilter { Topic = "sport/tennis" };

            Assert.IsNull(topicFilter.ShareName);
            Assert.IsNull(topicFilter.ParsedFilter);
            Assert.IsFalse(topicFilter.IsShared);
        }
    }
}