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
*文件名： ByteToMessageDecoderTests
*版本号： v26.11.5.1
*唯一标识：b37dc935-5881-4ecb-871b-150c40ad3632
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/9 11:04:09
*描述：解码器基类的多输出派发、所有权与异常语义
*
*=====================================================================
*修改标记
*修改时间：2026/11/9 11:04:09
*修改人： yswenli
*版本号： v26.11.5.1
*描述：解码器基类的多输出派发、所有权与异常语义
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.Caching;
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Handlers
{
    /// <summary>
    /// 解码器基类的多输出派发、所有权与异常语义：零输出不派发、按序派发、
    /// 多输出共享一帧按次 Retain/Release、中途失败释放余项、输入帧始终由调用方释放。
    /// </summary>
    [TestClass]
    public class ByteToMessageDecoderTests
    {
        /// <summary>
        /// 解码出零条消息时不触发任何下游派发，输入帧仍由调用方持有
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Decode_ZeroOutput_CompletesWithNoDispatch()
        {
            var frame = NewExactFrame(8);
            var sink = new RecordingInboundHandler();

            try
            {
                using (var pipeline = ChannelPipeline.Create(NewOptions()))
                {
                    pipeline.AddLast(new TestDecoder((ctx, input, output) => { }));
                    pipeline.AddLast(sink);

                    await pipeline.FireChannelReadAsync(frame);

                    Assert.AreEqual(0, sink.Messages.Count, "零输出不应派发任何消息");
                    Assert.IsFalse(frame.IsReleased, "解码器基类不得释放输入帧");
                }
            }
            finally
            {
                frame.Release();
            }
        }

        /// <summary>
        /// 一帧解出多条消息时按 output 下标顺序逐条派发
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Decode_MultipleOutputs_DispatchedInOrder()
        {
            var frame = NewExactFrame(8);
            var sink = new RecordingInboundHandler();

            try
            {
                using (var pipeline = ChannelPipeline.Create(NewOptions()))
                {
                    pipeline.AddLast(new TestDecoder((ctx, input, output) =>
                    {
                        output.Add("first");
                        output.Add("second");
                        output.Add("third");
                    }));
                    pipeline.AddLast(sink);

                    await pipeline.FireChannelReadAsync(frame);

                    CollectionAssert.AreEqual(
                        new object[] { "first", "second", "third" }, sink.Messages, "多输出派发顺序不符");
                }
            }
            finally
            {
                frame.Release();
            }
        }

        /// <summary>
        /// 多条输出共享同一帧时每条各 Retain 一次，下游各释放一次后引用归零、池缓冲恰好归还一次
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Decode_SharesFrameAcrossTwoOutputs_RefCountReachesZero()
        {
            var frame = NewPooledFrame(16);
            var returnedBefore = ReturnedTotal();
            var sink = new DisposingSinkHandler();

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                pipeline.AddLast(new TestDecoder((ctx, input, output) =>
                {
                    input.Retain();
                    output.Add(input);
                    input.Retain();
                    output.Add(input);
                }));
                pipeline.AddLast(sink);

                try
                {
                    await pipeline.FireChannelReadAsync(frame);

                    Assert.AreEqual(2, sink.Messages.Count, "两条输出未全部派发");
                    Assert.AreSame(frame, sink.Messages[0], "第一条输出不是原帧");
                    Assert.AreSame(frame, sink.Messages[1], "第二条输出不是原帧");
                    Assert.IsFalse(frame.IsReleased, "下游两份释放后队列条目仍持有原始引用");
                    Assert.AreEqual(returnedBefore, ReturnedTotal(), "引用未归零前不得归还池缓冲");
                }
                finally
                {
                    frame.Release();
                }
            }

            Assert.IsTrue(frame.IsReleased, "原始引用释放后应已归零");
            Assert.AreEqual(returnedBefore + 1, ReturnedTotal(), "池缓冲应恰好归还一次");
        }

        /// <summary>
        /// 第一条输出在转发前抛错：该份 Retain 由入站边界补偿，余项由基类释放，输入帧只由调用方释放
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Decode_FirstOutputThrowsBeforeForward_BothRetainsReleasedAndInputReleasedOnce()
        {
            var frame = NewPooledFrame(16);
            var returnedBefore = ReturnedTotal();
            var boom = new InvalidOperationException("boom");
            var sink = new ThrowingInboundHandler(boom);

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                pipeline.AddLast(new TestDecoder((ctx, input, output) =>
                {
                    input.Retain();
                    output.Add(input);
                    input.Retain();
                    output.Add(input);
                }));
                pipeline.AddLast(sink);

                try
                {
                    var thrown = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                        () => pipeline.FireChannelReadAsync(frame));

                    Assert.AreSame(boom, thrown, "冒泡的异常实例被替换");
                    Assert.AreEqual(1, sink.Calls, "失败后不得继续派发余项");
                    Assert.IsFalse(frame.IsReleased, "两份 Retain 已释放，原始引用仍由调用方持有");
                    Assert.AreEqual(returnedBefore, ReturnedTotal(), "原始引用未释放前不得归还池缓冲");
                }
                finally
                {
                    frame.Release();
                }
            }

            Assert.IsTrue(frame.IsReleased, "原始引用释放后应已归零");
            Assert.AreEqual(returnedBefore + 1, ReturnedTotal(), "池缓冲应恰好归还一次");
        }

        /// <summary>
        /// 第二条输出失败时停止派发，仅释放尚未开始的第三条；第一条由下游消费，第二条由入站边界补偿
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Decode_SecondOutputFails_RemainingOutputsReleasedOnce()
        {
            var first = new CountingDisposable();
            var second = new CountingDisposable();
            var third = new CountingDisposable();
            var frame = NewExactFrame(8);
            var sink = new FailOnNthHandler(2, new InvalidOperationException("boom"));

            try
            {
                using (var pipeline = ChannelPipeline.Create(NewOptions()))
                {
                    pipeline.AddLast(new TestDecoder((ctx, input, output) =>
                    {
                        output.Add(first);
                        output.Add(second);
                        output.Add(third);
                    }));
                    pipeline.AddLast(sink);

                    await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                        () => pipeline.FireChannelReadAsync(frame));
                }
            }
            finally
            {
                frame.Release();
            }

            Assert.AreEqual(1, first.DisposeCount, "已消费的第一条输出应恰好释放一次");
            Assert.AreEqual(1, second.DisposeCount, "失败项应由入站边界补偿一次");
            Assert.AreEqual(1, third.DisposeCount, "尚未开始的余项应由基类释放一次");
            Assert.AreEqual(2, sink.Calls, "失败后不得继续派发余项");
        }

        /// <summary>
        /// 第二条输出取消时停止派发并释放余项，异常原样冒泡且不走普通异常通知
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Decode_Canceled_RemainingReleasedOnceAndBubbles()
        {
            var first = new CountingDisposable();
            var second = new CountingDisposable();
            var third = new CountingDisposable();
            var frame = NewExactFrame(8);
            var sink = new FailOnNthHandler(2, new OperationCanceledException());
            var recorder = new RecordingExceptionHandler();

            try
            {
                using (var pipeline = ChannelPipeline.Create(NewOptions()))
                {
                    pipeline.AddLast(new TestDecoder((ctx, input, output) =>
                    {
                        output.Add(first);
                        output.Add(second);
                        output.Add(third);
                    }));
                    pipeline.AddLast(sink);
                    pipeline.AddLast(recorder);

                    await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                        () => pipeline.FireChannelReadAsync(frame));
                }
            }
            finally
            {
                frame.Release();
            }

            Assert.AreEqual(1, first.DisposeCount, "已消费的第一条输出应恰好释放一次");
            Assert.AreEqual(1, second.DisposeCount, "取消项应由入站边界补偿一次");
            Assert.AreEqual(1, third.DisposeCount, "尚未开始的余项应由基类释放一次");
            Assert.AreEqual(0, recorder.Exceptions.Count, "取消不得走普通异常通知");
        }

        /// <summary>
        /// Decode 自身抛错时先通知下游一次，再照常派发已追加消息，最后原异常冒泡且不再重复通知
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Decode_Throws_ExceptionNotifiedOnceThenBubbles()
        {
            var first = new CountingDisposable();
            var second = new CountingDisposable();
            var frame = NewExactFrame(8);
            var boom = new InvalidOperationException("boom");
            var sink = new DisposingSinkHandler();
            var recorder = new RecordingExceptionHandler();

            try
            {
                using (var pipeline = ChannelPipeline.Create(NewOptions()))
                {
                    pipeline.AddLast(new TestDecoder((ctx, input, output) =>
                    {
                        output.Add(first);
                        output.Add(second);
                        throw boom;
                    }));
                    pipeline.AddLast(sink);
                    pipeline.AddLast(recorder);

                    var thrown = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                        () => pipeline.FireChannelReadAsync(frame));

                    Assert.AreSame(boom, thrown, "冒泡的异常实例被替换");
                }
            }
            finally
            {
                frame.Release();
            }

            Assert.AreEqual(2, sink.Messages.Count, "Decode 失败前已追加的消息仍应按序派发");
            Assert.AreEqual(1, first.DisposeCount, "第一条输出应恰好释放一次");
            Assert.AreEqual(1, second.DisposeCount, "第二条输出应恰好释放一次");
            Assert.AreEqual(1, recorder.Exceptions.Count, "同一异常只能通知一次");
            Assert.AreSame(boom, recorder.Exceptions[0], "通知的异常实例被替换");
        }

        /// <summary>
        /// 解码器基类不释放输入帧，原始引用仍由执行器（或直接调用方）释放
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Decode_DoesNotReleaseInputFrame()
        {
            var frame = NewPooledFrame(16);
            var returnedBefore = ReturnedTotal();

            using (var pipeline = ChannelPipeline.Create(NewOptions()))
            {
                pipeline.AddLast(new TestDecoder((ctx, input, output) => output.Add(input.Frame.ToArray())));

                try
                {
                    await pipeline.FireChannelReadAsync(frame);

                    Assert.IsFalse(frame.IsReleased, "解码器基类不得释放输入帧");
                    Assert.AreEqual(returnedBefore, ReturnedTotal(), "输入帧未释放前不得归还池缓冲");
                }
                finally
                {
                    frame.Release();
                }
            }

            Assert.IsTrue(frame.IsReleased, "调用方释放后输入帧应归零");
            Assert.AreEqual(returnedBefore + 1, ReturnedTotal(), "池缓冲应恰好归还一次");
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

        private static PooledMessage NewExactFrame(int size)
        {
            var buffer = new byte[size];
            for (var i = 0; i < buffer.Length; i++)
                buffer[i] = (byte)i;

            return new PooledMessage(buffer, buffer, buffer.Length, false);
        }

        private static PooledMessage NewPooledFrame(int size)
        {
            var buffer = MemoryPoolManager.Rent(size);
            for (var i = 0; i < size; i++)
                buffer[i] = (byte)i;

            return new PooledMessage(buffer.AsMemory(0, size), buffer, size, true);
        }

        private static long ReturnedTotal()
        {
            var stats = MemoryPoolManager.GetStatistics();
            return stats.SmallPoolReturned + stats.MediumPoolReturned + stats.LargePoolReturned;
        }

        private sealed class TestDecoder : ByteToMessageDecoder
        {
            private readonly Action<IChannelHandlerContext, PooledMessage, List<object>> _decode;

            public TestDecoder(Action<IChannelHandlerContext, PooledMessage, List<object>> decode)
            {
                _decode = decode;
            }

            protected override void Decode(IChannelHandlerContext ctx, PooledMessage frame, List<object> output)
            {
                _decode(ctx, frame, output);
            }
        }

        private sealed class RecordingInboundHandler : InboundHandlerAdapter
        {
            public readonly List<object> Messages = new List<object>();

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                Messages.Add(msg);
                return Task.CompletedTask;
            }
        }

        private sealed class DisposingSinkHandler : InboundHandlerAdapter
        {
            public readonly List<object> Messages = new List<object>();

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                Messages.Add(msg);

                var disposable = msg as IDisposable;
                if (disposable != null)
                    disposable.Dispose();

                return Task.CompletedTask;
            }
        }

        private sealed class ThrowingInboundHandler : InboundHandlerAdapter
        {
            private readonly Exception _exception;

            private int _calls;

            public ThrowingInboundHandler(Exception exception)
            {
                _exception = exception;
            }

            public int Calls
            {
                get { return Volatile.Read(ref _calls); }
            }

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                Interlocked.Increment(ref _calls);
                throw _exception;
            }
        }

        /// <summary>前 n-1 条正常释放，第 n 条直接抛错且不释放，交给入站边界补偿。</summary>
        private sealed class FailOnNthHandler : InboundHandlerAdapter
        {
            private readonly int _failAt;
            private readonly Exception _exception;

            private int _calls;

            public FailOnNthHandler(int failAt, Exception exception)
            {
                _failAt = failAt;
                _exception = exception;
            }

            public int Calls
            {
                get { return Volatile.Read(ref _calls); }
            }

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var call = Interlocked.Increment(ref _calls);
                if (call == _failAt)
                    throw _exception;

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
                return Task.CompletedTask;
            }
        }

        /// <summary>记录 Dispose 次数的可释放消息替身，用于断言余项恰好释放一次。</summary>
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
    }
}