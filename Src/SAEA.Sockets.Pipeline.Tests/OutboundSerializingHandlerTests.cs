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
*命名空间：SAEA.Sockets.Pipeline.Tests
*文件名： OutboundSerializingHandlerTests
*版本号： net10.0
*唯一标识：137176a2-6623-4eb8-b325-f45fa954b5c3
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/20 10:12:00
*描述：出站写入串行器的顺序、并发、拒绝、摘链与重复转发用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/20 10:12:00
*修改人： yswenli
*版本号： net10.0
*描述：出站写入串行器的顺序、并发、拒绝、摘链与重复转发用例
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SAEA.Sockets.Pipeline.Tests
{
    /// <summary>
    /// 出站写入串行器用例：经真实通道链的业务写入口覆盖顺序、并发、待写上限拒绝、摘链清理
    /// 与同一节点重复转发。下游只装配记录器、不接传输层，避免用例依赖端口与执行器。
    /// </summary>
    [TestClass]
    public class OutboundSerializingHandlerTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 顺序等待的连续写是 keep-alive 连接的常态，串行器每次都处于空闲：
        /// 下游必须按调用顺序各收到一次，不能丢、不能重。
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task WriteAsync_顺序等待连续写_下游按调用顺序各收到一次()
        {
            using (var pipeline = NewPipeline())
            {
                var sink = new RecordingOutboundHandler();
                pipeline.AddLast(sink);
                pipeline.AddLast(new OutboundSerializingHandler());

                var expected = new List<string>();
                for (var i = 0; i < 20; i++)
                {
                    var message = "m" + i.ToString();
                    expected.Add(message);

                    // 每次写完再写下一条：串行器每次都回到空闲状态，这正是 keep-alive 连接的常态。
                    await pipeline.WriteAsync(message).WaitAsync(WaitTimeout);
                }

                CollectionAssert.AreEqual(expected, sink.Received());
            }
        }

        /// <summary>
        /// 多线程同时提交写：串行器只保证到达本节点后的 FIFO，不定义线程间先后，
        /// 因此断言集合一致而不是顺序一致，同时要求每个调用方任务都正常完成。
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task WriteAsync_并发提交_不丢消息不重复且全部完成()
        {
            using (var pipeline = NewPipeline())
            {
                var sink = new RecordingOutboundHandler();
                pipeline.AddLast(sink);
                pipeline.AddLast(new OutboundSerializingHandler(0));

                const int count = 50;
                var writes = new Task[count];
                for (var i = 0; i < count; i++)
                {
                    var message = "c" + i.ToString();
                    writes[i] = Task.Run(() => pipeline.WriteAsync(message));
                }

                await Task.WhenAll(writes).WaitAsync(WaitTimeout);

                Assert.AreEqual(count, sink.ReceivedCount, "下游收到的条数应等于提交条数。");
                CollectionAssert.AreEquivalent(ExpectedConcurrentMessages(count), sink.Received(), "并发写不能丢消息或重复调用。");

                for (var i = 0; i < writes.Length; i++)
                    Assert.AreEqual(TaskStatus.RanToCompletion, writes[i].Status, "每个并发写任务都应正常完成。");
            }
        }

        /// <summary>
        /// 已有写在途时提交超过待写上限的写：超出的写按上限拒绝并回抛，
        /// 其原始帧按未移交补偿释放，且因释放幂等只发生一次。
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task WriteAsync_超过待写上限_被拒绝且原始帧只释放一次()
        {
            using (var pipeline = NewPipeline())
            {
                var sink = new RecordingOutboundHandler { BlockFirstMessage = true };
                pipeline.AddLast(sink);
                pipeline.AddLast(new OutboundSerializingHandler(2));

                var first = pipeline.WriteAsync("first");
                var second = pipeline.WriteAsync("second");

                // 在途 1 项 + 排队 1 项已占满上限 2，本次写必须被拒绝。
                var owner = new CountingOwner();
                var rejected = pipeline.WriteAsync(new RawFrame(new byte[] { 1, 2, 3 }, owner));

                await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => rejected);
                Assert.AreEqual(1, owner.Count, "被拒绝的原始帧必须补偿释放，且只释放一次。");

                sink.Release();
                await first.WaitAsync(WaitTimeout);
                await second.WaitAsync(WaitTimeout);

                CollectionAssert.AreEqual(new List<object> { "first", "second" }, sink.Received());
            }
        }

        /// <summary>
        /// 摘链时仍在排队的写：公共任务以 <see cref="ChannelClosedException"/> 失败，
        /// 原始帧按未移交补偿一次，且这些消息不再交给下游；已移交项只让公共任务失败。
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task HandlerRemoved_仍有排队写_排队项以ChannelClosedException失败并补偿一次()
        {
            using (var pipeline = NewPipeline())
            {
                var sink = new RecordingOutboundHandler { BlockFirstMessage = true };
                var serializer = new OutboundSerializingHandler(0);
                pipeline.AddLast(sink);
                pipeline.AddLast(serializer);

                var active = pipeline.WriteAsync("active");

                var owners = new List<CountingOwner>();
                var queued = new List<Task>();
                for (var i = 0; i < 3; i++)
                {
                    var owner = new CountingOwner();
                    owners.Add(owner);
                    queued.Add(pipeline.WriteAsync(new RawFrame(new byte[] { (byte)i }, owner)));
                }

                pipeline.Remove(serializer);

                for (var i = 0; i < queued.Count; i++)
                    await Assert.ThrowsExceptionAsync<ChannelClosedException>(() => queued[i]);

                for (var i = 0; i < owners.Count; i++)
                    Assert.AreEqual(1, owners[i].Count, "排队项未移交，原始帧必须补偿释放且只释放一次。");

                Assert.AreEqual(1, sink.ReceivedCount, "排队中的消息不得再交给下游。");

                sink.Release();

                // 已移交项只让公共任务失败，消息仍在下游手里，不会重放也不再回调上游。
                await Assert.ThrowsExceptionAsync<ChannelClosedException>(() => active);
                Assert.AreEqual(1, sink.ReceivedCount, "已移交项的消息由下游收尾，不会被重放。");
            }
        }

        /// <summary>
        /// 同一节点的上下文视图只允许转发一次：第二次转发必须在产生重复帧之前被拒绝。
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task WriteAsync_同节点重复转发_抛InvalidOperationException()
        {
            using (var pipeline = NewPipeline())
            {
                var sink = new RecordingOutboundHandler();
                pipeline.AddLast(sink);
                pipeline.AddLast(new DoubleForwardOutboundHandler());

                await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => pipeline.WriteAsync("dup"));

                Assert.AreEqual(1, sink.ReceivedCount, "重复转发必须被拒绝，下游只应收到一条。");
            }
        }

        /// <summary>
        /// 空闲快写尚未结束时新写会进入队列：快写完成后必须接力排水，
        /// 否则排队项永远不会被处理。
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task WriteAsync_快写期间到达新写_排队项在快写结束后继续排水()
        {
            using (var pipeline = NewPipeline())
            {
                var sink = new RecordingOutboundHandler { BlockFirstMessage = true };
                pipeline.AddLast(sink);
                pipeline.AddLast(new OutboundSerializingHandler());

                var first = pipeline.WriteAsync("first");
                var second = pipeline.WriteAsync("second");

                sink.Release();

                await first.WaitAsync(WaitTimeout);
                await second.WaitAsync(WaitTimeout);

                CollectionAssert.AreEqual(new List<object> { "first", "second" }, sink.Received(), "快写结束后必须继续排水，不能丢掉排队项。");
            }
        }

        /// <summary>创建一条不接传输层的通道链；待写预算由各用例按需要控制。</summary>
        /// <returns>已通过结构校验的空链</returns>
        private static ChannelPipeline NewPipeline()
        {
            return ChannelPipeline.Create(new ChannelPipelineOptions
            {
                Splitter = new TakeAvailableSplitter(64 * 1024),
                QueueCapacity = 8,
                MaxConcurrentMessages = 1,
                // 用例不带执行器，业务写预算放开，由串行器自身的上限覆盖拒绝路径。
                MaxPendingWrites = 0
            });
        }

        /// <summary>构造并发用例期望收到的消息集合。</summary>
        /// <param name="count">消息条数</param>
        /// <returns>按序号生成的消息列表</returns>
        private static List<string> ExpectedConcurrentMessages(int count)
        {
            var expected = new List<string>(count);
            for (var i = 0; i < count; i++)
                expected.Add("c" + i.ToString());

            return expected;
        }

        /// <summary>
        /// 下游记录器：只记录收到的消息，不再向链头转发，因此用例不需要挂传输层。
        /// 可用 <see cref="BlockFirstMessage"/> 让首条消息挂起，制造「写者在途 + 后续排队」的场景。
        /// </summary>
        private sealed class RecordingOutboundHandler : OutboundHandlerAdapter
        {
            private readonly object _gate = new object();
            private readonly List<object> _messages = new List<object>();
            private TaskCompletionSource<bool> _firstMessageGate;

            /// <summary>是否让首条消息挂起直到 <see cref="Release"/> 被调用。</summary>
            internal bool BlockFirstMessage { get; set; }

            /// <summary>已记录的消息条数。</summary>
            internal int ReceivedCount
            {
                get
                {
                    lock (_gate)
                    {
                        return _messages.Count;
                    }
                }
            }

            /// <summary>取已记录消息的快照。</summary>
            /// <returns>按到达顺序排列的消息副本</returns>
            internal List<object> Received()
            {
                lock (_gate)
                {
                    return new List<object>(_messages);
                }
            }

            /// <summary>放行首条消息的挂起；未挂起时为无操作。</summary>
            internal void Release()
            {
                TaskCompletionSource<bool> gate;
                lock (_gate)
                {
                    gate = _firstMessageGate;
                }

                if (gate != null)
                    gate.TrySetResult(true);
            }

            /// <summary>记录消息，按需让首条挂起。</summary>
            /// <param name="ctx">当前节点上下文</param>
            /// <param name="msg">待发送消息</param>
            /// <returns>首条挂起时为未完成任务，其余为已完成任务</returns>
            public override Task WriteAsync(IChannelHandlerContext ctx, object msg)
            {
                Task blocked = null;

                lock (_gate)
                {
                    _messages.Add(msg);
                    if (BlockFirstMessage && _messages.Count == 1)
                    {
                        _firstMessageGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        blocked = _firstMessageGate.Task;
                    }
                }

                return blocked ?? Task.CompletedTask;
            }
        }

        /// <summary>对同一节点连续转发两次，用于验证重复转发保护。</summary>
        private sealed class DoubleForwardOutboundHandler : OutboundHandlerAdapter
        {
            /// <summary>先正常转发一次，再重复转发同一消息。</summary>
            /// <param name="ctx">当前节点上下文</param>
            /// <param name="msg">待发送消息</param>
            /// <returns>第二次转发被拒绝时以异常结束的任务</returns>
            public override async Task WriteAsync(IChannelHandlerContext ctx, object msg)
            {
                await ctx.WriteAsync(msg).ConfigureAwait(false);

                // 第二次转发必须由上下文视图拒绝，否则同一操作会产生重复帧。
                await ctx.WriteAsync(msg).ConfigureAwait(false);
            }
        }

        /// <summary>统计释放次数的背衬持有者。</summary>
        private sealed class CountingOwner : IDisposable
        {
            private int _count;

            /// <summary>累计释放次数。</summary>
            internal int Count
            {
                get { return Volatile.Read(ref _count); }
            }

            /// <summary>记录一次释放。</summary>
            public void Dispose()
            {
                Interlocked.Increment(ref _count);
            }
        }
    }
}
