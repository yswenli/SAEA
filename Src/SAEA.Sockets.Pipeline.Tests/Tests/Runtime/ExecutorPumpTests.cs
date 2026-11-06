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
*文件名： ExecutorPumpTests
*版本号： v26.11.5.1
*唯一标识：4319f754-47d7-4e40-88b1-e213473d0e4b
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：ExecutorPumpTests
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：ExecutorPumpTests
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
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Runtime
{
    /// <summary>
    /// 接收泵：分帧入库、背压暂停与恢复、缓冲上限与异常关闭。
    /// </summary>
    [TestClass]
    public class ExecutorPumpTests
    {
        private const int WaitTimeoutMs = 3000;

        /// <summary>
        /// 单帧到达时 handler 收到正确的帧体，消息在处理结束的 finally 中被释放且队列清空
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task SingleFrame_ArrivesAtHandlerAndIsReleased()
        {
            var options = NewOptions();
            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawByteStream("pump-single");
                var handler = new RecordingHandler();
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(stream, new TestChannel("pump-single"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();
                    await WriteAsync(stream, Frame(7, 1, 2, 3));

                    await WaitUntilAsync(() => handler.Frames.Count == 1, "handler 未收到单帧");

                    CollectionAssert.AreEqual(new byte[] { 7, 1, 2, 3 }, handler.Frames.ElementAt(0));

                    var message = handler.Messages.ElementAt(0);
                    await WaitUntilAsync(() => message.IsReleased, "消息未在执行包 finally 中释放");

                    Assert.AreEqual(0, executor.Traffic().QueuedMessages, "消费后队列应为空");
                }
                finally
                {
                    await StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 一次写入的三个粘包帧按写入顺序交付，且未触发背压时不调用恢复读取
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task ThreeCoalescedFrames_AreProcessedInOrder()
        {
            var options = NewOptions();
            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawByteStream("pump-three");
                var handler = new RecordingHandler();
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(stream, new TestChannel("pump-three"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();
                    await WriteAsync(stream, Concat(Frame(1, 10), Frame(2, 20, 21), Frame(3, 30, 31, 32)));

                    await WaitUntilAsync(() => handler.Frames.Count == 3, "handler 未收到 3 帧");

                    CollectionAssert.AreEqual(new byte[] { 1, 10 }, handler.Frames.ElementAt(0));
                    CollectionAssert.AreEqual(new byte[] { 2, 20, 21 }, handler.Frames.ElementAt(1));
                    CollectionAssert.AreEqual(new byte[] { 3, 30, 31, 32 }, handler.Frames.ElementAt(2));
                    Assert.AreEqual(0, stream.ResumeCount, "未触发背压时不应恢复读取");
                }
                finally
                {
                    await StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 队列（容量 2）写满时只暂停一次，暂停期间不再读取且队列长度不变，解开阻塞后 5 帧按序处理并在低水位恢复读取一次
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task FullQueue_PausesOnce_ResumesAtLowWatermark_AndKeepsOrder()
        {
            var options = new ChannelPipelineOptions
            {
                Splitter = LengthFieldSplitter.Default(1024),
                QueueCapacity = 2,
                MaxConcurrentMessages = 1
            };

            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawByteStream("pump-backpressure");
                var handler = new BlockingFirstHandler();
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(stream, new TestChannel("pump-backpressure"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    // 先让消费侧进入"一条在途 + 一条等并发额度"的状态，保证队列满后不会再有出队动作。
                    await WriteAsync(stream, Concat(Frame(1, 1), Frame(2, 2)));
                    await WaitUntilAsync(() => handler.FirstEntered.Task.IsCompleted, "首条消息未进入 handler");
                    await WaitUntilAsync(() => executor.Traffic().QueuedMessages == 0, "消费侧未取走前两帧");

                    // 再写 3 帧：队列容量 2，第 3 帧必然 TryWrite 失败并触发暂停。
                    await WriteAsync(stream, Concat(Frame(3, 3), Frame(4, 4), Frame(5, 5)));

                    await WaitUntilAsync(() => Volatile.Read(ref stream.PauseCount) == 1, "队列满未触发暂停");
                    Assert.AreEqual(2, executor.Traffic().QueuedMessages, "暂停时队列应保持满额");

                    // 暂停期间泵不得再调用 ReadAsync：无新数据进入 accumulator。
                    var totalAtPause = executor.Traffic().TotalReceivedBytes;
                    await Task.Delay(200);
                    Assert.AreEqual(1, Volatile.Read(ref stream.PauseCount), "暂停状态被重复触发");
                    Assert.AreEqual(2, executor.Traffic().QueuedMessages, "暂停期间队列长度不应变化");
                    Assert.AreEqual(totalAtPause, executor.Traffic().TotalReceivedBytes, "暂停期间不应继续读取");

                    handler.Release.TrySetResult(true);

                    await WaitUntilAsync(() => handler.Frames.Count == 5, "剩余帧未继续处理");
                    CollectionAssert.AreEqual(new byte[] { 1, 1 }, handler.Frames.ElementAt(0));
                    CollectionAssert.AreEqual(new byte[] { 2, 2 }, handler.Frames.ElementAt(1));
                    CollectionAssert.AreEqual(new byte[] { 3, 3 }, handler.Frames.ElementAt(2));
                    CollectionAssert.AreEqual(new byte[] { 4, 4 }, handler.Frames.ElementAt(3));
                    CollectionAssert.AreEqual(new byte[] { 5, 5 }, handler.Frames.ElementAt(4));

                    Assert.AreEqual(1, Volatile.Read(ref stream.ResumeCount), "低水位恢复应只发生一次");
                }
                finally
                {
                    handler.Release.TrySetResult(true);
                    await StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 包头只到一部分时不产帧，剩余字节到齐后产出一帧且帧体与原始负载一致
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task PartialHeader_DoesNotProduceFrameUntilRemainderArrives()
        {
            var options = NewOptions();
            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawByteStream("pump-partial");
                var handler = new RecordingHandler();
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(stream, new TestChannel("pump-partial"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    var wire = Frame(9, 1, 2, 3, 4, 5, 6, 7, 8, 9);
                    await WriteAsync(stream, wire.Take(5).ToArray());
                    await Task.Delay(200);

                    Assert.AreEqual(0, handler.Frames.Count, "半包头不得提前切帧");

                    await WriteAsync(stream, wire.Skip(5).ToArray());
                    await WaitUntilAsync(() => handler.Frames.Count == 1, "剩余字节到齐后未产出帧");

                    CollectionAssert.AreEqual(wire.Skip(8).ToArray(), handler.Frames.ElementAt(0));
                }
                finally
                {
                    await StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 缓冲字节超过 MaxBufferedBytes 时以 buffered-bytes-limit 关闭传输并结束执行器
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task BufferedBytesOverLimit_ShutsDownAndClosesStream()
        {
            var options = new ChannelPipelineOptions
            {
                Splitter = new NeverSplitSplitter(),
                MaxBufferedBytes = 17,
                QueueCapacity = 8,
                MaxConcurrentMessages = 1
            };

            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawByteStream("pump-buffered-limit");
                var executor = new ChannelPipelineExecutor(stream, new TestChannel("pump-buffered-limit"), options, CancellationToken.None);
                executor.Attach(pipeline);

                executor.Start();
                await WriteAsync(stream, new byte[64]);

                var reason = await stream.Closed.Task.WaitAsync(TimeSpan.FromSeconds(1));
                Assert.AreEqual("buffered-bytes-limit", reason);
                await executor.Completion.WaitAsync(TimeSpan.FromSeconds(1));
            }
        }

        /// <summary>
        /// 取消挂起读后泵停止，以 transport-closed 关闭传输且执行器正常结束、不向外抛异常
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task CancelPendingRead_StopsPumpAndCompletesWithoutThrowing()
        {
            var options = NewOptions();
            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawByteStream("pump-cancelled");
                var executor = new ChannelPipelineExecutor(stream, new TestChannel("pump-cancelled"), options, CancellationToken.None);
                executor.Attach(pipeline);

                executor.Start();
                await Task.Delay(100);

                stream.Pipe.Reader.CancelPendingRead();

                var reason = await stream.Closed.Task.WaitAsync(TimeSpan.FromSeconds(1));
                Assert.AreEqual("transport-closed", reason);
                await executor.Completion.WaitAsync(TimeSpan.FromSeconds(1));
            }
        }

        /// <summary>
        /// 守卫拒绝读取时守卫至少被调用一次，并以 guard-rejected 关闭传输后结束执行器
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task GuardRejection_ClosesWithObservableReason()
        {
            var guard = new TestGuard(false);
            var options = NewOptions(guard: guard);
            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawByteStream("pump-guard");
                var executor = new ChannelPipelineExecutor(stream, new TestChannel("pump-guard"), options, CancellationToken.None);
                executor.Attach(pipeline);

                executor.Start();
                await WriteAsync(stream, Frame(1, 1));

                var reason = await stream.Closed.Task.WaitAsync(TimeSpan.FromSeconds(1));
                Assert.AreEqual("guard-rejected", reason);
                Assert.IsTrue(guard.Calls >= 1, "守卫未被调用");
                await executor.Completion.WaitAsync(TimeSpan.FromSeconds(1));
            }
        }

        private static ChannelPipelineOptions NewOptions(int queueCapacity = 16, int maxConcurrent = 1, IChannelGuard guard = null)
        {
            return new ChannelPipelineOptions
            {
                Splitter = LengthFieldSplitter.Default(1024),
                QueueCapacity = queueCapacity,
                MaxConcurrentMessages = maxConcurrent,
                Guard = guard ?? NullChannelGuard.Instance
            };
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
            public readonly ConcurrentQueue<PooledMessage> Messages = new ConcurrentQueue<PooledMessage>();
            public readonly ConcurrentQueue<byte[]> Frames = new ConcurrentQueue<byte[]>();

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var pooled = (PooledMessage)msg;
                Messages.Enqueue(pooled);
                Frames.Enqueue(pooled.Frame.ToArray());
                return Task.CompletedTask;
            }
        }

        private sealed class BlockingFirstHandler : InboundHandlerAdapter
        {
            private int _calls;

            public readonly ConcurrentQueue<byte[]> Frames = new ConcurrentQueue<byte[]>();
            public readonly TaskCompletionSource<bool> FirstEntered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly TaskCompletionSource<bool> Release =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                Frames.Enqueue(((PooledMessage)msg).Frame.ToArray());

                if (Interlocked.Increment(ref _calls) == 1)
                {
                    FirstEntered.TrySetResult(true);
                    await Release.Task.ConfigureAwait(false);
                }
            }
        }

        private sealed class NeverSplitSplitter : IFrameSplitter
        {
            public int MaxFrameLength
            {
                get { return 8; }
            }

            public bool TrySplit(ReadOnlySequence<byte> buffer, out long consumed, out ReadOnlySequence<byte> frame)
            {
                consumed = 0;
                frame = default(ReadOnlySequence<byte>);
                return false;
            }

            public void Reset()
            {
            }
        }

        private sealed class TestGuard : IChannelGuard
        {
            private readonly bool _allow;
            private int _calls;

            public TestGuard(bool allow)
            {
                _allow = allow;
            }

            public int Calls
            {
                get { return Volatile.Read(ref _calls); }
            }

            public bool Allow(in ChannelTraffic traffic)
            {
                Interlocked.Increment(ref _calls);
                return _allow;
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
            private readonly List<byte[]> _sent = new List<byte[]>();

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

            public int PauseCount;
            public int ResumeCount;

            public readonly TaskCompletionSource<string> Closed =
                new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

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
                Interlocked.Increment(ref PauseCount);
            }

            public void Resume()
            {
                Interlocked.Increment(ref ResumeCount);
            }

            public Task CloseAsync(string reason)
            {
                Closed.TrySetResult(reason);
                return Task.CompletedTask;
            }
        }
    }
}