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
*文件名： InboundOwnershipTests
*版本号： v26.11.5.1
*唯一标识：92abc929-4577-4f7e-8f59-370bccd63c18
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/9 11:04:09
*描述：入站派发的所有权补偿与异常通知语义
*
*=====================================================================
*修改标记
*修改时间：2026/11/9 11:04:09
*修改人： yswenli
*版本号： v26.11.5.1
*描述：入站派发的所有权补偿与异常通知语义
*
*****************************************************************************/
using System;
using System.Buffers.Binary;
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

namespace SAEA.Sockets.Pipeline.Tests.Tests.Runtime
{
    /// <summary>
    /// 入站派发的所有权补偿与异常通知语义：链持有消息按边界释放、执行器原始帧只由执行器释放、
    /// 普通异常通知一次后冒泡、取消不通知但仍补偿。
    /// </summary>
    [TestClass]
    public class InboundOwnershipTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(2);

        /// <summary>
        /// 下游抛普通异常时只通知一次并原样冒泡；上游边界看到共享通知标记后不再重复通知
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Inbound_HandlerThrows_NotifiesOnceAndBubbles()
        {
            var boom = new InvalidOperationException("boom");
            var recorder = new RecordingExceptionHandler();

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                pipeline.AddLast(new ForwardingInboundHandler());
                pipeline.AddLast(new ThrowingInboundHandler(boom));
                pipeline.AddLast(recorder);

                var thrown = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => pipeline.FireChannelReadAsync(new object()));

