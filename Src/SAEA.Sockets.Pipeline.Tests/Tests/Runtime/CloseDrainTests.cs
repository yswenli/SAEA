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
*文件名： CloseDrainTests
*版本号： v26.11.5.1
*唯一标识：e4f3ccee-dd50-4f19-b585-89a309c98323
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/9 11:35:49
*描述：业务写收口状态机与超时补偿用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/9 11:35:49
*修改人： yswenli
*版本号： v26.11.5.1
*描述：业务写收口状态机与超时补偿用例
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
    /// 收口状态机：先停止接纳再等待已提交写，超时只强制完成未移交项，重复关闭复用同一完成源。
    /// </summary>
    [TestClass]
    public class CloseDrainTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(2);

        /// <summary>
        /// CloseAfterWriteAsync 在已提交写完成前不返回，写完成后才进入 Closing
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task CloseAfterWriteAsync_WaitsSubmittedWrites()
        {
            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                var handler = new GatedSwallowOutboundHandler();
                pipeline.AddLast(handler);

                var write = pipeline.WriteAsync(new object());
                await handler.Entered.Task.WaitAsync(WaitTimeout);

                var close = pipeline.CloseAfterWriteAsync("drain");
                await Task.Delay(100);
                Assert.IsFalse(close.IsCompleted, "已提交写未完成时 CloseAfterWriteAsync 提前返回");

                handler.Release.TrySetResult(true);

                await write.WaitAsync(WaitTimeout);
                await close.WaitAsync(WaitTimeout);

                Assert.AreEqual(2, pipeline.CloseState, "等待结束后未进入 Closing");
                Assert.AreEqual(0, pipeline.PendingWriteCount, "收口完成后待写计数未归零");
            }
        }

        /// <summary>
        /// DrainTimeout 为零时按 5 秒默认值等待，慢写仍会被等到而不是立即收口
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task CloseAfterWriteAsync_DrainTimeoutZero_UsesFiveSeconds()
        {
            Assert.AreEqual(TimeSpan.FromSeconds(5), ChannelPipeline.CloseAfterWriteDefaultDrainTimeout,
                "零值默认等待上限不是 5 秒");

            var options = NewOptions();
            options.DrainTimeout = TimeSpan.Zero;

            using (var pipeline = ChannelPipeline.Create(options))
            {
                var handler = new GatedSwallowOutboundHandler();
                pipeline.AddLast(handler);

                var write = pipeline.WriteAsync(new object());
                await handler.Entered.Task.WaitAsync(WaitTimeout);

                var close = pipeline.CloseAfterWriteAsync("zero-timeout");
                await Task.Delay(100);
                Assert.IsFalse(close.IsCompleted, "零值 DrainTimeout 被当成立即超时");

                handler.Release.TrySetResult(true);

                await write.WaitAsync(WaitTimeout);
                await close.WaitAsync(WaitTimeout);
            }
        }

        /// <summary>
        /// 收口超时只强制完成仍处于 Queued 的项并补偿原始帧；已移交项不伪造完成、不释放，等下游真实结束
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task CloseAfterWriteAsync_Timeout_FailsQueuedOnly()
        {
            // 第一层：直接在操作层验证 Queued 与 HandedOff 的超时语义。
            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                var queuedOwner = new CountingOwner();
                var queued = new OutboundOperation(pipeline, new RawFrame(new byte[] { 1 }, queuedOwner));
                queued.MarkActive();

                Assert.AreEqual(OutboundOperation.Queued, queued.State, "构造后的操作不处于 Queued");
                Assert.IsTrue(queued.TryAbortQueued(new ChannelClosedException("timeout")), "Queued 项未被超时完成");
                Assert.IsTrue(queued.Task.IsFaulted, "Queued 项超时后公共任务未失败");
                Assert.AreEqual(1, queuedOwner.DisposeCount, "Queued 项超时未补偿原始帧");
                Assert.AreEqual(OutboundOperation.Completed, queued.State, "Queued 项超时后状态未落地");
                Assert.IsFalse(queued.TryComplete(null), "已完成的项仍允许再次完成");

                var handedOwner = new CountingOwner();
                var handed = new OutboundOperation(pipeline, new RawFrame(new byte[] { 2 }, handedOwner));
                handed.MarkActive();
                Assert.IsTrue(handed.TryMarkHandedOff(), "已移交项构造失败");

                Assert.IsFalse(handed.TryAbortQueued(new ChannelClosedException("timeout")), "已移交项被超时强制完成");
                Assert.IsFalse(handed.Task.IsCompleted, "已移交项被伪造完成");
                Assert.AreEqual(0, handedOwner.DisposeCount, "已移交项被超时提前释放原始帧");

                handed.ClearActive();
                Assert.IsTrue(handed.TryComplete(null), "已移交项真实完成后未落地");
                await handed.Task.WaitAsync(WaitTimeout);
                Assert.AreEqual(0, handedOwner.DisposeCount, "真实完成后由链释放了已移交原始帧");
                Assert.IsFalse(handed.TryComplete(null), "已移交项允许重复完成");
            }

            // 第二层：管线层超时后已移交写仍在等待下游，释放后才以真实结果结束。
            var timeoutOptions = NewOptions();
            timeoutOptions.DrainTimeout = TimeSpan.FromMilliseconds(50);

            using (var pipeline = ChannelPipeline.Create(timeoutOptions))
            {
                var handler = new GatedThrowingOutboundHandler();
                pipeline.AddLast(handler);

                var owner = new CountingOwner();
                var write = pipeline.WriteAsync(new RawFrame(new byte[] { 3 }, owner));
                await handler.Entered.Task.WaitAsync(WaitTimeout);

                var close = pipeline.CloseAfterWriteAsync("timeout");
                await close.WaitAsync(WaitTimeout);

                Assert.IsFalse(write.IsCompleted, "已移交写在超时后被伪造完成");
                Assert.AreEqual(0, owner.DisposeCount, "已移交写在超时后被提前释放");

                handler.Release.TrySetResult(true);

                await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => write);
                Assert.AreEqual(1, owner.DisposeCount, "下游失败后边界未补偿原始帧");
                Assert.AreEqual(0, pipeline.PendingWriteCount, "真实结束后待写计数未归零");
            }
        }

        /// <summary>
        /// 重复调用 CloseAfterWriteAsync 复用同一完成源，不产生第二个等待或关闭流程
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task CloseAfterWriteAsync_Twice_ReusesSameCompletion()
        {
            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                var handler = new GatedSwallowOutboundHandler();
                pipeline.AddLast(handler);

                var write = pipeline.WriteAsync(new object());
                await handler.Entered.Task.WaitAsync(WaitTimeout);

                var first = pipeline.CloseAfterWriteAsync("first");
                var second = pipeline.CloseAfterWriteAsync("second");

                Assert.AreSame(first, second, "重复调用未复用同一完成源");

                handler.Release.TrySetResult(true);

                await write.WaitAsync(WaitTimeout);
                await first.WaitAsync(WaitTimeout);
                await second.WaitAsync(WaitTimeout);
            }
        }

        /// <summary>
        /// draining 状态下普通 CloseAsync 加入现有等待而不是抢占，已提交写仍能正常完成
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Close_WhileDraining_JoinsInsteadOfCanceling()
        {
            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                var handler = new GatedSwallowOutboundHandler();
                pipeline.AddLast(handler);

                var write = pipeline.WriteAsync(new object());
                await handler.Entered.Task.WaitAsync(WaitTimeout);

                var closeAfterWrite = pipeline.CloseAfterWriteAsync("drain");
                var plainClose = pipeline.CloseAsync("join");

                Assert.AreSame(closeAfterWrite, plainClose, "普通关闭没有加入现有等待");
                Assert.IsFalse(plainClose.IsCompleted, "普通关闭在已提交写完成前抢占完成");

                handler.Release.TrySetResult(true);

                await write.WaitAsync(WaitTimeout);
                await closeAfterWrite.WaitAsync(WaitTimeout);
                await plainClose.WaitAsync(WaitTimeout);

                Assert.AreEqual(0, pipeline.PendingWriteCount, "收口完成后待写计数未归零");
            }
        }

        /// <summary>
        /// 进入收口后新的业务写被拒绝并抛 ChannelClosedException，不新增待写计数
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task WriteAsync_AfterCloseRequested_Rejected()
        {
            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                var handler = new GatedSwallowOutboundHandler();
                pipeline.AddLast(handler);

                var write = pipeline.WriteAsync(new object());
                await handler.Entered.Task.WaitAsync(WaitTimeout);

                var close = pipeline.CloseAfterWriteAsync("reject-new");
                Assert.AreEqual(1, pipeline.PendingWriteCount, "收口等待期间待写计数不符");

                await Assert.ThrowsExceptionAsync<ChannelClosedException>(
                    () => pipeline.WriteAsync(new object()));

                Assert.AreEqual(1, pipeline.PendingWriteCount, "被拒绝的写仍占用了待写额度");

                handler.Release.TrySetResult(true);
                await write.WaitAsync(WaitTimeout);
                await close.WaitAsync(WaitTimeout);
            }
        }

        /// <summary>
        /// 操作层状态语义：Queued（含 active-not-handed）可被超时完成并补偿；active-handed-off 不被超时完成，真实完成后只落地一次
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Outbound_Queued_ActiveNotHanded_ActiveHandedOff_RemovalSemantics()
        {
            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                var queuedOwner = new CountingOwner();
                var queued = new OutboundOperation(pipeline, new RawFrame(new byte[] { 10 }, queuedOwner));
                queued.MarkActive();

                Assert.IsTrue(queued.IsActive, "Queued 项未标记为 active");
                Assert.AreEqual(OutboundOperation.Queued, queued.State, "Queued 项状态不符");
                Assert.IsTrue(queued.TryAbortQueued(new ChannelClosedException("removed")),
                    "active-not-handed 的 Queued 项不能被移除完成");
                Assert.AreEqual(1, queuedOwner.DisposeCount, "Queued 项移除时未补偿原始帧");
                Assert.IsTrue(queued.Task.IsFaulted, "Queued 项移除后公共任务未失败");

                var handedOwner = new CountingOwner();
                var handed = new OutboundOperation(pipeline, new RawFrame(new byte[] { 11 }, handedOwner));
                handed.MarkActive();
                Assert.IsTrue(handed.TryMarkHandedOff(), "active 项未能移交");

                Assert.IsFalse(handed.TryAbortQueued(new ChannelClosedException("removed")),
                    "active-handed-off 项被移除完成");
                Assert.IsFalse(handed.Task.IsCompleted, "active-handed-off 项被伪造完成");
                Assert.AreEqual(0, handedOwner.DisposeCount, "active-handed-off 项被提前释放");

                handed.ClearActive();
                Assert.IsFalse(handed.IsActive, "active 标记未清除");
                Assert.IsTrue(handed.TryComplete(null), "active-handed-off 项真实完成后未落地");
                await handed.Task.WaitAsync(WaitTimeout);
                Assert.AreEqual(OutboundOperation.Completed, handed.State, "真实完成后状态不符");
                Assert.AreEqual(0, handedOwner.DisposeCount, "真实完成后链释放了已移交原始帧");
                Assert.IsFalse(handed.TryComplete(null), "真实完成后仍允许重复完成");
            }
        }

        // ---------- 测试基础设施 ----------

        private static ChannelPipelineOptions NewOptions()
        {
            return new ChannelPipelineOptions
            {
                Splitter = LengthFieldSplitter.Default(64 * 1024),
                QueueCapacity = 16,
                MaxConcurrentMessages = 1,
                DrainTimeout = TimeSpan.FromSeconds(5),
                Guard = NullChannelGuard.Instance
            };
        }

        private sealed class CountingOwner : IDisposable
        {
            private int _disposeCount;

            public int DisposeCount
            {
                get { return Volatile.Read(ref _disposeCount); }
            }

            public void Dispose()
            {
                Interlocked.Increment(ref _disposeCount);
            }
        }

        private sealed class GatedSwallowOutboundHandler : OutboundHandlerAdapter
        {
            public readonly TaskCompletionSource<bool> Entered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public readonly TaskCompletionSource<bool> Release =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public override async Task WriteAsync(IChannelHandlerContext ctx, object msg)
            {
                Entered.TrySetResult(true);
                await Release.Task.ConfigureAwait(false);
            }
        }

        private sealed class GatedThrowingOutboundHandler : OutboundHandlerAdapter
        {
            public readonly TaskCompletionSource<bool> Entered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public readonly TaskCompletionSource<bool> Release =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public override async Task WriteAsync(IChannelHandlerContext ctx, object msg)
            {
                Entered.TrySetResult(true);
                await Release.Task.ConfigureAwait(false);
                throw new InvalidOperationException("downstream-failed");
            }
        }
    }
}