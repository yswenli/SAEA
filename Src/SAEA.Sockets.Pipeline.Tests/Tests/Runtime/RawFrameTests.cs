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
*文件名： RawFrameTests
*版本号： v26.11.5.1
*唯一标识：a1989251-3c47-4b73-b2c0-dc071752e161
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/9 11:35:49
*描述：RawFrame 所有权与出站边界补偿用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/9 11:35:49
*修改人： yswenli
*版本号： v26.11.5.1
*描述：RawFrame 所有权与出站边界补偿用例
*
*****************************************************************************/
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Sockets.Base;
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Runtime
{
    /// <summary>
    /// 原始帧的所有权：链头移交前由链补偿、移交后只由传输层释放，重复转发与异常路径不得重复释放。
    /// </summary>
    [TestClass]
    public class RawFrameTests
    {
        /// <summary>
        /// 移交前 Dispose 只释放一次背衬持有者，重复 Dispose 不再重复释放
        /// </summary>
        [TestMethod]
        public void RawFrame_DisposeBeforeHandoff_ReleasesOwnerOnce()
        {
            var owner = new CountingOwner();
            var raw = new RawFrame(new byte[] { 1, 2, 3 }, owner);

            raw.Dispose();
            raw.Dispose();

            Assert.AreEqual(1, owner.DisposeCount, "移交前重复 Dispose 导致持有者被重复释放");
        }

        /// <summary>
        /// 成功移交后 Dispose 不再释放背衬持有者，避免把仍在发送的缓冲提前归还
        /// </summary>
        [TestMethod]
        public void RawFrame_DisposeAfterHandoff_DoesNotReleaseOwner()
        {
            var owner = new CountingOwner();
            var raw = new RawFrame(new byte[] { 1, 2, 3 }, owner);

            Assert.IsTrue(raw.TryMarkHandedOff(), "首次移交应成功");
            raw.Dispose();

            Assert.AreEqual(0, owner.DisposeCount, "已移交的原始帧仍被链释放");
        }

        /// <summary>
        /// 链头已收口时写入原始帧先补偿持有者再抛 ChannelClosedException
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Pipeline_HeadSinkClosed_DisposesRawFrame()
        {
            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            using (var executor = NewExecutor(pipeline, new RecordingRawByteStream("closed")))
            {
                var owner = new CountingOwner();
                var raw = new RawFrame(new byte[] { 9 }, owner);

                await pipeline.CloseAsync("test-close");

                await Assert.ThrowsExceptionAsync<ChannelClosedException>(
                    () => pipeline.HeadSinkWriteAsync(raw));

                Assert.AreEqual(1, owner.DisposeCount, "链头关闭路径未补偿原始帧或重复补偿");
            }
        }

        /// <summary>
        /// 链未接入传输层时写入原始帧失败并补偿持有者一次
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Pipeline_NotAttached_DisposesRawFrame()
        {
            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                var owner = new CountingOwner();
                var raw = new RawFrame(new byte[] { 4 }, owner);

                await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => pipeline.WriteAsync(raw));

                Assert.AreEqual(1, owner.DisposeCount, "未接入传输层时原始帧未被补偿");
            }
        }

        /// <summary>
        /// 移交 CAS 成功后传输层发送抛错，链不得再释放持有者；持有者只由传输层释放一次
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Pipeline_AdapterThrowsAfterCas_TransportReleasesOwnerOnly()
        {
            var error = new InvalidOperationException("adapter-failed");
            var stream = new RecordingRawByteStream("adapter-throws", error);

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            using (var executor = NewExecutor(pipeline, stream))
            {
                var owner = new CountingOwner();
                var raw = new RawFrame(new byte[] { 5 }, owner);

                var thrown = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => pipeline.WriteAsync(raw));

                Assert.AreSame(error, thrown, "发送异常被替换");
                Assert.AreEqual(1, stream.SentCount, "适配器抛错前应已记录一次发送");
                Assert.AreEqual(0, owner.DisposeCount, "移交成功后链仍释放了持有者");

                raw.Dispose();
                Assert.AreEqual(0, owner.DisposeCount, "已移交后 Dispose 仍释放持有者");

                var transportOwner = stream.Owners[0];
                Assert.IsNotNull(transportOwner, "传输层未收到持有者");
                transportOwner.Dispose();
                Assert.AreEqual(1, owner.DisposeCount, "传输层释放后持有者计数不符");
            }
        }

        /// <summary>
        /// 出站处理器吞掉原始帧不转发时由边界补偿一次，业务写本身正常完成
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Pipeline_HandlerSwallowsRawFrame_ReleasesOnce()
        {
            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                pipeline.AddLast(new SwallowOutboundHandler());
                var owner = new CountingOwner();
                var raw = new RawFrame(new byte[] { 6 }, owner);

                await pipeline.WriteAsync(raw);

                Assert.AreEqual(1, owner.DisposeCount, "处理器吞掉原始帧后未补偿一次");
                Assert.AreEqual(0, pipeline.PendingWriteCount, "业务写完成后计数未归零");
            }
        }

        /// <summary>
        /// 出站处理器转发前抛错时由边界补偿原始帧一次，异常原样冒泡
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Pipeline_HandlerThrowsBeforeForward_ReleasesOnce()
        {
            var error = new InvalidOperationException("handler-failed");

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                pipeline.AddLast(new ThrowingOutboundHandler(error));
                var owner = new CountingOwner();
                var raw = new RawFrame(new byte[] { 7 }, owner);

                var thrown = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => pipeline.WriteAsync(raw));

                Assert.AreSame(error, thrown, "出站异常被替换");
                Assert.AreEqual(1, owner.DisposeCount, "转发前抛错未补偿或重复补偿原始帧");
            }
        }

        /// <summary>
        /// 同一节点对同一次写转发两次时第二次抛 InvalidOperationException，传输层只收到一帧且持有者只释放一次
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Pipeline_HandlerForwardsTwice_ThrowsAndReleasesOnce()
        {
            var stream = new RecordingRawByteStream("forward-twice");

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            using (var executor = NewExecutor(pipeline, stream))
            {
                pipeline.AddLast(new ForwardTwiceOutboundHandler());
                var owner = new CountingOwner();
                var raw = new RawFrame(new byte[] { 8, 9 }, owner);

                var thrown = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => pipeline.WriteAsync(raw));

                StringAssert.Contains(thrown.Message, "重复转发", "重复转发未按预期拒绝");
                Assert.AreEqual(1, stream.SentCount, "重复转发产生了重复帧");
                Assert.AreEqual(0, owner.DisposeCount, "移交成功的原始帧被链提前释放");

                raw.Dispose();
                Assert.AreEqual(0, owner.DisposeCount, "已移交后 Dispose 仍释放持有者");

                stream.Owners[0].Dispose();
                Assert.AreEqual(1, owner.DisposeCount, "传输层释放后持有者计数不符");
            }
        }

        /// <summary>
        /// 既有标准编码链（信封包裹 → 信封编码 → 帧编码）行为不变：一条实体写出一帧且字节与旧协议一致
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Pipeline_EnvelopeChain_StillWorks()
        {
            var registry = new TypeRegistry();
            registry.Register(7, typeof(ProbeEntity));
            var entity = new ProbeEntity { Name = "原始帧", Count = 3 };

            var stream = new RecordingRawByteStream("envelope-chain");

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            using (var executor = NewExecutor(pipeline, stream))
            {
                pipeline.AddLast(new FrameEncoderHandler(new LengthFieldFrameEncoder()));
                pipeline.AddLast(new CodecEncoderHandler(new EnvelopeCodec()));
                pipeline.AddLast(new EnvelopeWrapHandler(registry, new JsonMessageCodec()));

                await pipeline.WriteAsync(entity);

                Assert.AreEqual(1, stream.SentCount, "标准编码链一条实体应只写出一帧");

                var payload = new JsonMessageCodec().Encode(entity);
                var expected = new ArrayBufferWriter<byte>();
                new BaseSocketProtocal((byte)registry.ResolveCode(registry.Resolve(7)), payload).WriteTo(expected);
                CollectionAssert.AreEqual(expected.WrittenSpan.ToArray(), stream.SentFrames[0],
                    "标准编码链字节与旧协议不一致");
            }
        }

        /// <summary>
        /// 迁移链不装 CodecEncoderHandler 时原始帧可直达链头，字节一致且持有者交给传输层
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Pipeline_MigrationChainWithoutCodecEncoder_RawFrameReachesHead()
        {
            var stream = new RecordingRawByteStream("migration-chain");

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            using (var executor = NewExecutor(pipeline, stream))
            {
                pipeline.AddLast(new PassThroughOutboundHandler());
                var owner = new CountingOwner();
                var raw = new RawFrame(new byte[] { 0x10, 0x20, 0x30 }, owner);

                await pipeline.WriteAsync(raw);

                Assert.AreEqual(1, stream.SentCount, "原始帧未到达链头");
                CollectionAssert.AreEqual(new byte[] { 0x10, 0x20, 0x30 }, stream.SentFrames[0], "链头字节不一致");
                Assert.AreEqual(0, owner.DisposeCount, "移交后持有者被链提前释放");

                raw.Dispose();
                Assert.AreEqual(0, owner.DisposeCount, "已移交后 Dispose 仍释放持有者");

                stream.Owners[0].Dispose();
                Assert.AreEqual(1, owner.DisposeCount, "传输层释放后持有者计数不符");
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

        private sealed class ProbeEntity
        {
            public string Name { get; set; }

            public int Count { get; set; }
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
            private readonly List<IDisposable> _owners = new List<IDisposable>();
            private readonly Exception _sendError;

            public RecordingRawByteStream(string channelId)
                : this(channelId, null)
            {
            }

            public RecordingRawByteStream(string channelId, Exception sendError)
            {
                ChannelId = channelId;
                _sendError = sendError;
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

            public IDisposable[] Owners
            {
                get
                {
                    lock (_sent)
                    {
                        return _owners.ToArray();
                    }
                }
            }

            public Task SendAsync(ReadOnlyMemory<byte> data, IDisposable owner)
            {
                lock (_sent)
                {
                    _sent.Add(data.ToArray());
                    _owners.Add(owner);
                }

                if (_sendError != null)
                    throw _sendError;

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

        private sealed class ForwardTwiceOutboundHandler : OutboundHandlerAdapter
        {
            public override async Task WriteAsync(IChannelHandlerContext ctx, object msg)
            {
                await ctx.WriteAsync(msg).ConfigureAwait(false);
                await ctx.WriteAsync(msg).ConfigureAwait(false);
            }
        }
    }
}