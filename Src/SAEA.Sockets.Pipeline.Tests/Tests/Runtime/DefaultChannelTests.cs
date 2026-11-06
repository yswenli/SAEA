using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Runtime
{
    [TestClass]
    public class DefaultChannelTests
    {
        [TestMethod]
        public void SetGetRemove_RoundTripsAndReportsMissing()
        {
            var channel = new DefaultChannel("ch-crud", null);

            Assert.AreEqual("ch-crud", channel.Id);
            Assert.IsTrue(channel.IsActive);
            Assert.IsNull(channel.GetAttribute("absent"));

            channel.SetAttribute("auth", true);
            Assert.AreEqual(true, channel.GetAttribute("auth"));
            Assert.AreEqual(1, channel.Attributes.Count);

            channel.SetAttribute("auth", false);

            object removed;
            Assert.IsTrue(channel.TryRemoveAttribute("auth", out removed));
            Assert.AreEqual(false, removed);
            Assert.IsFalse(channel.TryRemoveAttribute("auth", out removed));
            Assert.AreEqual(0, channel.Attributes.Count);
        }

        [TestMethod]
        public void ConcurrentWrites_SixteenDistinctKeys_AllPreserved()
        {
            var channel = new DefaultChannel("ch-parallel", null);

            Parallel.For(0, 16, i => channel.SetAttribute("key" + i, i));

            Assert.AreEqual(16, channel.Attributes.Count);
            for (var i = 0; i < 16; i++)
            {
                Assert.AreEqual(i, channel.GetAttribute("key" + i), "键 key" + i + " 被并发写入覆盖。");
            }
        }

        [TestMethod]
        public async Task CloseAsync_SetsIsActiveFalse_AndForwardsReason()
        {
            string capturedReason = null;
            var channel = new DefaultChannel("ch-close", reason =>
            {
                capturedReason = reason;
                return Task.CompletedTask;
            });

            channel.SetAttribute("auth", true);

            await channel.CloseAsync("client-reset");

            Assert.AreEqual("client-reset", capturedReason);
            Assert.IsFalse(channel.IsActive);
        }

        [TestMethod]
        public async Task CloseAsync_WithoutDelegate_StillMarksInactive()
        {
            var channel = new DefaultChannel("ch-null-close", null);

            await channel.CloseAsync("noop");

            Assert.IsFalse(channel.IsActive);
        }

        [TestMethod]
        public async Task CloseAsync_Repeated_InvokesDelegateOnce()
        {
            var calls = 0;
            var channel = new DefaultChannel("ch-idempotent", reason =>
            {
                Interlocked.Increment(ref calls);
                return Task.CompletedTask;
            });

            await channel.CloseAsync("first");
            await channel.CloseAsync("second");
            await channel.CloseAsync("third");

            Assert.AreEqual(1, Volatile.Read(ref calls));
            Assert.IsFalse(channel.IsActive);
        }

        [TestMethod]
        public async Task CloseAsync_Concurrent_InvokesDelegateOnce()
        {
            var calls = 0;
            var channel = new DefaultChannel("ch-concurrent-close", reason =>
            {
                Interlocked.Increment(ref calls);
                return Task.CompletedTask;
            });

            var closes = new Task[16];
            for (var i = 0; i < closes.Length; i++)
            {
                closes[i] = channel.CloseAsync("concurrent-" + i);
            }

            await Task.WhenAll(closes);

            Assert.AreEqual(1, Volatile.Read(ref calls));
            Assert.IsFalse(channel.IsActive);
        }

        [TestMethod]
        public void MarkInactive_DeactivatesAndClearsAttributes()
        {
            var channel = new DefaultChannel("ch-mark", null);
            channel.SetAttribute("auth", true);
            channel.SetAttribute("session", 42);

            channel.MarkInactive();

            Assert.IsFalse(channel.IsActive);
            Assert.AreEqual(0, channel.Attributes.Count);
            Assert.IsNull(channel.GetAttribute("auth"));
        }

        [TestMethod]
        public void MarkInactive_Repeated_StaysInactiveAndEmpty()
        {
            var channel = new DefaultChannel("ch-mark-twice", null);
            channel.SetAttribute("auth", true);

            channel.MarkInactive();
            channel.MarkInactive();

            Assert.IsFalse(channel.IsActive);
            Assert.AreEqual(0, channel.Attributes.Count);
        }

        [TestMethod]
        public async Task MarkInactive_AfterCloseAsync_DoesNotInvokeDelegateAgain()
        {
            var calls = 0;
            var channel = new DefaultChannel("ch-close-then-mark", reason =>
            {
                Interlocked.Increment(ref calls);
                return Task.CompletedTask;
            });
            channel.SetAttribute("auth", true);

            await channel.CloseAsync("shutdown");
            Assert.AreEqual(1, channel.Attributes.Count, "CloseAsync 只进入收口，不清空属性。");

            channel.MarkInactive();

            Assert.AreEqual(1, Volatile.Read(ref calls));
            Assert.IsFalse(channel.IsActive);
            Assert.AreEqual(0, channel.Attributes.Count);
        }

        [TestMethod]
        public void Dispose_ClearsAttributesWithoutClosing()
        {
            var calls = 0;
            var channel = new DefaultChannel("ch-dispose", reason =>
            {
                Interlocked.Increment(ref calls);
                return Task.CompletedTask;
            });
            channel.SetAttribute("auth", true);

            channel.Dispose();

            Assert.AreEqual(0, channel.Attributes.Count);
            Assert.AreEqual(0, Volatile.Read(ref calls), "Dispose 不触发关闭委托。");
            Assert.IsTrue(channel.IsActive, "Dispose 不改变活跃状态。");
        }
    }
}