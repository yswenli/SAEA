using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.EventBus;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Common.Tests.EventBus
{
    /// <summary>
    /// 校验事件订阅者的默认参数与标识生成
    /// </summary>
    [TestClass]
    public class EventSubscriberTests
    {
        /// <summary>
        /// 验证按入参保存重试次数，并自动生成 Id 与处理器引用
        /// </summary>
        [TestMethod]
        public void EventSubscriber_ShouldInitializeCorrectly()
        {
            Func<EventMessage, CancellationToken, Task> handler = (msg, ct) => Task.CompletedTask;
            
            var sub = new EventSubscriber(handler, retryCount: 3);
            
            Assert.IsNotNull(sub.Id);
            Assert.AreEqual(3, sub.RetryCount);
            Assert.IsNotNull(sub.Handler);
        }
        
        /// <summary>
        /// 验证不指定重试次数时默认值为 3
        /// </summary>
        [TestMethod]
        public void EventSubscriber_ShouldUseDefaultRetryCount()
        {
            var sub = new EventSubscriber((msg, ct) => Task.CompletedTask);
            Assert.AreEqual(3, sub.RetryCount);
        }
        
        /// <summary>
        /// 验证两个订阅者实例生成的 Id 互不相同
        /// </summary>
        [TestMethod]
        public void EventSubscriber_ShouldGenerateUniqueId()
        {
            var sub1 = new EventSubscriber((msg, ct) => Task.CompletedTask);
            var sub2 = new EventSubscriber((msg, ct) => Task.CompletedTask);
            
            Assert.AreNotEqual(sub1.Id, sub2.Id);
        }
    }
}