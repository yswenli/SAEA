using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Internal;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Tests.FlowControl
{
    /// <summary>
    /// 入站流量控制器的配额占用与释放逻辑
    /// </summary>
    [TestClass]
    public class MqttInboundFlowControlTests
    {
        /// <summary>
        /// 验证 QoS 0 报文总能获取配额且不消耗任何配额
        /// </summary>
        [TestMethod]
        public void QoS0_AlwaysAcquiresAndDoesNotConsume()
        {
            var flowControl = new MqttInboundFlowControl(1);

            Assert.IsTrue(flowControl.TryAcquire(1, MqttQualityOfServiceLevel.AtMostOnce));
            Assert.IsTrue(flowControl.TryAcquire(2, MqttQualityOfServiceLevel.AtMostOnce));
            Assert.AreEqual(1, flowControl.Available);
        }

        /// <summary>
        /// 验证同一报文标识符重复获取配额只占用一个名额
        /// </summary>
        [TestMethod]
        public void DuplicatePacketIdentifier_DoesNotConsumeTwice()
        {
            var flowControl = new MqttInboundFlowControl(2);

            Assert.IsTrue(flowControl.TryAcquire(7, MqttQualityOfServiceLevel.AtLeastOnce));
            Assert.IsTrue(flowControl.TryAcquire(7, MqttQualityOfServiceLevel.AtLeastOnce));
            Assert.AreEqual(1, flowControl.Available);

            Assert.IsTrue(flowControl.TryAcquire(8, MqttQualityOfServiceLevel.AtLeastOnce));
            Assert.AreEqual(0, flowControl.Available);
        }

        /// <summary>
        /// 验证并发处理的报文数超过接收上限时获取配额失败且剩余配额为 0
        /// </summary>
        [TestMethod]
        public void ExceedingReceiveMaximum_ReturnsFalse()
        {
            var flowControl = new MqttInboundFlowControl(1);

            Assert.IsTrue(flowControl.TryAcquire(1, MqttQualityOfServiceLevel.ExactlyOnce));
            Assert.IsFalse(flowControl.TryAcquire(2, MqttQualityOfServiceLevel.ExactlyOnce));
            Assert.AreEqual(0, flowControl.Available);
        }

        /// <summary>
        /// 验证释放报文标识符后配额归还，先前被拒绝的报文可重新获取
        /// </summary>
        [TestMethod]
        public void ReleaseFreesQuota()
        {
            var flowControl = new MqttInboundFlowControl(1);

            Assert.IsTrue(flowControl.TryAcquire(1, MqttQualityOfServiceLevel.ExactlyOnce));
            Assert.IsFalse(flowControl.TryAcquire(2, MqttQualityOfServiceLevel.ExactlyOnce));

            flowControl.Release(1);

            Assert.AreEqual(1, flowControl.Available);
            Assert.IsTrue(flowControl.TryAcquire(2, MqttQualityOfServiceLevel.ExactlyOnce));
        }

        /// <summary>
        /// 验证剩余配额随每次获取递减，用尽后保持不变
        /// </summary>
        [TestMethod]
        public void AvailableReflectsRemainingQuota()
        {
            var flowControl = new MqttInboundFlowControl(3);

            Assert.AreEqual(3, flowControl.Available);

            Assert.IsTrue(flowControl.TryAcquire(1, MqttQualityOfServiceLevel.AtLeastOnce));
            Assert.AreEqual(2, flowControl.Available);

            Assert.IsTrue(flowControl.TryAcquire(2, MqttQualityOfServiceLevel.AtLeastOnce));
            Assert.AreEqual(1, flowControl.Available);

            Assert.IsTrue(flowControl.TryAcquire(3, MqttQualityOfServiceLevel.AtLeastOnce));
            Assert.AreEqual(0, flowControl.Available);

            Assert.IsFalse(flowControl.TryAcquire(4, MqttQualityOfServiceLevel.AtLeastOnce));
            Assert.AreEqual(0, flowControl.Available);
        }
    }
}