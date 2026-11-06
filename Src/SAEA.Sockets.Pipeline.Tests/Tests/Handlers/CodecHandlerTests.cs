using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.Caching;
using SAEA.Sockets.Base;
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Handlers
{
    /// <summary>
    /// 出站/入站编解码 handler 的契约用例：字节一致、所有权交接、异常不吞。
    /// </summary>
    [TestClass]
    public class CodecHandlerTests
    {
        private sealed class JsonPoco
        {
            public string Name { get; set; }
            public int Count { get; set; }
            public long Big { get; set; }
        }

        [TestMethod]
        public async Task Outbound_FullChain_WritesBytesIdenticalToLegacyProtocol()
        {
            var registry = NewRegistry(7, typeof(JsonPoco));
            var entity = new JsonPoco { Name = "管道", Count = 42, Big = 9000000000L };

            using (var rig = new OutboundRig(
                new FrameEncoderHandler(new LengthFieldFrameEncoder()),
                new CodecEncoderHandler(new EnvelopeCodec()),
                new EnvelopeWrapHandler(registry, new JsonMessageCodec())))
            {
                var ctx = await rig.CaptureContextAsync();
                await ctx.WriteAsync(entity);

                Assert.AreEqual(1, rig.Stream.SentCount, "一条实体应只落到传输层一帧");

                var payload = new JsonMessageCodec().Encode(entity);
                var expected = new ArrayBufferWriter<byte>();
                new BaseSocketProtocal((byte)registry.ResolveCode(registry.Resolve(7)), payload).WriteTo(expected);

                CollectionAssert.AreEqual(expected.WrittenSpan.ToArray(), rig.Stream.SentFrames[0],
                    "出站字节与 JsonMessageCodec.Encode + BaseSocketProtocal.WriteTo 不一致");
            }
        }

        [TestMethod]
        public async Task Outbound_OwnerTransferredToTransport_NotDisposedEarly_AndDisposedOnce()
        {
            var registry = NewRegistry(7, typeof(JsonPoco));
            var entity = new JsonPoco { Name = "所有权", Count = 1, Big = 2L };

            using (var rig = new OutboundRig(
                new FrameEncoderHandler(new LengthFieldFrameEncoder()),
                new CodecEncoderHandler(new EnvelopeCodec()),
                new EnvelopeWrapHandler(registry, new JsonMessageCodec())))
            {
                var ctx = await rig.CaptureContextAsync();
                var returnedBefore = ReturnedTotal();
                await ctx.WriteAsync(entity);

                // body writer 在 CodecEncoderHandler 的 finally 中已归还；帧 writer 随 TransportFrame 交给传输层。
                Assert.AreEqual(returnedBefore + 1, ReturnedTotal(),
                    "链返回时只有 body writer 应已归还，帧 writer 不得提前释放");

                var owner = rig.Stream.LastOwner;
                Assert.IsNotNull(owner, "传输层未收到帧 owner");
                var frameWriter = owner as PooledBufferWriter;
                Assert.IsNotNull(frameWriter, "帧 owner 应为 PooledBufferWriter");

                // Dispose 后 WrittenMemory 会抛 ObjectDisposedException，用它证伪"提前释放"。
                Assert.IsTrue(frameWriter.WrittenMemory.Length > 0, "链返回后 owner 已被提前释放");

                owner.Dispose();
                Assert.AreEqual(returnedBefore + 2, ReturnedTotal(), "owner 第一次释放后未归还原帧缓冲");

                owner.Dispose();
                Assert.AreEqual(returnedBefore + 2, ReturnedTotal(), "owner 被重复归还");
            }
        }

        [TestMethod]
        public async Task Inbound_CodecDecoder_ReleasesPooledMessage_AndForwardsEnvelope()
        {
            var payload = new byte[] { 0x11, 0x22, 0x33 };
            var size = 64;
            var buffer = MemoryPoolManager.Rent(size);
            buffer[0] = 9;
            payload.CopyTo(buffer, 1);

            var pooled = new PooledMessage(buffer.AsMemory(0, payload.Length + 1), buffer, size, true);
            var sink = new CapturingInboundHandler();
            var returnedBefore = ReturnedTotal();

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                pipeline.AddLast(new CodecDecoderHandler(new EnvelopeCodec()));
                pipeline.AddLast(sink);

                try
                {
                    await pipeline.FireChannelReadAsync(pooled);

                    Assert.IsTrue(pooled.IsReleased, "CodecDecoderHandler 未释放 PooledMessage");
                    Assert.AreEqual(returnedBefore + 1, ReturnedTotal(), "池化帧背衬未归还内存池");

                    var envelope = sink.Message as Envelope;
                    Assert.IsNotNull(envelope, "下游未收到 Envelope");
                    Assert.AreEqual(9, envelope.Type, "信封 type 不符");
                    CollectionAssert.AreEqual(payload, envelope.Payload, "信封 payload 不符");
                }
                finally
                {
                    pooled.Release();
                }
            }
        }

        [TestMethod]
        public async Task Inbound_UnknownTypeCode_SurfacesAsExceptionCaught()
        {
            var registry = NewRegistry(7, typeof(JsonPoco));
            var frame = new byte[] { 99, 0x01, 0x02 };
            var pooled = new PooledMessage(frame, frame, frame.Length, false);
            var tail = new ExceptionCapturingHandler();

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                pipeline.AddLast(new CodecDecoderHandler(new EnvelopeCodec()));
                pipeline.AddLast(new EnvelopeUnwrapHandler(registry, new JsonMessageCodec()));
                pipeline.AddLast(tail);

                try
                {
                    await pipeline.FireChannelReadAsync(pooled);

                    Assert.IsTrue(pooled.IsReleased, "未识别的类型码也必须在解码后归还帧");
                    Assert.AreEqual(1, tail.Exceptions.Count, "未知 type code 未转成 ExceptionCaught");
                    Assert.IsInstanceOfType(tail.Exceptions[0], typeof(InvalidOperationException),
                        "未知 type code 的异常类型不符");
                    StringAssert.Contains(tail.Exceptions[0].Message, "99", "异常未携带未知的 type code");
                }
                finally
                {
                    pooled.Release();
                }
            }
        }

        [TestMethod]
        public async Task Inbound_PayloadAboveCopyThreshold_StillDecodesEntity()
        {
            var registry = NewRegistry(7, typeof(JsonPoco));
            var messageCodec = new JsonMessageCodec();
            var entity = new JsonPoco { Name = "超阈值", Count = 7, Big = 8L };
            var payload = messageCodec.Encode(entity);
            Assert.IsTrue(payload.Length > 4, "用例需要 payload 超过阈值");

            var frame = new byte[payload.Length + 1];
            frame[0] = 7;
            payload.CopyTo(frame, 1);

            var pooled = new PooledMessage(frame, frame, frame.Length, false);
            var sink = new CapturingInboundHandler();

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                pipeline.AddLast(new CodecDecoderHandler(new EnvelopeCodec()));
                pipeline.AddLast(new EnvelopeUnwrapHandler(registry, messageCodec, 4));
                pipeline.AddLast(sink);

                try
                {
                    await pipeline.FireChannelReadAsync(pooled);

                    var restored = sink.Message as JsonPoco;
                    Assert.IsNotNull(restored, "超阈值 payload 未被解码成实体");
                    Assert.AreEqual("超阈值", restored.Name, "name 不符");
                    Assert.AreEqual(7, restored.Count, "count 不符");
                    Assert.AreEqual(8L, restored.Big, "big 不符");
                }
                finally
                {
                    pooled.Release();
                }
            }
        }

        [TestMethod]
        public async Task Outbound_CodecEncoder_WrongMessageType_ThrowsInvalidOperationException()
        {
            using (var rig = new OutboundRig(new CodecEncoderHandler(new EnvelopeCodec())))
            {
                var ctx = await rig.CaptureContextAsync();
                await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => ctx.WriteAsync(new object()));
                Assert.AreEqual(0, rig.Stream.SentCount, "错误类型不应产生任何发送");
            }
        }

        [TestMethod]
        public async Task Outbound_FrameEncoder_WrongMessageType_ThrowsInvalidOperationException()
        {
            using (var rig = new OutboundRig(new FrameEncoderHandler(new LengthFieldFrameEncoder())))
            {
                var ctx = await rig.CaptureContextAsync();
                await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => ctx.WriteAsync(new object()));
                Assert.AreEqual(0, rig.Stream.SentCount, "错误类型不应产生任何发送");
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

        private static TypeRegistry NewRegistry(int typeCode, Type clrType)
        {
            var registry = new TypeRegistry();
            registry.Register(typeCode, clrType);
            return registry;
        }

        private static long ReturnedTotal()
        {
            var stats = MemoryPoolManager.GetStatistics();
            return stats.SmallPoolReturned + stats.MediumPoolReturned + stats.LargePoolReturned;
        }

        private sealed class OutboundRig : IDisposable
        {
            private readonly ChannelPipelineExecutor _executor;

            public OutboundRig(params IChannelOutboundHandler[] outboundHandlers)
            {
                Stream = new RecordingRawByteStream("codec-outbound");
                var options = NewOptions();
                Pipeline = ChannelPipeline.Create(options);
                _executor = new ChannelPipelineExecutor(
                    Stream, new DefaultChannel("codec-outbound", null), options, CancellationToken.None);
                _executor.Attach(Pipeline);

                Sink = new CapturingInboundHandler();
                foreach (var handler in outboundHandlers)
                    Pipeline.AddLast(handler);
                Pipeline.AddLast(Sink);
            }

            public RecordingRawByteStream Stream { get; }

            public ChannelPipeline Pipeline { get; }

            public CapturingInboundHandler Sink { get; }

            /// <summary>走一次入站读取拿到链上 ctx，后续测试用它发起出站写。</summary>
            public async Task<IChannelHandlerContext> CaptureContextAsync()
            {
                await Pipeline.FireChannelReadAsync(new object());
                Assert.IsNotNull(Sink.Context, "入站读取后仍未拿到 handler ctx");
                return Sink.Context;
            }

            public void Dispose()
            {
                _executor.Dispose();
            }
        }

        private sealed class RecordingRawByteStream : IRawByteStream
        {
            private readonly Pipe _pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 4 * 1024 * 1024));
            private readonly List<byte[]> _sent = new List<byte[]>();
            private IDisposable _lastOwner;

            public RecordingRawByteStream(string channelId)
            {
                ChannelId = channelId;
            }

            public string ChannelId { get; }

            public PipeReader Input
            {
                get { return _pipe.Reader; }
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

            public IDisposable LastOwner
            {
                get
                {
                    lock (_sent)
                    {
                        return _lastOwner;
                    }
                }
            }

            public Task SendAsync(ReadOnlyMemory<byte> data, IDisposable owner)
            {
                lock (_sent)
                {
                    _sent.Add(data.ToArray());
                    _lastOwner = owner;
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

        private sealed class CapturingInboundHandler : InboundHandlerAdapter
        {
            public IChannelHandlerContext Context { get; private set; }

            public object Message { get; private set; }

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                Context = ctx;
                Message = msg;
                return Task.CompletedTask;
            }
        }

        private sealed class ExceptionCapturingHandler : InboundHandlerAdapter
        {
            public readonly List<Exception> Exceptions = new List<Exception>();

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                return Task.CompletedTask;
            }

            public override Task ChannelExceptionCaughtAsync(IChannelHandlerContext ctx, Exception ex)
            {
                Exceptions.Add(ex);
                return Task.CompletedTask;
            }
        }
    }
}