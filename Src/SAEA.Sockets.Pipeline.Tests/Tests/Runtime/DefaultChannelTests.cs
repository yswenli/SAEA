/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.Sockets.Pipeline.Tests.Tests.Runtime
*文件名： DefaultChannelTests
*版本号： v26.11.5.1
*唯一标识：3362d191-908d-4809-bad0-dbc233d73783
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：DefaultChannelTests
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：DefaultChannelTests
*
*****************************************************************************/
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Runtime
{
    /// <summary>
    /// 连接视图：连接属性的并发读写、失活清理与释放语义。
    /// </summary>
    [TestClass]
    public class DefaultChannelTests
    {
        /// <summary>
        /// 属性写入、覆盖、读取与移除往返正确，读取缺失键返回 null 且重复移除返回 false
        /// </summary>
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

        /// <summary>
        /// 16 个不同键并发写入后全部保留且各自值未被覆盖
        /// </summary>
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

        /// <summary>
        /// CloseAsync 把关闭原因透传给关闭委托并把连接标记为非活跃
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
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

        /// <summary>
        /// 未提供关闭委托时 CloseAsync 仍把连接标记为非活跃
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task CloseAsync_WithoutDelegate_StillMarksInactive()
        {
            var channel = new DefaultChannel("ch-null-close", null);

            await channel.CloseAsync("noop");

            Assert.IsFalse(channel.IsActive);
        }

        /// <summary>
        /// 串行重复调用 CloseAsync 只触发一次关闭委托，连接保持非活跃
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
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

        /// <summary>
        /// 16 路并发 CloseAsync 仍只触发一次关闭委托
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
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

        /// <summary>
        /// MarkInactive 把连接置为非活跃并清空全部连接属性
        /// </summary>
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

        /// <summary>
        /// 重复调用 MarkInactive 保持非活跃且属性仍为空
        /// </summary>
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

        /// <summary>
        /// CloseAsync 只进入收口不清属性，随后 MarkInactive 才清空属性且不再触发关闭委托
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
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

        /// <summary>
        /// Dispose 只清空连接属性，不触发关闭委托也不改变活跃状态
        /// </summary>
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