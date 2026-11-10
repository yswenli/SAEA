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
*文件名： SplitterSwitchTests
*版本号： v26.11.5.1
*唯一标识：87018a5c-8cac-4a67-8d78-c94eee76c483
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/9 11:16:22
*描述：可切换分帧器的上限与屏障用例，以及接收泵在同一 ReadResult 上的续切行为
*
*=====================================================================
*修改标记
*修改时间：2026/11/9 12:05:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：屏障超时按分帧协议错误上链后由泵收口，同步更新收口原因断言
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
    /// 可切换分帧器：固定上限、切换校验，以及排队/内联两条泵路径的停切与同缓冲续切。
    /// 所有等待都带显式超时，不依赖测试框架默认超时。
    /// </summary>
    [TestClass]
    public class SplitterSwitchTests
    {
        private const int WaitTimeoutMs = 3000;

        /// <summary>
        /// MaxFrameLength 取构造时所有可切换分帧器的最大值，且不随当前分帧器变化
        /// </summary>
        [TestMethod]
        public void SwitchableSplitter_MaxFrameLength_IsMaxOfAll()
        {
            var initial = new FixedLengthSplitter(8);
            var larger = new LengthFieldSplitter(64);
            var smaller = new LineSplitter(32);

            var splitter = new SwitchableSplitter(initial, larger, smaller);

            Assert.AreEqual(64, splitter.MaxFrameLength, "最大帧长应取所有协议的最大值。");
            Assert.AreSame(initial, splitter.Current, "初始分帧器应保持为 Current。");
        }

        /// <summary>
        /// 切换目标超过创建时固定的最大帧长时抛 InvalidOperationException，且不改 Current、不解除屏障
        /// </summary>
        [TestMethod]
        public void SwitchableSplitter_SwitchToOversized_Throws()
        {
            var initial = new LengthFieldSplitter(8);
            var splitter = new SwitchableSplitter(initial);
            splitter.StopAfterCurrent = true;

            Assert.ThrowsException<InvalidOperationException>(
                () => splitter.SwitchTo(new LengthFieldSplitter(16)));

            Assert.AreSame(initial, splitter.Current, "拒绝切换后 Current 不得改变。");
            Assert.IsTrue(splitter.StopAfterCurrent, "拒绝切换后屏障必须保留，等待方仍需显式复位。");
        }

        /// <summary>
        /// 切换回当前分帧器等价于复位屏障，Current 不变且后续可以继续切帧
        /// </summary>
        [TestMethod]
        public void SwitchableSplitter_SwitchToCurrent_ResetsBarrier()
        {
            var initial = new LineSplitter(32);
            var splitter = new SwitchableSplitter(initial);
            splitter.StopAfterCurrent = true;

            splitter.SwitchTo(splitter.Current);

            Assert.IsFalse(splitter.StopAfterCurrent, "SwitchTo(Current) 必须复位屏障。");
            Assert.AreSame(initial, splitter.Current);
        }

        /// <summary>
        /// 排队分发下停切后在同一 ReadResult 上等待切换，切换完成无需新字节即可把残余字节交给新分帧器
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Pump_StopAfterCurrent_Queued_ResumesSameReadResult()
        {
            var takeAvailable = new TakeAvailableSplitter(1024);
            SwitchableSplitter switchable = null;
            var initial = new BarrierLengthFieldSplitter(new LengthFieldSplitter(1024), () => switchable);
            switchable = new SwitchableSplitter(initial, takeAvailable);

            var options = NewOptions(switchable);
            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawByteStream("switch-queued");
                var handler = new RecordingHandler();
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(stream, new TestChannel("switch-queued"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    // 升级帧与「WS 首帧」在同一段：剩余 3 字节既不能按长度域切分，也不会有第二次写入。
                    await WriteAsync(stream, Concat(Frame(1, 1), new byte[] { 0xAA, 0xBB, 0xCC }));
                    await handler.FirstFrame.Task.WaitAsync(TimeSpan.FromSeconds(WaitTimeoutMs / 1000));

                    // 屏障开启后泵不得继续切帧，也不得把残余字节标为已检查。
                    await Task.Delay(100);
                    Assert.AreEqual(1, handler.Frames.Count, "屏障期间不得产出后续帧。");

                    switchable.SwitchTo(takeAvailable);

                    await WaitUntilAsync(() => handler.Frames.Count == 2, "切换后未在原缓冲上继续产出帧");
                    CollectionAssert.AreEqual(new byte[] { 0xAA, 0xBB, 0xCC }, handler.Frames.ElementAt(1),
                        "残余字节应由新分帧器整体产出。");
                    Assert.AreSame(takeAvailable, switchable.Current);
                }
                finally
                {
                    await StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 内联分发下每帧返回后复查屏障：停切后等待切换，切换后剩余字节按新分帧器整体产出
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Pump_StopAfterCurrent_Inline_StopsAfterCurrentFrame()
        {
            var takeAvailable = new TakeAvailableSplitter(1024);
            SwitchableSplitter switchable = null;
            var initial = new BarrierLengthFieldSplitter(new LengthFieldSplitter(1024), () => switchable);
            switchable = new SwitchableSplitter(initial, takeAvailable);

            var options = NewOptions(switchable, ChannelDispatchMode.Inline);
            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawByteStream("switch-inline");
                var handler = new RecordingHandler();
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(stream, new TestChannel("switch-inline"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    // 第二帧本身是合法的旧协议帧；若内联路径漏查屏障，它会被立刻按旧规则切出并投递。
                    var secondWire = Frame(2, 2);
                    await WriteAsync(stream, Concat(Frame(1, 1), secondWire));
                    await handler.FirstFrame.Task.WaitAsync(TimeSpan.FromSeconds(WaitTimeoutMs / 1000));

                    await Task.Delay(150);
                    Assert.AreEqual(1, handler.Frames.Count, "内联路径停切后不得继续投递旧协议帧。");

                    switchable.SwitchTo(takeAvailable);

                    await WaitUntilAsync(() => handler.Frames.Count == 2, "切换后未继续处理原缓冲剩余字节");
                    CollectionAssert.AreEqual(secondWire, handler.Frames.ElementAt(1),
                        "切换后剩余字节应按新分帧器输出，包含旧分帧器本会剥掉的头部。");
                    Assert.AreEqual(2, executor.InlineDispatchedCount, "两帧都应由内联路径执行。");
                }
                finally
                {
                    await StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 屏障上无人调用 SwitchTo 时按 SplitterSwitchTimeout 以协议错误收口并关闭连接
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Pump_NoSwitch_BarrierTimesOutAndShutsDown()
        {
            SwitchableSplitter switchable = null;
            var initial = new BarrierLengthFieldSplitter(new LengthFieldSplitter(1024), () => switchable);
            switchable = new SwitchableSplitter(initial);

            var options = NewOptions(switchable);
            options.SplitterSwitchTimeout = TimeSpan.FromMilliseconds(100);

            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawByteStream("switch-timeout");
                var handler = new RecordingHandler();
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(stream, new TestChannel("switch-timeout"), options, CancellationToken.None);
                executor.Attach(pipeline);

                executor.Start();
                await WriteAsync(stream, Concat(Frame(1, 1), new byte[] { 0xAA }));

                var reason = await stream.Closed.Task.WaitAsync(TimeSpan.FromSeconds(3));
                Assert.AreEqual("split-protocol-error", reason, "屏障超时应按分帧协议错误上链，无处理器接管时由泵强制收口。");
                await executor.Completion.WaitAsync(TimeSpan.FromSeconds(3));
            }
        }

        /// <summary>
        /// 处理器在屏障期间异步完成切换后，泵无需新字节即在同一 ReadResult 上继续切帧
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Pump_SwitchTo_WaitsOnSameReadResultWithoutNewBytes()
        {
            var takeAvailable = new TakeAvailableSplitter(1024);
            SwitchableSplitter switchable = null;
            var initial = new BarrierLengthFieldSplitter(new LengthFieldSplitter(1024), () => switchable);
            switchable = new SwitchableSplitter(initial, takeAvailable);

            var options = NewOptions(switchable);
            using (var pipeline = ChannelPipeline.Create(options))
            {
                var switched = 0;
                var handler = new RecordingHandler
                {
                    OnFrame = async msg =>
                    {
                        if (Interlocked.CompareExchange(ref switched, 1, 0) != 0)
                            return;

                        // 模拟业务先等待握手写完成再切换：屏障期间不得有新读取。
                        await Task.Delay(100);
                        switchable.SwitchTo(takeAvailable);
                    }
                };
                pipeline.AddLast(handler);

                var stream = new TestRawByteStream("switch-same-read");
                var executor = new ChannelPipelineExecutor(stream, new TestChannel("switch-same-read"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    // 只写一次：第二帧完全来自同一次 ReadAsync 的剩余字节。
                    await WriteAsync(stream, Concat(Frame(1, 1), new byte[] { 0x10, 0x20 }));

                    await WaitUntilAsync(() => handler.Frames.Count == 2, "处理器切换后未在原缓冲上续切");
                    CollectionAssert.AreEqual(new byte[] { 0x10, 0x20 }, handler.Frames.ElementAt(1));
                }
                finally
                {
                    await StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 屏障等待期间连接取消时执行器正常收口、不悬挂，关闭原因保留为传输层取消
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Pump_ConnectionCanceledDuringBarrier_ShutsDown()
        {
            SwitchableSplitter switchable = null;
            var initial = new BarrierLengthFieldSplitter(new LengthFieldSplitter(1024), () => switchable);
            switchable = new SwitchableSplitter(initial);

            var options = NewOptions(switchable);
            using (var pipeline = ChannelPipeline.Create(options))
            using (var transport = new CancellationTokenSource())
            {
                var stream = new TestRawByteStream("switch-cancel");
                var handler = new RecordingHandler();
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel("switch-cancel"), options, transport.Token);
                executor.Attach(pipeline);

                executor.Start();
                await WriteAsync(stream, Concat(Frame(1, 1), new byte[] { 0xAA }));
                await handler.FirstFrame.Task.WaitAsync(TimeSpan.FromSeconds(WaitTimeoutMs / 1000));

                await Task.Delay(100);
                Assert.AreEqual(1, handler.Frames.Count, "屏障期间不得产出后续帧。");

                transport.Cancel();

                var reason = await stream.Closed.Task.WaitAsync(TimeSpan.FromSeconds(3));
                Assert.AreEqual("transport-cancelled", reason, "连接取消应保留传输层关闭原因。");
                await executor.Completion.WaitAsync(TimeSpan.FromSeconds(3));
            }
        }

        private static ChannelPipelineOptions NewOptions(IFrameSplitter splitter, ChannelDispatchMode mode = ChannelDispatchMode.Queued)
        {
            return new ChannelPipelineOptions
            {
                Splitter = splitter,
                QueueCapacity = 16,
                MaxConcurrentMessages = 4,
                DispatchMode = mode,
                Guard = NullChannelGuard.Instance,
                SplitterSwitchTimeout = TimeSpan.FromSeconds(3)
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

        /// <summary>
        /// 在产出首帧后开启切换屏障的测试分帧器，模拟 HTTP 识别到协议升级请求。
        /// </summary>
        private sealed class BarrierLengthFieldSplitter : IFrameSplitter
        {
            private readonly LengthFieldSplitter _inner;
            private readonly Func<SwitchableSplitter> _owner;
            private int _armed = 1;

            public BarrierLengthFieldSplitter(LengthFieldSplitter inner, Func<SwitchableSplitter> owner)
            {
                _inner = inner;
                _owner = owner;
            }

            public int MaxFrameLength
            {
                get { return _inner.MaxFrameLength; }
            }

            public bool TrySplit(ReadOnlySequence<byte> buffer, out long consumed, out ReadOnlySequence<byte> frame)
            {
                if (!_inner.TrySplit(buffer, out consumed, out frame))
                    return false;

                if (Interlocked.Exchange(ref _armed, 0) == 1)
                    _owner().StopAfterCurrent = true;

                return true;
            }

            public void Reset()
            {
                _inner.Reset();
            }
        }

        private sealed class RecordingHandler : InboundHandlerAdapter
        {
            private readonly ConcurrentQueue<byte[]> _frames = new ConcurrentQueue<byte[]>();

            public IReadOnlyCollection<byte[]> Frames
            {
                get { return _frames; }
            }

            public readonly TaskCompletionSource<bool> FirstFrame =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public Func<PooledMessage, Task> OnFrame;

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var pooled = (PooledMessage)msg;
                _frames.Enqueue(pooled.Frame.ToArray());
                FirstFrame.TrySetResult(true);

                return OnFrame != null ? OnFrame(pooled) : Task.CompletedTask;
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

            public readonly TaskCompletionSource<string> Closed =
                new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

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
                Closed.TrySetResult(reason);
                return Task.CompletedTask;
            }
        }
    }
}