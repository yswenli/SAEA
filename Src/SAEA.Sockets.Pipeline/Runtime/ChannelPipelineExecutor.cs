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
        private readonly FrameAccumulator _accumulator = new FrameAccumulator();
        private readonly AsyncLocal<CancellationToken> _ambientToken = new AsyncLocal<CancellationToken>();

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
        private Task _pumpTask;
        private Task _consumerTask;
        private int _closing;
        private Task _shutdownTask;
        private string _shutdownReason;
        private int _inFlightSeq;
        private int _inFlightCount;

        /// <summary>
        /// 创建执行器。传输层令牌取消表示连接已断开，是消息令牌的兜底来源；
        /// 取消时一定转入收口，而不只是让泵循环退出。
        /// </summary>
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

        /// <summary>把本执行器与通道链双向绑定，必须在 <see cref="Start"/> 之前完成。</summary>
        public void Attach(ChannelPipeline pipeline)
        {
            if (pipeline == null)
                throw new ArgumentNullException(nameof(pipeline));

            _pipeline = pipeline;
            pipeline.Attach(_stream, this);
        }

        /// <summary>
        /// 请求进入收口。只置状态并启动后台收口，不等待收口完成；
        /// 需要彻底回收的调用方等待 <see cref="Completion"/>。
        /// </summary>
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
                if (Interlocked.CompareExchange(ref _closing, 1, 0) == 0)
                {
                    _shutdownReason = reason;

                    var defaultChannel = _channel as DefaultChannel;
                    if (defaultChannel != null)
                        defaultChannel.MarkInactive();

                    _shutdownTask = Task.Run(() => ShutdownCoreAsync(fromPump));
                }

                return _shutdownTask ?? Task.CompletedTask;
            }
        }

        /// <summary>启动接收泵与消费循环；未 <see cref="Attach"/> 时抛 <see cref="InvalidOperationException"/>。</summary>
        public void Start()
        {
            if (_pipeline == null)
                throw new InvalidOperationException("必须先 Attach 通道链再启动接收泵。");

            _pumpTask = Task.Run((Func<Task>)PumpAsync);
            _consumerTask = Task.Run((Func<Task>)ConsumeAsync);
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
                _accumulator.BufferedLength,
                Volatile.Read(ref _queued));
        }

        private void OnTransportCancelled()
        {
            BeginShutdown("transport-cancelled", false);
        }

        private async Task PumpAsync()
        {
            var ct = _linked.Token;

            try
            {
                while (true)
                {
                    // 1. 收口取消优先于任何读取。
                    if (ct.IsCancellationRequested)
                        break;

                    // 2. 先把上一轮因队列满留下的"已拆出但未 Consume"的帧送出去。
                    bool anyQueued;
                    var drained = DrainToQueue(out anyQueued);
                    _pendingComplete |= anyQueued;

                    if (!drained)
                    {
                        // 队列满：该帧未 Consume、仍在 accumulator；暂停期间不得调用 ReadAsync。
                        if (!await WaitQueueSpaceAsync(ct).ConfigureAwait(false))
                            break;
                        continue;
                    }

                    // 3. 完整帧已全部入队，剩下的是凑不成一帧的尾巴。
                    if (_accumulator.BufferedLength > _pipeline.EffectiveMaxBufferedBytes)
                    {
                        _ = BeginShutdown("buffered-bytes-limit", true);
                        break;
                    }

                    // 4. 本轮（或合并的上一轮）的帧已全部入队，只通知一次。
                    if (_pendingComplete)
                    {
                        await SafeFireReadCompleteAsync().ConfigureAwait(false);
                        _pendingComplete = false;
                    }

                    // 5. 再读新字节。
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

                    // 6. 传输层 Dispose / 外部 Disconnect 走 CancelPendingRead，必须单独收口，不能当空读。
                    if (result.IsCanceled)
                    {
                        SafeAdvanceTo(result.Buffer.End);
                        _ = BeginShutdown("transport-closed", true);
                        break;
                    }

                    // 7. 拷贝进 accumulator 后再拆帧；异常路径也必须放行本轮 Pipe 缓冲。
                    var complete = false;
                    var queuedThisRead = false;
                    try
                    {
                        foreach (var segment in result.Buffer)
                        {
                            _accumulator.Append(segment);
                        }
                        Interlocked.Add(ref _totalReceived, result.Buffer.Length);
                        complete = DrainToQueue(out queuedThisRead);
                    }
                    catch (Exception ex)
                    {
                        RecordPumpError(ex);
                        _ = BeginShutdown("split-error", true);
                        break;
                    }
                    finally
                    {
                        SafeAdvanceTo(result.Buffer.End);
                    }

                    // 8. 被队列满截断的轮次不通知，与下一次成功排空合并。
                    _pendingComplete |= queuedThisRead;
                    if (complete && _pendingComplete)
                    {
                        await SafeFireReadCompleteAsync().ConfigureAwait(false);
                        _pendingComplete = false;
                    }

                    // 9. 对端 EOF：本轮已处理完，直接收口，不把完成态当空读自旋。
                    if (result.IsCompleted)
                    {
                        _ = BeginShutdown("remote-eof", true);
                        break;
                    }

                    // 10. 只有完整排空的轮次才做流量检查；钩子异常按超限处理。
                    if (complete && !AllowTraffic())
                    {
                        _ = BeginShutdown("guard-rejected", true);
                        break;
                    }
                }
            }
            finally
            {
                try { _accumulator.Reset(); } catch { }
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
                        Interlocked.Decrement(ref _queued);

                        if (Volatile.Read(ref _queuePaused) == 1
                            && Volatile.Read(ref _queued) <= _pipeline.EffectiveResumeWatermark)
                        {
                            TryResumeFromPause();
                        }

                        if (Volatile.Read(ref _closing) == 1)
                        {
                            msg.Release();
                            continue;
                        }

                        try
                        {
                            await _concurrency.WaitAsync(ct).ConfigureAwait(false);
                        }
                        catch
                        {
                            msg.Release();
                            throw;
                        }

                        if (Volatile.Read(ref _closing) == 1)
                        {
                            _concurrency.Release();
                            msg.Release();
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
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Interlocked.Increment(ref _inFlightCount);

            var task = Task.Run(async () =>
            {
                await gate.Task.ConfigureAwait(false);

                try
                {
                    await ExecuteMessageAsync(msg).ConfigureAwait(false);
                }
                finally
                {
                    Task ignored;
                    _inFlight.TryRemove(id, out ignored);
                    Interlocked.Decrement(ref _inFlightCount);
                }
            });

            _inFlight[id] = task;
            gate.TrySetResult(true);
        }

        private async Task ExecuteMessageAsync(PooledMessage msg)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(_connectionToken.Token);
            if (_options.MessageTimeout > TimeSpan.Zero)
                cts.CancelAfter(_options.MessageTimeout);

            var previous = _ambientToken.Value;
            _ambientToken.Value = cts.Token;

            try
            {
                await _pipeline.FireChannelReadAsync(msg).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (Volatile.Read(ref _closing) == 0
                                                     && cts.IsCancellationRequested
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
                // 兜底：链内已把普通异常转成 ExceptionCaught；能到这里说明链本身出错，仍上报链尾。
                try
                {
                    await _pipeline.FireExceptionCaughtAsync(ex).ConfigureAwait(false);
                }
                catch
                {
                }
            }
            finally
            {
                _ambientToken.Value = previous;
                cts.Dispose();
                _concurrency.Release();
                msg.Release();
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

                // 3. 关闭写入端并排空尚未开始的消息。
                try { _queue.Writer.TryComplete(); } catch { }
                DrainQueue();

                // 4. 策略分叉：取消在途，或等待其自然结束。
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

                // 5. 等并发额度回到满额：CancelPending 下即取消落地，DrainInFlight 超时后放弃。
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

                // 6. 撤销登记、释放处理器计数、关闭传输层。
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

                try { await _stream.CloseAsync(_shutdownReason).ConfigureAwait(false); } catch { }
            }
            catch (Exception ex)
            {
                RecordPumpError(ex);
            }
            finally
            {
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

        private bool DrainToQueue(out bool anyQueued)
        {
            anyQueued = false;
            var splitter = _options.Splitter;

            while (splitter.TrySplit(_accumulator.Buffered, out var consumed, out var frame))
            {
                var msg = AllocateFrame(frame);

                // 先加计数再写队列：消费者若先取走，计数不会被减成负数。
                Interlocked.Increment(ref _queued);
                if (!_queue.Writer.TryWrite(msg))
                {
                    Interlocked.Decrement(ref _queued);
                    msg.Release();
                    SetQueuePausedOnce();
                    return false;
                }

                _accumulator.Consume(consumed);
                anyQueued = true;
            }

            return true;
        }

        private PooledMessage AllocateFrame(ReadOnlyMemory<byte> frame)
        {
            if (frame.Length >= _options.SmallFrameInlineThreshold)
            {
                var buffer = MemoryPoolManager.Rent(frame.Length);
                frame.Span.CopyTo(buffer.AsSpan());
                return new PooledMessage(new ReadOnlyMemory<byte>(buffer, 0, frame.Length), buffer, frame.Length, true);
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