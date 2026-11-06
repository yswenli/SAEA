using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.EventBus;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Common.Tests.EventBus
{
    /// <summary>
    /// 校验主题通道的收发与订阅者增删
    /// </summary>
    [TestClass]
    public class TopicChannelTests
    {
        /// <summary>
        /// 验证按指定容量构造主题通道后实例非空
        /// </summary>
        [TestMethod]
        public void TopicChannel_ShouldInitializeCorrectly()
        {
            var tc = new TopicChannel(capacity: 100);
            
            Assert.IsNotNull(tc);
        }
        
        /// <summary>
        /// 验证写入通道的消息能被完整读出且为同一实例
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task TopicChannel_ShouldPublishAndRead()
        {
            var tc = new TopicChannel(capacity: 10);
            var msg = new EventMessage("test", new byte[] { 1, 2, 3 });
            
            await tc.PublishAsync(msg);
            
            var readMsg = await tc.ReadAsync(CancellationToken.None);
            Assert.AreEqual(msg, readMsg);
        }
        
        /// <summary>
        /// 验证添加订阅者后查询到的订阅者数量为 1
        /// </summary>
        [TestMethod]
        public void TopicChannel_ShouldAddSubscriber()
        {
            var tc = new TopicChannel();
            var sub = new EventSubscriber((msg, ct) => Task.CompletedTask);
            
            tc.AddSubscriber(sub);
            
            Assert.AreEqual(1, tc.GetSubscribers().Count);
        }
        
        /// <summary>
        /// 验证按订阅者 Id 移除后查询到的订阅者数量为 0
        /// </summary>
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