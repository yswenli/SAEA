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
*文件名： LifecycleAndStartupTests
*版本号： v26.11.5.1
*唯一标识：ccb462b8-e1cd-4048-b37f-23f54f84f5d4
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/9 10:48:29
*描述：生命周期回调、执行器状态门、启动校验、链冻结与属性清理时序
*
*=====================================================================
*修改标记
*修改时间：2026/11/9 15:40:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补启动校验失败后的传输层关闭、登记摘除与属性清理用例
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
    /// 生命周期回调、执行器状态门、启动校验、链冻结与属性清理时序。
    /// </summary>
    [TestClass]
    public class LifecycleAndStartupTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(2);

        /// <summary>
        /// 未 Attach 就启动接收泵抛 InvalidOperationException
        /// </summary>
        [TestMethod]
        public void Executor_StartBeforeAttach_Throws()
        {
            var stream = new TestRawStream("start-before-attach");
            var channel = new DefaultChannel("start-before-attach", null);
            var executor = new ChannelPipelineExecutor(stream, channel, NewOptions(), CancellationToken.None);

            Assert.ThrowsException<InvalidOperationException>(() => executor.Start());
        }

        /// <summary>
        /// 进入收口后 Attach 抛 InvalidOperationException
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Executor_AttachAfterClosing_Throws()
        {
            var stream = new TestRawStream("attach-after-closing");
            var channel = new DefaultChannel("attach-after-closing", null);
            var executor = new ChannelPipelineExecutor(stream, channel, NewOptions(), CancellationToken.None);

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                await executor.BeginShutdown("test-close");

                Assert.ThrowsException<InvalidOperationException>(() => executor.Attach(pipeline));
            }

            await executor.Completion.WaitAsync(WaitTimeout);
        }

        /// <summary>
        /// 进入收口后 Start 抛 InvalidOperationException
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Executor_StartAfterClosing_Throws()
        {
            var stream = new TestRawStream("start-after-closing");
            var channel = new DefaultChannel("start-after-closing", null);
            var executor = new ChannelPipelineExecutor(stream, channel, NewOptions(), CancellationToken.None);

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                executor.Attach(pipeline);
                await executor.BeginShutdown("test-close");

                Assert.ThrowsException<InvalidOperationException>(() => executor.Start());
                await executor.Completion.WaitAsync(WaitTimeout);
            }
        }

        /// <summary>
        /// 启动后链已冻结，AddLast 抛 InvalidOperationException
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Pipeline_AddLastAfterStart_Throws()
        {
            var stream = new TestRawStream("freeze-add-last");
            var channel = new DefaultChannel("freeze-add-last", null);
            var options = NewOptions();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                var executor = new ChannelPipelineExecutor(stream, channel, options, CancellationToken.None);
                executor.Attach(pipeline);
                executor.Start();

                Assert.ThrowsException<InvalidOperationException>(
                    () => pipeline.AddLast(new RecordingInboundHandler()));

                await executor.BeginShutdown("test-done");
                await executor.Completion.WaitAsync(WaitTimeout);
            }
        }

        /// <summary>
        /// 启动后链已冻结，AddFirst 抛 InvalidOperationException
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Pipeline_AddFirstAfterStart_Throws()
        {
            var stream = new TestRawStream("freeze-add-first");
            var channel = new DefaultChannel("freeze-add-first", null);
            var options = NewOptions();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                var executor = new ChannelPipelineExecutor(stream, channel, options, CancellationToken.None);
                executor.Attach(pipeline);
                executor.Start();

                Assert.ThrowsException<InvalidOperationException>(
                    () => pipeline.AddFirst(new RecordingInboundHandler()));

                await executor.BeginShutdown("test-done");
                await executor.Completion.WaitAsync(WaitTimeout);
            }
        }

        /// <summary>
        /// 启动后链已冻结，Remove 抛 InvalidOperationException 且节点保留
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Pipeline_RemoveAfterStart_Throws()
        {
            var stream = new TestRawStream("freeze-remove");
            var channel = new DefaultChannel("freeze-remove", null);
            var options = NewOptions();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                var handler = new RecordingInboundHandler();
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(stream, channel, options, CancellationToken.None);
                executor.Attach(pipeline);
                executor.Start();

                Assert.ThrowsException<InvalidOperationException>(() => pipeline.Remove(handler));
                Assert.AreEqual(1, pipeline.Handlers.Count);

                await executor.BeginShutdown("test-done");
                await executor.Completion.WaitAsync(WaitTimeout);
            }
        }

        /// <summary>
        /// RequireTerminalHandler 为 true 时链尾缺少 ExceptionTerminalHandler，Start 失败并直接收口
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Executor_RequireTerminal_MissingTerminal_StartFails()
        {
            var options = NewOptions();
            options.RequireTerminalHandler = true;
            var stream = new TestRawStream("terminal-missing");
            var channel = new DefaultChannel("terminal-missing", null);

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new RecordingInboundHandler());

                var executor = new ChannelPipelineExecutor(stream, channel, options, CancellationToken.None);
                executor.Attach(pipeline);

                Assert.ThrowsException<InvalidOperationException>(() => executor.Start());
                await executor.Completion.WaitAsync(WaitTimeout);
            }
        }

        /// <summary>
        /// RequireTerminalHandler 为 true 时存在两个出站串行器，Start 失败
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Executor_RequireTerminal_TwoSerializers_StartFails()
        {
            var options = NewOptions();
            options.RequireTerminalHandler = true;
            var stream = new TestRawStream("two-serializers");
            var channel = new DefaultChannel("two-serializers", null);

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new OutboundSerializingHandler());
                pipeline.AddLast(new OutboundSerializingHandler());
                pipeline.AddLast(new ExceptionTerminalHandler());

                var executor = new ChannelPipelineExecutor(stream, channel, options, CancellationToken.None);
                executor.Attach(pipeline);

                Assert.ThrowsException<InvalidOperationException>(() => executor.Start());
                await executor.Completion.WaitAsync(WaitTimeout);
            }
        }

        /// <summary>
        /// RequireTerminalHandler 为 true 时串行器不在出站前缀最右侧，Start 失败
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Executor_RequireTerminal_SerializerNotRightmost_StartFails()
        {
            var options = NewOptions();
            options.RequireTerminalHandler = true;
            var stream = new TestRawStream("serializer-not-rightmost");
            var channel = new DefaultChannel("serializer-not-rightmost", null);

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new OutboundSerializingHandler());
                pipeline.AddLast(new NoopOutboundHandler());
                pipeline.AddLast(new ExceptionTerminalHandler());

                var executor = new ChannelPipelineExecutor(stream, channel, options, CancellationToken.None);
                executor.Attach(pipeline);

                Assert.ThrowsException<InvalidOperationException>(() => executor.Start());
                await executor.Completion.WaitAsync(WaitTimeout);
            }
        }

        /// <summary>
        /// RequireTerminalHandler 为 true 且装配正确时启动成功
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Executor_RequireTerminal_ValidAssembly_Starts()
        {
            var options = NewOptions();
            options.RequireTerminalHandler = true;
            var stream = new TestRawStream("terminal-valid");
            var channel = new DefaultChannel("terminal-valid", null);

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new OutboundSerializingHandler());
                pipeline.AddLast(new ExceptionTerminalHandler());

                var executor = new ChannelPipelineExecutor(stream, channel, options, CancellationToken.None);
                executor.Attach(pipeline);
                executor.Start();

                Assert.IsTrue(executor.IsActive);

                await executor.BeginShutdown("test-done");
                await executor.Completion.WaitAsync(WaitTimeout);
            }
        }

        /// <summary>
        /// 启动校验失败后收口：传输层按固定原因关闭、登记摘除一次、属性清空、连接转为非活跃
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Executor_StartValidationFails_ClosesStreamAndRemovesHost()
        {
            var options = NewOptions();
            options.RequireTerminalHandler = true;
            var stream = new TestRawStream("startup-validation-failed");
            var channel = new DefaultChannel("startup-validation-failed", null);
            var host = new RecordingPipelineHost();

            channel.SetAttribute("session", 7);

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new RecordingInboundHandler());

                var executor = new ChannelPipelineExecutor(
                    stream, channel, options, CancellationToken.None, host);
                executor.Attach(pipeline);

                Assert.ThrowsException<InvalidOperationException>(() => executor.Start());
                await executor.Completion.WaitAsync(WaitTimeout);

                Assert.AreEqual("startup-validation-failed", await stream.Closed.Task.WaitAsync(WaitTimeout),
                    "校验失败后未按固定原因关闭传输层");
                Assert.AreEqual(1, host.RemoveCount, "收口应只摘除一次登记");
                Assert.AreEqual("startup-validation-failed", host.RemovedId, "摘除登记时使用的连接标识不符");
                Assert.AreEqual(0, channel.Attributes.Count, "收口完成后连接属性未清空");
                Assert.IsFalse(executor.IsActive, "收口完成后连接不应仍为活跃");
            }
        }

        /// <summary>
        /// ChannelActiveAsync 先于 ChannelInactiveAsync 派发，两者各一次
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Executor_ChannelActiveThenInactive_DispatchedInOrder()
        {
            var stream = new TestRawStream("active-inactive-order");
            var channel = new DefaultChannel("active-inactive-order", null);
            var options = NewOptions();
            var handler = new LifecycleRecorderHandler();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(stream, channel, options, CancellationToken.None);
                executor.Attach(pipeline);
                executor.Start();

                await handler.Active.Task.WaitAsync(WaitTimeout);
                await executor.BeginShutdown("test-done");
                await executor.Completion.WaitAsync(WaitTimeout);

                CollectionAssert.AreEqual(new[] { "active", "inactive" }, handler.Events);
            }
        }

        /// <summary>
        /// ChannelInactiveAsync 中仍能读到收口前写入的连接属性，且此时连接已非活跃
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Executor_ChannelInactive_CanReadChannelAttributes()
        {
            var stream = new TestRawStream("inactive-read-attrs");
            var channel = new DefaultChannel("inactive-read-attrs", null);
            var options = NewOptions();
            var handler = new AttributeReadingHandler();

            channel.SetAttribute("session", 42);

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(stream, channel, options, CancellationToken.None);
                executor.Attach(pipeline);
                executor.Start();

                await handler.Active.Task.WaitAsync(WaitTimeout);
                await executor.BeginShutdown("test-done");
                await executor.Completion.WaitAsync(WaitTimeout);

                Assert.AreEqual(42, handler.Session);
                Assert.IsFalse(handler.WasActiveInInactive, "ChannelInactiveAsync 时连接应已非活跃");
            }
        }

        /// <summary>
        /// 收口完成后连接属性已被清理，且整个收口只清理一次
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Executor_AttributesClearedOnceAfterInactive()
        {
            var stream = new TestRawStream("clear-attributes-once");
            var channel = new DefaultChannel("clear-attributes-once", null);
            var options = NewOptions();
            var handler = new AttributeReadingHandler();

            channel.SetAttribute("session", 42);

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(stream, channel, options, CancellationToken.None);
                executor.Attach(pipeline);
                executor.Start();

                await handler.Active.Task.WaitAsync(WaitTimeout);
                await executor.BeginShutdown("test-done");
                await executor.Completion.WaitAsync(WaitTimeout);

                Assert.AreEqual(0, channel.Attributes.Count);
                Assert.AreEqual(1, channel.AttributesClearCount);
            }
        }

        /// <summary>
        /// 构造前传输层已取消时不再补发 ChannelActiveAsync
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Executor_ActiveNotDispatchedAfterInactive()
        {
            var stream = new TestRawStream("cancelled-before-start");
            var channel = new DefaultChannel("cancelled-before-start", null);
            var options = NewOptions();
            var handler = new LifecycleRecorderHandler();
            var cancelled = new CancellationTokenSource();
            cancelled.Cancel();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(stream, channel, options, cancelled.Token);

                Assert.ThrowsException<InvalidOperationException>(() => executor.Start());
                await executor.Completion.WaitAsync(WaitTimeout);

                Assert.AreEqual(0, handler.ActiveCount);
            }
        }

        /// <summary>
        /// HandlerAdded 抛异常时本次添加失败，节点不挂链
        /// </summary>
        [TestMethod]
        public void Pipeline_HandlerAddedThrows_NodeNotLinked()
        {
            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                Assert.ThrowsException<InvalidOperationException>(
                    () => pipeline.AddLast(new ThrowingOnAddedHandler()));

                Assert.AreEqual(0, pipeline.Handlers.Count);
            }
        }

        /// <summary>
        /// HandlerRemoved 抛异常不影响摘链结果，节点已不在链上
        /// </summary>
        [TestMethod]
        public void Pipeline_HandlerRemovedThrows_NodeStillUnlinked()
        {
            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                var handler = new ThrowingOnRemovedHandler();
                pipeline.AddLast(handler);

                pipeline.Remove(handler);

                Assert.AreEqual(0, pipeline.Handlers.Count);
            }
        }

        /// <summary>
        /// HandlerAdded 阶段节点尚未挂链：ChannelId 为 null，读取 Channel 抛 InvalidOperationException
        /// </summary>
        [TestMethod]
        public void Pipeline_HandlerAdded_ChannelNotReadable()
        {
            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                var handler = new CtxProbeOnAddedHandler();
                pipeline.AddLast(handler);

                Assert.IsTrue(handler.AddedObserved);
                Assert.IsNull(handler.ChannelIdInAdded);
                Assert.IsInstanceOfType(handler.ChannelError, typeof(InvalidOperationException));
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
        }

        private sealed class NoopOutboundHandler : OutboundHandlerAdapter
        {
            public override Task WriteAsync(IChannelHandlerContext ctx, object msg)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class LifecycleRecorderHandler : InboundHandlerAdapter
        {
            private readonly List<string> _events = new List<string>();
            private int _activeCount;

            public readonly TaskCompletionSource<bool> Active =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public List<string> Events
            {
                get
                {
                    lock (_events)
                    {
                        return new List<string>(_events);
                    }
                }
            }

            public int ActiveCount
            {
                get { return Volatile.Read(ref _activeCount); }
            }

            public override Task ChannelActiveAsync(IChannelHandlerContext ctx)
            {
                Interlocked.Increment(ref _activeCount);
                lock (_events)
                {
                    _events.Add("active");
                }

                Active.TrySetResult(true);
                return Task.CompletedTask;
            }

            public override Task ChannelInactiveAsync(IChannelHandlerContext ctx)
            {
                lock (_events)
                {
                    _events.Add("inactive");
                }

                return Task.CompletedTask;
            }
        }

        private sealed class AttributeReadingHandler : InboundHandlerAdapter
        {
            public readonly TaskCompletionSource<bool> Active =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public object Session { get; private set; }

            public bool WasActiveInInactive { get; private set; }

            public override Task ChannelActiveAsync(IChannelHandlerContext ctx)
            {
                Active.TrySetResult(true);
                return Task.CompletedTask;
            }

            public override Task ChannelInactiveAsync(IChannelHandlerContext ctx)
            {
                Session = ctx.Channel.GetAttribute("session");
                WasActiveInInactive = ctx.Channel.IsActive;
                return Task.CompletedTask;
            }
        }

        private sealed class ThrowingOnAddedHandler : InboundHandlerAdapter
        {
            public override void HandlerAdded(IChannelHandlerContext ctx)
            {
                throw new InvalidOperationException("added-failed");
            }
        }

        private sealed class ThrowingOnRemovedHandler : InboundHandlerAdapter
        {
            public override void HandlerRemoved(IChannelHandlerContext ctx)
            {
                throw new InvalidOperationException("removed-failed");
            }
        }

        private sealed class CtxProbeOnAddedHandler : InboundHandlerAdapter
        {
            public bool AddedObserved { get; private set; }

            public string ChannelIdInAdded { get; private set; }

            public Exception ChannelError { get; private set; }

            public override void HandlerAdded(IChannelHandlerContext ctx)
            {
                AddedObserved = true;
                ChannelIdInAdded = ctx.ChannelId;

                try
                {
                    var ignored = ctx.Channel;
                    Assert.Fail("HandlerAdded 阶段不应能读取 Channel。");
                }
                catch (InvalidOperationException ex)
                {
                    ChannelError = ex;
                }
            }
        }

        private sealed class RecordingPipelineHost : IPipelineHost
        {
            private int _removeCount;

            public int RemoveCount
            {
                get { return Volatile.Read(ref _removeCount); }
            }

            public string RemovedId { get; private set; }

            public bool TryGetChannel(string channelId, out IChannel channel)
            {
                channel = null;
                return false;
            }

            public bool TryRemoveChannel(string channelId, out IChannel channel)
            {
                Interlocked.Increment(ref _removeCount);
                RemovedId = channelId;
                channel = null;
                return true;
            }

            public Task CloseAsync(string channelId, string reason)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class TestRawStream : IRawByteStream
        {
            private readonly Pipe _pipe;

            public TestRawStream(string id)
            {
                ChannelId = id;
                _pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 4 * 1024 * 1024));
            }

            public string ChannelId { get; }

            public PipeReader Input
            {
                get { return _pipe.Reader; }
            }

            public readonly TaskCompletionSource<string> Closed =
                new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task SendAsync(ReadOnlyMemory<byte> data, IDisposable owner)
            {
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
                Closed.TrySetResult(reason);
                return Task.CompletedTask;
            }
        }
    }
}