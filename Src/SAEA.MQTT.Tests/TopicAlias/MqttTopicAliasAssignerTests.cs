using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Internal;

namespace SAEA.MQTT.Tests.TopicAlias
{
    /// <summary>
    /// 出站主题别名分配器的别名分配、复用与满载淘汰
    /// </summary>
    [TestClass]
    public class MqttTopicAliasAssignerTests
    {
        /// <summary>
        /// 验证最大别名数为 0 时不分配别名，返回 false 且仍要求报文携带主题
        /// </summary>
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

        /// <summary>
        /// 验证空主题不分配别名，返回 false 且仍要求报文携带主题
        /// </summary>
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

        /// <summary>
        /// 验证同一主题重复分配返回相同别名，且第二次不再携带主题
        /// </summary>
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

        /// <summary>
        /// 验证新主题按顺序从 1 开始分配别名，且首次分配都要求携带主题
        /// </summary>
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

        /// <summary>
        /// 验证别名用尽后按先进先出淘汰最早的映射，被淘汰的主题再次分配时复用被淘汰的别名
        /// </summary>
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