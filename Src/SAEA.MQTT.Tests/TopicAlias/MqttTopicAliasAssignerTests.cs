using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Internal;

namespace SAEA.MQTT.Tests.TopicAlias
{
    [TestClass]
    public class MqttTopicAliasAssignerTests
    {
        [TestMethod]
        public void MaximumZero_ReturnsFalseAndIncludesTopic()
        {
            var assigner = new MqttTopicAliasAssigner(0);

            ushort alias;
            bool includeTopic;
            var result = assigner.TryAssign("topic/a", out alias, out includeTopic);

            Assert.IsFalse(result);
            Assert.AreEqual((ushort)0, alias);
            Assert.IsTrue(includeTopic);
        }

        [TestMethod]
        public void EmptyTopic_ReturnsFalse()
        {
            var assigner = new MqttTopicAliasAssigner(5);

            ushort alias;
            bool includeTopic;
            var result = assigner.TryAssign(string.Empty, out alias, out includeTopic);

            Assert.IsFalse(result);
            Assert.AreEqual((ushort)0, alias);
            Assert.IsTrue(includeTopic);
        }

        [TestMethod]
        public void ExistingTopic_ReturnsSameAliasAndOmitsTopic()
        {
            var assigner = new MqttTopicAliasAssigner(5);

            ushort alias;
            bool includeTopic;
            var first = assigner.TryAssign("topic/a", out alias, out includeTopic);

            Assert.IsTrue(first);
            Assert.AreEqual((ushort)1, alias);
            Assert.IsTrue(includeTopic);

            var second = assigner.TryAssign("topic/a", out alias, out includeTopic);

            Assert.IsTrue(second);
            Assert.AreEqual((ushort)1, alias);
            Assert.IsFalse(includeTopic);
        }

        [TestMethod]
        public void NewTopics_AssignSequentialAliases()
        {
            var assigner = new MqttTopicAliasAssigner(5);

            ushort alias;
            bool includeTopic;

            Assert.IsTrue(assigner.TryAssign("topic/a", out alias, out includeTopic));
            Assert.AreEqual((ushort)1, alias);
            Assert.IsTrue(includeTopic);

            Assert.IsTrue(assigner.TryAssign("topic/b", out alias, out includeTopic));
            Assert.AreEqual((ushort)2, alias);
            Assert.IsTrue(includeTopic);

            Assert.IsTrue(assigner.TryAssign("topic/c", out alias, out includeTopic));
            Assert.AreEqual((ushort)3, alias);
            Assert.IsTrue(includeTopic);
        }

        [TestMethod]
        public void WhenFull_EvictsOldestAliasFifo()
        {
            var assigner = new MqttTopicAliasAssigner(2);

            ushort alias;
            bool includeTopic;

            Assert.IsTrue(assigner.TryAssign("topic/a", out alias, out includeTopic));
            Assert.AreEqual((ushort)1, alias);
            Assert.IsTrue(includeTopic);

            Assert.IsTrue(assigner.TryAssign("topic/b", out alias, out includeTopic));
            Assert.AreEqual((ushort)2, alias);
            Assert.IsTrue(includeTopic);

            Assert.IsTrue(assigner.TryAssign("topic/c", out alias, out includeTopic));
            Assert.AreEqual((ushort)1, alias);
            Assert.IsTrue(includeTopic);

            Assert.IsTrue(assigner.TryAssign("topic/c", out alias, out includeTopic));
            Assert.AreEqual((ushort)1, alias);
            Assert.IsFalse(includeTopic);

            // topic/a 已被淘汰，必须重新分配别名（淘汰最早的 topic/b）。
            Assert.IsTrue(assigner.TryAssign("topic/a", out alias, out includeTopic));
            Assert.AreEqual((ushort)2, alias);
            Assert.IsTrue(includeTopic);
        }
    }
}