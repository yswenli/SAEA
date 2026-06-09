using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.EventBus;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Common.Tests.EventBus
{
    [TestClass]
    public class EventSubscriberTests
    {
        [TestMethod]
        public void EventSubscriber_ShouldInitializeCorrectly()
        {
            Func<EventMessage, CancellationToken, Task> handler = (msg, ct) => Task.CompletedTask;
            
            var sub = new EventSubscriber(handler, retryCount: 3);
            
            Assert.IsNotNull(sub.Id);
            Assert.AreEqual(3, sub.RetryCount);
            Assert.IsNotNull(sub.Handler);
        }
        
        [TestMethod]
        public void EventSubscriber_ShouldUseDefaultRetryCount()
        {
            var sub = new EventSubscriber((msg, ct) => Task.CompletedTask);
            Assert.AreEqual(3, sub.RetryCount);
        }
        
        [TestMethod]
        public void EventSubscriber_ShouldGenerateUniqueId()
        {
            var sub1 = new EventSubscriber((msg, ct) => Task.CompletedTask);
            var sub2 = new EventSubscriber((msg, ct) => Task.CompletedTask);
            
            Assert.AreNotEqual(sub1.Id, sub2.Id);
        }
    }
}