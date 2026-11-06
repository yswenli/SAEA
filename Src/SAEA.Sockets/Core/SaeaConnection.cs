/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
 *Copyright (c) yswenli All Rights Reserved.
 *CLR版本： netstandard2.0
 *命名空间：SAEA.Sockets.Core
 *文件名： SaeaConnection
 *描述：IOCP 连接的双工流门面与 TLS 处理
 *
 *****************************************************************************/
using SAEA.Common.Caching;
using SAEA.Sockets.Ssl;
using SAEA.Sockets.Ssl.Internal;

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Sockets.Core
{
    /// <summary>
    /// 表示一条 IOCP 连接。流模式与 TLS 都通过本类承载：
    /// 入站密文/明文写入 <see cref="Pipe"/>，<see cref="Stream"/> 从 <see cref="Pipe"/> 读取；
    /// 出站写入经 TLS 加密后交给 IOCP 发送。
    /// </summary>
    internal sealed class SaeaConnection : IDisposable
    {
        /// <summary>
        /// IOCP 原始发送委托：由 Socket 实现负责串行化与所有权释放。
        /// </summary>
        private readonly Action<ArraySegment<byte>, IDisposable> _rawSend;

        private readonly Action _receiveResume;

        private readonly ISocketOption _option;

        private readonly Socket _socket;

        private readonly Pipe _pipe;

        private readonly PipeWriter _writer;

        private readonly PipeReader _reader;

        private readonly SaeaStream _stream;

        private SaeaTlsTransport _tls;

        private SemaphoreSlim _tlsLock;

        private Timer _handshakeTimer;

        /// <summary>
        /// 已从 BouncyCastle 取出的 TLS 记录，等待按顺序写入 Socket。
        /// 出站记录必须在释放 <see cref="_tlsLock"/> 后再发送，否则全双工场景中
        /// 发送背压会阻塞入站解密，形成互等。
        /// </summary>
        private readonly ConcurrentQueue<PendingOutput> _pendingOutput = new ConcurrentQueue<PendingOutput>();

        private int _drainingOutput;

        private int _state;

        private volatile bool _receivePaused;

        private volatile bool _appPaused;

        /// <summary>
        /// 构造连接。
        /// </summary>
        /// <param name="id">连接标识</param>
        /// <param name="socket">套接字</param>
        /// <param name="option">套接字配置</param>
        /// <param name="rawSend">原始发送委托</param>
        /// <param name="receiveResume">恢复接收回调</param>
        internal SaeaConnection(string id, Socket socket, ISocketOption option,
            Action<ArraySegment<byte>, IDisposable> rawSend, Action receiveResume)
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

        /// <summary>是否可写。</summary>
        public bool CanWrite => Volatile.Read(ref _state) == 0 && _socket.Connected;

        /// <summary>是否暂停接收：Pipe 背压或应用级暂停任一成立。</summary>
        public bool IsReceivePaused
        {
            get { return _receivePaused || _appPaused; }
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
            if (!_receivePaused)
            {
                try { _receiveResume?.Invoke(); } catch { }
            }
        }

        /// <summary>是否已关闭。</summary>
        public bool IsClosed => Volatile.Read(ref _state) != 0;

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
        /// 由 IOCP 接收回调调用。data 仅在调用期间有效。
        /// </summary>
        public void OnRawReceived(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0 || IsClosed) return;

            if (_tls == null)
            {
                WriteToPipe(data);
                return;
            }

            byte[] rented = null;
            var flushTask = default(Task<FlushResult>);
            var flushed = false;

            try
            {
                rented = ArrayPool<byte>.Shared.Rent(data.Length);
                data.CopyTo(rented);

                var acquired = false;
                var writer = _writer;
                var written = false;

                try
                {
                    acquired = _tlsLock.Wait(_option.ActionTimeout > 0 ? _option.ActionTimeout : 30 * 1000);
                    if (!acquired)
                        throw new SaeaTlsException("TLS 入站处理超时。");

                    _tls.OfferInput(rented, 0, data.Length);
                    CaptureOutput();

                    while (_tls.AvailableInput > 0)
                    {
                        var n = _tls.ReadInput(rented, 0, rented.Length);
                        if (n <= 0) break;
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
                    if (acquired) _tlsLock.Release();
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
        }

        /// <summary>
        /// 从入站 Pipe 读取明文。
        /// </summary>
        public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
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
                SendRawCopy(buffer, offset, count);
                return;
            }

            var acquired = false;
            try
            {
                acquired = await _tlsLock.WaitAsync(_option.ActionTimeout > 0 ? _option.ActionTimeout : 30 * 1000, cancellationToken)
                    .ConfigureAwait(false);
                if (!acquired)
                    throw new TimeoutException("TLS 出站处理超时。");

                _tls.WriteApplicationData(buffer, offset, count);
                CaptureOutput();
            }
            finally
            {
                if (acquired) _tlsLock.Release();
            }

            FlushOutput();
            RaiseCallbacks();
        }

        /// <summary>
        /// 同步路径的应用数据发送（服务端 SendAsync 使用）。握手未完成时在后台等待。
        /// </summary>
        internal void SendApplicationData(ReadOnlySpan<byte> data, Action<Exception> onError)
        {
            if (data.Length == 0) return;

            var copy = data.ToArray();

            if (_tls == null)
            {
                try { SendRawCopy(copy, 0, copy.Length); }
                catch (Exception ex) { onError?.Invoke(ex); }
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await WriteAsync(copy, 0, copy.Length, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    onError?.Invoke(ex);
                }
            });
        }

        /// <summary>
        /// 标记入站结束（EOF 或异常），唤醒挂起的读取。
        /// </summary>
        public void CompleteInput(Exception ex = null)
        {
            try { _writer.Complete(ex); } catch { }

            if (_tls != null && !_tls.IsClosed)
            {
                var acquired = false;
                try
                {
                    acquired = _tlsLock.Wait(1000);
                    if (acquired)
                    {
                        try { _tls.CloseInput(); } catch { }
                        CaptureOutput();
                    }
                }
                catch { }
                finally
                {
                    if (acquired) _tlsLock.Release();
                }

                FlushOutput();
            }
        }

        /// <summary>
        /// 释放连接。
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _state, 2) == 2) return;

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

            // 连接关闭时仍有未发出的 TLS 记录：归还池化缓冲区，避免长期占用 ArrayPool。
            while (_pendingOutput.TryDequeue(out var pending))
            {
                try { ArrayPool<byte>.Shared.Return(pending.Buffer); } catch { }
            }

            try { _tlsLock?.Dispose(); } catch { }
            _tlsLock = null;

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
            _receivePaused = true;

            _ = flushTask.ContinueWith(t =>
            {
                _receivePaused = false;
                try { _receiveResume?.Invoke(); } catch { }
            }, TaskContinuationOptions.ExecuteSynchronously);
        }

        /// <summary>
        /// 在持有 <see cref="_tlsLock"/> 时把 BouncyCastle 的待发送记录搬入队列。
        /// 真正的 Socket 写操作在锁外执行，避免发送背压阻塞入站解密。
        /// </summary>
        private void CaptureOutput()
        {
            if (_tls == null) return;

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

                _pendingOutput.Enqueue(new PendingOutput(buffer, read));
            }
        }

        /// <summary>
        /// 按产生顺序发送 TLS 记录。单个连接同一时刻只有一个发送者，
        /// 后续生产者只负责入队，避免乱序。
        /// </summary>
        private void FlushOutput()
        {
            while (true)
            {
                if (Interlocked.CompareExchange(ref _drainingOutput, 1, 0) != 0)
                    return;

                try
                {
                    while (_pendingOutput.TryDequeue(out var pending))
                    {
                        var owner = new PooledArrayOwner(pending.Buffer);
                        try
                        {
                            _rawSend(new ArraySegment<byte>(pending.Buffer, 0, pending.Length), owner);
                        }
                        catch
                        {
                            owner.Dispose();
                        }
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

        private void DrainOutputLocked()
        {
            var acquired = false;
            try
            {
                acquired = _tlsLock.Wait(_option.ActionTimeout > 0 ? _option.ActionTimeout : 30 * 1000);
                if (!acquired) return;
                CaptureOutput();
            }
            catch { }
            finally
            {
                if (acquired) _tlsLock.Release();
            }

            FlushOutput();
        }

        private void SendRawCopy(byte[] buffer, int offset, int count)
        {
            if (count <= 0 || _rawSend == null) return;

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
                return;
            }

            _rawSend(segment, writer);
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
    /// 一条待发送的 TLS 记录，缓冲区由 <see cref="ArrayPool{T}.Shared"/> 租借。
    /// </summary>
    internal readonly struct PendingOutput
    {
        internal PendingOutput(byte[] buffer, int length)
        {
            Buffer = buffer;
            Length = length;
        }

        internal readonly byte[] Buffer;

        internal readonly int Length;
    }

    /// <summary>
    /// 把池化数组的归还封装为 <see cref="IDisposable"/>，交给 IOCP 发送所有权链。
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