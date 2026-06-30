using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.EventBus;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Common.Tests.EventBus
{
    [TestClass]
    public class EventBusHostTests
    {
        [TestMethod]
        public void EventBusHost_ShouldInitializeCorrectly()
        {
            var topics = new Dictionary<string, TopicChannel>();
            var host = new EventBusHost(topics);
            
            Assert.IsNotNull(host);
            Assert.IsFalse(host.IsRunning);
        }
        
        [TestMethod]
        public async Task EventBusHost_ShouldStartAndStop()
        {
            var topics = new Dictionary<string, TopicChannel>();
            var host = new EventBusHost(topics);
            
            host.Start();
            Assert.IsTrue(host.IsRunning);
            
            await host.StopAsync(TimeSpan.FromSeconds(1));
            Assert.IsFalse(host.IsRunning);
        }
        
        [TestMethod]
        public async Task EventBusHost_ShouldDispatchMessageToSubscribers()
        {
            var tc = new TopicChannel();
            var received = 0;
            var sub = new EventSubscriber(async (msg, ct) => 
            {
                Interlocked.Increment(ref received);
            }, retryCount: 0);
            
            tc.AddSubscriber(sub);
            
            var topics = new Dictionary<string, TopicChannel> { ["test"] = tc };
            var host = new EventBusHost(topics);
            
            host.Start();
            
            await tc.PublishAsync(new EventMessage("test", new byte[] { 1 }));
            
            await Task.Delay(200);
            
            Assert.AreEqual(1, received);
            
            await host.StopAsync();
        }
    }
}