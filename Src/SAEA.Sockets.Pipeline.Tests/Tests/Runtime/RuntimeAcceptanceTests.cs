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
*文件名： RuntimeAcceptanceTests
*版本号： v26.11.5.1
*唯一标识：1081bf18-03a2-41bd-913e-87f72d9856dc
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：设计文档 §8.1 的运行时验收用例（内存传输替身 + 真执行器）；用例名前缀编号与 §8.1 条目对应， 不带编号的用例覆盖同一设计点下的边界分支
*
*=====================================================================
*修改标记
*修改时间：2026/11/9 11:39:22
*修改人： yswenli
*版本号： v26.11.5.1
*描述：语法型回包处理器改用 ctx.Pipeline.WriteAsync
*
*****************************************************************************/
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.Caching;
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Runtime
{
    /// <summary>
    /// 设计文档 §8.1 的运行时验收用例（内存传输替身 + 真执行器）；用例名前缀编号与 §8.1 条目对应，
    /// 不带编号的用例覆盖同一设计点下的边界分支。其它条目由 splitter、handler、executor
    /// 契约用例与演示工程覆盖，完整对应关系见实施计划的「验收用例 → 里程碑映射」。
    /// 与既有用例重复的条目不复刻：验收 16 的 IsSharable 计数由
    /// <see cref="ChannelPipelineStructureTests.NonSharableHandler_ReuseOnSecondChain_ThrowsUntilRemoved"/> 覆盖。
    /// </summary>
    [TestClass]
    public class RuntimeAcceptanceTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(2);

        /// <summary>
        /// 慢鉴权不会把同连接的消息串行化：完成顺序与到达顺序相反、峰值并发大于 1 且总耗时明显低于串行累加值
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Acceptance3_SlowAuth_DoesNotSerializeMessagesOnSameConnection()
        {
            // 五条消息的鉴权耗时按到达顺序递减，完成顺序必然与到达顺序相反；
            // 若执行器把同连接消息串行化，总耗时接近 600ms 且完成顺序等于到达顺序。
            var delays = new[] { 200, 160, 120, 80, 40 };
            var options = NewOptions(queueCapacity: 16, maxConcurrent: 5);

            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawByteStream("slow-auth");
                var auth = new SlowAuthHandler(delays);
                pipeline.AddLast(auth);

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel("slow-auth"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    var frames = new byte[delays.Length][];
                    for (var i = 0; i < frames.Length; i++)
                        frames[i] = Frame((byte)i, (byte)i);

                    var watch = Stopwatch.StartNew();
                    await WriteAsync(stream, Concat(frames));
                    await WaitUntilAsync(() => auth.Completed == delays.Length, "慢鉴权消息未全部完成");
                    watch.Stop();

                    CollectionAssert.AreEqual(new byte[] { 4, 3, 2, 1, 0 }, auth.CompletionOrder,
                        "完成顺序应为 40→80→120→160→200ms，与到达顺序相反");
                    Assert.IsTrue(auth.PeakConcurrency > 1,
                        "同连接消息未并发执行，峰值并发=" + auth.PeakConcurrency);
                    Assert.IsTrue(watch.ElapsedMilliseconds < 1000,
                        "总耗时 " + watch.ElapsedMilliseconds + "ms 超过 5×200ms，鉴权被串行化");
                }
                finally
                {
                    await StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 单并发时 50 帧严格按接收顺序处理；并发 4 时消息之间可并行，但同一条消息在两个链节点间仍保持 A 先 B 后
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Concurrency_SameConnection_KeepsReceiveOrder_AndPerMessageChainOrder()
        {
            // 单并发：严格按接收顺序。
            using (var pipeline = ChannelPipeline.Create(NewOptions(queueCapacity: 128, maxConcurrent: 1)))
            {
                var stream = new TestRawByteStream("order-single");
                var handler = new SequenceRecordingHandler();
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(stream, new TestChannel("order-single"), NewOptions(queueCapacity: 128, maxConcurrent: 1), CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();
                    var frames = new byte[50][];
                    for (var i = 0; i < frames.Length; i++)
                        frames[i] = Frame((byte)i, (byte)i);
                    await WriteAsync(stream, Concat(frames));

                    await WaitUntilAsync(() => handler.Count == 50, "单并发未处理完 50 帧");
                    CollectionAssert.AreEqual(Enumerable.Range(0, 50).Select(i => (byte)i).ToArray(), handler.Types.ToArray(),
                        "单并发下处理顺序与接收顺序不一致");
                }
                finally
                {
                    await StopAsync(executor);
                }
            }

            // 并行：同一条消息内部链顺序不变。
            var chainTracker = new ChainTracker();
            using (var pipeline = ChannelPipeline.Create(NewOptions(queueCapacity: 128, maxConcurrent: 4)))
            {
                var stream = new TestRawByteStream("order-parallel");
                var first = new ChainStepHandler("A", chainTracker);
                var second = new ChainStepHandler("B", chainTracker);
                pipeline.AddLast(first);
                pipeline.AddLast(second);

                var executor = new ChannelPipelineExecutor(stream, new TestChannel("order-parallel"), NewOptions(queueCapacity: 128, maxConcurrent: 4), CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();
                    var frames = new byte[50][];
                    for (var i = 0; i < frames.Length; i++)
                        frames[i] = Frame((byte)i, (byte)i);
                    await WriteAsync(stream, Concat(frames));

                    await WaitUntilAsync(() => chainTracker.Count == 100, "并行链未处理完 50 条消息");
                    for (var i = 0; i < 50; i++)
                    {
                        var firstIndex = chainTracker.IndexOf("A:" + i);
                        var secondIndex = chainTracker.IndexOf("B:" + i);
                        Assert.IsTrue(firstIndex >= 0 && secondIndex > firstIndex,
                            "消息 " + i + " 的内部链顺序被破坏：A=" + firstIndex + "，B=" + secondIndex);
                    }

                    Assert.IsTrue(chainTracker.PeakConcurrency > 1,
                        "并行度未超过 1，无法验证消息之间的并发；峰值=" + chainTracker.PeakConcurrency);
                }
                finally
                {
                    await StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 队列满时只暂停一次且暂停期间不再 ReadAsync，空出一格（4→3）不恢复，降到低水位（3→2）才恢复一次，最终 10 帧按序处理完且并发额度回满
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Acceptance4_Backpressure_PausesOnce_NoReadWhilePaused_ResumesAtLowWatermark()
        {
            var options = NewOptions(queueCapacity: 4, maxConcurrent: 1);
            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawByteStream("backpressure");
                var handler = new TwoGateHandler();
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(stream, new TestChannel("backpressure"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    // 先制造"一条在途 + 一条等并发额度"，使消费侧不会在暂停期间继续出队。
                    await WriteAsync(stream, Concat(Frame(1), Frame(2)));
                    await handler.FirstEntered.Task.WaitAsync(WaitTimeout);
                    await WaitUntilAsync(() => executor.QueuedCount == 0, "消费侧未取走前两帧");

                    // 再写 6 帧：容量 4 填满后第 5 帧被挡下，剩余 2 帧留在 accumulator，
                    // 低水位恢复后正好填满空闲的 2 格，因此全程只应 Pause 一次。
                    await WriteAsync(stream, Concat(Frame(3), Frame(4), Frame(5), Frame(6), Frame(7), Frame(8)));

                    await WaitUntilAsync(() => Volatile.Read(ref stream.PauseCount) == 1, "队列满未触发暂停");
                    Assert.AreEqual(4, executor.QueuedCount, "暂停时队列应保持满额 4");
                    Assert.AreEqual(0, Volatile.Read(ref stream.ResumeCount), "队列未降到低水位不应恢复");

                    var readsAtPause = stream.Reader.ReadCalls;
                    await Task.Delay(200);
                    Assert.AreEqual(1, Volatile.Read(ref stream.PauseCount), "暂停状态被重复触发");
                    Assert.AreEqual(4, executor.QueuedCount, "暂停期间队列长度不应变化");
                    Assert.AreEqual(readsAtPause, stream.Reader.ReadCalls, "暂停期间不得继续调用 ReadAsync");

                    handler.ReleaseFirst.TrySetResult(true);
                    await handler.SecondEntered.Task.WaitAsync(WaitTimeout);
                    await WaitUntilAsync(() => executor.QueuedCount == 3, "消费侧未从 4 降到 3");
                    await Task.Delay(100);
                    Assert.AreEqual(0, Volatile.Read(ref stream.ResumeCount), "空出一格（4→3）不得立即恢复");

                    handler.ReleaseSecond.TrySetResult(true);
                    await WaitUntilAsync(() => Volatile.Read(ref stream.ResumeCount) == 1, "降到低水位 2 未恢复");
                    await WaitUntilAsync(
                        () => handler.Count == 8 && executor.QueuedCount == 0 && executor.AvailableConcurrency == 1,
                        "恢复后积压帧未全部处理");

                    // 后两帧在系统稳定后写入，验证总帧数 10 且不会再次触发暂停。
                    await WriteAsync(stream, Concat(Frame(9), Frame(10)));
                    await WaitUntilAsync(
                        () => handler.Count == 10 && executor.QueuedCount == 0 && executor.AvailableConcurrency == 1,
                        "10 帧未全部处理");

                    Assert.AreEqual(1, Volatile.Read(ref stream.PauseCount), "整个用例只应暂停一次");
                    Assert.AreEqual(1, Volatile.Read(ref stream.ResumeCount), "整个用例只应恢复一次");
                    CollectionAssert.AreEqual(Enumerable.Range(1, 10).Select(i => (byte)i).ToArray(), handler.Types.ToArray(),
                        "背压恢复后帧顺序或内容错误");
                }
                finally
                {
                    handler.ReleaseFirst.TrySetResult(true);
                    handler.ReleaseSecond.TrySetResult(true);
                    await StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 同一组帧分别以一次性、逐字节和随机长度切分写入，产出的帧数与每帧字节内容完全一致
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Acceptance2_ArbitrarySplits_ProduceByteIdenticalFrames()
        {
            var frames = new[]
            {
                Frame(1, 1, 2, 3),
                Frame(2, 10, 11, 12, 13, 14),
                Frame(3)
            };
            var wire = Concat(frames);

            var oneShot = await CollectFramesAsync(wire, null, frames.Length);
            var oneByte = await CollectFramesAsync(wire, Enumerable.Repeat(1, wire.Length).ToArray(), frames.Length);

            var random = new Random(20261105);
            var randomSizes = new List<int>();
            var remaining = wire.Length;
            while (remaining > 0)
            {
                var size = Math.Min(remaining, random.Next(1, 8));
                randomSizes.Add(size);
                remaining -= size;
            }

            var randomSplit = await CollectFramesAsync(wire, randomSizes.ToArray(), frames.Length);

            Assert.AreEqual(frames.Length, oneShot.Count, "一次性输入帧数不符");
            for (var i = 0; i < frames.Length; i++)
            {
                var expected = frames[i].Skip(FrameConstants.P_LEN).ToArray();
                CollectionAssert.AreEqual(expected, oneShot[i], "一次性输入第 " + i + " 帧不符");
                CollectionAssert.AreEqual(oneShot[i], oneByte[i],
                    "逐字节切分第 " + i + " 帧与一次性输入不一致；期望长度=" + oneShot[i].Length
                    + "，实际长度=" + oneByte[i].Length
                    + "，期望=" + BitConverter.ToString(oneShot[i])
                    + "，实际=" + BitConverter.ToString(oneByte[i]));
                CollectionAssert.AreEqual(oneShot[i], randomSplit[i], "随机切分第 " + i + " 帧与一次性输入不一致");
            }
        }

        /// <summary>
        /// 连续处理 10 万个 20 字节小帧后队列与并发额度清空，包内存池的租借净增量回到基线
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Acceptance8_HundredThousandSmallFrames_PoolRentAndReturnBalanced()
        {
            var baselineNet = NetRented();

            var options = NewOptions(queueCapacity: 2048, maxConcurrent: 32);
            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawByteStream("memory-balance");
                var handler = new CountingHandler();
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(stream, new TestChannel("memory-balance"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();
                    var processed = 0;
                    for (var batch = 0; batch < 50; batch++)
                    {
                        await WriteAsync(stream, BuildFrames(2000, 20, batch));
                        processed += 2000;
                        var target = processed;
                        await WaitUntilAsync(() => handler.Count == target, "第 " + batch + " 批小帧未处理完");
                    }

                    await WaitUntilAsync(
                        () => executor.QueuedCount == 0 && executor.AvailableConcurrency == options.MaxConcurrentMessages,
                        "小帧处理结束后队列或在途未清空");
                }
                finally
                {
                    await StopAsync(executor);
                }
            }

            // 正常退出路径：accumulator 已在泵的 finally 中归还，整体租借/归还应回到基线。
            await WaitUntilAsync(() => NetRented() == baselineNet, "包级内存池租借/归还未配平");
            Assert.AreEqual(baselineNet, NetRented(), "10 万小帧后内存池租借差不为 0");
        }

        /// <summary>
        /// 每轮成功排空恰好通知一次 ChannelReadCompleteAsync，通知次数与非空 ReadAsync 次数一致，空闲时不产生空通知
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Acceptance13_ReadComplete_OncePerSuccessfulDrain_AndNoEmptyNotifications()
        {
            var options = NewOptions(queueCapacity: 64, maxConcurrent: 4);
            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawByteStream("read-complete");
                var handler = new ReadCompleteCountingHandler();
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(stream, new TestChannel("read-complete"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    await WriteAsync(stream, Concat(Frame(1), Frame(2), Frame(3)));
                    await WaitUntilAsync(() => handler.FrameCount == 3, "第一批 3 帧未处理完");
                    await WaitUntilAsync(() => handler.CompleteCount == 1, "一轮 3 帧应只通知 1 次");

                    Assert.AreEqual(1, handler.CompleteCount, "同一轮排空重复通知");
                    Assert.AreEqual(1, stream.Reader.NonEmptyReads, "第一批应只来自一次非空 ReadAsync");
                    Assert.AreEqual(handler.CompleteCount, stream.Reader.NonEmptyReads,
                        "通知次数应与非空 ReadAsync 次数一致（无空通知）");

                    await WriteAsync(stream, Concat(Frame(4), Frame(5), Frame(6)));
                    await WaitUntilAsync(() => handler.FrameCount == 6, "第二批 3 帧未处理完");
                    await WaitUntilAsync(() => handler.CompleteCount == 2, "第二轮应再通知 1 次");

                    Assert.AreEqual(2, handler.CompleteCount, "每次成功排空至多通知 1 次");
                    Assert.AreEqual(2, stream.Reader.NonEmptyReads, "第二批应只来自一次非空 ReadAsync");

                    // 泵在无数据时会阻塞在 ReadAsync，不得因此产生空通知。
                    await Task.Delay(200);
                    Assert.AreEqual(2, handler.CompleteCount, "无新数据时产生了空通知");
                    Assert.AreEqual(handler.CompleteCount, stream.Reader.NonEmptyReads,
                        "通知次数与非空 ReadAsync 次数不一致");
                }
                finally
                {
                    await StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 通知回调抛异常时只记录警告：执行器保持活跃、传输层不关闭，后续帧继续处理
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task ReadComplete_Throws_OnlyWarns_AndKeepsProcessing()
        {
            var options = NewOptions(queueCapacity: 32, maxConcurrent: 2);
            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawByteStream("complete-throws");
                var recorder = new ForwardingCountingHandler();
                var throwing = new ThrowingCompleteHandler();
                pipeline.AddLast(recorder);
                pipeline.AddLast(throwing);

                var executor = new ChannelPipelineExecutor(stream, new TestChannel("complete-throws"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();
                    await WriteAsync(stream, Frame(1));

                    await WaitUntilAsync(() => recorder.Count == 1, "首帧未到达 handler");
                    await WaitUntilAsync(() => throwing.CompleteCalls >= 1, "ChannelReadCompleteAsync 未被调用");
                    await Task.Delay(100);

                    Assert.IsTrue(executor.IsActive, "通知异常不应关闭连接");
                    Assert.IsFalse(stream.Closed.Task.IsCompleted, "通知异常不应关闭传输层");

                    await WriteAsync(stream, Frame(2));
                    await WaitUntilAsync(() => recorder.Count == 2, "通知异常后后续帧未继续处理");
                    Assert.IsTrue(executor.IsActive, "后续帧处理时连接仍应活跃");
                }
                finally
                {
                    await StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// AddFirst 插入的出站处理器落在链首并参与出站事件，写出的字节原样到达传输层出口
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task PipelineStructure_AddFirstOutbound_IsAccepted_AndOutboundPassesThroughIt()
        {
            var options = NewOptions();
            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawByteStream("add-first-outbound");
                var log = new ConcurrentQueue<string>();
                var inbound = new WriteFrameOnReadHandler(new byte[] { 9, 8, 7 });
                var outbound = new LoggingOutboundHandler("out", log);

                var executor = new ChannelPipelineExecutor(stream, new TestChannel("add-first-outbound"), options, CancellationToken.None);
                executor.Attach(pipeline);

                pipeline.AddLast(inbound);
                pipeline.AddFirst(outbound);

                Assert.AreSame(outbound, pipeline.Handlers[0], "AddFirst 的出站处理器未落在链首");

                await pipeline.FireChannelReadAsync(new object());

                CollectionAssert.AreEqual(new[] { "out" }, log.ToArray(), "出站事件未经过 AddFirst 的出站前缀");
                Assert.AreEqual(1, stream.SentCount, "出站帧未到达链头出口");
                CollectionAssert.AreEqual(new byte[] { 9, 8, 7 }, stream.SentFrames[0], "出站字节不一致");
            }
        }

        /// <summary>
        /// 16 路并发写属性后数量完整，收口完成后通道转为非活跃并清空全部属性
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Acceptance17_Attributes_ConcurrentWrites_AndClearedAfterShutdown()
        {
            var options = NewOptions();
            using (var pipeline = ChannelPipeline.Create(options))
            {
                var channel = new DefaultChannel("attributes", null);
                var stream = new TestRawByteStream("attributes");

                Parallel.For(0, 16, i => channel.SetAttribute("key" + i, i));
                Assert.AreEqual(16, channel.Attributes.Count, "16 路并发写后属性数量不符");

                var executor = new ChannelPipelineExecutor(stream, channel, options, CancellationToken.None);
                executor.Attach(pipeline);
                executor.Start();
                await Task.Delay(50);

                await executor.BeginShutdown("attributes-closed");
                await executor.Completion.WaitAsync(WaitTimeout);

                Assert.IsFalse(channel.IsActive, "收口后通道仍为活跃状态");
                Assert.AreEqual(0, channel.Attributes.Count, "收口后属性未清空");
                Assert.IsNull(channel.GetAttribute("key0"), "收口后仍可读到属性");
            }
        }

        /// <summary>
        /// 传输令牌取消后泵停止：队列与 accumulator 归零、并发额度回满、在途消息收到取消，传输层以 transport-cancelled 关闭
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Acceptance21_TransportCancellation_StopsPumpAndClearsQueueAndAccumulator()
        {
            var options = NewOptions(queueCapacity: 16, maxConcurrent: 1);
            var transportCts = new CancellationTokenSource();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawByteStream("cancel-propagation");
                var handler = new CancelAwareBlockingHandler();
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(stream, new TestChannel("cancel-propagation"), options, transportCts.Token);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();
                    await WriteAsync(stream, Concat(Frame(1), Frame(2), Frame(3), Frame(4), Frame(5)));
                    await handler.Entered.Task.WaitAsync(WaitTimeout);
                    await WaitUntilAsync(() => executor.QueuedCount >= 1, "取消前队列未积压");

                    transportCts.Cancel();

                    await executor.Completion.WaitAsync(TimeSpan.FromMilliseconds(500));
                    Assert.AreEqual(0, executor.QueuedCount, "取消后队列未归零");
                    Assert.AreEqual(0, executor.Traffic().BufferedBytes, "取消后 accumulator 未归零");
                    Assert.AreEqual(options.MaxConcurrentMessages, executor.AvailableConcurrency, "取消后并发额度未回到满额");
                    Assert.IsTrue(handler.Cancelled, "在途消息未收到取消");
                    Assert.AreEqual("transport-cancelled", await stream.Closed.Task.WaitAsync(TimeSpan.FromMilliseconds(500)),
                        "传输层关闭原因不符");
                }
                finally
                {
                    await StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 默认阈值下 20 字节小帧也走内存池且租借归还配平；显式正阈值下小帧精确分配不碰池；
        /// 两种配置下 8KB 帧都走内存池并在处理后归还
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Acceptance22_SmallFramePooling_FollowsThreshold_AndLargeFrameIsPooledAndReturned()
        {
            // 默认阈值 0：小帧也走池，靠引用归零配平。
            await RunSmallFramePoolingAsync(0, true);

            // 显式正阈值：小于阈值的帧精确分配，不碰池。
            await RunSmallFramePoolingAsync(512, false);
        }

        private static async Task RunSmallFramePoolingAsync(int threshold, bool expectPooled)
        {
            var options = NewOptions(queueCapacity: 2048, maxConcurrent: 32);
            options.SmallFrameInlineThreshold = threshold;
            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawByteStream("small-frame-alloc");
                var handler = new PoolingObservationHandler();
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(stream, new TestChannel("small-frame-alloc"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    // 预热：让 Pipe 与内存池进入稳定状态，之后的租借差只反映本条路径。
                    var processed = 0;
                    for (var batch = 0; batch < 5; batch++)
                    {
                        await WriteAsync(stream, BuildFrames(2000, 20, batch));
                        processed += 2000;
                        var target = processed;
                        await WaitUntilAsync(() => handler.Count == target, "预热批未处理完");
                    }

                    await WaitUntilAsync(
                        () => executor.QueuedCount == 0 && executor.AvailableConcurrency == options.MaxConcurrentMessages,
                        "预热后队列或在途未清空");

                    var netBefore = NetRented();
                    for (var batch = 0; batch < 50; batch++)
                    {
                        await WriteAsync(stream, BuildFrames(2000, 20, batch + 5));
                        processed += 2000;
                        var target = processed;
                        await WaitUntilAsync(() => handler.Count == target, "小帧批未处理完");
                    }

                    // 消息执行完毕即归还，池化只是中间态，净值必须回到基线。
                    await WaitUntilAsync(() => NetRented() == netBefore, "小帧处理后内存池租借未配平");
                    Assert.AreEqual(
                        expectPooled ? processed : 0,
                        handler.PooledCount,
                        expectPooled ? "默认阈值下小帧应走内存池" : "正阈值下小帧不应走内存池");

                    var smallCount = handler.Count;
                    var pooledBefore = handler.PooledCount;
                    var smallRented = RentedTotal();
                    var smallReturned = ReturnedTotal();

                    await WriteAsync(stream, Frame(200, new byte[8192]));
                    await WaitUntilAsync(() => handler.Count == smallCount + 1, "8KB 帧未处理");

                    Assert.AreEqual(pooledBefore + 1, handler.PooledCount, "8KB 帧应走内存池");
                    Assert.AreEqual(smallRented + 1, RentedTotal(), "8KB 帧应新增一次租借");
                    await WaitUntilAsync(() => ReturnedTotal() == smallReturned + 1, "8KB 帧处理后未归还内存池");
                }
                finally
                {
                    await StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 单消息超时释放并发额度但不进入收口、不关闭连接，后续消息可在忙等消息结束前完成
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Acceptance24_MessageTimeout_ReleasesConcurrencySlot_WithoutClosingConnection()
        {
            var options = NewOptions(queueCapacity: 16, maxConcurrent: 2, messageTimeout: TimeSpan.FromMilliseconds(100));
            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawByteStream("message-timeout");
                var handler = new TimeoutConcurrencyHandler();
                var tail = new ExceptionTailHandler();
                pipeline.AddLast(handler);
                pipeline.AddLast(tail);

                var executor = new ChannelPipelineExecutor(stream, new TestChannel("message-timeout"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();
                    await WriteAsync(stream, Concat(Frame(1), Frame(2)));

                    await WaitUntilAsync(
                        () => tail.Exceptions.Any(ex => ex is MessageTimeoutException),
                        "链尾未收到 MessageTimeoutException");

                    // 超时消息释放额度后，第三条消息应在忙等消息（300ms）结束前跑完。
                    await WriteAsync(stream, Frame(3));
                    await handler.ThirdDone.Task.WaitAsync(WaitTimeout);

                    Assert.IsFalse(handler.BusyDone.Task.IsCompleted,
                        "超时消息仍占用并发额度，第三条消息被忙等消息挡住");
                    Assert.IsTrue(executor.IsActive, "单消息超时不应进入收口");
                    Assert.IsFalse(stream.Closed.Task.IsCompleted, "单消息超时不应关闭传输层");

                    await handler.BusyDone.Task.WaitAsync(WaitTimeout);
                }
                finally
                {
                    await StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 未配置消息超时时 handler 拿到的就是连接令牌本身，收口取消连接令牌后同步取消
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Token_NoMessageTimeout_UsesConnectionTokenDirectly()
        {
            var options = NewOptions(queueCapacity: 8, maxConcurrent: 1);
            CancellationToken captured;

            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawByteStream("token-direct");
                var handler = new TokenCaptureHandler();
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(stream, new TestChannel("token-direct"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();
                    await WriteAsync(stream, Frame(1));
                    await handler.Seen.Task.WaitAsync(WaitTimeout);
                    captured = handler.Token;

                    // 未配置超时时不建链接令牌：handler 拿到的就是连接令牌本身，不是包装出来的新令牌。
                    Assert.AreEqual(executor.CurrentToken, captured, "未配置超时时消息令牌应是连接令牌本身");
                    Assert.IsFalse(captured.IsCancellationRequested, "消息执行期间连接令牌不应被取消");
                }
                finally
                {
                    await StopAsync(executor);
                }
            }

            // 令牌同源，收口取消连接令牌时消息令牌必然同步取消。
            Assert.IsTrue(captured.IsCancellationRequested, "收口后消息令牌未随连接令牌取消");
        }

        /// <summary>
        /// 外部断开（取消传输令牌并中断挂起读取）后泵退出、队列与 accumulator 归零、属性清空、引用配平，且不再调用 ReadAsync
        /// </summary>
        /// <returns>该用例的异步执行任务</returns>
        [TestMethod]
        public async Task Acceptance25_ExternalDisconnect_StopsPump_NoBusyLoop_AndBalance()
        {
            var baselineNet = NetRented();
            var options = NewOptions();
            var transportCts = new CancellationTokenSource();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                var channel = new DefaultChannel("external-disconnect", null);
                var stream = new TestRawByteStream("external-disconnect");

                var executor = new ChannelPipelineExecutor(stream, channel, options, transportCts.Token);
                executor.Attach(pipeline);

                executor.Start();
                await Task.Delay(100);
                Assert.IsTrue(stream.Reader.ReadCalls >= 1, "泵未进入读取状态");

                channel.SetAttribute("auth", true);
                transportCts.Cancel();
                stream.Input.CancelPendingRead();

                await executor.Completion.WaitAsync(TimeSpan.FromMilliseconds(500));
                await WaitUntilAsync(() => GetPumpTask(executor).IsCompleted, "泵任务在 500ms 内未结束");

                Assert.AreEqual(0, executor.QueuedCount, "收口后队列未归零");
                Assert.AreEqual(0, executor.Traffic().BufferedBytes, "收口后 accumulator 未归零");
                Assert.IsFalse(channel.IsActive, "收口后通道仍活跃");
                Assert.AreEqual(0, channel.Attributes.Count, "收口后属性未清空");

                // 泵退出后不得因 IsCanceled 忙循环，200ms 内不再出现新的 ReadAsync。
                var readsAfterClose = stream.Reader.ReadCalls;
                await Task.Delay(200);
                Assert.AreEqual(readsAfterClose, stream.Reader.ReadCalls, "泵退出后仍在调用 ReadAsync（忙循环）");

                await WaitUntilAsync(() => NetRented() == baselineNet, "外部断开后引用未配平");

                await stream.CloseAsync("again");
                await channel.CloseAsync("again");
            }
        }

        // ---------- 测试基础设施 ----------

        private static ChannelPipelineOptions NewOptions(
            int queueCapacity = 16,
            int maxConcurrent = 1,
            TimeSpan? messageTimeout = null)
        {
            return new ChannelPipelineOptions
            {
                Splitter = LengthFieldSplitter.Default(64 * 1024),
                QueueCapacity = queueCapacity,
                MaxConcurrentMessages = maxConcurrent,
                MessageTimeout = messageTimeout ?? TimeSpan.Zero,
                Guard = NullChannelGuard.Instance
            };
        }

        private static async Task StopAsync(ChannelPipelineExecutor executor)
        {
            await executor.BeginShutdown("test-done");
            await executor.Completion.WaitAsync(TimeSpan.FromSeconds(5));
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

        private static async Task<List<byte[]>> CollectFramesAsync(byte[] wire, int[] segments, int expectedFrames)
        {
            // 单并发：本用例只验证"切分不影响帧内容"，帧间顺序由 Concurrency_SameConnection_KeepsReceiveOrder_AndPerMessageChainOrder 单独覆盖；
            // 并发执行会让收集列表的顺序脱离接收顺序，造成与一次性输入的错误比对。
            var options = NewOptions(queueCapacity: 64, maxConcurrent: 1);
            using (var pipeline = ChannelPipeline.Create(options))
            {
                var stream = new TestRawByteStream("split-" + (segments == null ? "one" : segments.Length));
                var handler = new FrameCollectionHandler();
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(stream, new TestChannel(stream.ChannelId), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();
                    if (segments == null)
                    {
                        await WriteAsync(stream, wire);
                    }
                    else
                    {
                        var offset = 0;
                        foreach (var size in segments)
                        {
                            var chunk = new byte[size];
                            Buffer.BlockCopy(wire, offset, chunk, 0, size);
                            offset += size;
                            await WriteAsync(stream, chunk);
                        }
                    }

                    await WaitUntilAsync(() => handler.Frames.Count == expectedFrames,
                        "切分输入未产出预期帧数 " + expectedFrames);
                    await Task.Delay(20);
                    return handler.Frames.ToList();
                }
                finally
                {
                    await StopAsync(executor);
                }
            }
        }

        private static byte[] BuildFrames(int count, int bodyLength, int seed)
        {
            var result = new byte[count * (FrameConstants.P_Head + bodyLength)];
            for (var i = 0; i < count; i++)
            {
                var frame = Frame((byte)((seed * count + i) % 251), new byte[bodyLength]);
                Buffer.BlockCopy(frame, 0, result, i * frame.Length, frame.Length);
            }

            return result;
        }

        private static long RentedTotal()
        {
            var stats = MemoryPoolManager.GetStatistics();
            return stats.SmallPoolRented + stats.MediumPoolRented + stats.LargePoolRented;
        }

        private static long ReturnedTotal()
        {
            var stats = MemoryPoolManager.GetStatistics();
            return stats.SmallPoolReturned + stats.MediumPoolReturned + stats.LargePoolReturned;
        }

        private static long NetRented()
        {
            return RentedTotal() - ReturnedTotal();
        }

        private static Task GetPumpTask(ChannelPipelineExecutor executor)
        {
            var field = typeof(ChannelPipelineExecutor).GetField(
                "_pumpTask", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "无法定位执行器泵任务字段");
            return (Task)field.GetValue(executor);
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

        private static int TypeOf(object msg)
        {
            return ((PooledMessage)msg).Frame.Span[0];
        }

        private static async Task WriteAsync(TestRawByteStream stream, byte[] data)
        {
            await stream.Pipe.Writer.WriteAsync(data);
            await stream.Pipe.Writer.FlushAsync();
        }

        private sealed class CountingPipeReader : PipeReader
        {
            private readonly PipeReader _inner;
            private int _readCalls;
            private int _nonEmptyReads;

            public CountingPipeReader(PipeReader inner)
            {
                _inner = inner;
            }

            public int ReadCalls
            {
                get { return Volatile.Read(ref _readCalls); }
            }

            public int NonEmptyReads
            {
                get { return Volatile.Read(ref _nonEmptyReads); }
            }

            public override void AdvanceTo(SequencePosition consumed)
            {
                _inner.AdvanceTo(consumed);
            }

            public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
            {
                _inner.AdvanceTo(consumed, examined);
            }

            public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default(CancellationToken))
            {
                Interlocked.Increment(ref _readCalls);
                return ReadAndCountAsync(cancellationToken);
            }

            private async ValueTask<ReadResult> ReadAndCountAsync(CancellationToken cancellationToken)
            {
                var result = await _inner.ReadAsync(cancellationToken).ConfigureAwait(false);
                if (result.Buffer.Length > 0)
                    Interlocked.Increment(ref _nonEmptyReads);
                return result;
            }

            public override bool TryRead(out ReadResult result)
            {
                return _inner.TryRead(out result);
            }

            public override void CancelPendingRead()
            {
                _inner.CancelPendingRead();
            }

            public override void Complete(Exception exception = null)
            {
                _inner.Complete(exception);
            }
        }

        private sealed class TestRawByteStream : IRawByteStream
        {
            private readonly Pipe _pipe;
            private readonly CountingPipeReader _reader;
            private readonly List<byte[]> _sent = new List<byte[]>();

            public TestRawByteStream(string id)
            {
                ChannelId = id;
                _pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 4 * 1024 * 1024));
                _reader = new CountingPipeReader(_pipe.Reader);
            }

            public string ChannelId { get; }

            public Pipe Pipe
            {
                get { return _pipe; }
            }

            public PipeReader Input
            {
                get { return _reader; }
            }

            public CountingPipeReader Reader
            {
                get { return _reader; }
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

        private sealed class SequenceRecordingHandler : InboundHandlerAdapter
        {
            private readonly ConcurrentQueue<byte> _types = new ConcurrentQueue<byte>();

            public int Count
            {
                get { return _types.Count; }
            }

            public IEnumerable<byte> Types
            {
                get { return _types; }
            }

            public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                _types.Enqueue((byte)TypeOf(msg));
                await ctx.FireChannelReadAsync(msg);
            }
        }

        private sealed class SlowAuthHandler : InboundHandlerAdapter
        {
            private readonly int[] _delays;
            private readonly ConcurrentQueue<byte> _completionOrder = new ConcurrentQueue<byte>();
            private int _active;
            private int _peak;
            private int _completed;

            public SlowAuthHandler(int[] delays)
            {
                _delays = delays;
            }

            public byte[] CompletionOrder
            {
                get { return _completionOrder.ToArray(); }
            }

            public int PeakConcurrency
            {
                get { return Volatile.Read(ref _peak); }
            }

            public int Completed
            {
                get { return Volatile.Read(ref _completed); }
            }

            public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var seq = TypeOf(msg);
                var active = Interlocked.Increment(ref _active);
                while (true)
                {
                    var peak = Volatile.Read(ref _peak);
                    if (active <= peak || Interlocked.CompareExchange(ref _peak, active, peak) == peak)
                        break;
                }

                try
                {
                    await Task.Delay(_delays[seq], ctx.CancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Decrement(ref _active);
                }

                _completionOrder.Enqueue((byte)seq);
                Interlocked.Increment(ref _completed);

                await ctx.FireChannelReadAsync(msg).ConfigureAwait(false);
            }
        }

        private sealed class ChainTracker
        {
            private readonly ConcurrentQueue<string> _entries = new ConcurrentQueue<string>();
            private int _active;
            private int _peak;

            public int Count
            {
                get { return _entries.Count; }
            }

            public int PeakConcurrency
            {
                get { return Volatile.Read(ref _peak); }
            }

            public void Enter(string name)
            {
                _entries.Enqueue(name);
                var active = Interlocked.Increment(ref _active);
                while (true)
                {
                    var peak = Volatile.Read(ref _peak);
                    if (active <= peak || Interlocked.CompareExchange(ref _peak, active, peak) == peak)
                        break;
                }
            }

            public void Leave()
            {
                Interlocked.Decrement(ref _active);
            }

            public int IndexOf(string entry)
            {
                var index = 0;
                foreach (var item in _entries)
                {
                    if (item == entry)
                        return index;
                    index++;
                }

                return -1;
            }
        }

        private sealed class ChainStepHandler : InboundHandlerAdapter
        {
            private readonly string _name;
            private readonly ChainTracker _tracker;

            public ChainStepHandler(string name, ChainTracker tracker)
            {
                _name = name;
                _tracker = tracker;
            }

            public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var type = TypeOf(msg);
                _tracker.Enter(_name + ":" + type);
                try
                {
                    if (_name == "A")
                        await Task.Delay(1 + type % 5);

                    await ctx.FireChannelReadAsync(msg);
                }
                finally
                {
                    _tracker.Leave();
                }
            }
        }

        private sealed class TwoGateHandler : InboundHandlerAdapter
        {
            private readonly ConcurrentQueue<byte> _types = new ConcurrentQueue<byte>();
            private int _calls;

            public readonly TaskCompletionSource<bool> FirstEntered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public readonly TaskCompletionSource<bool> ReleaseFirst =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public readonly TaskCompletionSource<bool> SecondEntered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public readonly TaskCompletionSource<bool> ReleaseSecond =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public int Count
            {
                get { return _types.Count; }
            }

            public IEnumerable<byte> Types
            {
                get { return _types; }
            }

            public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                _types.Enqueue((byte)TypeOf(msg));
                var call = Interlocked.Increment(ref _calls);

                if (call == 1)
                {
                    FirstEntered.TrySetResult(true);
                    await ReleaseFirst.Task;
                }
                else if (call == 2)
                {
                    SecondEntered.TrySetResult(true);
                    await ReleaseSecond.Task;
                }
            }
        }

        private sealed class FrameCollectionHandler : InboundHandlerAdapter
        {
            public readonly List<byte[]> Frames = new List<byte[]>();

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                lock (Frames)
                {
                    Frames.Add(((PooledMessage)msg).Frame.ToArray());
                }

                return Task.CompletedTask;
            }
        }

        private sealed class CountingHandler : InboundHandlerAdapter
        {
            private int _count;

            public int Count
            {
                get { return Volatile.Read(ref _count); }
            }

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                Interlocked.Increment(ref _count);
                return Task.CompletedTask;
            }
        }

        private sealed class ReadCompleteCountingHandler : InboundHandlerAdapter
        {
            private int _frames;
            private int _completes;

            public int FrameCount
            {
                get { return Volatile.Read(ref _frames); }
            }

            public int CompleteCount
            {
                get { return Volatile.Read(ref _completes); }
            }

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                Interlocked.Increment(ref _frames);
                return Task.CompletedTask;
            }

            public override Task ChannelReadCompleteAsync(IChannelHandlerContext ctx)
            {
                Interlocked.Increment(ref _completes);
                return Task.CompletedTask;
            }
        }

        private sealed class ForwardingCountingHandler : InboundHandlerAdapter
        {
            private int _count;

            public int Count
            {
                get { return Volatile.Read(ref _count); }
            }

            public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                Interlocked.Increment(ref _count);
                await ctx.FireChannelReadAsync(msg);
            }
        }

        private sealed class ThrowingCompleteHandler : InboundHandlerAdapter
        {
            private int _completeCalls;

            public int CompleteCalls
            {
                get { return Volatile.Read(ref _completeCalls); }
            }

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                return ctx.FireChannelReadAsync(msg);
            }

            public override Task ChannelReadCompleteAsync(IChannelHandlerContext ctx)
            {
                Interlocked.Increment(ref _completeCalls);
                throw new InvalidOperationException("read-complete boom");
            }
        }

        private sealed class WriteFrameOnReadHandler : InboundHandlerAdapter
        {
            private readonly byte[] _payload;

            public WriteFrameOnReadHandler(byte[] payload)
            {
                _payload = payload;
            }

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                return ctx.Pipeline.WriteAsync(new TransportFrame(_payload, null));
            }
        }

        private sealed class LoggingOutboundHandler : OutboundHandlerAdapter
        {
            private readonly string _name;
            private readonly ConcurrentQueue<string> _log;

            public LoggingOutboundHandler(string name, ConcurrentQueue<string> log)
            {
                _name = name;
                _log = log;
            }

            public override Task WriteAsync(IChannelHandlerContext ctx, object msg)
            {
                _log.Enqueue(_name);
                return ctx.WriteAsync(msg);
            }
        }

        private sealed class CancelAwareBlockingHandler : InboundHandlerAdapter
        {
            public readonly TaskCompletionSource<bool> Entered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public volatile bool Cancelled;

            public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
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

        private sealed class PoolingObservationHandler : InboundHandlerAdapter
        {
            private int _count;
            private int _pooled;

            public int Count
            {
                get { return Volatile.Read(ref _count); }
            }

            public int PooledCount
            {
                get { return Volatile.Read(ref _pooled); }
            }

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var pooled = (PooledMessage)msg;
                if (pooled.IsPooled)
                    Interlocked.Increment(ref _pooled);
                Interlocked.Increment(ref _count);
                return Task.CompletedTask;
            }
        }

        private sealed class TimeoutConcurrencyHandler : InboundHandlerAdapter
        {
            public readonly TaskCompletionSource<bool> ThirdDone =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public readonly TaskCompletionSource<bool> BusyDone =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var type = TypeOf(msg);
                if (type == 1)
                {
                    // 只等消息令牌，100ms 超时后取消。
                    await Task.Delay(Timeout.Infinite, ctx.CancellationToken);
                }
                else if (type == 2)
                {
                    // 忽略令牌，忙等 300ms。
                    await Task.Delay(300);
                    BusyDone.TrySetResult(true);
                }
                else
                {
                    ThirdDone.TrySetResult(true);
                }
            }
        }

        private sealed class TokenCaptureHandler : InboundHandlerAdapter
        {
            public readonly TaskCompletionSource<bool> Seen =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public CancellationToken Token;

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                Token = ctx.CancellationToken;
                Seen.TrySetResult(true);
                return Task.CompletedTask;
            }
        }

        private sealed class ExceptionTailHandler : InboundHandlerAdapter
        {
            public readonly ConcurrentQueue<Exception> Exceptions = new ConcurrentQueue<Exception>();

            public override Task ChannelExceptionCaughtAsync(IChannelHandlerContext ctx, Exception ex)
            {
                Exceptions.Enqueue(ex);
                return Task.CompletedTask;
            }
        }
    }
}