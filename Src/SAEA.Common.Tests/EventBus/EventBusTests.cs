using global::SAEA.Common.EventBus;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SAEA.Common.Tests
{
    [TestClass]
    public class EventBusTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            global::SAEA.Common.EventBus.EventBus.Dispose();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            global::SAEA.Common.EventBus.EventBus.Dispose();
        }

        [TestMethod]
        public void EventBus_ShouldNotBeEnabledBeforeStart()
        {
            Assert.IsFalse(global::SAEA.Common.EventBus.EventBus.Enabled);
        }

        [TestMethod]
        public void EventBus_ShouldRegisterTopic()
        {
            global::SAEA.Common.EventBus.EventBus.RegisterTopic("test.topic", 100);
            global::SAEA.Common.EventBus.EventBus.Start();
            Assert.IsTrue(global::SAEA.Common.EventBus.EventBus.Enabled);
        }

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