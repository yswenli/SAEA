using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MQTT.Internal;
using SAEA.MQTT.Protocol;

namespace SAEA.MQTT.Tests.FlowControl
{
    [TestClass]
    public class MqttInboundFlowControlTests
    {
        [TestMethod]
        public void QoS0_AlwaysAcquiresAndDoesNotConsume()
        {
            var flowControl = new MqttInboundFlowControl(1);

            Assert.IsTrue(flowControl.TryAcquire(1, MqttQualityOfServiceLevel.AtMostOnce));
            Assert.IsTrue(flowControl.TryAcquire(2, MqttQualityOfServiceLevel.AtMostOnce));
            Assert.AreEqual(1, flowControl.Available);
        }

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

        [TestMethod]
        public void ExceedingReceiveMaximum_ReturnsFalse()
        {
            var flowControl = new MqttInboundFlowControl(1);

            Assert.IsTrue(flowControl.TryAcquire(1, MqttQualityOfServiceLevel.ExactlyOnce));
            Assert.IsFalse(flowControl.TryAcquire(2, MqttQualityOfServiceLevel.ExactlyOnce));
            Assert.AreEqual(0, flowControl.Available);
        }

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