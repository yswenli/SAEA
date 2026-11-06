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
*文件名： CodecHandlerTests
*版本号： v26.11.5.1
*唯一标识：bce67743-2def-40ac-9709-16fa6774a312
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：出站/入站编解码 handler 的契约用例：字节一致、所有权交接、异常不吞
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：出站/入站编解码 handler 的契约用例：字节一致、所有权交接、异常不吞
*
*****************************************************************************/
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

        /// <summary>
        /// 信封包裹、信封编码、帧编码三级出站链写出的一条实体只产生一帧，且字节与旧协议的 JSON 编码加长度头写出一致
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
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

#if DEBUG
        /// <summary>
        /// 日志 handler 会原样转发同一个中间载体；这是合法的多节点传递，
        /// Debug 契约检查必须只在"同一节点重复写同一实例"时触发。
        /// </summary>
        [TestMethod]
        public async Task Outbound_PassthroughHandler_ForwardsSameInstanceWithoutFalsePositive()
        {
            var registry = NewRegistry(7, typeof(JsonPoco));
            var entity = new JsonPoco { Name = "透传", Count = 1, Big = 2L };

            using (var rig = new OutboundRig(
                new FrameEncoderHandler(new LengthFieldFrameEncoder()),
                new OutboundLoggingHandler(),
                new CodecEncoderHandler(new EnvelopeCodec()),
                new EnvelopeWrapHandler(registry, new JsonMessageCodec())))
            {
                var ctx = await rig.CaptureContextAsync();
                await ctx.WriteAsync(entity);

                Assert.AreEqual(1, rig.Stream.SentCount, "透传 handler 不应被误判为重复写入");
            }
        }
#endif

        /// <summary>
        /// 链返回时只归还 body writer，帧缓冲所有权随 TransportFrame 交给传输层且只能释放一次
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
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

        /// <summary>
        /// 入站解码器解码后释放池化帧并归还背衬内存，下游收到类型码与负载正确的信封
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
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

        /// <summary>
        /// 未注册的类型码仍会归还帧，并以携带该类型码的 InvalidOperationException 形式走异常回调
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
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

        /// <summary>
        /// 负载超过拷贝阈值时解包仍能还原实体的字符串、int 与 long 字段
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
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

        /// <summary>
        /// 出站信封编码器收到非 Envelope 消息时抛 InvalidOperationException 且不发送任何字节
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
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

        /// <summary>
        /// 出站帧编码器收到非 Envelope 消息时抛 InvalidOperationException 且不发送任何字节
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
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

#if DEBUG
        /// <summary>
        /// 出站契约检查只在 Debug 构建下编译进来，因此本用例用 <c>#if DEBUG</c> 包裹；
        /// Release 构建下整个用例不存在，验证它需要跑 <c>dotnet test -c Debug</c>。
        /// </summary>
        [TestMethod]
        public async Task Outbound_SameMessageWrittenTwice_ThrowsContractViolationInDebug()
        {
            var registry = NewRegistry(7, typeof(JsonPoco));
            var entity = new JsonPoco { Name = "重复写入", Count = 2, Big = 3L };

            using (var rig = new OutboundRig(
                new FrameEncoderHandler(new LengthFieldFrameEncoder()),
                new CodecEncoderHandler(new EnvelopeCodec()),
                new EnvelopeWrapHandler(registry, new JsonMessageCodec())))
            {
                var ctx = await rig.CaptureContextAsync();
                await ctx.WriteAsync(entity);

                var failure = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => ctx.WriteAsync(entity));

                StringAssert.Contains(failure.Message, "重复写入", "契约异常未说明重复写入");
                Assert.AreEqual(1, rig.Stream.SentCount, "第二次写入不应产生额外帧");
            }
        }
#endif

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