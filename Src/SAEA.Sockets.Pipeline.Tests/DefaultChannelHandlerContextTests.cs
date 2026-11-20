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
*文件名： DefaultChannelHandlerContextTests
*版本号： net10.0
*唯一标识：3f42bc00-fe44-49a5-b237-33812c37b7bf
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/20 14:30:00
*描述：调用视图复位、失效与复用边界的用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/20 14:30:00
*修改人： yswenli
*版本号： net10.0
*描述：调用视图复位、失效与复用边界的用例
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SAEA.Sockets.Pipeline.Tests
{
    /// <summary>
    /// 调用视图用例：经真实通道链覆盖视图复位（同一节点连续两次写）、失效后继续使用、
    /// 以及复用后旧引用只能指向本次调用。下游只装配记录器、不接传输层，避免用例依赖端口与执行器。
    /// </summary>
    [TestClass]
    public class DefaultChannelHandlerContextTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 同一节点的视图会被复位后复用：连续两次写必须各自转发一次。
        /// 复位漏掉「已转发」标记时第二次会被判重复转发，因此再断言两次拿到的是同一实例，
        /// 否则本用例没有真正覆盖复位路径。
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task WriteAsync_同节点连续两次写_各转发一次且复用同一视图()
        {
            using (var pipeline = NewPipeline())
            {
                var sink = new RecordingOutboundHandler();
                var forwarder = new CapturingForwardOutboundHandler();
                pipeline.AddLast(sink);
                pipeline.AddLast(forwarder);

                await pipeline.WriteAsync("m1").WaitAsync(WaitTimeout);
                await pipeline.WriteAsync("m2").WaitAsync(WaitTimeout);

                CollectionAssert.AreEqual(
                    new List<object> { "m1", "m2" }, sink.Received(), "复用视图时每次写都必须各转发一次，不能丢也不能重。");

                Assert.IsNotNull(forwarder.FirstContext);
                Assert.AreSame(forwarder.FirstContext, forwarder.SecondContext, "连续两次写应复用同一个视图，本用例才覆盖了复位路径。");
            }
        }

        /// <summary>
        /// 视图失效后必须整体不可用：处理器把视图存下来、回调返回后再写，只能报「已失效」，
        /// 不能落成「重复转发」或「未绑定操作」等其他判定。
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task WriteAsync_出站视图失效后继续写_抛InvalidOperationException()
        {
            using (var pipeline = NewPipeline())
            {
                var sink = new RecordingOutboundHandler();
                var capturing = new CapturingOutboundHandler();
                pipeline.AddLast(sink);
                pipeline.AddLast(capturing);

                await pipeline.WriteAsync("m1").WaitAsync(WaitTimeout);

                var stale = capturing.Context;
                Assert.IsNotNull(stale, "处理器回调内必须拿到视图。");

                var error = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => stale.WriteAsync("m2")).ConfigureAwait(false);
                StringAssert.Contains(error.Message, "已失效", "失效视图必须按已失效报错。");
            }
        }

        /// <summary>
        /// 入站视图同样在回调结束后失效：旧引用继续向下游转发必须抛 <see cref="InvalidOperationException"/>。
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task ChannelReadAsync_入站视图失效后继续转发_抛InvalidOperationException()
        {
            using (var pipeline = NewPipeline())
            {
                var capturing = new CapturingInboundHandler();
                pipeline.AddLast(capturing);

                await pipeline.FireChannelReadAsync("m1").WaitAsync(WaitTimeout);

                var stale = capturing.Context;
                Assert.IsNotNull(stale, "处理器回调内必须拿到视图。");

                var error = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => stale.FireChannelReadAsync("m2")).ConfigureAwait(false);
                StringAssert.Contains(error.Message, "已失效", "失效视图必须按已失效报错。");
            }
        }

        /// <summary>
        /// 复用不会把上一条消息带给下一次调用：第二次派发时用上一次的视图转发新消息，
        /// 结果要么是旧引用未被复用而抛「已失效」，要么只把显式传入的 "x" 转发下去；
        /// 唯一不允许的是把 "first"/"second" 带到下游，因此两种结果都算通过。
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task ChannelReadAsync_复用视图的旧引用_只转发本次显式传入的消息()
        {
            using (var pipeline = NewPipeline())
            {
                var recorder = new RecordingInboundHandler();
                var capture = new ReuseInboundHandler();
                pipeline.AddLast(capture);
                pipeline.AddLast(recorder);

                // 第一次派发只保存视图，不向下游转发，避免 recorder 收到 "first"。
                await pipeline.FireChannelReadAsync("first").WaitAsync(WaitTimeout);

                // 第二次派发在回调内用上一次的引用转发 "x"：视图已被复位时按本次绑定派生，
                // 未被复位时旧引用已失效并抛异常；两种结果都不允许出现旧消息。
                capture.ForwardFromPrevious = true;
                await pipeline.FireChannelReadAsync("second").WaitAsync(WaitTimeout);

                Assert.IsTrue(
                    capture.ForwardedFromPrevious ^ capture.PreviousRejected,
                    "旧引用要么成功按本次绑定转发，要么已失效抛错，不能两者都不发生。");

                CollectionAssert.AreEqual(
                    new List<object> { "x" }, recorder.Received(), "旧引用只能转发显式传入的消息，不得复用上一条消息的绑定。");
            }
        }

        /// <summary>创建一条不接传输层的通道链；业务写预算放开，由各用例自行控制下游。</summary>
        /// <returns>已通过结构校验的空链</returns>
        private static ChannelPipeline NewPipeline()
        {
            return ChannelPipeline.Create(new ChannelPipelineOptions
            {
                Splitter = new TakeAvailableSplitter(64 * 1024),
                QueueCapacity = 8,
                MaxConcurrentMessages = 1,
                MaxPendingWrites = 0
            });
        }

        /// <summary>下游记录器：只记录收到的消息，不再向链头转发，因此用例不需要挂传输层。</summary>
        private sealed class RecordingOutboundHandler : OutboundHandlerAdapter
        {
            private readonly object _gate = new object();
            private readonly List<object> _messages = new List<object>();

            /// <summary>取已记录消息的快照。</summary>
            /// <returns>按到达顺序排列的消息副本</returns>
            internal List<object> Received()
            {
                lock (_gate)
                {
                    return new List<object>(_messages);
                }
            }

            /// <summary>记录消息，不再向下游转发。</summary>
            /// <param name="ctx">当前节点上下文</param>
            /// <param name="msg">待发送消息</param>
            /// <returns>已完成的任务</returns>
            public override Task WriteAsync(IChannelHandlerContext ctx, object msg)
            {
                lock (_gate)
                {
                    _messages.Add(msg);
                }

                return Task.CompletedTask;
            }
        }

        /// <summary>记录两次调用拿到的视图，并把消息原样转给下游。</summary>
        private sealed class CapturingForwardOutboundHandler : OutboundHandlerAdapter
        {
            private int _calls;

            /// <summary>第一次调用拿到的视图。</summary>
            internal IChannelHandlerContext FirstContext { get; private set; }

            /// <summary>第二次调用拿到的视图。</summary>
            internal IChannelHandlerContext SecondContext { get; private set; }

            /// <summary>记录视图后转发消息。</summary>
            /// <param name="ctx">当前节点上下文</param>
            /// <param name="msg">待发送消息</param>
            /// <returns>下游写出完成后结束的任务</returns>
            public override Task WriteAsync(IChannelHandlerContext ctx, object msg)
            {
                if (_calls++ == 0)
                    FirstContext = ctx;
                else
                    SecondContext = ctx;

                return ctx.WriteAsync(msg);
            }
        }

        /// <summary>只保存视图、不向下游转发，用于验证回调结束后视图失效。</summary>
        private sealed class CapturingOutboundHandler : OutboundHandlerAdapter
        {
            /// <summary>回调内拿到的视图。</summary>
            internal IChannelHandlerContext Context { get; private set; }

            /// <summary>保存视图后直接返回，消息停在本节点。</summary>
            /// <param name="ctx">当前节点上下文</param>
            /// <param name="msg">待发送消息</param>
            /// <returns>已完成的任务</returns>
            public override Task WriteAsync(IChannelHandlerContext ctx, object msg)
            {
                Context = ctx;
                return Task.CompletedTask;
            }
        }

        /// <summary>只保存视图、不向下游转发，用于验证入站视图回调结束后失效。</summary>
        private sealed class CapturingInboundHandler : InboundHandlerAdapter
        {
            /// <summary>回调内拿到的视图。</summary>
            internal IChannelHandlerContext Context { get; private set; }

            /// <summary>保存视图后直接返回，消息停在本节点。</summary>
            /// <param name="ctx">当前节点上下文</param>
            /// <param name="msg">待处理消息</param>
            /// <returns>已完成的任务</returns>
            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                Context = ctx;
                return Task.CompletedTask;
            }
        }

        /// <summary>记录收到的入站消息，不再向下游转发。</summary>
        private sealed class RecordingInboundHandler : InboundHandlerAdapter
        {
            private readonly object _gate = new object();
            private readonly List<object> _messages = new List<object>();

            /// <summary>取已记录消息的快照。</summary>
            /// <returns>按到达顺序排列的消息副本</returns>
            internal List<object> Received()
            {
                lock (_gate)
                {
                    return new List<object>(_messages);
                }
            }

            /// <summary>记录消息，不再向下游转发。</summary>
            /// <param name="ctx">当前节点上下文</param>
            /// <param name="msg">待处理消息</param>
            /// <returns>已完成的任务</returns>
            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                lock (_gate)
                {
                    _messages.Add(msg);
                }

                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// 保存上一次的视图，并在下一次派发中用它转发 "x"：
        /// 用于验证视图复用后旧引用只能按本次绑定工作，不会带上一条消息。
        /// </summary>
        private sealed class ReuseInboundHandler : InboundHandlerAdapter
        {
            private IChannelHandlerContext _previous;

            /// <summary>是否在下一次回调内用上一次的视图转发。</summary>
            internal bool ForwardFromPrevious { get; set; }

            /// <summary>旧引用成功按本次绑定转发。</summary>
            internal bool ForwardedFromPrevious { get; private set; }

            /// <summary>旧引用已失效并抛错。</summary>
            internal bool PreviousRejected { get; private set; }

            /// <summary>保存本次视图；按需用上一次的视图转发 "x"，不转发本次消息。</summary>
            /// <param name="ctx">当前节点上下文</param>
            /// <param name="msg">待处理消息</param>
            /// <returns>转发完成后结束的任务</returns>
            public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var previous = _previous;
                _previous = ctx;

                // 不转发本次消息：下游只可能收到旧引用显式转发的 "x"，断言才能区分是否带上了旧消息。
                if (!ForwardFromPrevious || previous == null)
                    return;

                try
                {
                    await previous.FireChannelReadAsync("x").ConfigureAwait(false);
                    ForwardedFromPrevious = true;
                }
                catch (InvalidOperationException)
                {
                    // 旧引用未被复用而失效属于允许的结果，另一种允许结果是只转发 "x"。
                    PreviousRejected = true;
                }
            }
        }
    }
}