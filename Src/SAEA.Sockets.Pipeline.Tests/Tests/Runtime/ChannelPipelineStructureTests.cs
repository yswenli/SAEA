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
*文件名： ChannelPipelineStructureTests
*版本号： v26.11.5.1
*唯一标识：b6a6ff54-867e-4f8a-bff1-a4aa028b43ed
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：ChannelPipelineStructureTests
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：ChannelPipelineStructureTests
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Runtime
{
    /// <summary>
    /// 通道链结构：入站/出站遍历方向、handler 增删顺序约束与链头交接。
    /// </summary>
    [TestClass]
    public class ChannelPipelineStructureTests
    {
        /// <summary>
        /// 入站读取按挂链顺序从链首遍历到链尾
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task FireChannelReadAsync_WalksInboundFromHeadToTail()
        {
            var log = new List<string>();
            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                pipeline.AddLast(new RecordingInboundHandler("first", log));
                pipeline.AddLast(new RecordingInboundHandler("second", log));
                pipeline.AddLast(new RecordingInboundHandler("third", log));

                await pipeline.FireChannelReadAsync(new object());

                CollectionAssert.AreEqual(new[] { "first", "second", "third" }, log);
            }
        }

        /// <summary>
        /// 入站处理器发起写出时，出站链从链尾向链首反向遍历（距链头最近的最后一个执行）
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task WriteAsync_WalksOutboundFromTailToHead()
        {
            var log = new List<string>();
            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                pipeline.AddLast(new RecordingOutboundHandler("head-most", log, forward: false));
                pipeline.AddLast(new RecordingOutboundHandler("inner", log, forward: true));
                pipeline.AddLast(new WriteOnReadInboundHandler(new object()));

                await pipeline.FireChannelReadAsync(new object());

                CollectionAssert.AreEqual(new[] { "inner", "head-most" }, log);
            }
        }

        /// <summary>
        /// 已挂入站处理器后再 Append 出站处理器抛 InvalidOperationException（出站必须集中在链首前缀）
        /// </summary>
        [TestMethod]
        public void AddLast_OutboundAfterInbound_Throws()
        {
            var log = new List<string>();
            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                pipeline.AddLast(new RecordingInboundHandler("in", log));

                Assert.ThrowsException<InvalidOperationException>(
                    () => pipeline.AddLast(new RecordingOutboundHandler("out", log)));
            }
        }

        /// <summary>
        /// 链中已有出站前缀时 AddFirst 入站处理器抛 InvalidOperationException，空链上 AddFirst 则接受并置于首位
        /// </summary>
        [TestMethod]
        public void AddFirst_InboundBeforeOutboundPrefix_ThrowsOtherwiseAccepted()
        {
            var log = new List<string>();

            using (var withPrefix = ChannelPipeline.Create(NewOptions()))
            {
                withPrefix.AddLast(new RecordingOutboundHandler("out", log));
                Assert.ThrowsException<InvalidOperationException>(
                    () => withPrefix.AddFirst(new RecordingInboundHandler("in", log)));
            }

            using (var emptyChain = ChannelPipeline.Create(NewOptions()))
            {
                var inbound = new RecordingInboundHandler("in", log);
                emptyChain.AddFirst(inbound);

                Assert.AreEqual(1, emptyChain.Handlers.Count);
                Assert.AreSame(inbound, emptyChain.Handlers[0]);
            }
        }

        /// <summary>
        /// 移除占用前缀位置的入站处理器后可再挂出站处理器，链上最终只保留两个出站节点
        /// </summary>
        [TestMethod]
        public void Remove_ThenAddLast_RestoresOutboundPrefixState()
        {
            var log = new List<string>();
            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                var firstInbound = new RecordingInboundHandler("in-1", log);
                var outbound = new RecordingOutboundHandler("out-1", log);
                var secondInbound = new RecordingInboundHandler("in-2", log);
                var secondOutbound = new RecordingOutboundHandler("out-2", log);

                pipeline.AddLast(firstInbound);
                Assert.ThrowsException<InvalidOperationException>(() => pipeline.AddLast(outbound));

                pipeline.Remove(firstInbound);
                pipeline.AddLast(outbound);

                pipeline.AddLast(secondInbound);
                Assert.ThrowsException<InvalidOperationException>(() => pipeline.AddLast(secondOutbound));

                pipeline.Remove(secondInbound);
                pipeline.AddLast(secondOutbound);

                CollectionAssert.AreEqual(
                    new IChannelHandler[] { outbound, secondOutbound },
                    pipeline.Handlers.ToArray());
            }
        }

        /// <summary>
        /// 非共享 handler 实例挂到第二条链时抛 InvalidOperationException，从第一条链移除后才能重新挂载
        /// </summary>
        [TestMethod]
        public void NonSharableHandler_ReuseOnSecondChain_ThrowsUntilRemoved()
        {
            var log = new List<string>();
            var handler = new RecordingInboundHandler("shared", log);
            var first = ChannelPipeline.Create(NewOptions());
            var second = ChannelPipeline.Create(NewOptions());

            using (first)
            using (second)
            {
                first.AddLast(handler);

                Assert.ThrowsException<InvalidOperationException>(() => second.AddLast(handler));

                first.Remove(handler);
                second.AddLast(handler);

                Assert.AreSame(handler, second.Handlers[0]);
            }
        }

        /// <summary>
        /// 入站处理器抛出普通异常时不向调用方抛出，而是转为异常回调交给链上后续节点
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task InboundHandlerFailure_IsReportedAsExceptionCaught_WithoutThrowing()
        {
            var recorder = new ExceptionRecorderHandler();
            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                pipeline.AddLast(new ThrowingInboundHandler(new InvalidOperationException("boom")));
                pipeline.AddLast(recorder);

                await pipeline.FireChannelReadAsync(new object());

                Assert.IsNotNull(recorder.Captured);
                Assert.AreEqual("boom", recorder.Captured.Message);
            }
        }

        /// <summary>
        /// 入站处理器抛 OperationCanceledException 时异常直接传播给调用方
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task InboundCancellation_PropagatesToCaller()
        {
            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                pipeline.AddLast(new ThrowingInboundHandler(new OperationCanceledException()));

                await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                    () => pipeline.FireChannelReadAsync(new object()));
            }
        }

        /// <summary>
        /// 链头收到非 TransportFrame 消息时以 InvalidOperationException 拒绝，错误信息包含 TransportFrame
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task HeadSink_RejectsNonTransportFrame()
        {
            var probe = new WriteProbeInboundHandler("not-a-frame");
            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                pipeline.AddLast(probe);

                await pipeline.FireChannelReadAsync(new object());

                Assert.IsInstanceOfType(probe.Captured, typeof(InvalidOperationException));
                StringAssert.Contains(probe.Captured.Message, "TransportFrame");
            }
        }

        /// <summary>
        /// 未 Attach 到传输层时写帧抛 InvalidOperationException，且不提前释放帧的 owner
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task HeadSink_WithoutAttach_Throws()
        {
            var owner = new TrackingOwner();
            var probe = new WriteProbeInboundHandler(new TransportFrame(ReadOnlyMemory<byte>.Empty, owner));
            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                pipeline.AddLast(probe);

                await pipeline.FireChannelReadAsync(new object());

                Assert.IsInstanceOfType(probe.Captured, typeof(InvalidOperationException));
                Assert.IsFalse(owner.Disposed);
            }
        }

        /// <summary>
        /// Attach 后写帧按原字节交给传输层，且 owner 不在此处释放（所有权交给传输层）
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task HeadSink_AfterAttach_SendsFrameAndLeavesOwnerToTransport()
        {
            var stream = new TestRawByteStream();
            var channel = new DefaultChannel("ch-head-sink", null);
            var owner = new TrackingOwner();
            var frame = new TransportFrame(new byte[] { 1, 2, 3 }, owner);
            var probe = new WriteProbeInboundHandler(frame);

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                var executor = new ChannelPipelineExecutor(stream, channel, NewOptions(), CancellationToken.None);
                executor.Attach(pipeline);
                pipeline.AddLast(probe);

                await pipeline.FireChannelReadAsync(new object());

                Assert.IsNull(probe.Captured);
                Assert.AreEqual(1, stream.Sent.Count);
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, stream.Sent[0].Data.ToArray());
                Assert.IsFalse(owner.Disposed);
            }
        }

        /// <summary>
        /// 连接收口后旧 ctx 写出即使类型非法也按 ChannelClosedException 报错，且不产生发送
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task HeadSink_AfterClose_ReportsClosedBeforeRejectingType()
        {
            var stream = new TestRawByteStream();
            var channel = new DefaultChannel("ch-closed", null);
            var probe = new CtxCapturingInboundHandler();

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                var executor = new ChannelPipelineExecutor(stream, channel, NewOptions(), CancellationToken.None);
                executor.Attach(pipeline);
                pipeline.AddLast(probe);

                await pipeline.FireChannelReadAsync(new object());

                // 摘链后按钮旧 ctx 仍可被业务持有；再让连接进入收口。
                pipeline.Remove(probe);
                await pipeline.CloseAsync("test-close");

                // 收口判定必须优先于类型判定：此时写出非 TransportFrame 也只能报关闭。
                await Assert.ThrowsExceptionAsync<ChannelClosedException>(
                    () => probe.Context.WriteAsync(new object()));

                await executor.Completion;
                Assert.AreEqual(0, stream.Sent.Count);
            }
        }

        private static ChannelPipelineOptions NewOptions()
        {
            return new ChannelPipelineOptions
            {
                Splitter = LengthFieldSplitter.Default(1024)
            };
        }

        private sealed class RecordingInboundHandler : InboundHandlerAdapter
        {
            private readonly string _name;
            private readonly List<string> _log;

            public RecordingInboundHandler(string name, List<string> log)
            {
                _name = name;
                _log = log;
            }

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                _log.Add(_name);
                return ctx.FireChannelReadAsync(msg);
            }
        }

        private sealed class RecordingOutboundHandler : OutboundHandlerAdapter
        {
            private readonly string _name;
            private readonly List<string> _log;
            private readonly bool _forward;

            public RecordingOutboundHandler(string name, List<string> log, bool forward = true)
            {
                _name = name;
                _log = log;
                _forward = forward;
            }

            public override Task WriteAsync(IChannelHandlerContext ctx, object msg)
            {
                _log.Add(_name);
                return _forward ? ctx.WriteAsync(msg) : Task.CompletedTask;
            }
        }

        private sealed class WriteOnReadInboundHandler : InboundHandlerAdapter
        {
            private readonly object _message;

            public WriteOnReadInboundHandler(object message)
            {
                _message = message;
            }

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                return ctx.WriteAsync(_message);
            }
        }

        private sealed class WriteProbeInboundHandler : InboundHandlerAdapter
        {
            private readonly object _message;

            public WriteProbeInboundHandler(object message)
            {
                _message = message;
            }

            public Exception Captured { get; private set; }

            public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                try
                {
                    await ctx.WriteAsync(_message);
                }
                catch (Exception ex)
                {
                    Captured = ex;
                }
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

        private sealed class ThrowingInboundHandler : InboundHandlerAdapter
        {
            private readonly Exception _exception;

            public ThrowingInboundHandler(Exception exception)
            {
                _exception = exception;
            }

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                throw _exception;
            }
        }

        private sealed class ExceptionRecorderHandler : InboundHandlerAdapter
        {
            public Exception Captured { get; private set; }

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                return Task.CompletedTask;
            }

            public override Task ChannelExceptionCaughtAsync(IChannelHandlerContext ctx, Exception ex)
            {
                Captured = ex;
                return Task.CompletedTask;
            }
        }

        private sealed class TrackingOwner : IDisposable
        {
            public bool Disposed { get; private set; }

            public void Dispose()
            {
                Disposed = true;
            }
        }

        private sealed class TestRawByteStream : IRawByteStream
        {
            public string ChannelId
            {
                get { return "test-channel"; }
            }

            public PipeReader Input
            {
                get { return null; }
            }

            public List<SentFrame> Sent { get; } = new List<SentFrame>();

            public Task SendAsync(ReadOnlyMemory<byte> data, IDisposable owner)
            {
                Sent.Add(new SentFrame(data, owner));
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

        private sealed class SentFrame
        {
            public SentFrame(ReadOnlyMemory<byte> data, IDisposable owner)
            {
                Data = data;
                Owner = owner;
            }

            public ReadOnlyMemory<byte> Data { get; }

            public IDisposable Owner { get; }
        }
    }
}