using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.EventBus;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Common.Tests.EventBus
{
    [TestClass]
    public class TopicChannelTests
    {
        [TestMethod]
        public void TopicChannel_ShouldInitializeCorrectly()
        {
            var tc = new TopicChannel(capacity: 100);
            
            Assert.IsNotNull(tc);
        }
        
        [TestMethod]
        public async Task TopicChannel_ShouldPublishAndRead()
        {
            var tc = new TopicChannel(capacity: 10);
            var msg = new EventMessage("test", new byte[] { 1, 2, 3 });
            
            await tc.PublishAsync(msg);
            
            var readMsg = await tc.ReadAsync(CancellationToken.None);
            Assert.AreEqual(msg, readMsg);
        }
        
        [TestMethod]
        public void TopicChannel_ShouldAddSubscriber()
        {
            var tc = new TopicChannel();
            var sub = new EventSubscriber((msg, ct) => Task.CompletedTask);
            
            tc.AddSubscriber(sub);
            
            Assert.AreEqual(1, tc.GetSubscribers().Count);
        }
        
        [TestMethod]
        public void TopicChannel_ShouldRemoveSubscriber()
        {
            var tc = new TopicChannel();
            var sub = new EventSubscriber((msg, ct) => Task.CompletedTask);
            
            tc.AddSubscriber(sub);
            tc.RemoveSubscriber(sub.Id);
            
            Assert.AreEqual(0, tc.GetSubscribers().Count);
        }
    }
}