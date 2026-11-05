/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
 *Copyright (c) yswenli All Rights Reserved.
 *CLR版本： netstandard2.0
 *命名空间：SAEA.Sockets.Ssl.Internal
 *文件名： SaeaTlsTransport
 *描述：BouncyCastle TLS 传输适配器基类
 *
 *****************************************************************************/
using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Sockets.Ssl.Internal
{
    /// <summary>
    /// BouncyCastle TLS 非阻塞传输适配器基类。所有方法均不持有内部锁，
    /// 由 <c>SaeaConnection</c> 通过 <see cref="SemaphoreSlim"/> 串行化调用。
    /// </summary>
    internal abstract class SaeaTlsTransport : ISaeaTlsContext, IDisposable
    {
        private readonly TaskCompletionSource<bool> _handshakeTcs =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _pendingStarted;
        private int _pendingCompleted;
        private Exception _pendingFailure;
        private Exception _handshakeException;

        private X509Certificate2 _remoteCertificate;

        /// <summary>
        /// 构造传输适配器。
        /// </summary>
        protected SaeaTlsTransport(SaeaTlsOptions options)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// TLS 配置。
        /// </summary>
        protected SaeaTlsOptions Options { get; }

        /// <summary>
        /// 握手完成信号。握手失败时该任务以异常结束。
        /// </summary>
        public Task HandshakeTask => _handshakeTcs.Task;

        /// <summary>开始握手（非阻塞）。</summary>
        public abstract void StartHandshake();

        /// <summary>喂入密文。BC 内部会复制，调用方可立即归还缓冲区。</summary>
        public abstract void OfferInput(byte[] data, int offset, int count);

        /// <summary>可读取的明文字节数。</summary>
        public abstract int AvailableInput { get; }

        /// <summary>读取明文。</summary>
        public abstract int ReadInput(byte[] buffer, int offset, int count);

        /// <summary>写入应用数据（加密）。</summary>
        public abstract void WriteApplicationData(byte[] buffer, int offset, int count);

        /// <summary>可读取的密文字节数。</summary>
        public abstract int AvailableOutput { get; }

        /// <summary>读取密文。</summary>
        public abstract int ReadOutput(byte[] buffer, int offset, int count);

        /// <summary>是否仍在握手中。</summary>
        public abstract bool IsHandshaking { get; }

        /// <summary>是否已关闭。</summary>
        public abstract bool IsClosed { get; }

        /// <summary>关闭入站方向（发送 close_notify，尽力而为）。</summary>
        public abstract void CloseInput();

        /// <summary>发送 close_notify 并关闭协议（尽力而为）。</summary>
        public abstract void Close();

        /// <summary>是否已完成认证。</summary>
        public bool IsAuthenticated => _handshakeTcs.Task.Status == TaskStatus.RanToCompletion;

        /// <summary>是否为服务端。</summary>
        public abstract bool IsServer { get; }

        /// <summary>SNI 主机名。</summary>
        public abstract string SniHostName { get; }

        /// <summary>协商出的协议版本。</summary>
        public abstract string NegotiatedProtocol { get; }

        /// <summary>协商出的加密套件。</summary>
        public abstract string CipherSuite { get; }

        /// <summary>本地证书。</summary>
        public abstract X509Certificate2 LocalCertificate { get; }

        /// <summary>远端证书。</summary>
        public virtual X509Certificate2 RemoteCertificate => _remoteCertificate;

        /// <summary>握手异常。</summary>
        public Exception HandshakeException => _handshakeException;

        /// <summary>记录远端证书（由 Peer / 认证对象调用）。</summary>
        internal void SetRemoteCertificate(X509Certificate2 certificate)
        {
            _remoteCertificate = certificate;
        }

        /// <summary>创建服务端 TLS 适配器。</summary>
        public static SaeaTlsTransport CreateServer(SaeaTlsOptions options)
            => new SaeaTlsServerTransport(options);

        /// <summary>创建客户端 TLS 适配器。</summary>
        public static SaeaTlsTransport CreateClient(SaeaTlsOptions options)
            => new SaeaTlsClientTransport(options);

        /// <summary>标记握手开始（由 Peer 调用，不触发用户回调）。</summary>
        public void NoteHandshakeStarted()
        {
            Interlocked.Exchange(ref _pendingStarted, 1);
        }

        /// <summary>标记握手完成（由 Peer 调用，不触发用户回调）。</summary>
        public void NoteHandshakeCompleted()
        {
            Interlocked.Exchange(ref _pendingCompleted, 1);
            _handshakeTcs.TrySetResult(true);
        }

        /// <summary>标记握手失败（由 Peer / 超时 / IO 错误调用）。</summary>
        public void NoteHandshakeFailed(Exception ex)
        {
            if (ex == null)
                ex = new SaeaTlsException("TLS 握手失败");

            if (Interlocked.CompareExchange(ref _handshakeException, ex, null) != null)
                ex = _handshakeException;

            Interlocked.Exchange(ref _pendingFailure, ex);
            _handshakeTcs.TrySetException(ex);
        }

        /// <summary>中止握手并关闭协议。</summary>
        public void AbortHandshake(Exception ex)
        {
            NoteHandshakeFailed(ex);
            try { Close(); } catch { }
        }

        /// <summary>是否存在等待触发的握手回调。</summary>
        public bool HasPendingCallbacks =>
            Volatile.Read(ref _pendingStarted) != 0 ||
            Volatile.Read(ref _pendingCompleted) != 0 ||
            Volatile.Read(ref _pendingFailure) != null;

        /// <summary>
        /// 触发挂起的握手回调。必须在释放 TLS 锁之后调用。
        /// </summary>
        public void RaisePendingCallbacks(SaeaTlsHandshakeContext context)
        {
            if (context == null) context = new SaeaTlsHandshakeContext();

            if (Interlocked.Exchange(ref _pendingStarted, 0) != 0)
                SafeInvoke(Options.HandshakeStarted, context);

            if (Interlocked.Exchange(ref _pendingCompleted, 0) != 0)
                SafeInvoke(Options.HandshakeCompleted, context);

            var failure = Interlocked.Exchange(ref _pendingFailure, null);
            if (failure != null)
            {
                context.Exception = failure;
                SafeInvoke(Options.HandshakeFailed, context);
            }
        }

        /// <summary>释放协议资源。</summary>
        public virtual void Dispose()
        {
            try { Close(); } catch { }
        }

        private static void SafeInvoke(Action<SaeaTlsHandshakeContext> callback, SaeaTlsHandshakeContext context)
        {
            if (callback == null) return;
            try { callback(context); } catch { }
        }
    }
}