                Assert.AreSame(boom, thrown, "冒泡的异常实例被替换");
                Assert.AreEqual(1, recorder.Exceptions.Count, "同一次消息派发只能通知一次异常");
                Assert.AreSame(boom, recorder.Exceptions[0], "通知的异常实例被替换");
            }
        }

        /// <summary>
        /// 链持有消息未转发就抛错时由当前边界补偿释放一次，执行器原始帧不受影响
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Inbound_HandlerThrowsBeforeForward_ReleasesChainOwnedMessageOnce()
        {
            var message = new CountingDisposable();
            var boom = new InvalidOperationException("boom");
            var recorder = new RecordingExceptionHandler();

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                pipeline.AddLast(new EmittingInboundHandler(message));
                pipeline.AddLast(new ThrowingInboundHandler(boom));
                pipeline.AddLast(recorder);

                var thrown = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => pipeline.FireChannelReadAsync(new object()));

                Assert.AreSame(boom, thrown, "冒泡的异常实例被替换");
                Assert.AreEqual(1, message.DisposeCount, "链持有消息在未转发抛错时应恰好补偿一次");
                Assert.AreEqual(1, recorder.Exceptions.Count, "异常应只通知一次");
            }
        }

        /// <summary>
        /// 消息已成功转发后本节点再抛错，补偿责任已随转发移交下游，本边界不得重复释放
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Inbound_HandlerThrowsAfterForward_DoesNotDoubleRelease()
        {
            var message = new CountingDisposable();
            var boom = new InvalidOperationException("boom");
            var recorder = new RecordingExceptionHandler();

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                pipeline.AddLast(new EmittingInboundHandler(message));
                pipeline.AddLast(new ForwardThenThrowHandler(boom));
                pipeline.AddLast(new DisposingInboundHandler());
                pipeline.AddLast(recorder);

                var thrown = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => pipeline.FireChannelReadAsync(new object()));

                Assert.AreSame(boom, thrown, "冒泡的异常实例被替换");
                Assert.AreEqual(1, message.DisposeCount, "转发后下游已释放，当前边界不得重复释放");
                Assert.AreEqual(1, recorder.Exceptions.Count, "异常应只通知一次");
            }
        }

        /// <summary>
        /// 取消与普通异常一致地补偿链持有消息，但不得走普通异常通知
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Inbound_Canceled_CompensatesOwnershipAndBubbles()
        {
            var message = new CountingDisposable();
            var recorder = new RecordingExceptionHandler();

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                pipeline.AddLast(new EmittingInboundHandler(message));
                pipeline.AddLast(new ThrowingInboundHandler(new OperationCanceledException()));
                pipeline.AddLast(recorder);

                await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                    () => pipeline.FireChannelReadAsync(new object()));

                Assert.AreEqual(1, message.DisposeCount, "取消也必须补偿链持有消息一次");
                Assert.AreEqual(0, recorder.Exceptions.Count, "取消不得走普通异常通知");
            }
        }

        /// <summary>
        /// 链持有消息转发时已无下一入站节点，终点记录 Debug 并释放它，避免缺少 terminal 时静默泄漏
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Inbound_NoNextInboundNode_ReleasesChainOwnedAndLogs()
        {
            var message = new CountingDisposable();

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            using (var capture = new ConsoleCapture())
            {
                pipeline.AddLast(new EmittingInboundHandler(message));

                Console.SetOut(capture.Writer);
                try
                {
                    await pipeline.FireChannelReadAsync(new object());

                    var found = await capture.WaitForAsync("入站链尾没有下一个入站节点", TimeSpan.FromSeconds(3));
                    Assert.IsTrue(found, "链尾丢弃链持有消息时未记录 Debug；捕获到的输出：" + capture.Text);
                }
                finally
                {
                    Console.SetOut(capture.Original);
                }

                Assert.AreEqual(1, message.DisposeCount, "链尾没有下一个入站节点时应释放链持有消息一次");
            }
        }

        /// <summary>
        /// 执行器派发的原始帧标记为执行器持有：处理器抛错时链边界不代它释放，最终由执行器 finally 恰好归还一次
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Inbound_ExecutorOwnedOriginalFrame_ReleasedByExecutorOnly()
        {
            var options = NewOptions();
            var stream = new TestRawByteStream("executor-owned");
            var handler = new ThrowingCapturingHandler(new InvalidOperationException("boom"));

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel("executor-owned"), options, CancellationToken.None);
                executor.Attach(pipeline);

                var returnedBefore = ReturnedTotal();
                try
                {
                    executor.Start();
                    await WriteAsync(stream, Frame(1, 1));

                    await WaitUntilAsync(() => handler.Message != null, "原始帧未到达处理器");
                    var frame = handler.Message;

                    await WaitUntilAsync(() => frame.IsReleased, "执行器未释放原始帧");
                    Assert.AreEqual(returnedBefore + 1, ReturnedTotal(), "原始帧应恰好归还池缓冲一次");
                }
                finally
                {
                    await StopAsync(executor);
                }
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

        private static byte[] Frame(byte type, params byte[] body)
        {
            var wire = new byte[FrameConstants.P_Head + body.Length];
            BinaryPrimitives.WriteInt64LittleEndian(wire.AsSpan(0, FrameConstants.P_LEN), body.Length);
            wire[FrameConstants.P_LEN] = type;
            body.CopyTo(wire, FrameConstants.P_Head);
            return wire;
        }

        private static async Task WriteAsync(TestRawByteStream stream, byte[] data)
        {
            await stream.Pipe.Writer.WriteAsync(data);
            await stream.Pipe.Writer.FlushAsync();
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

        private static async Task StopAsync(ChannelPipelineExecutor executor)
        {
            await executor.BeginShutdown("test-done");
            await executor.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        }

        private static long ReturnedTotal()
        {
            var stats = MemoryPoolManager.GetStatistics();
            return stats.SmallPoolReturned + stats.MediumPoolReturned + stats.LargePoolReturned;
        }

        /// <summary>记录 Dispose 次数的可释放消息替身，用于断言 0/1/2 次补偿。</summary>
        private sealed class CountingDisposable : IDisposable
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

        /// <summary>把一条新消息交给下游，模拟 decoder 产出的链持有消息。</summary>
        private sealed class EmittingInboundHandler : InboundHandlerAdapter
        {
            private readonly object _message;

            public EmittingInboundHandler(object message)
            {
                _message = message;
            }

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                return ctx.FireChannelReadAsync(_message);
            }
        }

        private sealed class ForwardingInboundHandler : InboundHandlerAdapter
        {
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

        /// <summary>先转发给下游，再抛出异常，用于验证已转发边界不重复补偿。</summary>
        private sealed class ForwardThenThrowHandler : InboundHandlerAdapter
        {
            private readonly Exception _exception;

            public ForwardThenThrowHandler(Exception exception)
            {
                _exception = exception;
            }

            public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                await ctx.FireChannelReadAsync(msg).ConfigureAwait(false);
                throw _exception;
            }
        }

        private sealed class DisposingInboundHandler : InboundHandlerAdapter
        {
            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var disposable = msg as IDisposable;
                if (disposable != null)
                    disposable.Dispose();

                return Task.CompletedTask;
            }
        }

        /// <summary>记录异常并终止传播；用于断言只通知一次。</summary>
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
                return Task.CompletedTask;
            }
        }

        /// <summary>记录执行器派发的原始帧后抛错，用于观察执行器是否只释放一次。</summary>
        private sealed class ThrowingCapturingHandler : InboundHandlerAdapter
        {
            private readonly Exception _exception;

            public ThrowingCapturingHandler(Exception exception)
            {
                _exception = exception;
            }

            public PooledMessage Message { get; private set; }

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                Message = msg as PooledMessage;
                throw _exception;
            }
        }

        /// <summary>
        /// 临时接管 <see cref="Console.Out"/>，轮询等待 LogHelper 后台线程刷出的文本。
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

        private sealed class TestRawByteStream : IRawByteStream
        {
            private readonly Pipe _pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 4 * 1024 * 1024));

            public TestRawByteStream(string channelId)
            {
                ChannelId = channelId;
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
    }
}