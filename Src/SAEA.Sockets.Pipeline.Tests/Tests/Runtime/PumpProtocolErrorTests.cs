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
*文件名： PumpProtocolErrorTests
*版本号： v26.11.5.1
*唯一标识：05af5eff-8c7e-4363-8f05-bbdbc76d037a
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/9 12:10:00
*描述：分帧协议错误先上链再按状态收口的接收泵用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/9 12:10:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：分帧协议错误先上链再按状态收口的接收泵用例
*
*****************************************************************************/
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Runtime
{
    /// <summary>
    /// 分帧协议错误：先派上链让业务有机会回包与收口，派发返回后仍为 Open 才由泵强制收口。
    /// </summary>
    [TestClass]
    public class PumpProtocolErrorTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 分帧器抛 FrameProtocolException 时同一异常被派上链，且无处理器收口时由泵强制收口
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Pump_SplitProtocolError_ExceptionReachesChain()
        {
            var error = new FrameProtocolException("bad-frame");
            var options = NewOptions(new ThrowingSplitter(error));
            var stream = new TestRawStream("protocol-reaches-chain");

            using (var pipeline = ChannelPipeline.Create(options))
            using (var executor = new ChannelPipelineExecutor(
                stream, new TestChannel("protocol-reaches-chain"), options, CancellationToken.None))
            {
                var handler = new RecordingExceptionHandler();
                pipeline.AddLast(handler);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();
                    await WriteAsync(stream, new byte[] { 1, 2, 3 });

                    var caught = await handler.Caught.Task.WaitAsync(WaitTimeout);
                    Assert.AreSame(error, caught, "分帧协议错误未原样派上链");
                }
                finally
                {
                    await executor.Completion.WaitAsync(WaitTimeout);
                }

                Assert.IsFalse(executor.IsActive, "无处理器收口时泵未强制收口");
            }
        }

        /// <summary>
        /// 处理器在异常回调中先回包再关闭时，响应完成交接且收口原因来自处理器
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Pump_SplitProtocolError_HandlerWritesResponseThenCloses_ResponseHandedOff()
        {
            var options = NewOptions(new ThrowingSplitter(new FrameProtocolException("bad-frame")));
            var stream = new TestRawStream("protocol-response");

            using (var pipeline = ChannelPipeline.Create(options))
            using (var executor = new ChannelPipelineExecutor(
                stream, new TestChannel("protocol-response"), options, CancellationToken.None))
            {
                pipeline.AddLast(new OutboundSerializingHandler());
                pipeline.AddLast(new ResponseThenCloseHandler());
                executor.Attach(pipeline);

                executor.Start();
                await WriteAsync(stream, new byte[] { 1 });

                await executor.Completion.WaitAsync(WaitTimeout);

                Assert.AreEqual(1, stream.SentCount, "协议错误响应未完成交接");
                CollectionAssert.AreEqual(new byte[] { 0x0F, 0x0E }, stream.SentFrames[0], "响应字节不符");
                Assert.AreEqual(ResponseThenCloseHandler.CloseReason, stream.ClosedReason,
                    "收口原因不是处理器发起的关闭");
            }
        }

        /// <summary>
        /// 处理器不接管收口时，派发返回后连接仍为 Open，由泵以协议错误原因强制收口
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Pump_SplitProtocolError_NoHandlerCloses_ForcesShutdown()
        {
            var options = NewOptions(new ThrowingSplitter(new FrameProtocolException("bad-frame")));
            var stream = new TestRawStream("protocol-force");

            using (var pipeline = ChannelPipeline.Create(options))
            using (var executor = new ChannelPipelineExecutor(
                stream, new TestChannel("protocol-force"), options, CancellationToken.None))
            {
                var handler = new RecordingExceptionHandler();
                pipeline.AddLast(handler);
                executor.Attach(pipeline);

                executor.Start();
                await WriteAsync(stream, new byte[] { 1, 2 });

                await handler.Caught.Task.WaitAsync(WaitTimeout);
                await executor.Completion.WaitAsync(WaitTimeout);

                Assert.IsFalse(executor.IsActive, "协议错误后执行器仍为活跃状态");
                Assert.AreEqual("split-protocol-error", stream.ClosedReason, "泵未以协议错误原因收口");
            }
        }

        /// <summary>
        /// 处理器已在异常回调中进入 draining 时，泵不再起第二个关闭流程，收口原因保持处理器指定值
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Pump_SplitProtocolError_HandlerAlreadyDraining_NoSecondClose()
        {
            var options = NewOptions(new ThrowingSplitter(new FrameProtocolException("bad-frame")));
            var stream = new TestRawStream("protocol-draining");

            using (var pipeline = ChannelPipeline.Create(options))
            using (var executor = new ChannelPipelineExecutor(
                stream, new TestChannel("protocol-draining"), options, CancellationToken.None))
            {
                pipeline.AddLast(new DrainOnlyHandler());
                executor.Attach(pipeline);

                executor.Start();
                await WriteAsync(stream, new byte[] { 1 });

                await executor.Completion.WaitAsync(WaitTimeout);

                Assert.AreEqual(DrainOnlyHandler.CloseReason, stream.ClosedReason,
                    "处理器已进入 draining 时泵又起了第二个关闭流程");
            }
        }

        // ---------- 测试基础设施 ----------

        private static ChannelPipelineOptions NewOptions(IFrameSplitter splitter)
        {
            return new ChannelPipelineOptions
            {
                Splitter = splitter,
                QueueCapacity = 16,
                MaxConcurrentMessages = 1,
                Guard = NullChannelGuard.Instance
            };
        }

        private static async Task WriteAsync(TestRawStream stream, byte[] data)
        {
            await stream.Pipe.Writer.WriteAsync(data);
            await stream.Pipe.Writer.FlushAsync();
        }

        private sealed class ThrowingSplitter : IFrameSplitter
        {
            private readonly FrameProtocolException _error;

            public ThrowingSplitter(FrameProtocolException error)
            {
                _error = error;
            }

            public int MaxFrameLength
            {
                get { return 1024; }
            }

            public bool TrySplit(ReadOnlySequence<byte> buffer, out long consumed, out ReadOnlySequence<byte> frame)
            {
                consumed = 0;
                frame = default(ReadOnlySequence<byte>);
                throw _error;
            }

            public void Reset()
            {
            }
        }

        private sealed class TestRawStream : IRawByteStream
        {
            private readonly Pipe _pipe;
            private readonly List<byte[]> _sent = new List<byte[]>();
            private string _closedReason;

            public TestRawStream(string id)
            {
                ChannelId = id;
                _pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 4 * 1024 * 1024));
            }

            public string ChannelId { get; }

            public Pipe Pipe
            {
                get { return _pipe; }
            }

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

            public string ClosedReason
            {
                get { return Volatile.Read(ref _closedReason); }
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
                Volatile.Write(ref _closedReason, reason);
                return Task.CompletedTask;
            }
        }

        private sealed class TestChannel : IChannel
        {
            private readonly ConcurrentDictionary<string, object> _attributes =
                new ConcurrentDictionary<string, object>();

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
                Volatile.Write(ref _active, 0);
                return Task.CompletedTask;
            }
        }

        private sealed class RecordingExceptionHandler : InboundHandlerAdapter
        {
            public readonly TaskCompletionSource<Exception> Caught =
                new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                return Task.CompletedTask;
            }

            public override Task ChannelExceptionCaughtAsync(IChannelHandlerContext ctx, Exception ex)
            {
                Caught.TrySetResult(ex);
                return Task.CompletedTask;
            }
        }

        private sealed class ResponseThenCloseHandler : InboundHandlerAdapter
        {
            public const string CloseReason = "handler-protocol-response";

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                return Task.CompletedTask;
            }

            public override async Task ChannelExceptionCaughtAsync(IChannelHandlerContext ctx, Exception ex)
            {
                await ctx.Pipeline.WriteAsync(new RawFrame(new byte[] { 0x0F, 0x0E }, null)).ConfigureAwait(false);
                await ctx.CloseAfterWriteAsync(CloseReason).ConfigureAwait(false);
            }
        }

        private sealed class DrainOnlyHandler : InboundHandlerAdapter
        {
            public const string CloseReason = "handler-drain";

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                return Task.CompletedTask;
            }

            public override async Task ChannelExceptionCaughtAsync(IChannelHandlerContext ctx, Exception ex)
            {
                await ctx.CloseAfterWriteAsync(CloseReason).ConfigureAwait(false);
            }
        }
    }
}