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
*命名空间：SAEA.Sockets.Core
*文件名： SaeaConnection
*版本号： v26.11.5.1
*唯一标识：54d2d6a1-eea7-4645-a7a4-66b2d8490c78
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/5 15:06:36
*描述：saea 连接的双工流门面与 TLS 处理
*
*=====================================================================
*修改标记
*修改时间：2026/11/24 18:12:35
*修改人： yswenli
*版本号： 
*描述：v26.11.24.2
*
*****************************************************************************/
using SAEA.Common.Caching;
using SAEA.Sockets.Ssl;
using SAEA.Sockets.Ssl.Internal;

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Sockets.Core
{
    /// <summary>
    /// 表示一条 saea 连接。流模式与 TLS 都通过本类承载：
    /// 入站密文/明文写入 <see cref="Pipe"/>，<see cref="Stream"/> 从 <see cref="Pipe"/> 读取；
    /// 出站写入经 TLS 加密后交给 saea 发送。
    /// </summary>
    internal sealed class SaeaConnection : IDisposable
    {
        /// <summary>
        /// saea 原始发送委托：由 Socket 实现负责串行化与所有权释放，返回本次交接到内核的任务。
        /// </summary>
        private readonly Func<ArraySegment<byte>, IDisposable, Task> _rawSend;

        private readonly Action _receiveResume;

        private readonly ISocketOption _option;

        private readonly Socket _socket;

        private readonly Pipe _pipe;

        private readonly PipeWriter _writer;

        private readonly PipeReader _reader;

        private readonly SaeaStream _stream;

        private SaeaTlsTransport _tls;

        /// <summary>
        /// TLS 读写的互斥锁。不能在 <see cref="Dispose"/> 中释放或置空：收尾阶段仍有并发等待与释放，
        /// 字段被置空后 <c>Release()</c> 会抛 NullReferenceException；未使用 AvailableWaitHandle 的
        /// SemaphoreSlim 不持有操作系统句柄，随连接对象一起交给 GC 即可。
        /// </summary>
        private SemaphoreSlim _tlsLock;

        private Timer _handshakeTimer;

        /// <summary>
        /// 已从 BouncyCastle 取出的 TLS 记录，等待按顺序写入 Socket。
        /// 出站记录必须在释放 <see cref="_tlsLock"/> 后再发送，否则全双工场景中
        /// 发送背压会阻塞入站解密，形成互等。
        /// </summary>
        private readonly ConcurrentQueue<PendingOutput> _pendingOutput = new ConcurrentQueue<PendingOutput>();

        private int _drainingOutput;

        /// <summary>
        /// 已交给发送路径的 TLS 记录数，用于验证一次应用数据的全部记录都完成交接。
        /// </summary>
        private long _tlsRecordsGenerated;

        /// <summary>
        /// 已完成底层交接的 TLS 记录数。
        /// </summary>
        private long _tlsRecordsHandedOff;

        // 排空状态：0 未排空、1 排空中、2 已完成。只有 0→1 能发起排空，保证半关只做一次。
        private const int GracefulNone = 0;
        private const int GracefulDraining = 1;
        private const int GracefulCompleted = 2;

        private int _gracefulState;

        private long _drainRemaining;

        private TaskCompletionSource<bool> _gracefulCompletion;

        private Action _gracefulClose;

        /// <summary>
        /// 排空发起闸门：排空规格、完成信号与排空状态必须整体发布，避免并发调用方读到半成品状态。
        /// </summary>
        private readonly object _gracefulGate = new object();

        /// <summary>
        /// 入站是否已结束（EOF 或异常）。排空发起晚于入站结束时不会再收到字节，必须直接收口。
        /// </summary>
        private volatile bool _inputCompleted;

        private int _state;

        /// <summary>
        /// 传输层暂停标记：1 表示因 Pipe 背压暂停接收。
        /// <para>
        /// 用整数配 <see cref="Interlocked"/> 交换，保证「从暂停态恢复」只续投一次接收：
        /// 重复续投会对同一个接收参数再次提交，触发内核/套接字错误并断开连接。
        /// </para>
        /// </summary>
        private int _receivePausedFlag;

        private volatile bool _appPaused;

        /// <summary>
        /// 入站接管者描述；非 null 表示 <see cref="Input"/> 已被输入处理器独占。
        /// </summary>
        private volatile string _inputClaimedBy;

        /// <summary>
        /// 构造连接。
        /// </summary>
        /// <param name="id">连接标识</param>
        /// <param name="socket">套接字</param>
        /// <param name="option">套接字配置</param>
        /// <param name="rawSend">原始发送委托</param>
        /// <param name="receiveResume">恢复接收回调</param>
        internal SaeaConnection(string id, Socket socket, ISocketOption option,
            Func<ArraySegment<byte>, IDisposable, Task> rawSend, Action receiveResume)
        {
            Id = id;
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _option = option ?? throw new ArgumentNullException(nameof(option));
            _rawSend = rawSend ?? throw new ArgumentNullException(nameof(rawSend));
            _receiveResume = receiveResume;

            var readBufferSize = Math.Max(option.ReadBufferSize > 0 ? option.ReadBufferSize : 64 * 1024, 1024);

            _pipe = new Pipe(new PipeOptions(
                useSynchronizationContext: false,
                pauseWriterThreshold: Math.Max(readBufferSize * 4, 64 * 1024),
                resumeWriterThreshold: Math.Max(readBufferSize * 2, 32 * 1024)));

            _writer = _pipe.Writer;
            _reader = _pipe.Reader;
            _stream = new SaeaStream(this);
        }

        /// <summary>连接标识。</summary>
        public string Id { get; }

        /// <summary>双工流门面。</summary>
        public Stream Stream => _stream;

        /// <summary>入站应用数据读取器。</summary>
        public PipeReader Input => _reader;

        /// <summary>TLS 状态；未启用 TLS 时为 null。</summary>
        public ISaeaTlsContext Tls => _tls;

        /// <summary>是否已附加 TLS 传输。</summary>
        public bool IsTlsActive => _tls != null;

        /// <summary>握手任务；未启用 TLS 时已完成。</summary>
        public Task HandshakeTask => _tls != null ? _tls.HandshakeTask : Task.CompletedTask;

        /// <summary>TLS 记录生成计数（含握手与告警记录），用于验证发送完成点。</summary>
        internal long TlsRecordsGenerated => Interlocked.Read(ref _tlsRecordsGenerated);

        /// <summary>TLS 记录完成底层交接的计数。</summary>
        internal long TlsRecordsHandedOff => Interlocked.Read(ref _tlsRecordsHandedOff);

        /// <summary>是否可写。</summary>
        public bool CanWrite => Volatile.Read(ref _state) == 0 && _socket.Connected;

        /// <summary>是否暂停接收：Pipe 背压或应用级暂停任一成立。</summary>
        public bool IsReceivePaused
        {
            get { return Volatile.Read(ref _receivePausedFlag) != 0 || _appPaused; }
        }

        /// <summary>是否因应用级暂停而停止接收。</summary>
        public bool IsApplicationPaused
        {
            get { return _appPaused; }
        }

        /// <summary>暂停接收（应用级）。幂等。</summary>
        public void PauseApplication()
        {
            _appPaused = true;
        }

        /// <summary>
        /// 恢复接收（应用级）。Pipe 背压仍未解除时继续保持暂停，
        /// 由背压本身的续体负责恢复读取。
        /// </summary>
        public void ResumeApplication()
        {
            _appPaused = false;
            if (Volatile.Read(ref _receivePausedFlag) == 0)
            {
                try { _receiveResume?.Invoke(); } catch { }
            }
        }

        /// <summary>是否已关闭。</summary>
        public bool IsClosed => Volatile.Read(ref _state) != 0;

        /// <summary>
        /// 是否处于排空入站状态：发送方向已半关，但接收方向仍在丢弃剩余请求体。
        /// </summary>
        internal bool IsGracefulDraining => Volatile.Read(ref _gracefulState) == GracefulDraining;

        /// <summary>
        /// 标记入站 <see cref="Input"/> 已被独占消费。PipeReader 不允许并发读，
        /// 标记后 <see cref="ReadAsync"/> 立即报错，避免第二个读者静默抢走字节。
        /// 首次标记生效，后续调用保留最初的接管者描述。
        /// </summary>
        /// <param name="owner">接管者描述，用于异常信息</param>
        internal void ClaimInput(string owner)
        {
            if (_inputClaimedBy != null) return;
            _inputClaimedBy = string.IsNullOrEmpty(owner) ? "输入处理器" : owner;
        }

        /// <summary>
        /// 附加 TLS 传输并开始握手。必须在接收循环启动前调用。
        /// </summary>
        internal void AttachTls(SaeaTlsTransport tls)
        {
            if (tls == null) throw new ArgumentNullException(nameof(tls));
            if (_tls != null) throw new InvalidOperationException("TLS 已附加。");

            _tls = tls;
            _tlsLock = new SemaphoreSlim(1, 1);

            _tls.NoteHandshakeStarted();

            var timeout = _option.Tls != null ? _option.Tls.HandshakeTimeout : TimeSpan.FromSeconds(10);
            if (timeout <= TimeSpan.Zero) timeout = TimeSpan.FromSeconds(10);

            _handshakeTimer = new Timer(OnHandshakeTimeout, null, timeout, Timeout.InfiniteTimeSpan);

            var acquired = false;
            try
            {
                acquired = _tlsLock.Wait(_option.ActionTimeout > 0 ? _option.ActionTimeout : 30 * 1000);
                if (!acquired)
                {
                    AbortHandshake(new TimeoutException("TLS 握手启动超时。"));
                    return;
                }

                _tls.StartHandshake();
                CaptureOutput();
            }
            catch (Exception ex)
            {
                AbortHandshake(ex);
                return;
            }
            finally
            {
                if (acquired) _tlsLock.Release();
            }

            FlushOutput();
            RaiseCallbacks();
        }

        /// <summary>
        /// 由 saea 接收回调调用。data 仅在调用期间有效。
        /// </summary>
        public void OnRawReceived(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0 || IsClosed) return;

            if (_tls == null)
            {
                if (Volatile.Read(ref _gracefulState) == GracefulDraining)
                {
                    // 排空期间读侧已停，写入 Pipe 无人消费，超过背压阈值会暂停接收并把排空拖到超时；
                    // 这里只计数不落缓冲，排空既不占内存也不受背压影响。
                    ConsumeDrain(data.Length);
                    return;
                }

                WriteToPipe(data);
                return;
            }

            byte[] rented = null;
            var flushTask = default(Task<FlushResult>);
            var flushed = false;
            var drainedBytes = 0;

            try
            {
                rented = ArrayPool<byte>.Shared.Rent(data.Length);
                data.CopyTo(rented);

                var acquired = false;
                var writer = _writer;
                var written = false;
                var tlsLock = _tlsLock;

                try
                {
                    acquired = tlsLock.Wait(_option.ActionTimeout > 0 ? _option.ActionTimeout : 30 * 1000);
                    if (!acquired)
                        throw new SaeaTlsException("TLS 入站处理超时。");

                    _tls.OfferInput(rented, 0, data.Length);
                    CaptureOutput();

                    while (_tls.AvailableInput > 0)
                    {
                        var n = _tls.ReadInput(rented, 0, rented.Length);
                        if (n <= 0) break;

                        if (Volatile.Read(ref _gracefulState) == GracefulDraining)
                        {
                            // TLS 排空也按解密后字节计数，且不落 Pipe，理由同上
                            drainedBytes += n;
                            continue;
                        }

                        writer.Write(new ReadOnlySpan<byte>(rented, 0, n));
                        written = true;
                    }

                    if (written)
                    {
                        flushTask = writer.FlushAsync().AsTask();
                        flushed = true;
                    }
                }
                finally
                {
                    if (acquired) tlsLock.Release();
                }

                FlushOutput();
                RaiseCallbacks();

                if (!_tls.IsClosed && _tls.AvailableOutput > 0)
                {
                    DrainOutputLocked();
                }
            }
            catch (Exception ex)
            {
                _tls?.NoteHandshakeFailed(ex);
                RaiseCallbacks();
                CompleteInput(ex);
                return;
            }
            finally
            {
                if (rented != null) ArrayPool<byte>.Shared.Return(rented);
            }

            if (flushed && !flushTask.IsCompleted)
            {
                PauseReceive(flushTask);
            }

            // TLS 按解密后的明文字节计数：排空规格描述的是请求体长度，与密文长度无关。
            ConsumeDrain(drainedBytes);
        }

        /// <summary>
        /// 从入站 Pipe 读取明文。
        /// </summary>
        /// <param name="buffer">目标缓冲区</param>
        /// <param name="offset">起始偏移</param>
        /// <param name="count">最大读取字节数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>实际读取字节数；入站结束时为 0</returns>
        /// <exception cref="InvalidOperationException">入站数据已被输入处理器接管</exception>
        public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var claimedBy = _inputClaimedBy;
            if (claimedBy != null)
            {
                // PipeReader 并发读会抢数据或抛异步异常，这里提前给出可定位的失败原因。
                throw new InvalidOperationException(
                    $"连接 {Id} 的入站数据已由{claimedBy}接管，不能再通过 GetStream()、ReceiveAsync() 或 ChannelInfo.Stream 读取；" +
                    "入站数据只能由该处理器消费，写方向不受影响。");
            }

            if (count <= 0) return 0;

            while (true)
            {
                var result = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                var sequence = result.Buffer;

                if (sequence.Length == 0)
                {
                    _reader.AdvanceTo(sequence.End);
                    if (result.IsCompleted) return 0;
                    continue;
                }

                var length = (int)Math.Min(count, sequence.Length);
                sequence.Slice(0, length).CopyTo(buffer.AsSpan(offset, length));
                _reader.AdvanceTo(sequence.GetPosition(length));
                return length;
            }
        }

        /// <summary>
        /// 写入应用数据。TLS 模式会等待握手完成。
        /// </summary>
        public async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (count <= 0) return;

            await HandshakeTask.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (IsClosed)
                throw new IOException("连接已关闭。");

            if (_tls == null)
            {
                await SendRawCopyAsync(buffer, offset, count).ConfigureAwait(false);
                return;
            }

            var tlsLock = _tlsLock;
            List<PendingOutput> records;
            var acquired = false;
            try
            {
                acquired = await tlsLock.WaitAsync(_option.ActionTimeout > 0 ? _option.ActionTimeout : 30 * 1000, cancellationToken)
                    .ConfigureAwait(false);
                if (!acquired)
                    throw new TimeoutException("TLS 出站处理超时。");

                _tls.WriteApplicationData(buffer, offset, count);
                records = CaptureOutputTracked();
            }
            finally
            {
                if (acquired) tlsLock.Release();
            }

            // 必须等本次应用数据拆出的全部 TLS 记录完成底层交接，不能入队后立即返回。
            await FlushOutputAsync(records).ConfigureAwait(false);
            RaiseCallbacks();
        }

        /// <summary>
        /// 应用数据发送（服务端/客户端发送重载使用）。内部先复制入参，返回的 Task 在
        /// 本次应用数据拆出的全部 TLS 记录完成底层交接后才完成。
        /// </summary>
        /// <param name="data">待发送的应用数据，为空时返回已完成任务</param>
        /// <returns>交接完成的任务</returns>
        internal Task SendApplicationData(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) return Task.CompletedTask;

            var copy = data.ToArray();
            return SendApplicationDataAsync(copy);
        }

        private async Task SendApplicationDataAsync(byte[] copy)
        {
            if (_tls == null)
            {
                await SendRawCopyAsync(copy, 0, copy.Length).ConfigureAwait(false);
                return;
            }

            await WriteAsync(copy, 0, copy.Length, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// 标记入站结束（EOF 或异常），唤醒挂起的读取。
        /// </summary>
        public void CompleteInput(Exception ex = null)
        {
            // 先记录入站结束再判断排空状态：排空可能在本行之后才发起，发起时会读该标记直接收口。
            _inputCompleted = true;

            // 对端已结束发送：排空中不会再有新字节到达，直接按排空完成收口。
            if (Volatile.Read(ref _gracefulState) == GracefulDraining)
                FinishGracefulClose();

            try { _writer.Complete(ex); } catch { }

            // 快照字段：Dispose 与接收回调可能并发执行，收尾路径不允许再向外抛异常。
            var tls = _tls;
            var tlsLock = _tlsLock;

            if (tls == null || tlsLock == null || tls.IsClosed) return;

            try
            {
                var acquired = false;
                try
                {
                    acquired = tlsLock.Wait(1000);
                    if (acquired)
                    {
                        try { tls.CloseInput(); } catch { }
                        CaptureOutput();
                    }
                }
                finally
                {
                    if (acquired) tlsLock.Release();
                }

                FlushOutput();
            }
            catch
            {
                // 连接已进入关闭流程，TLS 收尾失败只影响本次告警记录，不再冒泡到接收循环。
            }
        }

        /// <summary>
        /// 半关发送方向，按字节上限与超时排空入站后通过 <paramref name="close"/> 收口。
        /// <para>
        /// 半关让对端立即收到响应与结束信号，同时保持接收方向可读，把已经发出的请求体读完，
        /// 避免带着未读字节关闭 socket 触发 RST 而丢失 413 响应。参数非正或连接已关闭时直接收口。
        /// </para>
        /// </summary>
        /// <param name="drainBytes">关闭前还需读取的字节数</param>
        /// <param name="timeoutMs">排空超时毫秒数</param>
        /// <param name="close">排空结束后的关闭动作，由传输层传入</param>
        /// <returns>连接真正关闭后完成的任务</returns>
        internal Task DrainAndCloseAsync(long drainBytes, int timeoutMs, Action close)
        {
            if (drainBytes <= 0 || timeoutMs <= 0 || IsClosed)
            {
                try { close?.Invoke(); } catch { }
                return Task.CompletedTask;
            }

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_gracefulGate)
            {
                if (Volatile.Read(ref _gracefulState) != GracefulNone)
                {
                    // 已在排空或已收口（会话超时与协议错误可能同时要求关闭）：复用同一个完成信号，不再重复半关。
                    var running = Volatile.Read(ref _gracefulCompletion);
                    return running != null ? running.Task : Task.CompletedTask;
                }

                // 闸门内按「先写依赖、后发状态」的顺序发布：并发调用方只有看到 Draining 时，
                // 排空字节数与完成信号一定已可见，不会读到半成品而误判为已关闭。
                Interlocked.Exchange(ref _gracefulClose, close);
                Interlocked.Exchange(ref _drainRemaining, drainBytes);
                Interlocked.Exchange(ref _gracefulCompletion, completion);
                Volatile.Write(ref _gracefulState, GracefulDraining);
            }

            // 排空前那批数据可能已把接收置为暂停；读侧已停，只能由这里恢复，否则排空收不到任何字节。
            // 这里无条件续投：暂停标记未必已置位，按标记判断会漏投，排空便一直等到超时。
            ForceResumeReceiveForDrain();

            try { _socket.Shutdown(SocketShutdown.Send); } catch { }

            // 超时用延时任务兜底：对端不发完也不关闭时，收口不能无限挂住。
            Task.Delay(timeoutMs).ContinueWith(
                t => FinishGracefulClose(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            // 入站可能在本方法发布排空状态之前就已结束：此时不会再有字节到达，也不会再触发 ConsumeDrain。
            if (_inputCompleted) FinishGracefulClose();

            return completion.Task;
        }

        /// <summary>
        /// 累计已读入站字节；达到排空规格后触发关闭。非排空状态下调用无副作用。
        /// </summary>
        /// <param name="count">本次读入的字节数</param>
        internal void ConsumeDrain(int count)
        {
            if (count <= 0) return;
            if (Volatile.Read(ref _gracefulState) != GracefulDraining) return;

            if (Interlocked.Add(ref _drainRemaining, -count) > 0) return;

            FinishGracefulClose();
        }

        /// <summary>
        /// 结束排空并执行关闭动作，幂等；关闭动作放到线程池执行，避免在接收回调栈上释放连接。
        /// </summary>
        private void FinishGracefulClose()
        {
            if (Interlocked.Exchange(ref _gracefulState, GracefulCompleted) != GracefulDraining) return;

            var completion = Interlocked.Exchange(ref _gracefulCompletion, null);
            var close = Interlocked.Exchange(ref _gracefulClose, null);

            ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    if (close != null) close();
                    else Dispose();
                }
                catch
                {
                    // 关闭路径不允许再向外抛：异常只会影响本连接，收口仍需完成。
                }
                finally
                {
                    if (completion != null) completion.TrySetResult(true);
                }
            });
        }

        /// <summary>
        /// 释放连接。
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _state, 2) == 2) return;

            // 外部直接释放时排空等待方必须落地，否则收口末端会一直等一个不可能完成的信号。
            Volatile.Write(ref _gracefulState, GracefulCompleted);
            var gracefulCompletion = Interlocked.Exchange(ref _gracefulCompletion, null);
            if (gracefulCompletion != null) gracefulCompletion.TrySetResult(true);

            try { _handshakeTimer?.Dispose(); } catch { }
            _handshakeTimer = null;

            try { _reader.CancelPendingRead(); } catch { }
            try { _writer.Complete(); } catch { }
            try { _reader.Complete(); } catch { }

            var tls = _tls;
            if (tls != null)
            {
                try { tls.AbortHandshake(new SaeaTlsException("连接已关闭。")); } catch { }
                try { tls.Dispose(); } catch { }
            }

            // 连接关闭时仍有未发出的 TLS 记录：归还池化缓冲区，并让等待交接的发送方失败。
            while (_pendingOutput.TryDequeue(out var pending))
            {
                try { ArrayPool<byte>.Shared.Return(pending.Buffer); } catch { }
                pending.MarkFailed(new IOException("连接已关闭。"));
            }

            try { _socket.Shutdown(SocketShutdown.Both); } catch { }
            try { _socket.Close(); } catch { }
        }

        private void WriteToPipe(ReadOnlySpan<byte> data)
        {
            try
            {
                _writer.Write(data);
                var flushTask = _writer.FlushAsync().AsTask();
                if (!flushTask.IsCompleted)
                    PauseReceive(flushTask);
            }
            catch (Exception ex)
            {
                CompleteInput(ex);
            }
        }

        private void PauseReceive(Task flushTask)
        {
            Volatile.Write(ref _receivePausedFlag, 1);

            _ = flushTask.ContinueWith(t =>
            {
                ResumeReceiveOnce();
            }, TaskContinuationOptions.ExecuteSynchronously);
        }

        /// <summary>
        /// 从传输层暂停态恢复一次接收；未处于暂停态时不做任何事，避免重复提交接收。
        /// </summary>
        private void ResumeReceiveOnce()
        {
            if (Interlocked.Exchange(ref _receivePausedFlag, 0) == 0) return;

            // 应用级暂停优先：恢复仍由 ResumeApplication 负责，这里只清传输层标记。
            if (_appPaused) return;

            try { _receiveResume?.Invoke(); } catch { }
        }

        /// <summary>
        /// 排空开始时无条件续投一次接收：暂停标记可能尚未置位，按标记判断会漏投，排空只能等到超时。
        /// 应用级暂停由业务控制，这里不解除。
        /// </summary>
        private void ForceResumeReceiveForDrain()
        {
            Interlocked.Exchange(ref _receivePausedFlag, 0);

            if (_appPaused) return;

            try { _receiveResume?.Invoke(); } catch { }
        }

        /// <summary>
        /// 在持有 <see cref="_tlsLock"/> 时把 BouncyCastle 的待发送记录搬入队列。
        /// 真正的 Socket 写操作在锁外执行，避免发送背压阻塞入站解密。
        /// </summary>
        private void CaptureOutput()
        {
            CaptureOutputTracked();
        }

        /// <summary>
        /// 与 <see cref="CaptureOutput"/> 相同，并返回本次新入队的记录，
        /// 供应用数据发送等待这些记录全部完成底层交接。
        /// </summary>
        /// <returns>本次新捕获的记录；没有新记录时为 null</returns>
        private List<PendingOutput> CaptureOutputTracked()
        {
            if (_tls == null) return null;

            List<PendingOutput> captured = null;
            while (_tls.AvailableOutput > 0)
            {
                var size = Math.Min(_tls.AvailableOutput, 64 * 1024);
                var buffer = ArrayPool<byte>.Shared.Rent(size);
                var read = _tls.ReadOutput(buffer, 0, size);
                if (read <= 0)
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                    break;
                }

                var pending = new PendingOutput(buffer, read);
                Interlocked.Increment(ref _tlsRecordsGenerated);
                _pendingOutput.Enqueue(pending);
                if (captured == null) captured = new List<PendingOutput>();
                captured.Add(pending);
            }

            return captured;
        }

        /// <summary>
        /// 按产生顺序发送 TLS 记录。单个连接同一时刻只有一个发送者，
        /// 后续生产者只负责入队，避免乱序。
        /// </summary>
        private void FlushOutput()
        {
            StartDrain();
        }

        /// <summary>
        /// 等待指定记录全部完成底层交接后返回。
        /// </summary>
        /// <param name="records">本次应用数据产生的记录</param>
        private async Task FlushOutputAsync(List<PendingOutput> records)
        {
            StartDrain();

            if (records == null || records.Count == 0) return;

            var tasks = new Task[records.Count];
            for (var i = 0; i < records.Count; i++)
            {
                tasks[i] = records[i].Completion.Task;
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private void StartDrain()
        {
            while (true)
            {
                if (Interlocked.CompareExchange(ref _drainingOutput, 1, 0) != 0)
                    return;

                try
                {
                    while (_pendingOutput.TryDequeue(out var pending))
                    {
                        SendPending(pending);
                    }
                }
                finally
                {
                    Volatile.Write(ref _drainingOutput, 0);
                }

                if (_pendingOutput.IsEmpty)
                    return;
            }
        }

        /// <summary>
        /// 把一条记录交给底层发送路径，并在发送任务完成时记录交接结果。
        /// </summary>
        /// <param name="pending">待发送记录</param>
        private void SendPending(PendingOutput pending)
        {
            var owner = new PooledArrayOwner(pending.Buffer);
            Task sendTask;
            try
            {
                sendTask = _rawSend(new ArraySegment<byte>(pending.Buffer, 0, pending.Length), owner);
            }
            catch (Exception ex)
            {
                owner.Dispose();
                pending.MarkFailed(ex);
                return;
            }

            if (sendTask == null)
            {
                pending.MarkHandedOff();
                return;
            }

            if (sendTask.IsCompleted)
            {
                OnSendCompleted(sendTask, pending);
                return;
            }

            sendTask.ContinueWith(t => OnSendCompleted(t, pending), CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        private void OnSendCompleted(Task sendTask, PendingOutput pending)
        {
            if (sendTask.Status == TaskStatus.RanToCompletion)
            {
                Interlocked.Increment(ref _tlsRecordsHandedOff);
                pending.MarkHandedOff();
                return;
            }

            if (sendTask.IsCanceled)
            {
                pending.MarkFailed(new OperationCanceledException("TLS 记录发送已取消。"));
                return;
            }

            var error = sendTask.Exception != null ? sendTask.Exception.GetBaseException() : null;
            pending.MarkFailed(error ?? new IOException("TLS 记录发送失败。"));
        }

        private void DrainOutputLocked()
        {
            var tlsLock = _tlsLock;
            if (tlsLock == null) return;

            var acquired = false;
            try
            {
                acquired = tlsLock.Wait(_option.ActionTimeout > 0 ? _option.ActionTimeout : 30 * 1000);
                if (!acquired) return;
                CaptureOutput();
            }
            catch { }
            finally
            {
                if (acquired) tlsLock.Release();
            }

            FlushOutput();
        }

        private Task SendRawCopyAsync(byte[] buffer, int offset, int count)
        {
            if (count <= 0 || _rawSend == null) return Task.CompletedTask;

            var writer = new PooledBufferWriter(count);
            try
            {
                buffer.AsSpan(offset, count).CopyTo(writer.GetSpan(count));
                writer.Advance(count);
            }
            catch
            {
                writer.Dispose();
                throw;
            }

            if (!writer.TryGetArray(out var segment) || segment.Array == null)
            {
                writer.Dispose();
                return Task.CompletedTask;
            }

            try
            {
                return _rawSend(segment, writer);
            }
            catch
            {
                // 没有交接成功时由这里归还缓冲，交给发送路径的数据所有权随之失效。
                writer.Dispose();
                throw;
            }
        }

        private void OnHandshakeTimeout(object state)
        {
            if (_tls == null || _tls.IsAuthenticated || IsClosed) return;

            AbortHandshake(new TimeoutException($"TLS 握手超时，超时时间：{_option.Tls?.HandshakeTimeout.TotalMilliseconds ?? 0}毫秒。"));
        }

        private void AbortHandshake(Exception ex)
        {
            try { _tls?.AbortHandshake(ex); } catch { }

            var acquired = false;
            try
            {
                if (_tlsLock != null)
                {
                    acquired = _tlsLock.Wait(1000);
                    if (acquired) CaptureOutput();
                }
            }
            catch { }
            finally
            {
                if (acquired) _tlsLock.Release();
            }

            FlushOutput();
            RaiseCallbacks();
            try { _writer.Complete(ex); } catch { }
            try { _socket.Shutdown(SocketShutdown.Both); } catch { }
            try { _socket.Close(); } catch { }
        }

        private void RaiseCallbacks()
        {
            var tls = _tls;
            if (tls == null || !tls.HasPendingCallbacks) return;

            tls.RaisePendingCallbacks(new SaeaTlsHandshakeContext
            {
                IsServer = tls.IsServer,
                RemoteEndPoint = SafeRemoteEndPoint(),
                TargetHost = _option.Tls?.TargetHost,
                SniHostName = tls.SniHostName,
                LocalCertificate = tls.LocalCertificate,
                RemoteCertificate = tls.RemoteCertificate,
                NegotiatedProtocol = tls.NegotiatedProtocol,
                CipherSuite = tls.CipherSuite,
                Exception = tls.HandshakeException
            });
        }

        private string SafeRemoteEndPoint()
        {
            try { return _socket.RemoteEndPoint?.ToString(); }
            catch { return null; }
        }
    }

    /// <summary>
    /// 一条待发送的 TLS 记录，缓冲区由 <see cref="ArrayPool{T}.Shared"/> 租借；
    /// <see cref="Completion"/> 标记该记录是否已经完成底层交接。
    /// </summary>
    internal sealed class PendingOutput
    {
        internal PendingOutput(byte[] buffer, int length)
        {
            Buffer = buffer;
            Length = length;
            Completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        internal readonly byte[] Buffer;

        internal readonly int Length;

        internal readonly TaskCompletionSource<bool> Completion;

        /// <summary>标记记录已完成底层交接。</summary>
        internal void MarkHandedOff()
        {
            Completion.TrySetResult(true);
        }

        /// <summary>
        /// 标记记录交接失败。没有等待者的记录（握手、告警）同样需要观察异常，
        /// 避免未观察的任务异常在终结器线程上报告。
        /// </summary>
        /// <param name="exception">失败原因</param>
        internal void MarkFailed(Exception exception)
        {
            if (!Completion.TrySetException(exception)) return;

            _ = Completion.Task.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    /// <summary>
    /// 把池化数组的归还封装为 <see cref="IDisposable"/>，交给 saea 发送所有权链。
    /// </summary>
    internal sealed class PooledArrayOwner : IDisposable
    {
        private byte[] _buffer;

        internal PooledArrayOwner(byte[] buffer)
        {
            _buffer = buffer;
        }

        public void Dispose()
        {
            var buffer = Interlocked.Exchange(ref _buffer, null);
            if (buffer != null)
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}