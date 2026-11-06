using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Internal;

namespace SAEA.MQTT.Tests.TopicAlias
{
    /// <summary>
    /// 入站主题别名解析器的校验与映射建立/覆盖
    /// </summary>
    [TestClass]
    public class MqttTopicAliasResolverTests
    {
        /// <summary>
        /// 验证最大别名数为 0 时任何别名都判为超出上限
        /// </summary>
        [TestMethod]
        public void MaximumZero_ReturnsExceedsMaximum()
        {
            var resolver = new MqttTopicAliasResolver(0);

            string resolvedTopic;
            MqttTopicAliasResolver.AliasError error;
            var result = resolver.TryResolve(1, "topic/a", out resolvedTopic, out error);

            Assert.IsFalse(result);
            Assert.IsNull(resolvedTopic);
            Assert.AreEqual(MqttTopicAliasResolver.AliasError.ExceedsMaximum, error);
        }

        /// <summary>
        /// 验证别名为 0（协议非法值）时解析失败并返回 Zero 错误
        /// </summary>
        [TestMethod]
        public void ZeroAlias_ReturnsZero()
        {
            var resolver = new MqttTopicAliasResolver(5);

            string resolvedTopic;
            MqttTopicAliasResolver.AliasError error;
            var result = resolver.TryResolve(0, "topic/a", out resolvedTopic, out error);

            Assert.IsFalse(result);
            Assert.IsNull(resolvedTopic);
            Assert.AreEqual(MqttTopicAliasResolver.AliasError.Zero, error);
        }

        /// <summary>
        /// 验证别名超过最大别名数（5 时传入 6）时解析失败并返回 ExceedsMaximum 错误
        /// </summary>
        [TestMethod]
        public void AliasAboveMaximum_ReturnsExceedsMaximum()
        {
            var resolver = new MqttTopicAliasResolver(5);

            string resolvedTopic;
            MqttTopicAliasResolver.AliasError error;
            var result = resolver.TryResolve(6, "topic/a", out resolvedTopic, out error);

            Assert.IsFalse(result);
            Assert.IsNull(resolvedTopic);
            Assert.AreEqual(MqttTopicAliasResolver.AliasError.ExceedsMaximum, error);
        }

        /// <summary>
        /// 验证带主题的首次解析建立别名映射，之后同别名附带空主题即可解析出原主题
        /// </summary>
        [TestMethod]
        public void NonEmptyTopic_EstablishesMapping()
        {
            var resolver = new MqttTopicAliasResolver(5);

            string resolvedTopic;
            MqttTopicAliasResolver.AliasError error;
            var assigned = resolver.TryResolve(2, "topic/a", out resolvedTopic, out error);

            Assert.IsTrue(assigned);
            Assert.AreEqual("topic/a", resolvedTopic);
            Assert.AreEqual(MqttTopicAliasResolver.AliasError.None, error);

            var resolved = resolver.TryResolve(2, string.Empty, out resolvedTopic, out error);

            Assert.IsTrue(resolved);
            Assert.AreEqual("topic/a", resolvedTopic);
            Assert.AreEqual(MqttTopicAliasResolver.AliasError.None, error);
        }

        /// <summary>
        /// 验证同一别名再次携带非空主题时覆盖旧映射，后续解析出新的主题
        /// </summary>
        [TestMethod]
        public void NonEmptyTopic_OverwritesExistingMapping()
        {
            var resolver = new MqttTopicAliasResolver(5);

            string resolvedTopic;
            MqttTopicAliasResolver.AliasError error;
            resolver.TryResolve(2, "topic/a", out resolvedTopic, out error);
            resolver.TryResolve(2, "topic/b", out resolvedTopic, out error);

            var resolved = resolver.TryResolve(2, string.Empty, out resolvedTopic, out error);

            Assert.IsTrue(resolved);
            Assert.AreEqual("topic/b", resolvedTopic);
            Assert.AreEqual(MqttTopicAliasResolver.AliasError.None, error);
        }

        /// <summary>
        /// 验证别名未建立映射且报文主题为空时解析失败并返回 Unknown 错误
        /// </summary>
        [TestMethod]
        public void EmptyTopicWithUnknownAlias_ReturnsUnknown()
        {
            var resolver = new MqttTopicAliasResolver(5);

            string resolvedTopic;
            MqttTopicAliasResolver.AliasError error;
            var result = resolver.TryResolve(3, string.Empty, out resolvedTopic, out error);

            Assert.IsFalse(result);
            Assert.IsNull(resolvedTopic);
            Assert.AreEqual(MqttTopicAliasResolver.AliasError.Unknown, error);
        }
    }
}