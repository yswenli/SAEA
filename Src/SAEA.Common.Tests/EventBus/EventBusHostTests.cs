using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.EventBus;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Common.Tests.EventBus
{
    /// <summary>
    /// 校验事件总线宿主对主题通道的调度与启停
    /// </summary>
    [TestClass]
    public class EventBusHostTests
    {
        /// <summary>
        /// 验证构造后实例非空且未启动时 IsRunning 为 false
        /// </summary>
        [TestMethod]
        public void EventBusHost_ShouldInitializeCorrectly()
        {
            var topics = new Dictionary<string, TopicChannel>();
            var host = new EventBusHost(topics);
            
            Assert.IsNotNull(host);
            Assert.IsFalse(host.IsRunning);
        }
        
        /// <summary>
        /// 验证启动后 IsRunning 为 true，按超时停止后恢复为 false
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
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
        
        /// <summary>
        /// 验证向主题发布消息后宿主把消息派发给已注册的订阅者
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
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