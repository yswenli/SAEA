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
*文件名： OutboundWriteTests
*版本号： v26.11.5.1
*唯一标识：778d4e24-7771-42c1-929c-10e2540daaff
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/9 11:35:49
*描述：业务写入口与出站上下文转发用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/9 15:42:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补同步与异步出站节点的待写计数及 draining 中旧写跨节点转发用例
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Runtime
{
    /// <summary>
    /// 业务写入口：一次写只领取一个操作与一份计数，出站上下文只做链内转发。
    /// </summary>
    [TestClass]
    public class OutboundWriteTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(2);

        /// <summary>
        /// 单次业务写经出站链交接到传输层后公共任务完成，待写计数归零
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task WriteAsync_SingleWrite_CompletesAfterHandoff()
        {
            var stream = new RecordingRawByteStream("single-write");

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            using (var executor = NewExecutor(pipeline, stream))
            {
                pipeline.AddLast(new PassThroughOutboundHandler());

                await pipeline.WriteAsync(new TransportFrame(new byte[] { 1, 2, 3 }, null));

                Assert.AreEqual(1, stream.SentCount, "业务写未到达传输层");
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, stream.SentFrames[0], "发出的字节不一致");
                Assert.AreEqual(0, pipeline.PendingWriteCount, "写完成后待写计数未归零");
            }
        }

        /// <summary>
        /// 出站处理器抛错时公共任务只以该异常失败一次，待写计数同时归零
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task WriteAsync_HandlerThrows_CompletesOnceWithFailure()
        {
            var error = new InvalidOperationException("outbound-failed");

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                pipeline.AddLast(new ThrowingOutboundHandler(error));

                var thrown = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => pipeline.WriteAsync(new object()));

                Assert.AreSame(error, thrown, "业务写未以处理器抛出的同一异常失败");
                Assert.AreEqual(0, pipeline.PendingWriteCount, "失败后待写计数未归零");
            }
        }

        /// <summary>
        /// 自定义处理器吞掉原始帧时写正常完成，原始帧由边界补偿一次
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task WriteAsync_CustomHandlerSwallows_CompletesAndNoLeak()
        {
            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                pipeline.AddLast(new SwallowOutboundHandler());
                var owner = new CountingOwner();

                await pipeline.WriteAsync(new RawFrame(new byte[] { 4 }, owner));

                Assert.AreEqual(1, owner.DisposeCount, "吞掉的原始帧未补偿或重复补偿");
                Assert.AreEqual(0, pipeline.PendingWriteCount, "写完成后待写计数未归零");
            }
        }

        /// <summary>
        /// 写出站前连接已收口：原始帧补偿一次，公共任务以 ChannelClosedException 失败一次
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task WriteAsync_HeadClosedBeforeHandoff_FailsOnce()
        {
            var stream = new RecordingRawByteStream("head-closed");
            var handler = new GatedForwardOutboundHandler();

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            using (var executor = NewExecutor(pipeline, stream))
            {
                pipeline.AddLast(handler);
                var owner = new CountingOwner();
                var raw = new RawFrame(new byte[] { 5 }, owner);

                var write = pipeline.WriteAsync(raw);
                await handler.Entered.Task.WaitAsync(WaitTimeout);

                _ = executor.BeginShutdown("head-closed");
                handler.Release.TrySetResult(true);

                await Assert.ThrowsExceptionAsync<ChannelClosedException>(() => write);

                Assert.AreEqual(1, owner.DisposeCount, "链头关闭前未移交的原始帧补偿次数不符");
                Assert.AreEqual(0, stream.SentCount, "链头关闭后仍有数据交给传输层");
                Assert.AreEqual(0, pipeline.PendingWriteCount, "失败后待写计数未归零");
            }
        }

        /// <summary>
        /// 未接入传输层时业务写失败并补偿原始帧一次
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task WriteAsync_NotAttached_FailsAndDisposesRawFrame()
        {
            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                var owner = new CountingOwner();

                await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => pipeline.WriteAsync(new RawFrame(new byte[] { 6 }, owner)));

                Assert.AreEqual(1, owner.DisposeCount, "未接入传输层时原始帧补偿次数不符");
                Assert.AreEqual(0, pipeline.PendingWriteCount, "失败后待写计数未归零");
            }
        }

        /// <summary>
        /// 出站回调之外调用 ctx.WriteAsync 抛 InvalidOperationException
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task CtxWriteAsync_OutsideOutboundCallback_Throws()
        {
            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                var probe = new CtxCapturingInboundHandler();
                pipeline.AddLast(probe);

                await pipeline.FireChannelReadAsync(new object());

                Assert.IsNotNull(probe.Context, "未捕获到入站上下文");
                Assert.ThrowsException<InvalidOperationException>(
                    () => probe.Context.WriteAsync(new object()));
            }
        }

        /// <summary>
        /// 出站回调内 ctx.WriteAsync 沿同一操作向链首转发，两个节点看到同一笔待写且只产生一帧
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task CtxWriteAsync_InsideOutboundCallback_ForwardsSameOperation()
        {
            var stream = new RecordingRawByteStream("same-operation");

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            using (var executor = NewExecutor(pipeline, stream))
            {
                var headMost = new PendingObservingOutboundHandler(pipeline);
                var inner = new PendingObservingOutboundHandler(pipeline);
                pipeline.AddLast(headMost);
                pipeline.AddLast(inner);

                await pipeline.WriteAsync(new TransportFrame(new byte[] { 7 }, null));

                Assert.AreEqual(1, headMost.ObservedPending, "链首节点未看到同一笔待写");
                Assert.AreEqual(1, inner.ObservedPending, "链尾节点未看到同一笔待写");
                Assert.AreEqual(1, stream.SentCount, "链内转发产生了额外帧");
                Assert.AreEqual(0, pipeline.PendingWriteCount, "写完成后待写计数未归零");
            }
        }

        /// <summary>
        /// 同一出站节点对同一次写转发两次时第二次抛 InvalidOperationException，只产生一帧
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task CtxWriteAsync_RepeatedSameNode_Throws()
        {
            var stream = new RecordingRawByteStream("repeated-node");

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            using (var executor = NewExecutor(pipeline, stream))
            {
                pipeline.AddLast(new ForwardTwiceOutboundHandler());

                var thrown = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => pipeline.WriteAsync(new TransportFrame(new byte[] { 8 }, null)));

                StringAssert.Contains(thrown.Message, "重复转发", "重复转发未按预期拒绝");
                Assert.AreEqual(1, stream.SentCount, "重复转发产生了重复帧");
                Assert.AreEqual(0, pipeline.PendingWriteCount, "失败后待写计数未归零");
            }
        }

        /// <summary>
        /// 出站回调内调用 CloseAfterWriteAsync 抛 InvalidOperationException，不产生发送
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task CloseAfterWriteAsync_InsideOutboundCallback_Throws()
        {
            var stream = new RecordingRawByteStream("close-after-write-inside");

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            using (var executor = NewExecutor(pipeline, stream))
            {
                pipeline.AddLast(new CloseAfterWriteInsideOutboundHandler());

                var thrown = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => pipeline.WriteAsync(new TransportFrame(new byte[] { 9 }, null)));

                StringAssert.Contains(thrown.Message, "CloseAfterWriteAsync", "拒绝原因不是出站回调内自等");
                Assert.AreEqual(0, stream.SentCount, "拒绝路径不应产生发送");
                Assert.AreEqual(0, pipeline.PendingWriteCount, "失败后待写计数未归零");
            }
        }

        /// <summary>
        /// N 线程 × M 次并发业务写全部完成，发送数正确且待写计数只归零一次
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task WriteAsync_ConcurrentWrites_CountExactlyOnce()
        {
            const int writers = 8;
            const int perWriter = 25;
            var stream = new RecordingRawByteStream("concurrent-writes");

            var options = NewOptions();
            options.MaxPendingWrites = 0;

            using (var pipeline = ChannelPipeline.Create(options))
            using (var executor = NewExecutor(pipeline, stream))
            {
                pipeline.AddLast(new PassThroughOutboundHandler());

                var tasks = new List<Task>(writers * perWriter);
                for (var writer = 0; writer < writers; writer++)
                {
                    var writerId = writer;
                    tasks.Add(Task.Run(async () =>
                    {
                        for (var i = 0; i < perWriter; i++)
                        {
                            await pipeline.WriteAsync(
                                new TransportFrame(new byte[] { (byte)writerId, (byte)i }, null));
                        }
                    }));
                }

                await Task.WhenAll(tasks);

                Assert.AreEqual(writers * perWriter, stream.SentCount, "并发写丢失或重复发送");
                Assert.AreEqual(0, pipeline.PendingWriteCount, "并发写完成后待写计数未归零");
            }
        }

        /// <summary>
        /// 同步与异步出站处理器在同一笔写上各只看到一次待写计数，最终只产生一帧
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task WriteAsync_SyncAndAsyncHandlers_CountExactlyOnce()
        {
            var stream = new RecordingRawByteStream("sync-async-count");

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            using (var executor = NewExecutor(pipeline, stream))
            {
                var headMost = new SyncPendingObserver(pipeline);
                var inner = new AsyncPendingObserver(pipeline);
                pipeline.AddLast(headMost);
                pipeline.AddLast(inner);

                await pipeline.WriteAsync(new TransportFrame(new byte[] { 10 }, null));

                Assert.AreEqual(1, headMost.ObservedPending, "同步节点未看到本次写的待写计数");
                Assert.AreEqual(1, inner.ObservedPending, "异步节点未看到本次写的待写计数");
                Assert.AreEqual(1, stream.SentCount, "同步与异步节点协作产生了额外帧");
                Assert.AreEqual(0, pipeline.PendingWriteCount, "写完成后待写计数未归零");
            }
        }

        /// <summary>
        /// 已提交写进入 draining 后仍继续向剩余节点转发并完成发送，关闭等待其交接完成
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task WriteAsync_DrainingOldWrite_ForwardsAcrossRemainingHandlers()
        {
            var stream = new RecordingRawByteStream("draining-old-write");
            var gated = new GatedForwardOutboundHandler();

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            using (var executor = NewExecutor(pipeline, stream))
            {
                pipeline.AddLast(gated);
                pipeline.AddLast(new PassThroughOutboundHandler());

                var write = pipeline.WriteAsync(new TransportFrame(new byte[] { 11 }, null));
                await gated.Entered.Task.WaitAsync(WaitTimeout);

                var close = pipeline.CloseAfterWriteAsync("draining-old-write");
                Assert.AreEqual(1, pipeline.CloseState, "关闭请求未进入 draining 写");

                gated.Release.TrySetResult(true);

                await write;
                await close.WaitAsync(WaitTimeout);
                await executor.Completion.WaitAsync(WaitTimeout);

                Assert.AreEqual(1, stream.SentCount, "draining 中的旧写未完成转发");
                Assert.AreEqual(0, pipeline.PendingWriteCount, "写完成后待写计数未归零");
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
                Guard = NullChannelGuard.Instance
            };
        }

        private static ChannelPipelineExecutor NewExecutor(ChannelPipeline pipeline, IRawByteStream stream)
        {
            var executor = new ChannelPipelineExecutor(
                stream, new DefaultChannel(stream.ChannelId, null), NewOptions(), CancellationToken.None);
            executor.Attach(pipeline);
            return executor;
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

        private sealed class RecordingRawByteStream : IRawByteStream
        {
            private readonly List<byte[]> _sent = new List<byte[]>();

            public RecordingRawByteStream(string channelId)
            {
                ChannelId = channelId;
            }

            public string ChannelId { get; }

            public PipeReader Input
            {
                get { return null; }
            }

            public int SentCount
            {
                get
                {
                    lock (_sent)
                    {
                        return _sent.Count;
                    }
                }
            }

            public byte[][] SentFrames
            {
                get
                {
                    lock (_sent)
                    {
                        return _sent.ToArray();
                    }
                }
            }

            public Task SendAsync(ReadOnlyMemory<byte> data, IDisposable owner)
            {
                lock (_sent)
                {
                    _sent.Add(data.ToArray());
                }

                return Task.CompletedTask;
            }

            public void Pause()
            {
            }

            public void Resume()
            {
            }

            public Task CloseAsync(string reason)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class PassThroughOutboundHandler : OutboundHandlerAdapter
        {
            public override Task WriteAsync(IChannelHandlerContext ctx, object msg)
            {
                return ctx.WriteAsync(msg);
            }
        }

        private sealed class SwallowOutboundHandler : OutboundHandlerAdapter
        {
            public override Task WriteAsync(IChannelHandlerContext ctx, object msg)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class ThrowingOutboundHandler : OutboundHandlerAdapter
        {
            private readonly Exception _error;

            public ThrowingOutboundHandler(Exception error)
            {
                _error = error;
            }

            public override Task WriteAsync(IChannelHandlerContext ctx, object msg)
            {
                throw _error;
            }
        }

        private sealed class GatedForwardOutboundHandler : OutboundHandlerAdapter
        {
            public readonly TaskCompletionSource<bool> Entered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public readonly TaskCompletionSource<bool> Release =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public override async Task WriteAsync(IChannelHandlerContext ctx, object msg)
            {
                Entered.TrySetResult(true);
                await Release.Task.ConfigureAwait(false);
                await ctx.WriteAsync(msg).ConfigureAwait(false);
            }
        }

        private sealed class PendingObservingOutboundHandler : OutboundHandlerAdapter
        {
            private readonly ChannelPipeline _pipeline;
            private int _observedPending;

            public PendingObservingOutboundHandler(ChannelPipeline pipeline)
            {
                _pipeline = pipeline;
            }

            public int ObservedPending
            {
                get { return Volatile.Read(ref _observedPending); }
            }

            public override async Task WriteAsync(IChannelHandlerContext ctx, object msg)
            {
                Volatile.Write(ref _observedPending, _pipeline.PendingWriteCount);
                await ctx.WriteAsync(msg).ConfigureAwait(false);
            }
        }

        private sealed class SyncPendingObserver : OutboundHandlerAdapter
        {
            private readonly ChannelPipeline _pipeline;
            private int _observedPending;

            public SyncPendingObserver(ChannelPipeline pipeline)
            {
                _pipeline = pipeline;
            }

            public int ObservedPending
            {
                get { return Volatile.Read(ref _observedPending); }
            }

            public override Task WriteAsync(IChannelHandlerContext ctx, object msg)
            {
                Volatile.Write(ref _observedPending, _pipeline.PendingWriteCount);
                return ctx.WriteAsync(msg);
            }
        }

        private sealed class AsyncPendingObserver : OutboundHandlerAdapter
        {
            private readonly ChannelPipeline _pipeline;
            private int _observedPending;

            public AsyncPendingObserver(ChannelPipeline pipeline)
            {
                _pipeline = pipeline;
            }

            public int ObservedPending
            {
                get { return Volatile.Read(ref _observedPending); }
            }

            public override async Task WriteAsync(IChannelHandlerContext ctx, object msg)
            {
                Volatile.Write(ref _observedPending, _pipeline.PendingWriteCount);
                await ctx.WriteAsync(msg).ConfigureAwait(false);
            }
        }

        private sealed class ForwardTwiceOutboundHandler : OutboundHandlerAdapter
        {
            public override async Task WriteAsync(IChannelHandlerContext ctx, object msg)
            {
                await ctx.WriteAsync(msg).ConfigureAwait(false);
                await ctx.WriteAsync(msg).ConfigureAwait(false);
            }
        }

        private sealed class CloseAfterWriteInsideOutboundHandler : OutboundHandlerAdapter
        {
            public override Task WriteAsync(IChannelHandlerContext ctx, object msg)
            {
                return ctx.CloseAfterWriteAsync("inside-outbound");
            }
        }

        private sealed class CtxCapturingInboundHandler : InboundHandlerAdapter
        {
            public IChannelHandlerContext Context { get; private set; }

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                Context = ctx;
                return Task.CompletedTask;
            }
        }
    }
}