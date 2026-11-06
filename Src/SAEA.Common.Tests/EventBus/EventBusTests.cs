using global::SAEA.Common.EventBus;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SAEA.Common.Tests
{
    /// <summary>
    /// 校验事件总线的注册、订阅发布、失败重试与并发发布
    /// </summary>
    [TestClass]
    public class EventBusTests
    {
        /// <summary>
        /// 每个用例前释放总线单例，避免上个用例的订阅与状态残留
        /// </summary>
        [TestInitialize]
        public void TestInitialize()
        {
            global::SAEA.Common.EventBus.EventBus.Dispose();
        }

        /// <summary>
        /// 每个用例后再次释放总线单例，回收后台调度资源
        /// </summary>
        [TestCleanup]
        public void TestCleanup()
        {
            global::SAEA.Common.EventBus.EventBus.Dispose();
        }

        /// <summary>
        /// 验证未调用 Start 前 Enabled 为 false
        /// </summary>
        [TestMethod]
        public void EventBus_ShouldNotBeEnabledBeforeStart()
        {
            Assert.IsFalse(global::SAEA.Common.EventBus.EventBus.Enabled);
        }

        /// <summary>
        /// 验证注册主题并启动后 Enabled 变为 true
        /// </summary>
        [TestMethod]
        public void EventBus_ShouldRegisterTopic()
        {
            global::SAEA.Common.EventBus.EventBus.RegisterTopic("test.topic", 100);
            global::SAEA.Common.EventBus.EventBus.Start();
            Assert.IsTrue(global::SAEA.Common.EventBus.EventBus.Enabled);
        }

        /// <summary>
        /// 验证订阅后向同主题发布消息可触发一次订阅回调
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task EventBus_ShouldPublishAndSubscribe()
        {
            var received = false;
            var subId = global::SAEA.Common.EventBus.EventBus.Subscribe("test.topic", async msg =>
            {
                received = true;
            }, retryCount: 0);

            global::SAEA.Common.EventBus.EventBus.Start();

            global::SAEA.Common.EventBus.EventBus.Publish("test.topic", new byte[] { 1, 2, 3 });

            await Task.Delay(100);

            Assert.IsTrue(received);

            global::SAEA.Common.EventBus.EventBus.Unsubscribe("test.topic", subId);
        }

        /// <summary>
        /// 验证同主题的两个订阅者都能收到同一条消息
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task EventBus_ShouldSupportMultipleSubscribers()
        {
            var count = 0;
            var sub1 = global::SAEA.Common.EventBus.EventBus.Subscribe("test.multi", async msg => count++);
            var sub2 = global::SAEA.Common.EventBus.EventBus.Subscribe("test.multi", async msg => count++);

            global::SAEA.Common.EventBus.EventBus.Start();

            global::SAEA.Common.EventBus.EventBus.Publish("test.multi", new byte[] { 1 });

            await Task.Delay(100);

            Assert.AreEqual(2, count);

            global::SAEA.Common.EventBus.EventBus.Unsubscribe("test.multi", sub1);
            global::SAEA.Common.EventBus.EventBus.Unsubscribe("test.multi", sub2);
        }

        /// <summary>
        /// 验证据重试次数 3，订阅回调抛出异常时会重试至累计执行 3 次
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task EventBus_ShouldRetryOnFailure()
        {
            var attempts = 0;
            var subId = global::SAEA.Common.EventBus.EventBus.Subscribe("test.retry", async msg =>
            {
                attempts++;
                if (attempts < 3) throw new Exception("retry");
            }, retryCount: 3);

            global::SAEA.Common.EventBus.EventBus.Start();

            global::SAEA.Common.EventBus.EventBus.Publish("test.retry", new byte[] { 1 });

            await Task.Delay(500);

            Assert.AreEqual(3, attempts);

            global::SAEA.Common.EventBus.EventBus.Unsubscribe("test.retry", subId);
        }

        /// <summary>
        /// 验证 100 个并发发布全部被同一订阅者收到，计数不丢失
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task EventBus_ShouldHandleConcurrentPublish()
        {
            var count = 0;
            var subId = global::SAEA.Common.EventBus.EventBus.Subscribe("test.concurrent", async msg =>
            {
                count++;
            }, retryCount: 0);

            global::SAEA.Common.EventBus.EventBus.Start();

            var tasks = new System.Collections.Generic.List<Task>();
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(() => global::SAEA.Common.EventBus.EventBus.Publish("test.concurrent", new byte[] { (byte)i })));
            }

            await Task.WhenAll(tasks);
            await Task.Delay(200);

            Assert.AreEqual(100, count);

            global::SAEA.Common.EventBus.EventBus.Unsubscribe("test.concurrent", subId);
        }
    }
}