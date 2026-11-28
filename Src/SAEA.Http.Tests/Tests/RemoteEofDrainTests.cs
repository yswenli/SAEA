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
 *命名空间：SAEA.Http.Tests
 *文件名： RemoteEofDrainTests
 *版本号： net10.0
 *唯一标识：608e5e64-eda8-4a82-b0cb-8f4a34ee4237
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/28 07:07:26
 *描述：对端半关写方向后先排空在途再收口的确定性管线用例
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/28 11:56:11
 *修改人： yswenli
 *版本号： net10.0
 *描述：新增 RemoteEofDrainTimeout 覆盖 DrainTimeout 的排空上限用例
 *
 *****************************************************************************/
using System;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Http.Tests.Tests.Support;
using SAEA.Sockets.Pipeline;

namespace SAEA.Http.Tests
{
    /// <summary>
    /// 对端只半关写方向（读到 EOF 但接收方向仍可用）时，执行器必须先等在途消息与已提交写出
    /// 排空再收口，不能把在途响应写拒绝掉。
    /// </summary>
    [TestClass]
    public class RemoteEofDrainTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 处理器在 EOF 之后才写出响应：排空期间写必须成功交接，传输层要等处理器放行后才关闭。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task RemoteEofDrain_InFlightHandlerWriteAfterEof_IsDelivered()
        {
            var options = PipelineTestKit.CreateOptions(new TakeAvailableSplitter(64 * 1024));
            options.DrainOnRemoteEof = true;
            options.DrainTimeout = TimeSpan.FromSeconds(3);
            options.MaxConcurrentMessages = 1;

