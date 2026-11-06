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
*文件名： InlineDispatchTests
*版本号： v26.11.5.1
*唯一标识：ec71849e-c8dc-4206-8967-3f6a1cd28e8b
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 14:30:37
*描述：同步内联分发的行为用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 14:30:37
*修改人： yswenli
*版本号： v26.11.5.1
*描述：同步内联分发的行为用例
*
*****************************************************************************/
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.Caching;
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Runtime
{
    /// <summary>
    /// 内联分发：顺序、挂起回落、异常路由、超时模式的降级与引用配平。
    /// </summary>
    [TestClass]
    public class InlineDispatchTests
    {
        private const int WaitTimeoutMs = 3000;

        /// <summary>
        /// 同步处理器时所有帧都在接收线程内联执行，顺序不变、队列不积压、并发额度回到满额
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task InlineMode_SynchronousHandler_AllFramesInlineInOrder()
        {
            const int FrameCount = 100;

            var options = NewOptions(ChannelDispatchMode.Inline, queueCapacity: 64, maxConcurrent: 4);
            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawByteStream("inline-sync");
                var handler = new RecordingHandler();
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(stream, new TestChannel("inline-sync"), options, CancellationToken.None);
                executor.Attach(pipeline);

                var baselineNet = NetRented();

                try
                {
                    executor.Start();

                    var parts = new byte[FrameCount][];
                    for (var i = 0; i < FrameCount; i++)
                        parts[i] = Frame((byte)i, (byte)i);

                    await WriteAsync(stream, Concat(parts));
                    await WaitUntilAsync(() => handler.Types.Count == FrameCount, "内联未处理完全部帧");

                    CollectionAssert.AreEqual(
                        Enumerable.Range(0, FrameCount).Select(i => (byte)i).ToArray(),
                        handler.Types.ToArray(),
                        "内联分发打乱了帧顺序");

                    Assert.AreEqual(FrameCount, executor.InlineDispatchedCount, "全部帧都应走内联路径");
                    Assert.AreEqual(0, executor.QueuedCount, "同步处理器不应在队列里留消息");
                    Assert.AreEqual(options.MaxConcurrentMessages, executor.AvailableConcurrency, "内联后并发额度未回到满额");

                    await WaitUntilAsync(() => NetRented() == baselineNet, "内联执行后引用未配平");
                }
                finally
                {
                    await StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 处理器在第二帧挂起时后续帧回落到排队分发，全部帧仍按序交付且额度最终归位
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task InlineMode_HandlerSuspends_FallsBackToQueueAndKeepsOrder()
        {
            const int FrameCount = 10;
            const byte SuspendType = 1;

            var options = NewOptions(ChannelDispatchMode.Inline, queueCapacity: 64, maxConcurrent: 1);
            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawByteStream("inline-suspend");
                var handler = new SuspendingHandler(SuspendType);
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(stream, new TestChannel("inline-suspend"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    var parts = new byte[FrameCount][];
                    for (var i = 0; i < FrameCount; i++)
                        parts[i] = Frame((byte)i, (byte)i);

                    await WriteAsync(stream, Concat(parts));

                    // 等挂起的那帧进入处理器，同时确认其余帧已经回落到队列。
                    await handler.Suspended.Task.WaitAsync(TimeSpan.FromSeconds(3));
                    await WaitUntilAsync(() => executor.QueuedCount > 0, "挂起后未回落到排队分发");

                    handler.Release.TrySetResult(true);

                    await WaitUntilAsync(() => handler.Types.Count == FrameCount, "挂起释放后未处理完全部帧");

                    CollectionAssert.AreEqual(
                        Enumerable.Range(0, FrameCount).Select(i => (byte)i).ToArray(),
                        handler.Types.ToArray(),
                        "内联与排队混合时分发顺序错乱");

                    var inline = executor.InlineDispatchedCount;
                    Assert.IsTrue(inline >= 1, "挂起前的帧应已内联执行");
                    Assert.IsTrue(inline < FrameCount, "挂起后剩余帧不应继续内联");

                    await WaitUntilAsync(
                        () => executor.QueuedCount == 0 && executor.AvailableConcurrency == options.MaxConcurrentMessages,
                        "全部处理完后队列或额度未归位");
                }
                finally
                {
                    await StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 同步处理器抛异常时上报到链尾，连接继续处理后续帧
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task InlineMode_HandlerThrows_RoutesToExceptionCaughtAndContinues()
        {
            var options = NewOptions(ChannelDispatchMode.Inline, queueCapacity: 8, maxConcurrent: 2);
            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawByteStream("inline-throw");
                var throwing = new ThrowingHandler(1);
                var terminal = new ExceptionCollectingHandler();
                pipeline.AddLast(throwing);
                pipeline.AddLast(terminal);

                var executor = new ChannelPipelineExecutor(stream, new TestChannel("inline-throw"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();
                    await WriteAsync(stream, Concat(Frame(1, 1), Frame(2, 2), Frame(3, 3)));

                    await WaitUntilAsync(() => throwing.Types.Count == 2, "异常帧之后的帧未继续处理");
                    await WaitUntilAsync(() => terminal.Errors.Count == 1, "异常未上报到链尾");

                    Assert.AreEqual("inline-boom", terminal.Errors.First().Message, "上报的异常不符");
                    CollectionAssert.AreEqual(new byte[] { 2, 3 }, throwing.Types.ToArray(), "异常后帧顺序不符");
                    Assert.AreEqual(0, executor.QueuedCount, "异常后不应在队列里留消息");
                }
                finally
                {
                    await StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 配置消息超时时内联模式自动降级为排队分发，消息仍全部交付
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task InlineMode_WithMessageTimeout_FallsBackToQueuedDispatch()
        {
            var options = new ChannelPipelineOptions
            {
                Splitter = LengthFieldSplitter.Default(1024),
                QueueCapacity = 16,
                MaxConcurrentMessages = 2,
                MessageTimeout = TimeSpan.FromMilliseconds(200),
                DispatchMode = ChannelDispatchMode.Inline,
                Guard = NullChannelGuard.Instance
            };

            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawByteStream("inline-timeout");
                var handler = new RecordingHandler();
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(stream, new TestChannel("inline-timeout"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();
                    await WriteAsync(stream, Concat(Frame(1, 1), Frame(2, 2), Frame(3, 3)));

                    await WaitUntilAsync(() => handler.Types.Count == 3, "超时模式下未处理完帧");
                    Assert.AreEqual(0, executor.InlineDispatchedCount, "配置消息超时时不应内联分发");
                }
                finally
                {
                    await StopAsync(executor);
                }
            }
        }

        private static ChannelPipelineOptions NewOptions(ChannelDispatchMode mode, int queueCapacity, int maxConcurrent)
        {
            return new ChannelPipelineOptions
            {
                Splitter = LengthFieldSplitter.Default(1024),
                QueueCapacity = queueCapacity,
                MaxConcurrentMessages = maxConcurrent,
                DispatchMode = mode,
                Guard = NullChannelGuard.Instance
            };
        }

        private static long NetRented()
        {
            var stats = MemoryPoolManager.GetStatistics();
            return (stats.SmallPoolRented + stats.MediumPoolRented + stats.LargePoolRented)
                - (stats.SmallPoolReturned + stats.MediumPoolReturned + stats.LargePoolReturned);
        }

        private static async Task StopAsync(ChannelPipelineExecutor executor)
        {
            await executor.BeginShutdown("test-done");
            await executor.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        }

        private static async Task WaitUntilAsync(Func<bool> condition, string message)
        {
            var deadline = Environment.TickCount64 + WaitTimeoutMs;
            while (!condition())
            {
                if (Environment.TickCount64 > deadline)
                    Assert.Fail("等待超时：" + message);
                await Task.Delay(10);
            }
        }

        private static byte[] Frame(byte type, params byte[] body)
        {
            var wire = new byte[FrameConstants.P_Head + body.Length];
            BinaryPrimitives.WriteInt64LittleEndian(wire.AsSpan(0, FrameConstants.P_LEN), body.Length);
            wire[FrameConstants.P_LEN] = type;
            body.CopyTo(wire, FrameConstants.P_Head);
            return wire;
        }

        private static byte[] Concat(params byte[][] parts)
        {
            var total = parts.Sum(part => part.Length);
            var result = new byte[total];
            var offset = 0;
            foreach (var part in parts)
            {
                Buffer.BlockCopy(part, 0, result, offset, part.Length);
                offset += part.Length;
            }

            return result;
        }

        private static async Task WriteAsync(TestRawByteStream stream, byte[] data)
        {
            await stream.Pipe.Writer.WriteAsync(data);
            await stream.Pipe.Writer.FlushAsync();
        }

        private sealed class RecordingHandler : InboundHandlerAdapter
        {
            // 用例构造的帧把类型字节放在帧体首位，处理器只按首字节记录顺序。
            private readonly ConcurrentQueue<byte> _types = new ConcurrentQueue<byte>();

            public IReadOnlyCollection<byte> Types
            {
                get { return _types; }
            }

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                _types.Enqueue(((PooledMessage)msg).Frame.Span[0]);
                return Task.CompletedTask;
            }
        }

        private sealed class SuspendingHandler : InboundHandlerAdapter
        {
            private readonly byte _suspendType;
            private int _suspended;

            public SuspendingHandler(byte suspendType)
            {
                _suspendType = suspendType;
            }

            private readonly ConcurrentQueue<byte> _types = new ConcurrentQueue<byte>();

            /// <summary>已交付帧的类型字节，按到达顺序记录。</summary>
            public IReadOnlyCollection<byte> Types
            {
                get { return _types; }
            }

            public readonly TaskCompletionSource<bool> Suspended =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public readonly TaskCompletionSource<bool> Release =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var pooled = (PooledMessage)msg;
                _types.Enqueue(pooled.Frame.Span[0]);

                // 只让指定类型的那一帧挂起；消息背衬在 await 期间保持有效。
                if (pooled.Frame.Span[0] == _suspendType
                    && Interlocked.CompareExchange(ref _suspended, 1, 0) == 0)
                {
                    Suspended.TrySetResult(true);
                    await Release.Task.ConfigureAwait(false);
                }
            }
        }

        private sealed class ThrowingHandler : InboundHandlerAdapter
        {
            private readonly byte _throwType;

            public ThrowingHandler(byte throwType)
            {
                _throwType = throwType;
            }

            public readonly ConcurrentQueue<byte> Types = new ConcurrentQueue<byte>();

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var type = ((PooledMessage)msg).Frame.Span[0];
                if (type == _throwType)
                    throw new InvalidOperationException("inline-boom");

                Types.Enqueue(type);
                return Task.CompletedTask;
            }
        }

        private sealed class ExceptionCollectingHandler : InboundHandlerAdapter
        {
            public readonly ConcurrentQueue<Exception> Errors = new ConcurrentQueue<Exception>();

            public override Task ChannelExceptionCaughtAsync(IChannelHandlerContext ctx, Exception ex)
            {
                Errors.Enqueue(ex);
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

        private sealed class TestRawByteStream : IRawByteStream
        {
            private readonly Pipe _pipe;

            public TestRawByteStream(string id)
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
    }
}