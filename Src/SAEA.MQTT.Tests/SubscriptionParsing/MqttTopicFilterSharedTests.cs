using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT;

namespace SAEA.MQTT.Tests.SubscriptionParsing
{
    /// <summary>
    /// 共享订阅过滤器 <c>$share/{组名}/{过滤器}</c> 的解析与合法性校验
    /// </summary>
    [TestClass]
    public class MqttTopicFilterSharedTests
    {
        /// <summary>
        /// 验证合法共享过滤器解析出组名与去掉前缀后的内层过滤器
        /// </summary>
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

        /// <summary>
        /// 验证内层过滤器含 + 与 # 通配符时仍能正确解析出组名 farm 与内层过滤器
        /// </summary>
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

        /// <summary>
        /// 验证组名或内层过滤器为空、以及缺少 $share 前缀分隔符时解析失败且两个输出参数为 null
        /// </summary>
        /// <param name="topic">待解析的主题过滤器</param>
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

        /// <summary>
        /// 验证组名中包含 + 或 # 通配符时解析失败
        /// </summary>
        /// <param name="topic">待解析的主题过滤器</param>
        [DataTestMethod]
        [DataRow("$share/gr+oup/sport")]
        [DataRow("$share/gr#oup/sport")]
        public void TryParseShared_WildcardInShareName_ReturnsFalse(string topic)
        {
            string shareName;
            string filter;

            Assert.IsFalse(MqttTopicFilter.TryParseShared(topic, out shareName, out filter));
        }

        /// <summary>
        /// 验证内层过滤器本身非法（# 不在末级、+ 不独占层级、# 与字符相连）时解析失败
        /// </summary>
        /// <param name="topic">待解析的主题过滤器</param>
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

        /// <summary>
        /// 验证普通过滤器、大小写不符的 $SHARE、相似前缀、空串与 null 均解析失败且输出参数为 null
        /// </summary>
        /// <param name="topic">待解析的主题过滤器</param>
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

        /// <summary>
        /// 验证普通过滤器构造后不设置共享相关元数据：组名与内层过滤器为 null、IsShared 为 false
        /// </summary>
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