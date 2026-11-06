using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
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

        [TestMethod]
        public async Task UnknownTypeCode_SurfacesAsExceptionCaught_AndClosesChannel()
        {
            using (var rig = new PipelineRig("unknown-type"))
            {
                var captured = new RecordingExceptionHandler();
                rig.Pipeline.AddLast(new EnvelopeUnwrapHandler(new TypeRegistry(), new JsonMessageCodec()));
                rig.Pipeline.AddLast(captured);
                rig.Pipeline.AddLast(new ExceptionTerminalHandler());

                await rig.Pipeline.FireChannelReadAsync(new Envelope { Type = 99, Payload = Array.Empty<byte>() });

                Assert.AreEqual(1, captured.Exceptions.Count, "未知 type code 未进入 ExceptionCaught");
                Assert.IsInstanceOfType(captured.Exceptions[0], typeof(InvalidOperationException),
                    "未知 type code 的异常类型不符");
                Assert.AreEqual(ExceptionTerminalHandler.HandlerExceptionReason, rig.Channel.LastCloseReason,
                    "链尾未按 handler-exception 关闭连接");
            }
        }

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

                await rig.Pipeline.FireChannelReadAsync(new OtherMessage());

                Assert.AreEqual(1, captured.Exceptions.Count, "日志处理器吞掉了下游异常");
                Assert.AreSame(thrown, captured.Exceptions[0], "上报的异常被替换");
            }
        }

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