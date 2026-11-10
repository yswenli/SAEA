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
*命名空间：SAEA.Sockets.Pipeline.Tests.Tests.Handlers
*文件名： OutboundSerializingHandlerTests
*版本号： v26.11.5.1
*唯一标识：44f92ee4-093a-4142-bca7-1a7b1a1c2511
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/9 12:10:00
*描述：出站串行器的 FIFO、待写预算与摘链清理用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/9 12:10:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：出站串行器的 FIFO、待写预算与摘链清理用例
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Handlers
{
    /// <summary>
    /// 出站串行器：异步编码器下的 FIFO、并发写不交错、待写项数预算与摘链清理。
    /// </summary>
    [TestClass]
    public class OutboundSerializingHandlerTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 异步编码器内随机让出线程时，单线程发起的写仍按发起顺序到达编码器
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Serializer_AsyncEncoder_MaintainsFifo()
        {
            const int count = 16;
            var stream = new RecordingRawByteStream("serializer-fifo");

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            using (var executor = NewExecutor(pipeline, stream))
            {
                var encoder = new RecordingAsyncEncoder(3);
                pipeline.AddLast(encoder);
                pipeline.AddLast(new OutboundSerializingHandler());

                var writes = new List<Task>(count);
                for (var i = 0; i < count; i++)
                    writes.Add(pipeline.WriteAsync(new TransportFrame(new byte[] { (byte)i }, null)));

                await Task.WhenAll(writes).WaitAsync(WaitTimeout);

                var expected = new int[count];
                for (var i = 0; i < count; i++)
                    expected[i] = i;

                CollectionAssert.AreEqual(expected, encoder.Order.ToArray(), "异步编码器下出站顺序不是 FIFO");
                Assert.AreEqual(count, stream.SentCount, "发送帧数不符");
                Assert.AreEqual(0, pipeline.PendingWriteCount, "写完成后待写计数未归零");
            }
        }

        /// <summary>
        /// 多线程并发业务写时编码器同一时刻只执行一个，且总帧数不丢不重
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Serializer_ConcurrentBusinessWrites_NoInterleaving()
        {
            const int writers = 8;
            const int perWriter = 10;
            var stream = new RecordingRawByteStream("serializer-concurrent");

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            using (var executor = NewExecutor(pipeline, stream))
            {
                var encoder = new RecordingAsyncEncoder(2);
                pipeline.AddLast(encoder);
                pipeline.AddLast(new OutboundSerializingHandler());

                var workers = new List<Task>(writers);
                for (var writer = 0; writer < writers; writer++)
                {
                    var writerId = writer;
                    workers.Add(Task.Run(async () =>
                    {
                        for (var i = 0; i < perWriter; i++)
                            await pipeline.WriteAsync(new TransportFrame(new byte[] { (byte)writerId, (byte)i }, null));
                    }));
                }

                await Task.WhenAll(workers).WaitAsync(WaitTimeout);

                Assert.AreEqual(1, encoder.MaxConcurrent, "串行器允许两个编码器调用同时执行");
                Assert.AreEqual(writers * perWriter, encoder.Calls, "编码器收到的写次数不符");
                Assert.AreEqual(writers * perWriter, stream.SentCount, "并发写丢失或重复发送");
                Assert.AreEqual(0, pipeline.PendingWriteCount, "并发写完成后待写计数未归零");
            }
        }

        /// <summary>
        /// 待写项数达到上限后新写直接失败，已入队的两项仍正常交接
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Serializer_ExceedsMaxPendingWrites_FailsNewWrite()
        {
            var stream = new RecordingRawByteStream("serializer-budget");

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            using (var executor = NewExecutor(pipeline, stream))
            {
                var encoder = new GatedEncoder();
                pipeline.AddLast(encoder);
                pipeline.AddLast(new OutboundSerializingHandler(2));

                var first = pipeline.WriteAsync(new TransportFrame(new byte[] { 1 }, null));
                await encoder.Entered.Task.WaitAsync(WaitTimeout);

                var second = pipeline.WriteAsync(new TransportFrame(new byte[] { 2 }, null));

                var rejected = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => pipeline.WriteAsync(new TransportFrame(new byte[] { 3 }, null)));
                StringAssert.Contains(rejected.Message, "MaxPendingWrites", "超限拒绝原因不是待写预算");

                encoder.Release.TrySetResult(true);
                await first.WaitAsync(WaitTimeout);
                await second.WaitAsync(WaitTimeout);

                Assert.AreEqual(2, stream.SentCount, "已入队的写未全部交接");
                Assert.AreEqual(0, pipeline.PendingWriteCount, "超限拒绝后待写计数未归零");
            }
        }

        /// <summary>
        /// 仍有排队项时摘链：排队项禁止移交并按未移交补偿原始帧，已移交项不提前释放消息
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Serializer_RemovedWhileQueued_FailsWithoutHandoff()
        {
            var stream = new RecordingRawByteStream("serializer-removed-queued");

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            using (var executor = NewExecutor(pipeline, stream))
            {
                var encoder = new GatedEncoder();
                pipeline.AddLast(encoder);
                var serializer = new OutboundSerializingHandler();
                pipeline.AddLast(serializer);

                var activeOwner = new CountingOwner();
                var queuedOwner = new CountingOwner();

                var active = pipeline.WriteAsync(new RawFrame(new byte[] { 1 }, activeOwner));
                await encoder.Entered.Task.WaitAsync(WaitTimeout);
                var queued = pipeline.WriteAsync(new RawFrame(new byte[] { 2 }, queuedOwner));

                pipeline.Remove(serializer);

                await Assert.ThrowsExceptionAsync<ChannelClosedException>(() => queued).WaitAsync(WaitTimeout);
                await Assert.ThrowsExceptionAsync<ChannelClosedException>(() => active).WaitAsync(WaitTimeout);

                Assert.AreEqual(1, queuedOwner.DisposeCount, "排队项摘链时未按未移交补偿原始帧");
                Assert.AreEqual(0, activeOwner.DisposeCount, "已移交项被串行器提前释放");

                encoder.Release.TrySetResult(true);
                await encoder.Finished.Task.WaitAsync(WaitTimeout);

                Assert.AreEqual(1, encoder.Received, "已摘链的排队项仍被交给下游");
                Assert.AreEqual(0, activeOwner.DisposeCount, "已移交原始帧被串行器重复补偿");
                Assert.AreEqual(0, pipeline.PendingWriteCount, "摘链后待写计数未归零");
            }
        }

        /// <summary>
        /// 写者正持有已移交项时摘链：只让公共任务失败，消息仍由下游完成且迟完成不重复减计数
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Serializer_RemovedWhileActiveHandedOff_DoesNotDoubleComplete()
        {
            var stream = new RecordingRawByteStream("serializer-removed-active");

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            using (var executor = NewExecutor(pipeline, stream))
            {
                var encoder = new GatedEncoder();
                pipeline.AddLast(encoder);
                var serializer = new OutboundSerializingHandler();
                pipeline.AddLast(serializer);

                var owner = new CountingOwner();
                var write = pipeline.WriteAsync(new RawFrame(new byte[] { 1 }, owner));
                await encoder.Entered.Task.WaitAsync(WaitTimeout);

                pipeline.Remove(serializer);

                await Assert.ThrowsExceptionAsync<ChannelClosedException>(() => write).WaitAsync(WaitTimeout);
                Assert.AreEqual(0, owner.DisposeCount, "已移交项摘链时被提前释放");
                Assert.AreEqual(0, pipeline.PendingWriteCount, "摘链后待写计数未归零");

                encoder.Release.TrySetResult(true);
                await encoder.Finished.Task.WaitAsync(WaitTimeout);

                Assert.AreEqual(1, stream.SentCount, "已移交消息未由下游完成交接");
                Assert.AreEqual(0, owner.DisposeCount, "迟完成时对已移交原始帧重复释放");
                Assert.AreEqual(0, pipeline.PendingWriteCount, "迟完成时重复减计数");
            }
        }

        /// <summary>
        /// 收口超时后已移交写仍等下游真实结束；迟完成只落地一次，原始帧只补偿一次
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Serializer_TimeoutRace_LateCompletionDoesNotDecrementTwice()
        {
            var options = NewOptions();
            options.DrainTimeout = TimeSpan.FromMilliseconds(50);
            var stream = new RecordingRawByteStream("serializer-timeout-race");

            using (var pipeline = ChannelPipeline.Create(options))
            using (var executor = NewExecutor(pipeline, stream))
            {
                var encoder = new GatedEncoder();
                pipeline.AddLast(encoder);
                pipeline.AddLast(new OutboundSerializingHandler());

                var owner = new CountingOwner();
                var write = pipeline.WriteAsync(new RawFrame(new byte[] { 1 }, owner));
                await encoder.Entered.Task.WaitAsync(WaitTimeout);

                var close = pipeline.CloseAfterWriteAsync("timeout-race");
                await close.WaitAsync(WaitTimeout);
                await executor.Completion.WaitAsync(WaitTimeout);

                await Assert.ThrowsExceptionAsync<ChannelClosedException>(() => write).WaitAsync(WaitTimeout);
                Assert.AreEqual(0, owner.DisposeCount, "已移交项被收口超时提前释放");
                Assert.AreEqual(0, pipeline.PendingWriteCount, "收口后待写计数未归零");

                encoder.Release.TrySetResult(true);
                await encoder.Finished.Task.WaitAsync(WaitTimeout);

                Assert.AreEqual(1, owner.DisposeCount, "收口后原始帧未补偿或重复补偿");
                Assert.AreEqual(0, pipeline.PendingWriteCount, "迟完成时重复减计数");
            }
        }

        /// <summary>
        /// 前一个写失败只完成自己，后续写仍按顺序继续并成功交接
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Serializer_OneWriteFails_DoesNotBlockFollowingWrites()
        {
            var stream = new RecordingRawByteStream("serializer-one-fails");

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            using (var executor = NewExecutor(pipeline, stream))
            {
                pipeline.AddLast(new FailFirstEncoder());
                pipeline.AddLast(new OutboundSerializingHandler());

                var failing = pipeline.WriteAsync(new TransportFrame(new byte[] { 1 }, null));
                var following = pipeline.WriteAsync(new TransportFrame(new byte[] { 2 }, null));

                var thrown = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => failing).WaitAsync(WaitTimeout);
                StringAssert.Contains(thrown.Message, "encoder-failed", "失败写未冒泡编码器异常");

                await following.WaitAsync(WaitTimeout);

                Assert.AreEqual(1, stream.SentCount, "失败写阻断了后续写");
                CollectionAssert.AreEqual(new byte[] { 2 }, stream.SentFrames[0], "后续写内容不符");
                Assert.AreEqual(0, pipeline.PendingWriteCount, "失败后待写计数未归零");
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
                MaxPendingWrites = 0,
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

        private static int FirstByte(object msg)
        {
            return ((TransportFrame)msg).Data.Span[0];
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

        /// <summary>异步编码器：让出线程后记录到达顺序，并检测同一时刻是否有两个调用重叠。</summary>
        private sealed class RecordingAsyncEncoder : OutboundHandlerAdapter
        {
            private readonly int _yieldCount;
            private readonly List<int> _order = new List<int>();
            private int _active;
            private int _maxConcurrent;
            private int _calls;

            public RecordingAsyncEncoder(int yieldCount)
            {
                _yieldCount = yieldCount;
            }

            public List<int> Order
            {
                get
                {
                    lock (_order)
                    {
                        return new List<int>(_order);
                    }
                }
            }

            public int MaxConcurrent
            {
                get { return Volatile.Read(ref _maxConcurrent); }
            }

            public int Calls
            {
                get { return Volatile.Read(ref _calls); }
            }

            public override async Task WriteAsync(IChannelHandlerContext ctx, object msg)
            {
                var current = Interlocked.Increment(ref _active);
                var observed = Volatile.Read(ref _maxConcurrent);
                while (current > observed
                       && Interlocked.CompareExchange(ref _maxConcurrent, current, observed) != observed)
                {
                    observed = Volatile.Read(ref _maxConcurrent);
                }

                try
                {
                    for (var i = 0; i < _yieldCount; i++)
                        await Task.Yield();

                    lock (_order)
                    {
                        _order.Add(FirstByte(msg));
                    }

                    Interlocked.Increment(ref _calls);
                }
                finally
                {
                    Interlocked.Decrement(ref _active);
                }

                await ctx.WriteAsync(msg).ConfigureAwait(false);
            }
        }

        /// <summary>受闸门控制的编码器：进入后等待放行，再交给链头。</summary>
        private sealed class GatedEncoder : OutboundHandlerAdapter
        {
            public readonly TaskCompletionSource<bool> Entered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public readonly TaskCompletionSource<bool> Release =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public readonly TaskCompletionSource<bool> Finished =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private int _received;

            public int Received
            {
                get { return Volatile.Read(ref _received); }
            }

            public override async Task WriteAsync(IChannelHandlerContext ctx, object msg)
            {
                Interlocked.Increment(ref _received);
                Entered.TrySetResult(true);
                await Release.Task.ConfigureAwait(false);

                try
                {
                    await ctx.WriteAsync(msg).ConfigureAwait(false);
                }
                finally
                {
                    Finished.TrySetResult(true);
                }
            }
        }

        /// <summary>首字节为 1 时抛错，其余透传；用于验证单写失败不阻断后续。</summary>
        private sealed class FailFirstEncoder : OutboundHandlerAdapter
        {
            public override Task WriteAsync(IChannelHandlerContext ctx, object msg)
            {
                if (FirstByte(msg) == 1)
                    throw new InvalidOperationException("encoder-failed");

                return ctx.WriteAsync(msg);
            }
        }
    }
}