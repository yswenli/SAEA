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
*文件名： ExecutorShutdownTests
*版本号： v26.11.5.1
*唯一标识：98384e99-5727-40e5-8959-08d35410a630
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：ExecutorShutdownTests
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：ExecutorShutdownTests
*
*****************************************************************************/
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Runtime
{
    /// <summary>
    /// 执行器关闭与超时：单消息超时、外部关闭排空与传输取消。
    /// </summary>
    [TestClass]
    public class ExecutorShutdownTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(2);

        /// <summary>
        /// 单条消息超时后链尾收到 MessageTimeoutException，执行器与传输层都保持活跃（不因单消息超时关闭连接）
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task SingleMessageTimeout_ReportsTimeoutAndKeepsConnectionActive()
        {
            var options = NewOptions(maxConcurrent: 2, messageTimeout: TimeSpan.FromMilliseconds(100));

            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawStream("shutdown-timeout");
                var channel = new DefaultChannel("shutdown-timeout", null);
                var handler = new TimeoutHandler();
                var tail = new ExceptionTail();
                pipeline.AddLast(handler);
                pipeline.AddLast(tail);

                var executor = new ChannelPipelineExecutor(stream, channel, options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();
                    await WriteAsync(stream, Concat(Frame(1), Frame(2)));

                    await handler.TimedOut.Task.WaitAsync(WaitTimeout);
                    await handler.Completed.Task.WaitAsync(WaitTimeout);
                    await WaitUntilAsync(
                        () => tail.Exceptions.Any(ex => ex is MessageTimeoutException),
                        "链尾未收到 MessageTimeoutException");

                    Assert.IsTrue(executor.IsActive, "单消息超时不应关闭连接");
                    Assert.IsFalse(stream.Closed.Task.IsCompleted, "单消息超时不应关闭传输层");
                }
                finally
                {
                    await StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 处理器忽略取消令牌时不会被强制中止：实际耗时不低于其自身休眠时长，不上报超时且连接保持活跃
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task HandlerIgnoringToken_IsNotKilled_AndFinishesNormally()
        {
            var options = NewOptions(maxConcurrent: 1, messageTimeout: TimeSpan.FromMilliseconds(50));

            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawStream("shutdown-ignore-token");
                var channel = new DefaultChannel("shutdown-ignore-token", null);
                var handler = new BusyHandler(300);
                var tail = new ExceptionTail();
                pipeline.AddLast(handler);
                pipeline.AddLast(tail);

                var executor = new ChannelPipelineExecutor(stream, channel, options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();
                    var watch = Stopwatch.StartNew();
                    await WriteAsync(stream, Frame(1));
                    await handler.Done.Task.WaitAsync(WaitTimeout);
                    watch.Stop();

                    Assert.IsTrue(watch.ElapsedMilliseconds >= 250,
                        "忽略令牌的处理器被提前中止，实际耗时 " + watch.ElapsedMilliseconds + "ms");
                    Assert.AreEqual(0, tail.Exceptions.Count, "处理器正常结束不应上报超时");
                    Assert.IsTrue(executor.IsActive, "忽略令牌不应导致连接被关闭");
                }
                finally
                {
                    await StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 外部收口后泵与消费者停止：在途消息收到取消、队列排空、并发额度回满，按给定原因关闭传输并把执行器与通道置为非活跃，之后不再启动新消息
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task ExternalShutdown_StopsPumpAndConsumer_DrainsQueue_AndClosesStream()
        {
            var options = NewOptions(maxConcurrent: 1, queueCapacity: 8);

            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawStream("shutdown-external");
                var channel = new DefaultChannel("shutdown-external", null);
                var handler = new CancelAwareHandler();
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(stream, channel, options, CancellationToken.None);
                executor.Attach(pipeline);

                executor.Start();
                await WriteAsync(stream, Concat(Frame(1), Frame(2), Frame(3)));
                await handler.Entered.Task.WaitAsync(WaitTimeout);
                await WaitUntilAsync(() => executor.QueuedCount >= 1, "队列未积压待处理消息");

                await executor.BeginShutdown("test");
                await executor.Completion.WaitAsync(WaitTimeout);

                Assert.IsTrue(handler.Cancelled, "在途消息未收到连接取消");
                Assert.AreEqual(0, executor.QueuedCount, "队列未被排空");
                Assert.AreEqual(1, executor.AvailableConcurrency, "并发额度未回到满额");
                Assert.AreEqual("test", await stream.Closed.Task.WaitAsync(WaitTimeout), "关闭原因不匹配");
                Assert.IsFalse(executor.IsActive, "执行器仍为活跃状态");
                Assert.IsFalse(channel.IsActive, "通道仍为活跃状态");

                await Task.Delay(50);
                Assert.AreEqual(1, handler.Entries, "收口后消费者仍启动了新消息");
            }
        }

        /// <summary>
        /// 收口后在途消息的回包抛 ChannelClosedException 并传播到链尾，业务副作用已完成但无数据交给传输层
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task ReplyAfterShutdown_ThrowsChannelClosed_SurfacesAtTail_AndSendsNothing()
        {
            var options = NewOptions(
                maxConcurrent: 1,
                drainPolicy: DisconnectDrainPolicy.DrainInFlight,
                drainTimeout: TimeSpan.FromSeconds(2));

            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawStream("shutdown-reply");
                var channel = new DefaultChannel("shutdown-reply", null);
                var handler = new PostShutdownWriteHandler();
                var tail = new ExceptionTail();
                pipeline.AddLast(handler);
                pipeline.AddLast(tail);

                var executor = new ChannelPipelineExecutor(stream, channel, options, CancellationToken.None);
                executor.Attach(pipeline);

                executor.Start();
                await WriteAsync(stream, Frame(1));
                await handler.Entered.Task.WaitAsync(WaitTimeout);

                await executor.BeginShutdown("reply-closed");
                handler.Release.TrySetResult(true);
                await executor.Completion.WaitAsync(WaitTimeout);

                Assert.IsInstanceOfType(handler.WriteError, typeof(ChannelClosedException),
                    "收口后的回包未抛 ChannelClosedException");
                Assert.AreEqual(1, handler.SideEffects, "业务副作用应已完成");
                Assert.IsTrue(tail.Exceptions.Any(ex => ex is ChannelClosedException),
                    "ChannelClosedException 未沿链传播到链尾");
                Assert.AreEqual(0, stream.SentCount, "收口后不应再有数据交给传输层");
            }
        }

        /// <summary>
        /// DrainInFlight 策略会等在途消息跑完再完成收口；DrainTimeout 到点则放弃等待直接完成 Completion，且不阻塞在途消息后续结束
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task DrainInFlight_WaitsForRunningMessage_AndGivesUpAfterTimeout()
        {
            var waitOptions = NewOptions(
                maxConcurrent: 1,
                drainPolicy: DisconnectDrainPolicy.DrainInFlight,
                drainTimeout: TimeSpan.FromSeconds(2));

            using (var pipeline = ChannelPipeline.Create(waitOptions))
            {
                var stream = new TestRawStream("drain-wait");
                var channel = new DefaultChannel("drain-wait", null);
                var handler = new DelayedHandler(200);
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(stream, channel, waitOptions, CancellationToken.None);
                executor.Attach(pipeline);

                executor.Start();
                await WriteAsync(stream, Frame(1));
                await handler.Entered.Task.WaitAsync(WaitTimeout);

                await executor.BeginShutdown("drain-wait-done");
                await executor.Completion.WaitAsync(WaitTimeout);

                Assert.IsTrue(handler.Done.Task.IsCompleted, "DrainInFlight 未等在途消息跑完");
                Assert.AreEqual(1, handler.Entries, "在途消息未执行完成");
            }

            var timeoutOptions = NewOptions(
                maxConcurrent: 1,
                drainPolicy: DisconnectDrainPolicy.DrainInFlight,
                drainTimeout: TimeSpan.FromMilliseconds(100));

            using (var pipeline = ChannelPipeline.Create(timeoutOptions))
            {
                var stream = new TestRawStream("drain-timeout");
                var channel = new DefaultChannel("drain-timeout", null);
                var handler = new GatedHandler();
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(stream, channel, timeoutOptions, CancellationToken.None);
                executor.Attach(pipeline);

                executor.Start();
                await WriteAsync(stream, Frame(1));
                await handler.Entered.Task.WaitAsync(WaitTimeout);

                await executor.BeginShutdown("drain-timeout-given-up");
                await executor.Completion.WaitAsync(WaitTimeout);

                Assert.IsFalse(handler.Done.Task.IsCompleted, "DrainTimeout 到点后不应继续等待在途消息");
                Assert.IsTrue(executor.Completion.IsCompleted, "超时放弃后 Completion 仍应完成");

                handler.Release.TrySetResult(true);
                await handler.Done.Task.WaitAsync(WaitTimeout);
            }
        }

        /// <summary>
        /// CancelPending 策略取消在途消息并完成收口，取消后并发额度回到满额
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task CancelPending_CancelsInFlightMessage_AndCompletes()
        {
            var options = NewOptions(maxConcurrent: 1, drainPolicy: DisconnectDrainPolicy.CancelPending);

            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawStream("cancel-pending");
                var channel = new DefaultChannel("cancel-pending", null);
                var handler = new CancelAwareHandler();
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(stream, channel, options, CancellationToken.None);
                executor.Attach(pipeline);

                executor.Start();
                await WriteAsync(stream, Frame(1));
                await handler.Entered.Task.WaitAsync(WaitTimeout);

                await executor.BeginShutdown("cancel-pending-done");
                await executor.Completion.WaitAsync(WaitTimeout);

                Assert.IsTrue(handler.Cancelled, "CancelPending 未取消在途消息");
                Assert.AreEqual(1, executor.AvailableConcurrency, "取消后并发额度未回到满额");
            }
        }

        /// <summary>
        /// 重复 Dispose 幂等：收口仍能完成，并以 disposed 原因关闭传输
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Dispose_IsIdempotent_AndCompletes()
        {
            var options = NewOptions();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawStream("dispose-twice");
                var channel = new DefaultChannel("dispose-twice", null);
                var executor = new ChannelPipelineExecutor(stream, channel, options, CancellationToken.None);
                executor.Attach(pipeline);

                executor.Start();
                await Task.Delay(50);

                executor.Dispose();
                executor.Dispose();

                await executor.Completion.WaitAsync(WaitTimeout);
                Assert.IsTrue(executor.Completion.IsCompleted, "两次 Dispose 后收口未完成");
                Assert.AreEqual("disposed", await stream.Closed.Task.WaitAsync(WaitTimeout), "关闭原因不匹配");
            }
        }

        /// <summary>
        /// 传输层取消令牌触发收口：Completion 完成、以 transport-cancelled 关闭传输且执行器转为非活跃
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task TransportCancellation_TriggersShutdown_WithTransportCancelledReason()
        {
            var options = NewOptions();
            var transportCts = new CancellationTokenSource();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawStream("transport-cancel");
                var channel = new DefaultChannel("transport-cancel", null);
                var executor = new ChannelPipelineExecutor(stream, channel, options, transportCts.Token);
                executor.Attach(pipeline);

                executor.Start();
                await Task.Delay(50);

                transportCts.Cancel();

                await executor.Completion.WaitAsync(TimeSpan.FromMilliseconds(500));
                Assert.AreEqual("transport-cancelled",
                    await stream.Closed.Task.WaitAsync(TimeSpan.FromMilliseconds(500)),
                    "传输层取消失败或关闭原因不匹配");
                Assert.IsFalse(executor.IsActive, "传输层取消后执行器仍为活跃状态");
            }
        }

        private static ChannelPipelineOptions NewOptions(
            int queueCapacity = 16,
            int maxConcurrent = 2,
            TimeSpan? messageTimeout = null,
            DisconnectDrainPolicy drainPolicy = DisconnectDrainPolicy.CancelPending,
            TimeSpan? drainTimeout = null)
        {
            return new ChannelPipelineOptions
            {
                Splitter = LengthFieldSplitter.Default(1024),
                QueueCapacity = queueCapacity,
                MaxConcurrentMessages = maxConcurrent,
                MessageTimeout = messageTimeout ?? TimeSpan.Zero,
                DrainPolicy = drainPolicy,
                DrainTimeout = drainTimeout ?? TimeSpan.FromSeconds(2),
                Guard = NullChannelGuard.Instance
            };
        }

        private static async Task StopAsync(ChannelPipelineExecutor executor)
        {
            await executor.BeginShutdown("test-done");
            await executor.Completion.WaitAsync(WaitTimeout);
        }

        private static async Task WaitUntilAsync(Func<bool> condition, string message)
        {
            var watch = Stopwatch.StartNew();
            while (!condition())
            {
                if (watch.Elapsed > WaitTimeout)
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

        private static async Task WriteAsync(TestRawStream stream, byte[] data)
        {
            await stream.Pipe.Writer.WriteAsync(data);
            await stream.Pipe.Writer.FlushAsync();
        }

        private sealed class TimeoutHandler : InboundHandlerAdapter
        {
            public readonly TaskCompletionSource<bool> TimedOut =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public readonly TaskCompletionSource<bool> Completed =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var type = ((PooledMessage)msg).Frame.Span[0];
                if (type == 1)
                {
                    try
                    {
                        await Task.Delay(Timeout.Infinite, ctx.CancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        TimedOut.TrySetResult(true);
                        throw;
                    }
                }
                else
                {
                    await Task.Delay(20);
                    Completed.TrySetResult(true);
                }
            }
        }

        private sealed class BusyHandler : InboundHandlerAdapter
        {
            private readonly int _milliseconds;

            public readonly TaskCompletionSource<bool> Done =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public BusyHandler(int milliseconds)
            {
                _milliseconds = milliseconds;
            }

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                Thread.Sleep(_milliseconds);
                Done.TrySetResult(true);
                return Task.CompletedTask;
            }
        }

        private sealed class CancelAwareHandler : InboundHandlerAdapter
        {
            private int _entries;

            public readonly TaskCompletionSource<bool> Entered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public volatile bool Cancelled;

            public int Entries
            {
                get { return Volatile.Read(ref _entries); }
            }

            public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                Interlocked.Increment(ref _entries);
                Entered.TrySetResult(true);

                try
                {
                    await Task.Delay(Timeout.Infinite, ctx.CancellationToken);
                }
                catch (OperationCanceledException)
                {
                    Cancelled = true;
                }
            }
        }

        private sealed class PostShutdownWriteHandler : InboundHandlerAdapter
        {
            public readonly TaskCompletionSource<bool> Entered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public readonly TaskCompletionSource<bool> Release =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public Exception WriteError;
            public int SideEffects;

            public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                Entered.TrySetResult(true);
                await Release.Task;

                Interlocked.Increment(ref SideEffects);

                try
                {
                    await ctx.WriteAsync(new TransportFrame(new byte[] { 0xAA }, null));
                }
                catch (Exception ex)
                {
                    WriteError = ex;
                    throw;
                }
            }
        }

        private sealed class DelayedHandler : InboundHandlerAdapter
        {
            private readonly int _milliseconds;

            public readonly TaskCompletionSource<bool> Entered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public readonly TaskCompletionSource<bool> Done =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public volatile int Entries;

            public DelayedHandler(int milliseconds)
            {
                _milliseconds = milliseconds;
            }

            public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                Interlocked.Increment(ref Entries);
                Entered.TrySetResult(true);
                await Task.Delay(_milliseconds);
                Done.TrySetResult(true);
            }
        }

        private sealed class GatedHandler : InboundHandlerAdapter
        {
            public readonly TaskCompletionSource<bool> Entered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public readonly TaskCompletionSource<bool> Release =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public readonly TaskCompletionSource<bool> Done =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                Entered.TrySetResult(true);
                await Release.Task;
                Done.TrySetResult(true);
            }
        }

        private sealed class ExceptionTail : InboundHandlerAdapter
        {
            public readonly ConcurrentQueue<Exception> Exceptions = new ConcurrentQueue<Exception>();

            public override Task ChannelExceptionCaughtAsync(IChannelHandlerContext ctx, Exception ex)
            {
                Exceptions.Enqueue(ex);
                return Task.CompletedTask;
            }
        }

        private sealed class TestRawStream : IRawByteStream
        {
            private readonly Pipe _pipe;
            private readonly List<byte[]> _sent = new List<byte[]>();

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

            public int PauseCount;
            public int ResumeCount;

            public readonly TaskCompletionSource<string> Closed =
                new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

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