            var stream = new TestRawByteStream("remote-eof-drain");
            var handler = new HoldingResponseHandler();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel("remote-eof-drain"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    await PipelineTestKit.WriteAsync(stream, Encoding.ASCII.GetBytes("PING")).ConfigureAwait(false);

                    await handler.Entered.WaitAsync(WaitTimeout).ConfigureAwait(false);

                    // 完成写端模拟对端 FIN：泵的下一轮读取会返回 IsCompleted，但接收方向仍可用。
                    stream.Pipe.Writer.Complete();

                    // 给泵一点时间真正读到 EOF；此时处理器仍被挂起，连接不能已经收口。
                    await Task.Delay(200).ConfigureAwait(false);

                    Assert.IsFalse(
                        stream.Closed.Task.IsCompleted,
                        "读到 EOF 后应等排空在途消息，而不是立刻关闭传输层。");

                    handler.Release();

                    var reason = await stream.Closed.Task.WaitAsync(WaitTimeout).ConfigureAwait(false);

                    Assert.AreEqual("remote-eof", reason, "关闭原因应保持为对端半关。");
                    Assert.IsNull(handler.WriteError, "排空期间的在途写不应被拒绝。");

                    var sent = stream.Sent.ToArray();
                    Assert.AreEqual(1, sent.Length, "应恰好写出一帧响应。");
                    CollectionAssert.AreEqual(handler.ResponseBytes, sent[0], "响应字节应完整交接给传输层。");
                }
                finally
                {
                    handler.Release();
                    await PipelineTestKit.StopAsync(executor).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// 处理器永远不完成时，排空等待必须受 DrainTimeout 约束：半关且不读的对端不能把连接挂住。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task RemoteEofDrain_HandlerNeverCompletes_ClosesWithinTimeout()
        {
            var options = PipelineTestKit.CreateOptions(new TakeAvailableSplitter(64 * 1024));
            options.DrainOnRemoteEof = true;
            options.DrainTimeout = TimeSpan.FromMilliseconds(200);

            // 处理器永不结束时取消等待无法打断它，整体收口必须由 DrainTimeout 兜底；
            // 用有界排空策略避免收口停在 CancelPending 的无限等待上。
            options.DrainPolicy = DisconnectDrainPolicy.DrainInFlight;

            var stream = new TestRawByteStream("remote-eof-timeout");
            var handler = new HoldingResponseHandler();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel("remote-eof-timeout"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    await PipelineTestKit.WriteAsync(stream, Encoding.ASCII.GetBytes("PING")).ConfigureAwait(false);

                    await handler.Entered.WaitAsync(WaitTimeout).ConfigureAwait(false);

                    stream.Pipe.Writer.Complete();

                    var reason = await stream.Closed.Task.WaitAsync(WaitTimeout).ConfigureAwait(false);

                    Assert.AreEqual("remote-eof", reason, "排空超时后仍应按对端半关收口。");
                }
                finally
                {
                    handler.Release();
                    await PipelineTestKit.StopAsync(executor).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// 排空期间传输层发送失败时写方向已不可用：必须立即收口，不能等处理器跑完或排空超时。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task RemoteEofDrain_WriteFailsWhileDraining_ClosesWithoutWaitingForHandler()
        {
            var options = PipelineTestKit.CreateOptions(new TakeAvailableSplitter(64 * 1024));
            options.DrainOnRemoteEof = true;

            // 排空上限远大于断言窗口：提前收口只能来自写失败，不能是超时兜底。
            options.DrainTimeout = TimeSpan.FromSeconds(10);
            options.DrainPolicy = DisconnectDrainPolicy.DrainInFlight;
            options.MaxConcurrentMessages = 1;

            var stream = new FailingSendStream("remote-eof-write-failure");
            var handler = new FailingWriteHoldingHandler();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel("remote-eof-write-failure"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    await stream.WriteAsync(Encoding.ASCII.GetBytes("PING")).WaitAsync(WaitTimeout).ConfigureAwait(false);

                    await handler.Entered.WaitAsync(WaitTimeout).ConfigureAwait(false);

                    // 处理器仍在挂起时对端 FIN 到达，排空开始，但连接此时仍应为活跃。
                    stream.Pipe.Writer.Complete();
                    await Task.Delay(200).ConfigureAwait(false);

                    Assert.IsTrue(executor.IsActive, "排空开始时连接仍应活跃，用于区分提前收口与既有失败。");

                    handler.FailWrite();
                    await handler.WriteCompleted.WaitAsync(WaitTimeout).ConfigureAwait(false);

                    var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
                    while (executor.IsActive && DateTime.UtcNow < deadline)
                        await Task.Delay(20).ConfigureAwait(false);

                    Assert.IsFalse(executor.IsActive, "写出失败后应立即收口，不应等处理器跑完或排空超时。");
                    Assert.IsNotNull(handler.WriteError, "本次写应失败，用于触发写方向失效。");
                }
                finally
                {
                    handler.Release();
                    await PipelineTestKit.StopAsync(executor).ConfigureAwait(false);
                }

                Assert.AreEqual(
                    "remote-eof",
                    await stream.Closed.Task.WaitAsync(WaitTimeout).ConfigureAwait(false),
                    "提前收口仍应保留对端半关的关闭原因。");
            }
        }

        /// <summary>
        /// 单独配置 RemoteEofDrainTimeout 时排空上限必须以它为准：DrainTimeout 已到也不能提前收口。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task RemoteEofDrain_RemoteEofTimeoutOverridesDrainTimeout_KeepsWaiting()
        {
            var options = PipelineTestKit.CreateOptions(new TakeAvailableSplitter(64 * 1024));
            options.DrainOnRemoteEof = true;
            options.DrainTimeout = TimeSpan.FromMilliseconds(200);
            options.RemoteEofDrainTimeout = TimeSpan.FromSeconds(3);
            options.MaxConcurrentMessages = 1;
            options.DrainPolicy = DisconnectDrainPolicy.DrainInFlight;

            var stream = new TestRawByteStream("remote-eof-custom-timeout");
            var handler = new HoldingResponseHandler();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(handler);

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel("remote-eof-custom-timeout"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    await PipelineTestKit.WriteAsync(stream, Encoding.ASCII.GetBytes("PING")).ConfigureAwait(false);

                    await handler.Entered.WaitAsync(WaitTimeout).ConfigureAwait(false);

                    stream.Pipe.Writer.Complete();

                    // 越过 DrainTimeout 的 200ms，但仍在 RemoteEofDrainTimeout 的 3 秒内。
                    await Task.Delay(700).ConfigureAwait(false);

                    Assert.IsFalse(
                        stream.Closed.Task.IsCompleted,
                        "配置了更长的 RemoteEofDrainTimeout 时不应按 DrainTimeout 提前收口。");

                    handler.Release();

                    var reason = await stream.Closed.Task.WaitAsync(WaitTimeout).ConfigureAwait(false);

                    Assert.AreEqual("remote-eof", reason, "排空完成后仍应按对端半关收口。");
                    Assert.IsNull(handler.WriteError, "排空期间的在途写不应被拒绝。");

                    var sent = stream.Sent.ToArray();
                    Assert.AreEqual(1, sent.Length, "应恰好写出一帧响应。");
                    CollectionAssert.AreEqual(handler.ResponseBytes, sent[0], "响应字节应完整交接给传输层。");
                }
                finally
                {
                    handler.Release();
                    await PipelineTestKit.StopAsync(executor).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// 收到入站消息后挂起，直到测试放行才通过独立业务写回一帧响应，并记录本次写的异常。
        /// </summary>
        private sealed class HoldingResponseHandler : InboundHandlerAdapter
        {
            private static readonly byte[] Response =
                Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 4\r\n\r\nDONE");

            private readonly TaskCompletionSource<bool> _entered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> _release =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private Exception _writeError;

            /// <summary>处理器收到第一条消息后完成。</summary>
            public Task Entered
            {
                get { return _entered.Task; }
            }

            /// <summary>本次响应写出的异常；成功时为 null。</summary>
            public Exception WriteError
            {
                get { return _writeError; }
            }

            /// <summary>本处理器写出的完整响应字节。</summary>
            public byte[] ResponseBytes
            {
                get { return Response; }
            }

            /// <summary>放行挂起的处理器；重复调用无副作用。</summary>
            public void Release()
            {
                _release.TrySetResult(true);
            }

            /// <inheritdoc />
            public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                _entered.TrySetResult(true);

                await _release.Task.ConfigureAwait(false);

                try
                {
                    // 入站处理器发起独立业务写必须走真实链；ctx.WriteAsync 只允许在出站回调内转发。
                    await ctx.Pipeline.WriteAsync(new RawFrame(Response, null)).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _writeError = ex;
                }
            }
        }

        /// <summary>
        /// 收到入站消息后等测试指令写一次注定失败的响应，随后继续挂起，保持消息在途。
        /// </summary>
        private sealed class FailingWriteHoldingHandler : InboundHandlerAdapter
        {
            private readonly TaskCompletionSource<bool> _entered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> _failWrite =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> _writeCompleted =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> _release =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private Exception _writeError;

            /// <summary>处理器收到第一条消息后完成。</summary>
            public Task Entered
            {
                get { return _entered.Task; }
            }

            /// <summary>本次响应写出结束（成功或失败）后完成。</summary>
            public Task WriteCompleted
            {
                get { return _writeCompleted.Task; }
            }

            /// <summary>本次响应写出的异常；成功时为 null。</summary>
            public Exception WriteError
            {
                get { return _writeError; }
            }

            /// <summary>放行处理器执行那一次注定失败的写；重复调用无副作用。</summary>
            public void FailWrite()
            {
                _failWrite.TrySetResult(true);
            }

            /// <summary>放行挂起的处理器；重复调用无副作用。</summary>
            public void Release()
            {
                _release.TrySetResult(true);
            }

            /// <inheritdoc />
            public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                _entered.TrySetResult(true);

                await _failWrite.Task.ConfigureAwait(false);

                try
                {
                    await ctx.Pipeline.WriteAsync(new RawFrame(Encoding.ASCII.GetBytes("FAILED"), null)).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _writeError = ex;
                }
                finally
                {
                    _writeCompleted.TrySetResult(true);
                }

                await _release.Task.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 发送必定失败的内存字节流：模拟客户端已断开、写方向不可用的连接。
        /// </summary>
        private sealed class FailingSendStream : IRawByteStream
        {
            private readonly Pipe _pipe;

            /// <summary>以连接标识创建发送必定失败的字节流。</summary>
            /// <param name="id">连接标识。</param>
            public FailingSendStream(string id)
            {
                ChannelId = id;

                // 与测试替身保持一致：4 MB 写阈值让写入整段请求时不会因管道背压阻塞。
                _pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 4 * 1024 * 1024));
            }

            /// <inheritdoc />
            public string ChannelId { get; }

            /// <summary>底层管道，供测试直接写入或观察。</summary>
            public Pipe Pipe
            {
                get { return _pipe; }
            }

            /// <inheritdoc />
            public PipeReader Input
            {
                get { return _pipe.Reader; }
            }

            /// <summary>传输层关闭时完成，结果为关闭原因。</summary>
            public TaskCompletionSource<string> Closed { get; } =
                new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>向管道写入一段字节并立即刷新。</summary>
            /// <param name="data">待写入字节。</param>
            /// <returns>写入并刷新完成后结束的任务。</returns>
            public async Task WriteAsync(byte[] data)
            {
                await _pipe.Writer.WriteAsync(data).ConfigureAwait(false);
                await _pipe.Writer.FlushAsync().ConfigureAwait(false);
            }

            /// <inheritdoc />
            public Task SendAsync(ReadOnlyMemory<byte> data, IDisposable owner)
            {
                // 发送失败也由传输层释放所有权；否则池化缓冲会被这次失败泄漏。
                if (owner != null)
                {
                    try { owner.Dispose(); }
                    catch (Exception) { }
                }

                throw new ChannelClosedException("模拟写方向失效:" + ChannelId);
            }

            /// <inheritdoc />
            public void Pause()
            {
            }

            /// <inheritdoc />
            public void Resume()
            {
            }

            /// <inheritdoc />
            public Task CloseAsync(string reason)
            {
                Closed.TrySetResult(reason);

                // 取消挂起的读取，让接收泵能感知传输层关闭而不是永久等待。
                try { _pipe.Reader.CancelPendingRead(); }
                catch (InvalidOperationException) { }

                return Task.CompletedTask;
            }
        }
    }
}