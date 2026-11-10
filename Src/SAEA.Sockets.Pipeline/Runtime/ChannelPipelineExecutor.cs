/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.Sockets.Pipeline
*文件名： ChannelPipelineExecutor
*版本号： v26.11.5.1
*唯一标识：b81745bb-1b3d-42f1-9353-365ecef2d13e
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：每连接的运行时载体：一个接收泵把字节切成帧写入有界队列，一个消费者循环把帧交给链
*
*=====================================================================
*修改标记
*修改时间：2026/11/9 12:05:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：分帧协议错误先上链，派发后仍为 Open 才强制收口，已进入收口状态不再起第二个关闭流程
*
*****************************************************************************/
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using SAEA.Common;
using SAEA.Common.Caching;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 每连接的运行时载体：一个接收泵把字节切成帧写入有界队列，一个消费者循环把帧交给链。
    /// 队列满时泵暂停读取并等待低水位信号，恢复动作由消费侧执行。
    /// </summary>
    public sealed class ChannelPipelineExecutor : IDisposable
    {
        private const int StateCreated = 0;
        private const int StateAttached = 1;
        private const int StateStarted = 2;
        private const int StateClosing = 3;
        private const int StateCompleted = 4;

        private readonly IRawByteStream _stream;
        private readonly PipeReader _reader;
        private readonly IChannel _channel;
        private readonly ChannelPipelineOptions _options;
        private readonly IPipelineHost _host;

        private readonly CancellationTokenSource _pumpCts = new CancellationTokenSource();
        private readonly CancellationTokenSource _connectionToken = new CancellationTokenSource();
        private readonly CancellationTokenSource _linked;

        private readonly System.Threading.Channels.Channel<PooledMessage> _queue;
        private readonly SemaphoreSlim _resumeSignal = new SemaphoreSlim(0, 1);
        private readonly SemaphoreSlim _concurrency;
        private readonly AsyncLocal<CancellationToken> _ambientToken = new AsyncLocal<CancellationToken>();

        // 活动与失活回调共用同一把闸门：保证 ChannelActiveAsync 不会晚于 ChannelInactiveAsync。
        private readonly SemaphoreSlim _lifecycleGate = new SemaphoreSlim(1, 1);

        private readonly object _shutdownGate = new object();
        private readonly TaskCompletionSource<bool> _completion =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentDictionary<int, Task> _inFlight = new ConcurrentDictionary<int, Task>();
        private readonly object _drainGate = new object();

        private ChannelPipeline _pipeline;
        private int _queued;
        private int _queuePaused;
        private bool _pendingComplete;
        private long _totalReceived;
        private long _bufferedBytes;
        private Task _pumpTask;
        private Task _consumerTask;
        private int _closing;
        private Task _shutdownTask;
        private string _shutdownReason;
        private int _inFlightSeq;
        private int _inFlightCount;
        private int _inlineDispatched;
        private int _state;
        private bool _activeDispatched;
        private bool _inactiveDispatched;

        /// <summary>
        /// 创建执行器。传输层令牌取消表示连接已断开，是消息令牌的兜底来源；
        /// 取消时一定转入收口，而不只是让泵循环退出。
        /// </summary>
        /// <param name="stream">与传输层解耦的字节流接入面，不能为 null。</param>
        /// <param name="channel">连接的应用级视图，不能为 null。</param>
        /// <param name="options">执行器配置，不能为 null。</param>
        /// <param name="transportToken">传输层断开令牌，取消时转入收口。</param>
        /// <param name="host">连接登记表；不需要登记时为 null。</param>
        public ChannelPipelineExecutor(
            IRawByteStream stream,
            IChannel channel,
            ChannelPipelineOptions options,
            CancellationToken transportToken,
            IPipelineHost host = null)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (channel == null)
                throw new ArgumentNullException(nameof(channel));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            _stream = stream;
            _reader = stream.Input;
            _channel = channel;
            _options = options;
            _host = host;

            _concurrency = new SemaphoreSlim(options.MaxConcurrentMessages, options.MaxConcurrentMessages);
            _queue = System.Threading.Channels.Channel.CreateBounded<PooledMessage>(
                new System.Threading.Channels.BoundedChannelOptions(options.QueueCapacity)
                {
                    FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait,
                    SingleReader = true,
                    SingleWriter = true
                });

            _linked = CancellationTokenSource.CreateLinkedTokenSource(_pumpCts.Token, transportToken);
            transportToken.Register(OnTransportCancelled);
        }

        /// <summary>连接的应用级视图。</summary>
        public IChannel Channel
        {
            get { return _channel; }
        }

        /// <summary>是否尚未进入收口，供链头出站做快速拒绝。</summary>
        public bool IsActive
        {
            get { return Volatile.Read(ref _closing) == 0; }
        }

        /// <summary>收口第 2–6 步的后台任务；完成后表示资源已回收。</summary>
        public Task Completion
        {
            get { return _completion.Task; }
        }

        /// <summary>本执行器使用的配置。</summary>
        internal ChannelPipelineOptions Options
        {
            get { return _options; }
        }

        /// <summary>连接登记表；未配置时为 null。</summary>
        internal IPipelineHost Host
        {
            get { return _host; }
        }

        /// <summary>
        /// 当前异步流中的消息令牌；消息执行包注入时用注入值，否则回落到连接级令牌。
        /// </summary>
        internal CancellationToken CurrentToken
        {
            get
            {
                var scoped = _ambientToken.Value;
                return scoped == default(CancellationToken) ? _connectionToken.Token : scoped;
            }
        }

        /// <summary>当前队列中的消息数，仅供测试与诊断读取。</summary>
        internal int QueuedCount
        {
            get { return Volatile.Read(ref _queued); }
        }

        /// <summary>当前可用的在途消息额度，仅供测试与诊断读取。</summary>
        internal int AvailableConcurrency
        {
            get { return _concurrency.CurrentCount; }
        }

        /// <summary>已通过内联快路径启动的消息数，仅供测试与诊断读取。</summary>
        internal int InlineDispatchedCount
        {
            get { return Volatile.Read(ref _inlineDispatched); }
        }

        /// <summary>把本执行器与通道链双向绑定，必须在 <see cref="Start"/> 之前完成。</summary>
        /// <param name="pipeline">要绑定的通道链，不能为 null。</param>
        public void Attach(ChannelPipeline pipeline)
        {
            if (pipeline == null)
                throw new ArgumentNullException(nameof(pipeline));

            lock (_shutdownGate)
            {
                var state = Volatile.Read(ref _state);
                if (state == StateCreated)
                {
                    _pipeline = pipeline;
                    pipeline.Attach(_stream, this);
                    Volatile.Write(ref _state, StateAttached);
                    return;
                }

                if (state == StateAttached)
                    throw new InvalidOperationException("执行器已绑定通道链，不能重复 Attach。");

                throw new InvalidOperationException("连接已进入收口或已完成，不能再 Attach 通道链。");
            }
        }

        /// <summary>
        /// 请求进入收口。只置状态并启动后台收口，不等待收口完成；
        /// 需要彻底回收的调用方等待 <see cref="Completion"/>。
        /// </summary>
        /// <param name="reason">关闭原因，写入日志用于区分收口来源。</param>
        /// <returns>启动收口后即完成的任务，不等待资源回收。</returns>
        public Task BeginShutdown(string reason)
        {
            BeginShutdown(reason, false);
            return Task.CompletedTask;
        }

        /// <summary>泵自身触发收口时用，避免"外部收口等泵、泵等收口"互等。</summary>
        internal Task BeginShutdown(string reason, bool fromPump)
        {
            lock (_shutdownGate)
            {
                return BeginShutdownLocked(reason, fromPump);
            }
        }

        private Task BeginShutdownLocked(string reason, bool fromPump)
        {
            if (Interlocked.CompareExchange(ref _closing, 1, 0) == 0)
            {
                _shutdownReason = reason;

                // 状态门先落地：Closing 之后 Attach / Start 必须直接抛，不能再建新资源。
                Volatile.Write(ref _state, StateClosing);

                var defaultChannel = _channel as DefaultChannel;
                if (defaultChannel != null)
                    defaultChannel.MarkInactive();

                _shutdownTask = Task.Run(() => ShutdownCoreAsync(fromPump));
            }

            return _shutdownTask ?? Task.CompletedTask;
        }

        /// <summary>
        /// 启动接收泵与消费循环。必须先 <see cref="Attach"/>，且只能启动一次。
        /// 启动校验失败时直接转入收口并抛出，避免已建连接悬挂。
        /// </summary>
        public void Start()
        {
            lock (_shutdownGate)
            {
                var state = Volatile.Read(ref _state);
                if (state == StateClosing || state == StateCompleted)
                    throw new InvalidOperationException("连接已进入收口或已完成，不能再启动接收泵。");

                if (state == StateStarted)
                    throw new InvalidOperationException("接收泵已启动，不能重复启动。");

                if (state != StateAttached)
                    throw new InvalidOperationException("必须先 Attach 通道链再启动接收泵。");

                try
                {
                    if (_options.RequireTerminalHandler)
                        ValidateStartup(_pipeline);

                    // 冻链必须早于泵首次读取：启动校验只对启动那一刻的链形状成立。
                    _pipeline.Freeze();
                    Volatile.Write(ref _state, StateStarted);
                }
                catch
                {
                    BeginShutdownLocked("startup-validation-failed", false);
                    throw;
                }
            }

            Task.Run((Func<Task>)BeginStartAsync);
        }

        /// <summary>
        /// 启动期后台序列：建消费循环 → 派发活动通知 → 启动接收泵。
        /// 活动通知早于泵首次读取，处理器可在其中回包。
        /// </summary>
        private async Task BeginStartAsync()
        {
            try
            {
                _consumerTask = Task.Run((Func<Task>)ConsumeAsync);

                await DispatchActiveAsync().ConfigureAwait(false);

                // 活动通知期间已进入收口时不再启动泵，收口流程负责剩余资源。
                if (Volatile.Read(ref _closing) != 0)
                    return;

                _pumpTask = Task.Run((Func<Task>)PumpAsync);
            }
            catch (Exception ex)
            {
                RecordPumpError(ex);
                _ = BeginShutdown("start-failure", false);
            }
        }

        /// <summary>
        /// 派发活动通知。与失活派发共用一把闸门，已进入收口或失活已先派发时不再补发。
        /// </summary>
        private async Task DispatchActiveAsync()
        {
            await _lifecycleGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_activeDispatched || _inactiveDispatched || Volatile.Read(ref _closing) != 0)
                    return;

                _activeDispatched = true;

                var pipeline = _pipeline;
                if (pipeline != null)
                    await pipeline.FireChannelActiveAsync().ConfigureAwait(false);
            }
            finally
            {
                _lifecycleGate.Release();
            }
        }

        /// <summary>派发失活通知；幂等，异常由链内记录，不阻断收口。</summary>
        private async Task DispatchInactiveAsync()
        {
            await _lifecycleGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_inactiveDispatched)
                    return;

                _inactiveDispatched = true;

                var pipeline = _pipeline;
                if (pipeline != null)
                    await pipeline.FireChannelInactiveAsync().ConfigureAwait(false);
            }
            finally
            {
                _lifecycleGate.Release();
            }
        }

        /// <summary>
        /// 启动校验：链尾最后一个入站节点必须是兜底处理器，出站串行器恰好一个且位于出站前缀最右侧。
        /// </summary>
        private static void ValidateStartup(ChannelPipeline pipeline)
        {
            var handlers = pipeline.Handlers;

            IChannelHandler lastInbound = null;
            for (var i = handlers.Count - 1; i >= 0; i--)
            {
                if (handlers[i] is IChannelInboundHandler)
                {
                    lastInbound = handlers[i];
                    break;
                }
            }

            if (!(lastInbound is ExceptionTerminalHandler))
            {
                throw new InvalidOperationException(
                    "RequireTerminalHandler 为 true 时，链尾最后一个入站节点必须是 ExceptionTerminalHandler；"
                    + "当前为 " + (lastInbound == null ? "(无入站节点)" : lastInbound.GetType().FullName) + "。");
            }

            var serializerCount = 0;
            var rightmostOutbound = -1;
            var rightmostSerializer = -1;
            for (var i = 0; i < handlers.Count; i++)
            {
                if (!(handlers[i] is IChannelOutboundHandler))
                    continue;

                rightmostOutbound = i;
                if (handlers[i] is OutboundSerializingHandler)
                {
                    serializerCount++;
                    rightmostSerializer = i;
                }
            }

            if (serializerCount != 1)
            {
                throw new InvalidOperationException(
                    "RequireTerminalHandler 为 true 时，链上必须恰好装配一个 OutboundSerializingHandler，实际为 "
                    + serializerCount + " 个。");
            }

            if (rightmostSerializer != rightmostOutbound)
            {
                throw new InvalidOperationException(
                    "OutboundSerializingHandler 必须位于出站前缀最右侧（紧贴第一个入站节点），"
                    + "否则异步编码器会让先发起的写后到。");
            }
        }

        /// <summary>
        /// 请求进入收口并同步等待回收，最多 5 秒；不抛异常。
        /// 需要精确感知回收时应等待 <see cref="Completion"/>。
        /// </summary>
        public void Dispose()
        {
            try
            {
                BeginShutdown("disposed", false);

                var completion = Completion;
                if (completion.IsCompleted)
                    return;

                var deadline = Task.Delay(TimeSpan.FromSeconds(5));
                Task.WhenAny(completion, deadline).GetAwaiter().GetResult();
            }
            catch
            {
            }
        }

        /// <summary>流量快照：累计接收字节、未成帧残余、当前队列长度。</summary>
        internal ChannelTraffic Traffic()
        {
            return new ChannelTraffic(
                Interlocked.Read(ref _totalReceived),
                Volatile.Read(ref _bufferedBytes),
                Volatile.Read(ref _queued));
        }

        private void OnTransportCancelled()
        {
            BeginShutdown("transport-cancelled", false);
        }

        private async Task PumpAsync()
        {
            var ct = _linked.Token;
            var splitter = _options.Splitter;

            try
            {
                while (true)
                {
                    // 1. 收口取消优先于任何读取。
                    if (ct.IsCancellationRequested)
                        break;

                    // 2. 读一段新字节；残字节不搬出 Pipe，AdvanceTo 只推进到最后一条成帧位置。
                    ReadResult result;
                    try
                    {
                        result = await _reader.ReadAsync(ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        RecordPumpError(ex);
                        _ = BeginShutdown("read-error", true);
                        break;
                    }

                    // 3. 传输层 Dispose / 外部 Disconnect 走 CancelPendingRead，必须单独收口，不能当空读。
                    if (result.IsCanceled)
                    {
                        SafeAdvanceTo(result.Buffer.Start);
                        _ = BeginShutdown("transport-closed", true);
                        break;
                    }

                    var buffer = result.Buffer;
                    Interlocked.Add(ref _totalReceived, buffer.Length);

                    var consumed = buffer.Start;
                    var complete = false;
                    var queuedThisRead = false;
                    var aborted = false;

                    // 4. 直接在 Pipe 缓冲上切帧：成帧即复制成 PooledMessage 入队，残帧继续留在 Pipe。
                    // 可切换分帧器在协议边界置 StopAfterCurrent 后，本轮必须停在同一个 ReadResult 的
                    // consumed 位置等待切换，不能先 AdvanceTo 再丢缓冲另读：没有新字节时残帧不会被重新返回。
                    var switchable = splitter as SwitchableSplitter;
                    try
                    {
                        while (true)
                        {
                            var pending = buffer.Slice(consumed);
                            if (!splitter.TrySplit(pending, out var frameConsumed, out var frame))
                            {
                                complete = true;
                                break;
                            }

                            // 消费量为 0 会让循环原地打转，按协议错误收口。
                            if (frameConsumed <= 0)
                                throw new FrameProtocolException("分帧器返回成功时必须消费至少 1 字节。");

                            var msg = AllocateFrame(frame);

                            // 内联快路径：无在途、队列空时在接收线程直接执行；同步完成就继续切下一帧。
                            // 一旦某帧挂起，在途数变为非零，后续帧自动回落到下面的排队分发。
                            if (CanDispatchInline() && TryStartInline(msg))
                            {
                                consumed = pending.GetPosition(frameConsumed);
                                queuedThisRead = true;

                                // 内联处理器可能已同步完成切换并复位屏障；未复位时与排队分支走同一等待逻辑。
                                if (switchable != null && switchable.StopAfterCurrent)
                                    await switchable.WaitForSwitchAsync(_options.SplitterSwitchTimeout, ct).ConfigureAwait(false);

                                continue;
                            }

                            // 先加计数再写队列：消费者若先取走，计数不会被减成负数。
                            Interlocked.Increment(ref _queued);
                            if (!_queue.Writer.TryWrite(msg))
                            {
                                Interlocked.Decrement(ref _queued);
                                msg.Release();
                                SetQueuePausedOnce();

                                // 队列满：该帧尚未消费、仍在当前 Pipe 缓冲里；暂停期间不得调用 ReadAsync。
                                if (!await WaitQueueSpaceAsync(ct).ConfigureAwait(false))
                                {
                                    aborted = true;
                                    break;
                                }

                                continue;
                            }

                            consumed = pending.GetPosition(frameConsumed);
                            queuedThisRead = true;

                            // 已入队的旧协议消息照常执行；这里只停止继续切分并原地等待切换。
                            if (switchable != null && switchable.StopAfterCurrent)
                                await switchable.WaitForSwitchAsync(_options.SplitterSwitchTimeout, ct).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        RecordPumpError(ex);

                        if (ex is FrameProtocolException)
                        {
                            // 协议错误先上链，让业务有机会回包并自行收口；分帧器本身绝不直接写 socket。
                            SafeAdvanceTo(buffer.End);

                            try
                            {
                                await _pipeline.FireExceptionCaughtAsync(ex).ConfigureAwait(false);
                            }
                            catch (Exception notifyError)
                            {
                                RecordPumpError(notifyError);
                            }

                            // 派发后仍为 Open 说明没有处理器接管收口，由泵强制收口；
                            // 已进入 DrainingWrites / Closing 时交给既有状态机，避免起第二个关闭流程。
                            if (_pipeline.IsOpen)
                                _ = BeginShutdown("split-protocol-error", true);

                            break;
                        }

                        _ = BeginShutdown("split-error", true);
                        SafeAdvanceTo(buffer.End);
                        break;
                    }

                    if (aborted)
                        break;

                    // 5. 只消费已入队的完整帧，未成帧残字节留给下一次 ReadAsync。
                    SafeAdvanceTo(consumed, buffer.End);
                    Volatile.Write(ref _bufferedBytes, buffer.Slice(consumed).Length);

                    // 6. 完整帧已全部入队，剩下的是凑不成一帧的尾巴。
                    if (complete && buffer.Slice(consumed).Length > _pipeline.EffectiveMaxBufferedBytes)
                    {
                        _ = BeginShutdown("buffered-bytes-limit", true);
                        break;
                    }

                    // 7. 本轮（或合并的上一轮）的帧已全部入队，只通知一次。
                    _pendingComplete |= queuedThisRead;
                    if (complete && _pendingComplete)
                    {
                        await SafeFireReadCompleteAsync().ConfigureAwait(false);
                        _pendingComplete = false;
                    }

                    // 8. 对端 EOF：本轮已处理完，直接收口，不把完成态当空读自旋。
                    if (result.IsCompleted)
                    {
                        _ = BeginShutdown("remote-eof", true);
                        break;
                    }

                    // 9. 只有完整排空的轮次才做流量检查；钩子异常按超限处理。
                    if (complete && !AllowTraffic())
                    {
                        _ = BeginShutdown("guard-rejected", true);
                        break;
                    }
                }
            }
            finally
            {
                Volatile.Write(ref _bufferedBytes, 0);
                try { _reader.Complete(); } catch (ObjectDisposedException) { } catch (InvalidOperationException) { }
            }
        }

        private async Task ConsumeAsync()
        {
            var ct = _connectionToken.Token;

            try
            {
                while (await _queue.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
                {
                    PooledMessage msg;
                    while (TryReadQueue(out msg))
                    {
                        // 先记在途再减队列数：接收泵以「队列数 0 且在途数 0」判定可内联，
                        // 这个顺序保证已出队但尚未启动的消息不会被看漏。
                        Interlocked.Increment(ref _inFlightCount);
                        Interlocked.Decrement(ref _queued);

                        if (Volatile.Read(ref _queuePaused) == 1
                            && Volatile.Read(ref _queued) <= _pipeline.EffectiveResumeWatermark)
                        {
                            TryResumeFromPause();
                        }

                        if (Volatile.Read(ref _closing) == 1)
                        {
                            AbandonOutstanding(msg);
                            continue;
                        }

                        try
                        {
                            await _concurrency.WaitAsync(ct).ConfigureAwait(false);
                        }
                        catch
                        {
                            AbandonOutstanding(msg);
                            throw;
                        }

                        if (Volatile.Read(ref _closing) == 1)
                        {
                            _concurrency.Release();
                            AbandonOutstanding(msg);
                            continue;
                        }

                        StartMessage(msg);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 收口取消：退出消费循环，收口流程负责剩余条目。
            }
            catch (System.Threading.Channels.ChannelClosedException)
            {
                // 写入端已完成：退出消费循环。
            }
            finally
            {
                DrainQueue();
            }
        }

        private void StartMessage(PooledMessage msg)
        {
            var id = Interlocked.Increment(ref _inFlightSeq);

            // 内联启动：同步完成的消息不排队、不额外分配 Task；挂起后由 async 状态机自带的任务继续，
            // 只有真正挂起的消息才登记进在途表供收口等待。
            var task = ExecuteMessageAsync(msg, id);
            if (task.IsCompleted)
                return;

            _inFlight[id] = task;

            // finally 可能先于登记执行（此时 TryRemove 落空），复查一次，避免残留已完成条目。
            if (task.IsCompleted)
            {
                Task ignored;
                _inFlight.TryRemove(id, out ignored);
            }
        }

        /// <summary>
        /// 是否允许在当前线程直接执行消息：仅内联模式、未配置消息超时、未收口、队列空且无在途消息。
        /// 超时模式需要每消息 linked 令牌，内联执行会把令牌注入到接收线程的异步流上，故一律回落排队。
        /// </summary>
        private bool CanDispatchInline()
        {
            return _options.DispatchMode == ChannelDispatchMode.Inline
                && _options.MessageTimeout <= TimeSpan.Zero
                && Volatile.Read(ref _closing) == 0
                && Volatile.Read(ref _queued) == 0
                && Volatile.Read(ref _inFlightCount) == 0;
        }

        /// <summary>
        /// 在当前线程启动一条消息。抢不到并发额度时返回 false 且不触碰消息，由调用方改走队列。
        /// </summary>
        /// <param name="msg">已持有独立背衬的帧消息。</param>
        /// <returns>消息是否已交给通道链。</returns>
        private bool TryStartInline(PooledMessage msg)
        {
            // 内联判定与额度占用之间可能被消费侧抢先，Wait(0) 是这两条路径的唯一裁决点。
            if (!_concurrency.Wait(0))
                return false;

            Interlocked.Increment(ref _inFlightCount);

            // 内联分发只由接收泵发起，递增不需要原子操作。
            _inlineDispatched++;

            StartInlineMessage(msg);
            return true;
        }

        /// <summary>
        /// 内联执行一条消息：同步完成时就地收口，挂起后才建续接任务并登记在途表。
        /// </summary>
        /// <param name="msg">已持有独立背衬的帧消息。</param>
        private void StartInlineMessage(PooledMessage msg)
        {
            Task task;
            try
            {
                task = _pipeline.FireChannelReadAsync(msg);
            }
            catch (Exception ex)
            {
                // 链内已在出错节点下游通知过一次，能同步抛出的只剩取消与链自身错误；
                // 执行器只兜底记录，不能再次全链上报。
                HandleInlineFailure(ex, msg);
                return;
            }

            // netstandard2.0 没有 IsCompletedSuccessfully，用状态判断同步成功。
            if (task.Status == TaskStatus.RanToCompletion)
            {
                FinishInline(msg);
                return;
            }

            var id = Interlocked.Increment(ref _inFlightSeq);
            var waiter = AwaitInlineAsync(task, msg);

            // 续接任务通常已挂起；极少数情况同步完成时直接结束，不登记。
            if (waiter.IsCompleted)
                return;

            _inFlight[id] = waiter;
            if (waiter.IsCompleted)
            {
                Task ignored;
                _inFlight.TryRemove(id, out ignored);
            }
        }

        private async Task AwaitInlineAsync(Task pending, PooledMessage msg)
        {
            try
            {
                await pending.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                LogHelper.Debug("通道链消息已取消:" + _stream.ChannelId);
            }
            catch (Exception ex)
            {
                // 链内已通知过下游，执行器只兜底记录。
                LogHelper.Warn("通道链消息处理异常:" + _stream.ChannelId, ex);
            }
            finally
            {
                FinishInline(msg);
            }
        }

        private void HandleInlineFailure(Exception ex, PooledMessage msg)
        {
            if (ex is OperationCanceledException)
            {
                LogHelper.Debug("通道链消息已取消:" + _stream.ChannelId);
            }
            else
            {
                // 链内已通知过下游，执行器只兜底记录。
                LogHelper.Warn("通道链消息处理异常:" + _stream.ChannelId, ex);
            }

            FinishInline(msg);
        }

        /// <summary>内联消息的收口：先还并发额度再减在途数，最后归还消息。</summary>
        private void FinishInline(PooledMessage msg)
        {
            _concurrency.Release();
            msg.Release();
            Interlocked.Decrement(ref _inFlightCount);
        }

        /// <summary>已出队但未开始执行就退出的消息：归还引用并撤回在途计数。</summary>
        private void AbandonOutstanding(PooledMessage msg)
        {
            msg.Release();
            Interlocked.Decrement(ref _inFlightCount);
        }

        private async Task ExecuteMessageAsync(PooledMessage msg, int id)
        {
            CancellationTokenSource timeoutCts = null;
            var hasScopedToken = false;
            var previous = default(CancellationToken);

            // 未配置超时时消息令牌直接取连接令牌：不建 linked CTS，也不写 AsyncLocal。
            if (_options.MessageTimeout > TimeSpan.Zero)
            {
                timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(_connectionToken.Token);
                timeoutCts.CancelAfter(_options.MessageTimeout);

                previous = _ambientToken.Value;
                _ambientToken.Value = timeoutCts.Token;
                hasScopedToken = true;
            }

            try
            {
                await _pipeline.FireChannelReadAsync(msg).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (Volatile.Read(ref _closing) == 0
                                                     && timeoutCts != null
                                                     && timeoutCts.IsCancellationRequested
                                                     && !_connectionToken.IsCancellationRequested)
            {
                var timeout = _options.MessageTimeout;
                try
                {
                    await _pipeline.FireExceptionCaughtAsync(new MessageTimeoutException(_stream.ChannelId, timeout))
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogHelper.Debug("通道链消息超时上报失败:" + _stream.ChannelId + " " + ex.Message);
                }
            }
            catch (OperationCanceledException)
            {
                LogHelper.Debug("通道链消息已取消:" + _stream.ChannelId);
            }
            catch (Exception ex)
            {
                // 兜底：链内已在本节点下游通知过普通异常；能到这里说明是链自身或取消路径出错，
                // 执行器只记录，不再全链上报，避免同一异常被通知两次。
                LogHelper.Warn("通道链消息处理异常:" + _stream.ChannelId, ex);
            }
            finally
            {
                if (hasScopedToken)
                    _ambientToken.Value = previous;

                if (timeoutCts != null)
                    timeoutCts.Dispose();

                Task ignored;
                _inFlight.TryRemove(id, out ignored);

                // 先还并发额度再减在途数：内联判定以「在途数为 0」代表额度必然满额。
                _concurrency.Release();
                msg.Release();
                Interlocked.Decrement(ref _inFlightCount);
            }
        }

        private async Task ShutdownCoreAsync(bool fromPump)
        {
            try
            {
                // 1. 停泵：取消泵令牌。
                try { _pumpCts.Cancel(); } catch (ObjectDisposedException) { }

                // 2. 外部触发时等泵返回；泵自身触发时由泵的 finally 收尾，不能互等。
                if (!fromPump)
                {
                    var pump = Volatile.Read(ref _pumpTask);
                    if (pump != null)
                    {
                        try { await pump.ConfigureAwait(false); } catch { }
                    }
                }

                // 3. 失活通知必须早于 DrainPolicy：处理器此时仍可读取连接级属性，但连接已不可写。
                await DispatchInactiveAsync().ConfigureAwait(false);

                // 4. 关闭写入端，阻止消费循环继续等待新帧。
                try { _queue.Writer.TryComplete(); } catch { }

                // 5. 策略分叉：取消在途，或等待其自然结束。
                bool drained;
                if (_options.DrainPolicy == DisconnectDrainPolicy.CancelPending)
                {
                    try { _connectionToken.Cancel(); } catch (ObjectDisposedException) { }
                    drained = await WaitForInFlightAsync(TimeSpan.Zero).ConfigureAwait(false);
                }
                else
                {
                    drained = await WaitForInFlightAsync(_options.DrainTimeout).ConfigureAwait(false);
                }

                // 6. 收回尚未开始执行的队列帧。
                DrainQueue();

                // 7. 等并发额度回到满额：CancelPending 下即取消落地，DrainInFlight 超时后放弃。
                // 成功等到在途清零时消费循环必然临近退出，等它收尾；超时放弃时不能等，
                // 否则卡死的处理器会把收口永久挂住。
                if (drained)
                {
                    var consumer = Volatile.Read(ref _consumerTask);
                    if (consumer != null)
                    {
                        try { await consumer.ConfigureAwait(false); } catch { }
                    }
                }

                // 8. 撤销登记、释放处理器计数、清理连接级属性、关闭传输层。
                var defaultChannel = _channel as DefaultChannel;
                if (defaultChannel != null)
                    defaultChannel.MarkInactive();

                if (_host != null)
                {
                    IChannel removed;
                    try { _host.TryRemoveChannel(_stream.ChannelId, out removed); } catch { }
                }

                if (_pipeline != null)
                {
                    try { _pipeline.Dispose(); } catch { }
                }

                // 属性只能在失活通知与登记摘除之后清理，且整个收口只清一次。
                if (defaultChannel != null)
                    defaultChannel.ClearAttributes();

                try { await _stream.CloseAsync(_shutdownReason).ConfigureAwait(false); } catch { }
            }
            catch (Exception ex)
            {
                RecordPumpError(ex);
            }
            finally
            {
                Volatile.Write(ref _state, StateCompleted);
                _completion.TrySetResult(true);
            }
        }

        private Task[] SnapshotInFlight()
        {
            var tasks = new List<Task>(_inFlight.Count);
            foreach (var pair in _inFlight)
                tasks.Add(pair.Value);

            return tasks.ToArray();
        }

        private async Task<bool> WaitForInFlightAsync(TimeSpan timeout)
        {
            var infinite = timeout == TimeSpan.Zero;
            var elapsed = System.Diagnostics.Stopwatch.StartNew();

            while (true)
            {
                var pending = SnapshotInFlight();
                if (pending.Length > 0)
                {
                    var all = Task.WhenAll(pending);

                    if (infinite)
                    {
                        try { await all.ConfigureAwait(false); } catch { }
                    }
                    else
                    {
                        var remaining = timeout - elapsed.Elapsed;
                        if (remaining <= TimeSpan.Zero)
                            return false;

                        var delay = Task.Delay(remaining);
                        var winner = await Task.WhenAny(all, delay).ConfigureAwait(false);
                        if (winner != all)
                            return false;

                        try { await all.ConfigureAwait(false); } catch { }
                    }
                }

                if (Volatile.Read(ref _inFlightCount) == 0
                    && _concurrency.CurrentCount >= _options.MaxConcurrentMessages)
                {
                    return true;
                }

                if (!infinite && elapsed.Elapsed >= timeout)
                    return false;

                await Task.Delay(5).ConfigureAwait(false);
            }
        }

        private PooledMessage AllocateFrame(ReadOnlySequence<byte> frame)
        {
            if (frame.Length > int.MaxValue)
                throw new FrameProtocolException($"帧长度 {frame.Length} 超过可索引上限。");

            var length = (int)frame.Length;

            // 零长帧走精确分配：Rent(0) 会按最小池档发放，反而放大占用。
            if (length > 0 && length >= _options.SmallFrameInlineThreshold)
            {
                var buffer = MemoryPoolManager.Rent(length);
                frame.CopyTo(buffer.AsSpan(0, length));
                return new PooledMessage(new ReadOnlyMemory<byte>(buffer, 0, length), buffer, length, true);
            }

            var exact = frame.ToArray();
            return new PooledMessage(exact, exact, exact.Length, false);
        }

        private void SetQueuePausedOnce()
        {
            if (Interlocked.CompareExchange(ref _queuePaused, 1, 0) == 0)
            {
                _stream.Pause();

                // 空窗竞态：Pause 之后消费侧可能已经腾空，立即复核一次。
                if (Volatile.Read(ref _queued) <= _pipeline.EffectiveResumeWatermark)
                    TryResumeFromPause();
            }
        }

        private void TryResumeFromPause()
        {
            if (Interlocked.CompareExchange(ref _queuePaused, 0, 1) == 1)
            {
                _stream.Resume();

                try { _resumeSignal.Release(); }
                catch (SemaphoreFullException) { }
            }
        }

        private async Task<bool> WaitQueueSpaceAsync(CancellationToken ct)
        {
            try
            {
                await _resumeSignal.WaitAsync(ct).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private bool AllowTraffic()
        {
            try
            {
                var traffic = Traffic();
                return _pipeline.EffectiveGuard.Allow(in traffic);
            }
            catch (Exception ex)
            {
                RecordPumpError(ex);
                return false;
            }
        }

        private void SafeAdvanceTo(SequencePosition consumed)
        {
            try { _reader.AdvanceTo(consumed); }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        private void SafeAdvanceTo(SequencePosition consumed, SequencePosition examined)
        {
            try { _reader.AdvanceTo(consumed, examined); }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        private async Task SafeFireReadCompleteAsync()
        {
            try
            {
                await _pipeline.FireChannelReadCompleteAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                RecordPumpError(ex);
            }
        }

        private void RecordPumpError(Exception ex)
        {
            try { LogHelper.Warn("通道链接收泵异常:" + _stream.ChannelId, ex); }
            catch { }
        }

        private bool TryReadQueue(out PooledMessage msg)
        {
            lock (_drainGate)
            {
                return _queue.Reader.TryRead(out msg);
            }
        }

        private void DrainQueue()
        {
            lock (_drainGate)
            {
                PooledMessage leftover;
                while (_queue.Reader.TryRead(out leftover))
                {
                    Interlocked.Decrement(ref _queued);
                    leftover.Release();
                }
            }
        }
    }
}