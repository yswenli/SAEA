using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.EventBus;
using System;

namespace SAEA.Common.Tests.EventBus
{
    /// <summary>
    /// 校验事件消息的属性初始化与可空上下文
    /// </summary>
    [TestClass]
    public class EventMessageTests
    {
        /// <summary>
        /// 验证主题、数据与上下文按入参保存，时间戳不晚于当前 UTC 时间
        /// </summary>
        [TestMethod]
        public void EventMessage_ShouldInitializeCorrectly()
        {
            var data = new byte[] { 1, 2, 3 };
            var context = new { Key = "value" };
            
            var msg = new EventMessage("test.topic", data, context);
            
            Assert.AreEqual("test.topic", msg.Topic);
            Assert.AreSame(data, msg.Data);
            Assert.AreEqual(context, msg.Context);
            Assert.IsTrue(msg.Timestamp <= DateTime.UtcNow);
        }
        
        /// <summary>
        /// 验证不传上下文时 Context 为 null 而不抛异常
        /// </summary>
        [TestMethod]
        public void EventMessage_ShouldAllowNullContext()
        {
            var msg = new EventMessage("test.topic", new byte[] { 1 });
            Assert.IsNull(msg.Context);
        }
    }
}