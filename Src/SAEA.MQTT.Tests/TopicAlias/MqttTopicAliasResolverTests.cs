using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Internal;

namespace SAEA.MQTT.Tests.TopicAlias
{
    [TestClass]
    public class MqttTopicAliasResolverTests
    {
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