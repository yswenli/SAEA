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
*文件名： DispatchAndTerminalTests
*版本号： v26.11.5.1
*唯一标识：932c5050-775c-4ac8-bdf9-3c9315faf7d3
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：路由、两级鉴权、日志与链尾处理的契约用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/9 11:04:09
*修改人： yswenli
*版本号： v26.11.5.1
*描述：入站异常改为通知一次后冒泡，同步改写未知类型码与日志透传用例
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.Caching;
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Handlers
{
    /// <summary>
    /// 路由、两级鉴权、日志与链尾处理的契约用例。
    /// </summary>
    [TestClass]
    public class DispatchAndTerminalTests
    {
        /// <summary>
        /// 命中路由时业务处理器收到原消息，且 ctx 为分发器节点自身的上下文
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Dispatcher_Hit_InvokesRegisteredHandlerWithOwnNodeContext()
        {
            var handler = new RecordingMessageHandler();
            using (var rig = new PipelineRig("dispatch-hit"))
            {
                var dispatcher = new MessageDispatcher().Register<LoginMessage>(handler);
                rig.Pipeline.AddLast(dispatcher);

                await rig.Pipeline.FireChannelReadAsync(new LoginMessage { Name = "登录" });

                Assert.AreEqual(1, handler.Calls, "命中路由的业务处理器未被调用");
                var received = handler.LastMessage as LoginMessage;
                Assert.IsNotNull(received, "业务处理器收到的消息类型不符");
                Assert.AreEqual("登录", received.Name, "业务处理器收到的消息内容不符");
                Assert.AreSame(dispatcher, handler.LastContext.Handler,
                    "HandleAsync 必须收到分发器节点自身的 ctx，否则回包会跳过出站前缀");
            }
        }

        /// <summary>
        /// 未命中路由且配置兜底时只调用兜底处理器并原样传入消息
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Dispatcher_MissWithFallback_InvokesFallbackOnly()
        {
            var routed = new RecordingMessageHandler();
            var fallback = new RecordingMessageHandler();
            using (var rig = new PipelineRig("dispatch-fallback"))
            {
                rig.Pipeline.AddLast(new MessageDispatcher()
                    .Register<LoginMessage>(routed)
                    .RegisterFallback(fallback));

                var message = new OtherMessage();
                await rig.Pipeline.FireChannelReadAsync(message);

                Assert.AreEqual(0, routed.Calls, "未命中不应调用已注册的处理器");
                Assert.AreEqual(1, fallback.Calls, "兜底处理器未被调用");
                Assert.AreSame(message, fallback.LastMessage, "兜底处理器收到的不是原消息");
            }
        }

        /// <summary>
        /// 未命中且无兜底时把原消息继续传给下一个入站处理器
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Dispatcher_MissWithoutFallback_ForwardsToNextInboundHandler()
        {
            var sink = new RecordingInboundHandler();
            using (var rig = new PipelineRig("dispatch-forward"))
            {
                rig.Pipeline.AddLast(new MessageDispatcher());
                rig.Pipeline.AddLast(sink);

                var message = new OtherMessage();
                await rig.Pipeline.FireChannelReadAsync(message);

                Assert.AreEqual(1, sink.Calls, "未命中且无兜底时应记 Warn 并继续下传");
                Assert.AreSame(message, sink.LastMessage, "下传的消息被替换");
            }
        }

        /// <summary>
        /// 未命中且无兜底时先输出含「未命中路由，继续下传」的 Warn 日志再下传消息（日志异步刷出，需轮询等待）
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Dispatcher_MissWithoutFallback_EmitsWarnBeforeForwarding()
        {
            var sink = new RecordingInboundHandler();
            using (var rig = new PipelineRig("dispatch-warn"))
            {
                rig.Pipeline.AddLast(new MessageDispatcher());
                rig.Pipeline.AddLast(sink);

                var message = new OtherMessage();
                using (var capture = new ConsoleCapture())
                {
                    Console.SetOut(capture.Writer);
                    try
                    {
                        await rig.Pipeline.FireChannelReadAsync(message);

                        var found = await capture.WaitForAsync(
                            "未命中路由，继续下传", TimeSpan.FromSeconds(3));
                        Assert.IsTrue(
                            found,
                            "未命中路由应经 LogHelper 记一条 Warn；捕获到的输出：" + capture.Text);
                    }
                    finally
                    {
                        Console.SetOut(capture.Original);
                    }
                }

                Assert.AreEqual(1, sink.Calls, "记 Warn 后仍必须继续下传");
                Assert.AreSame(message, sink.LastMessage, "下传的消息被替换");
            }
        }

        /// <summary>
        /// 分发器处于入站链尾时未命中的消息就地 Dispose，不静默泄漏
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Dispatcher_MissWithoutFallbackAndNoTail_DisposesMessageInPlace()
        {
            using (var rig = new PipelineRig("dispatch-last"))
            {
                rig.Pipeline.AddLast(new MessageDispatcher());

                var message = new DisposableMessage();
                await rig.Pipeline.FireChannelReadAsync(message);

                Assert.IsTrue(message.Disposed,
                    "分发器是最后一个入站处理器时应就地丢弃并 Dispose 消息，而不是静默泄漏");
            }
        }

        /// <summary>
        /// 鉴权在解包之前的场景：非白名单类型码不做反序列化、不下传并以 unauthenticated 关闭连接，白名单类型正常放行
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Auth_BeforeUnwrap_BlocksNonWhitelistTypeWithoutDeserializing()
        {
            var registry = new TypeRegistry();
            registry.Register(1, typeof(LoginMessage));
            var codec = new CountingMessageCodec(new JsonMessageCodec());

            using (var rig = new PipelineRig("auth-block"))
            {
                var sink = new RecordingInboundHandler();
                rig.Pipeline.AddLast(new AuthHandler(new[] { 1 }));
                rig.Pipeline.AddLast(new EnvelopeUnwrapHandler(registry, codec));
                rig.Pipeline.AddLast(sink);

                await rig.Pipeline.FireChannelReadAsync(new Envelope
                {
                    Type = 2,
                    Payload = new JsonMessageCodec().Encode(new OtherMessage())
                });

                Assert.AreEqual(0, codec.DecodeCalls, "未认证且非白名单的帧不得进入反序列化");
                Assert.AreEqual(0, sink.Calls, "未认证且非白名单的帧不得继续下传");
                Assert.AreEqual("unauthenticated", rig.Channel.LastCloseReason,
                    "未认证且非白名单的帧应关闭连接");
            }

            using (var rig = new PipelineRig("auth-pass"))
            {
                var sink = new RecordingInboundHandler();
                rig.Pipeline.AddLast(new AuthHandler(new[] { 1 }));
                rig.Pipeline.AddLast(new EnvelopeUnwrapHandler(registry, codec));
                rig.Pipeline.AddLast(sink);

                await rig.Pipeline.FireChannelReadAsync(new Envelope
                {
                    Type = 1,
                    Payload = new JsonMessageCodec().Encode(new LoginMessage { Name = "白名单" })
                });

                Assert.AreEqual(1, codec.DecodeCalls, "白名单类型应放行到反序列化");
                Assert.AreEqual(1, sink.Calls, "白名单类型应继续下传");
                Assert.IsNull(rig.Channel.LastCloseReason, "白名单类型不得关闭连接");
            }
        }

        /// <summary>
        /// 未注册的类型码转成 InvalidOperationException 走异常回调，链尾按 handler-exception 关闭连接，异常随后冒泡
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task UnknownTypeCode_SurfacesAsExceptionCaught_AndClosesChannel()
        {
            using (var rig = new PipelineRig("unknown-type"))
            {
                var captured = new RecordingExceptionHandler();
                rig.Pipeline.AddLast(new EnvelopeUnwrapHandler(new TypeRegistry(), new JsonMessageCodec()));
                rig.Pipeline.AddLast(captured);
                rig.Pipeline.AddLast(new ExceptionTerminalHandler());

                var thrown = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => rig.Pipeline.FireChannelReadAsync(new Envelope { Type = 99, Payload = Array.Empty<byte>() }));

                Assert.AreEqual(1, captured.Exceptions.Count, "未知 type code 未进入 ExceptionCaught");
                Assert.IsInstanceOfType(captured.Exceptions[0], typeof(InvalidOperationException),
                    "未知 type code 的异常类型不符");
                Assert.AreSame(thrown, captured.Exceptions[0], "通知下游与冒泡给调用方的必须是同一个异常实例");
                Assert.AreEqual(ExceptionTerminalHandler.HandlerExceptionReason, rig.Channel.LastCloseReason,
                    "链尾未按 handler-exception 关闭连接");
            }
        }

        /// <summary>
        /// 链尾对单消息超时与 ChannelClosedException 只记录且不关闭连接，其它异常记录 Error 并关闭
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Terminal_ExceptionBranches_WarnOrDebugWithoutClosing_AndErrorCloses()
        {
            using (var rig = new PipelineRig("terminal-timeout"))
            {
                rig.Pipeline.AddLast(new ExceptionTerminalHandler());
                await rig.Pipeline.FireExceptionCaughtAsync(new MessageTimeoutException("terminal-timeout", TimeSpan.FromSeconds(1)));
                Assert.IsNull(rig.Channel.LastCloseReason, "单消息超时不得关闭连接");
            }

            using (var rig = new PipelineRig("terminal-closed"))
            {
                rig.Pipeline.AddLast(new ExceptionTerminalHandler());
                await rig.Pipeline.FireExceptionCaughtAsync(new ChannelClosedException("连接已在收口"));
                Assert.IsNull(rig.Channel.LastCloseReason, "ChannelClosedException 不得重复关闭连接");
            }

            using (var rig = new PipelineRig("terminal-error"))
            {
                rig.Pipeline.AddLast(new ExceptionTerminalHandler());
                await rig.Pipeline.FireExceptionCaughtAsync(new InvalidOperationException("boom"));
                Assert.AreEqual(ExceptionTerminalHandler.HandlerExceptionReason, rig.Channel.LastCloseReason,
                    "其它异常应在链尾记录 Error 并关闭连接");
            }
        }

        /// <summary>
        /// 入站日志处理器不改变也不吞掉消息，下游异常仍原对象上报并继续冒泡
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task InboundLogging_DoesNotChangeMessage_AndExceptionStillPropagates()
        {
            using (var rig = new PipelineRig("logging-pass"))
            {
                var sink = new RecordingInboundHandler();
                rig.Pipeline.AddLast(new InboundLoggingHandler());
                rig.Pipeline.AddLast(sink);

                var message = new LoginMessage { Name = "原样" };
                await rig.Pipeline.FireChannelReadAsync(message);

                Assert.AreEqual(1, sink.Calls, "日志处理器吞掉了消息");
                Assert.AreSame(message, sink.LastMessage, "日志处理器改变了消息对象");
            }

            using (var rig = new PipelineRig("logging-throw"))
            {
                var thrown = new InvalidOperationException("下游异常");
                var captured = new RecordingExceptionHandler();
                rig.Pipeline.AddLast(new InboundLoggingHandler());
                rig.Pipeline.AddLast(new ThrowingInboundHandler(thrown));
                rig.Pipeline.AddLast(captured);

                var bubbled = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => rig.Pipeline.FireChannelReadAsync(new OtherMessage()));

                Assert.AreSame(thrown, bubbled, "冒泡的异常被替换");
                Assert.AreEqual(1, captured.Exceptions.Count, "日志处理器吞掉了下游异常");
                Assert.AreSame(thrown, captured.Exceptions[0], "上报的异常被替换");
            }
        }

        /// <summary>
        /// 帧透传分支通过 Retain 交给下游，两份引用全部释放后池化背衬只归还一次
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task FramePassthrough_RetainAndRelease_ReturnsPooledBackingExactlyOnce()
        {
            const int size = 64;
            var buffer = MemoryPoolManager.Rent(size);
            buffer[0] = 0x2A;
            var pooled = new PooledMessage(buffer.AsMemory(0, 1), buffer, size, true);
            var sink = new ReleasingPooledMessageHandler();
            var before = ReturnedTotal();

            using (var rig = new PipelineRig("passthrough"))
            {
                rig.Pipeline.AddLast(new FramePassthroughHandler());
                rig.Pipeline.AddLast(sink);

                try
                {
                    await rig.Pipeline.FireChannelReadAsync(pooled);

                    Assert.AreSame(pooled, sink.Message, "透传分支收到的不是原 PooledMessage");
                    Assert.IsFalse(pooled.IsReleased,
                        "下游 Release 的是 Retain 出来的那一份，队列条目仍持有引用");
                    Assert.AreEqual(before, ReturnedTotal(), "引用未全部释放前不得归还池缓冲");
                }
                finally
                {
                    pooled.Release();
                }
            }

            Assert.IsTrue(pooled.IsReleased, "两份引用都释放后队列条目应已释放");
            Assert.AreEqual(before + 1, ReturnedTotal(), "池化背衬应恰好归还一次");
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

        private static long ReturnedTotal()
        {
            var stats = MemoryPoolManager.GetStatistics();
            return stats.SmallPoolReturned + stats.MediumPoolReturned + stats.LargePoolReturned;
        }

        private sealed class PipelineRig : IDisposable
        {
            private readonly ChannelPipelineExecutor _executor;

            public PipelineRig(string id)
            {
                Options = NewOptions();
                Pipeline = ChannelPipeline.Create(Options);
                Channel = new TestChannel(id);
                Stream = new TestRawByteStream(id);
                _executor = new ChannelPipelineExecutor(Stream, Channel, Options, CancellationToken.None);
                _executor.Attach(Pipeline);
            }

            public ChannelPipeline Pipeline { get; }

            public TestChannel Channel { get; }

            public TestRawByteStream Stream { get; }

            public ChannelPipelineOptions Options { get; }

            public void Dispose()
            {
                _executor.Dispose();
            }
        }

        /// <summary>
        /// 临时接管 <see cref="Console.Out"/>，用于断言 LogHelper 异步刷出的文本。
        /// LogHelper 由后台线程写日志（空闲时每 500ms 轮询一次），因此断言只能轮询等待。
        /// </summary>
        private sealed class ConsoleCapture : IDisposable
        {
            private readonly object _gate = new object();

            private readonly StringBuilder _text = new StringBuilder();

            public ConsoleCapture()
            {
                Original = Console.Out;
                Writer = new LockedTextWriter(this);
            }

            public TextWriter Original { get; }

            public TextWriter Writer { get; }

            public string Text
            {
                get
                {
                    lock (_gate)
                        return _text.ToString();
                }
            }

            public async Task<bool> WaitForAsync(string expected, TimeSpan timeout)
            {
                var watch = Stopwatch.StartNew();
                while (watch.Elapsed < timeout)
                {
                    if (Contains(expected))
                        return true;

                    await Task.Delay(50).ConfigureAwait(false);
                }

                return Contains(expected);
            }

            public void Dispose()
            {
                Console.SetOut(Original);
            }

            private bool Contains(string expected)
            {
                return Text.IndexOf(expected, StringComparison.Ordinal) >= 0;
            }

            private void Append(string value)
            {
                lock (_gate)
                    _text.Append(value);
            }

            private sealed class LockedTextWriter : TextWriter
            {
                private readonly ConsoleCapture _owner;

                public LockedTextWriter(ConsoleCapture owner)
                {
                    _owner = owner;
                }

                public override Encoding Encoding
                {
                    get { return Encoding.UTF8; }
                }

                public override void Write(char value)
                {
                    _owner.Append(value.ToString());
                }

                public override void Write(string value)
                {
                    _owner.Append(value);
                }

                public override void WriteLine()
                {
                    _owner.Append(Environment.NewLine);
                }

                public override void WriteLine(string value)
                {
                    _owner.Append(value + Environment.NewLine);
                }
            }
        }

        private sealed class TestChannel : IChannel
        {
            private readonly ConcurrentDictionary<string, object> _attributes =
                new ConcurrentDictionary<string, object>();

            private readonly List<string> _closeReasons = new List<string>();

            private int _active = 1;

            public TestChannel(string id)
            {
                Id = id;
            }

            public string Id { get; }

            public bool IsActive
            {
                get { return Volatile.Read(ref _active) == 1; }
            }

            public IReadOnlyDictionary<string, object> Attributes
            {
                get { return _attributes; }
            }

            public string LastCloseReason
            {
                get
                {
                    lock (_closeReasons)
                    {
                        return _closeReasons.Count == 0 ? null : _closeReasons[_closeReasons.Count - 1];
                    }
                }
            }

            public void SetAttribute(string key, object value)
            {
                _attributes[key] = value;
            }

            public object GetAttribute(string key)
            {
                object value;
                return _attributes.TryGetValue(key, out value) ? value : null;
            }

            public bool TryRemoveAttribute(string key, out object value)
            {
                return _attributes.TryRemove(key, out value);
            }

            public Task CloseAsync(string reason)
            {
                lock (_closeReasons)
                {
                    _closeReasons.Add(reason);
                }

                Volatile.Write(ref _active, 0);
                return Task.CompletedTask;
            }
        }

        private sealed class TestRawByteStream : IRawByteStream
        {
            private readonly Pipe _pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 4 * 1024 * 1024));

            public TestRawByteStream(string channelId)
            {
                ChannelId = channelId;
            }

            public string ChannelId { get; }

            public PipeReader Input
            {
                get { return _pipe.Reader; }
            }

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
                return Task.CompletedTask;
            }
        }

        private sealed class RecordingMessageHandler : IMessageHandler
        {
            public int Calls { get; private set; }

            public object LastMessage { get; private set; }

            public IChannelHandlerContext LastContext { get; private set; }

            public Task HandleAsync(IChannelHandlerContext ctx, object message)
            {
                Calls++;
                LastMessage = message;
                LastContext = ctx;
                return Task.CompletedTask;
            }
        }

        private sealed class RecordingInboundHandler : InboundHandlerAdapter
        {
            public int Calls { get; private set; }

            public object LastMessage { get; private set; }

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                Calls++;
                LastMessage = msg;
                return Task.CompletedTask;
            }
        }

        private sealed class ReleasingPooledMessageHandler : InboundHandlerAdapter
        {
            public object Message { get; private set; }

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                Message = msg;

                var disposable = msg as IDisposable;
                if (disposable != null)
                    disposable.Dispose();

                return Task.CompletedTask;
            }
        }

        private sealed class RecordingExceptionHandler : InboundHandlerAdapter
        {
            public readonly List<Exception> Exceptions = new List<Exception>();

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                return Task.CompletedTask;
            }

            public override Task ChannelExceptionCaughtAsync(IChannelHandlerContext ctx, Exception ex)
            {
                Exceptions.Add(ex);
                return ctx.FireExceptionCaughtAsync(ex);
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

        private sealed class CountingMessageCodec : IMessageCodec
        {
            private readonly IMessageCodec _inner;

            public CountingMessageCodec(IMessageCodec inner)
            {
                _inner = inner;
            }

            public int DecodeCalls;

            public object Decode(ReadOnlyMemory<byte> payload, Type targetType)
            {
                Interlocked.Increment(ref DecodeCalls);
                return _inner.Decode(payload, targetType);
            }

            public byte[] Encode(object entity)
            {
                return _inner.Encode(entity);
            }
        }

        private sealed class LoginMessage
        {
            public string Name { get; set; }
        }

        private sealed class OtherMessage
        {
        }

        private sealed class DisposableMessage : IDisposable
        {
            public bool Disposed { get; private set; }

            public void Dispose()
            {
                Disposed = true;
            }
        }
    }
}