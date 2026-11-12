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
*命名空间：SAEA.P2P.Ice
*文件名： TcpDirectChannel
*版本号： v26.11.5.1
*唯一标识：544090c2-f137-44a4-a6fa-ce3e2689889b
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 13:33:14
*描述：TCP 直连通道的建链、检查与数据面衔接
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 13:33:14
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TCP 直连通道
*
*****************************************************************************/
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Common;
using SAEA.P2P.Stun;
using SAEA.P2P.Transport;

namespace SAEA.P2P.Ice
{
    /// <summary>
    /// TCP 直连通道：主动侧负责「建链 → 一次 Binding 检查 → 转数据面」，被动侧只应答入向连接上的检查，
    /// 不主动发检查也不提名。检查通过后同一条 TCP 连接同时承载 STUN 与业务帧，两者都走 RFC 4571 分帧。
    /// 任何检查失败都会自行关闭连接，调用方据 <see cref="TcpDirectResult.RemoteEndPoint"/>
    /// 把该远端候选对应的所有 pair 判为 Failed。
    /// </summary>
    public sealed class TcpDirectChannel : IDisposable
    {
        /// <summary>
        /// 日志节点名
        /// </summary>
        private const string LogNode = "TcpDirectChannel";

        /// <summary>
        /// 单次读入的缓冲大小，一帧最大 65535 字节，分多轮读入由分帧器拼接
        /// </summary>
        private const int ReadBufferSize = 8 * 1024;

        /// <summary>
        /// 保护流写入与连接状态的同步对象
        /// </summary>
        private readonly object _syncRoot = new object();

        /// <summary>
        /// 检查阶段与后台读循环共用的分帧器；同一时刻只有一个读者，切换时不复位以保留已读到的后续帧
        /// </summary>
        private readonly Rfc4571Framer _framer = new Rfc4571Framer();

        /// <summary>
        /// 检查阶段与后台读循环共用的读缓冲，避免每轮读都分配
        /// </summary>
        private readonly byte[] _readBuffer = new byte[ReadBufferSize];

        /// <summary>
        /// 本端口令；校验入向检查请求与签发应答都用它
        /// </summary>
        private readonly string _localPassword;

        /// <summary>
        /// 本地连通性检查实例，只用来复用请求属性构造
        /// </summary>
        private readonly ConnectivityCheck _check;

        /// <summary>
        /// 连接侧传输工厂，每次重试都新建一个实例
        /// </summary>
        private readonly Func<ITcpTransport> _transportFactory;

        /// <summary>
        /// 底层传输；未连接、检查失败或已释放时为 null
        /// </summary>
        private ITcpTransport _transport;

        /// <summary>
        /// 后台读循环的取消源；未进入数据面时为 null
        /// </summary>
        private CancellationTokenSource _loopCts;

        /// <summary>
        /// 后台读循环任务；未进入数据面时为 null
        /// </summary>
        private Task _readLoopTask;

        /// <summary>
        /// 本地端点，建链成功后取传输的实际绑定地址
        /// </summary>
        private IPEndPoint _localEndPoint;

        /// <summary>
        /// 远端端点；被动侧拿不到对端端点，保持 null
        /// </summary>
        private IPEndPoint _remoteEndPoint;

        /// <summary>
        /// 检查超时毫秒数
        /// </summary>
        private int _checkTimeoutMilliseconds = 5000;

        /// <summary>
        /// 建链最大尝试次数
        /// </summary>
        private int _connectAttempts = 3;

        /// <summary>
        /// 首次重试前的退避毫秒数，之后每次翻倍
        /// </summary>
        private int _retryDelayMilliseconds = 100;

        /// <summary>
        /// 已投递的业务帧数量
        /// </summary>
        private long _receivedFrameCount;

        /// <summary>
        /// 是否已完成检查、可以收发业务帧
        /// </summary>
        private volatile bool _connected;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private volatile bool _disposed;

        /// <summary>
        /// 收到一条完整业务帧时触发，参数为已复制的载荷，不含 0xC0 前缀；回调返回后仍可用
        /// </summary>
        public event Action<ReadOnlyMemory<byte>> FrameReceived;

        /// <summary>
        /// 已投递的业务帧数量
        /// </summary>
        public long ReceivedFrameCount
        {
            get { return Interlocked.Read(ref _receivedFrameCount); }
        }

        /// <summary>
        /// 是否已完成检查并处在数据面可用状态；检查失败或释放后为 false
        /// </summary>
        public bool IsConnected
        {
            get { return _connected && !_disposed; }
        }

        /// <summary>
        /// 本地端点；建链成功前为 null，失败后保留最后使用的本地端点
        /// </summary>
        public IPEndPoint LocalEndPoint
        {
            get { return _localEndPoint; }
        }

        /// <summary>
        /// 远端端点；主动侧为检查的目标端点，被动侧为 null
        /// </summary>
        public IPEndPoint RemoteEndPoint
        {
            get { return _remoteEndPoint; }
        }

        /// <summary>
        /// 检查阶段超时毫秒数，默认 5000
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">取值小于等于 0 时抛出</exception>
        public int CheckTimeoutMilliseconds
        {
            get { return _checkTimeoutMilliseconds; }
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(CheckTimeoutMilliseconds), value, "检查超时必须大于 0 毫秒");
                }

                _checkTimeoutMilliseconds = value;
            }
        }

        /// <summary>
        /// 建链最大尝试次数，默认 3
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">取值小于等于 0 时抛出</exception>
        public int ConnectAttempts
        {
            get { return _connectAttempts; }
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(ConnectAttempts), value, "建链尝试次数必须大于 0");
                }

                _connectAttempts = value;
            }
        }

        /// <summary>
        /// 首次重试前的退避毫秒数，默认 100；之后每次失败翻倍
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">取值小于 0 时抛出</exception>
        public int RetryDelayMilliseconds
        {
            get { return _retryDelayMilliseconds; }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(RetryDelayMilliseconds), value, "重试退避不能为负数");
                }

                _retryDelayMilliseconds = value;
            }
        }

        /// <summary>
        /// 创建 TCP 直连通道
        /// </summary>
        /// <param name="localUfrag">本地 ufrag，非空</param>
        /// <param name="localPassword">本端口令，非空；不进日志与异常消息</param>
        /// <param name="transportFactory">连接侧传输工厂，为 null 时用内置的 SAEA.Sockets 实现</param>
        /// <exception cref="ArgumentException">ufrag 或口令为空时抛出</exception>
        public TcpDirectChannel(string localUfrag, string localPassword, Func<ITcpTransport> transportFactory = null)
        {
            if (string.IsNullOrEmpty(localUfrag))
            {
                throw new ArgumentException("本地 ufrag 不能为空", nameof(localUfrag));
            }
            if (string.IsNullOrEmpty(localPassword))
            {
                throw new ArgumentException("本端口令不能为空", nameof(localPassword));
            }

            _localPassword = localPassword;
            _check = new ConnectivityCheck(localUfrag, localPassword);
            _transportFactory = transportFactory ?? (() => new SaeaTcpTransport());
        }

        /// <summary>
        /// 主动连接指定远端端点，做一次 Binding 检查，通过后转为数据面通道。
        /// 每次重试都新建传输；本地端口被占用时下一次改用系统分配端口重绑，其余失败按退避重试。
        /// </summary>
        /// <param name="localEndPoint">本地绑定端点，端口传 0 表示由系统分配</param>
        /// <param name="remoteEndPoint">对端候选端点</param>
        /// <param name="remoteUfrag">对端 ufrag</param>
        /// <param name="remotePassword">对端口令，用于签发请求与校验应答</param>
        /// <param name="role">本地角色，决定检查请求携带的角色属性</param>
        /// <param name="tiebreaker">角色冲突判定用的 64 位随机数</param>
        /// <param name="priority">检查请求携带的候选优先级</param>
        /// <param name="useCandidate">是否在请求里提名该候选对</param>
        /// <param name="cancellationToken">取消令牌；建链阶段取消直接抛异常，检查阶段取消返回 Cancelled 结果</param>
        /// <returns>检查结果；失败时连接已被本通道关闭</returns>
        /// <exception cref="ArgumentNullException">localEndPoint 或 remoteEndPoint 为 null 时抛出</exception>
        /// <exception cref="ArgumentException">ufrag 或口令为空时抛出</exception>
        /// <exception cref="InvalidOperationException">通道已连接时抛出</exception>
        /// <exception cref="ObjectDisposedException">通道已释放时抛出</exception>
        /// <exception cref="OperationCanceledException">建链或退避等待期间被调用方取消</exception>
        public async Task<TcpDirectResult> ConnectAsync(
            IPEndPoint localEndPoint,
            IPEndPoint remoteEndPoint,
            string remoteUfrag,
            string remotePassword,
            IceRole role,
            ulong tiebreaker,
            uint priority,
            bool useCandidate,
            CancellationToken cancellationToken)
        {
            if (localEndPoint == null)
            {
                throw new ArgumentNullException(nameof(localEndPoint));
            }
            if (remoteEndPoint == null)
            {
                throw new ArgumentNullException(nameof(remoteEndPoint));
            }
            if (string.IsNullOrEmpty(remoteUfrag))
            {
                throw new ArgumentException("对端 ufrag 不能为空", nameof(remoteUfrag));
            }
            if (string.IsNullOrEmpty(remotePassword))
            {
                throw new ArgumentException("对端口令不能为空", nameof(remotePassword));
            }

            ThrowIfDisposed();

            lock (_syncRoot)
            {
                if (_connected)
                {
                    throw new InvalidOperationException("通道已经连接，不能重复发起连接检查");
                }
            }

            var stopwatch = Stopwatch.StartNew();
            var bindEndPoint = localEndPoint;
            Exception lastError = null;

            for (var attempt = 1; attempt <= _connectAttempts; attempt++)
            {
                var transport = _transportFactory();
                if (transport == null)
                {
                    throw new InvalidOperationException("传输工厂没有返回可用的传输实例");
                }

                try
                {
                    await transport.ConnectAsync(remoteEndPoint, bindEndPoint, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    try { transport.Dispose(); } catch { }
                    throw;
                }
                catch (SocketException ex)
                {
                    try { transport.Dispose(); } catch { }
                    lastError = ex;

                    if (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
                    {
                        // 原端口已被占用时继续按该端口重试只会重复失败，后续改用系统分配端口重绑
                        bindEndPoint = new IPEndPoint(localEndPoint.Address, 0);
                    }

                    P2PLogHelper.Debug(LogNode, "第 " + attempt + " 次连接失败：" + ex.SocketErrorCode);
                    await DelayBeforeRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
                    continue;
                }
                catch (Exception ex)
                {
                    // 地址族不一致之类的参数错误同样只按退避重试，失败原因留给最后一次
                    try { transport.Dispose(); } catch { }
                    lastError = ex;
                    P2PLogHelper.Debug(LogNode, "第 " + attempt + " 次连接失败：" + ex.GetType().Name);
                    await DelayBeforeRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                bool attached;
                lock (_syncRoot)
                {
                    attached = !_disposed;
                    if (attached)
                    {
                        _transport = transport;
                        _localEndPoint = transport.LocalEndPoint ?? bindEndPoint;
                        _remoteEndPoint = remoteEndPoint;
                    }
                }

                if (!attached)
                {
                    try { transport.Dispose(); } catch { }
                    throw new ObjectDisposedException(GetType().Name);
                }

                return await RunActiveCheckAsync(remoteUfrag, remotePassword, role, tiebreaker, priority, useCandidate, stopwatch, cancellationToken).ConfigureAwait(false);
            }

            P2PLogHelper.Debug(LogNode, "建链 " + _connectAttempts + " 次均失败");

            return TcpDirectResult.Fail(
                TcpDirectFailure.ConnectFailed,
                lastError == null ? "连接失败" : lastError.Message,
                remoteEndPoint,
                stopwatch.Elapsed);
        }

        /// <summary>
        /// 接收入向连接，只应答对端在本连接上发来的检查请求，不主动发检查也不提名。
        /// 检查通过后转为数据面通道；失败时连接已被本通道关闭。
        /// </summary>
        /// <param name="accepted">监听侧已接受的传输</param>
        /// <param name="cancellationToken">取消令牌；检查阶段取消返回 Cancelled 结果</param>
        /// <returns>检查结果；接受侧拿不到对端端点，成功时映射地址与远端端点均为 null</returns>
        /// <exception cref="ArgumentNullException">accepted 为 null 时抛出</exception>
        /// <exception cref="InvalidOperationException">通道已连接时抛出</exception>
        /// <exception cref="ObjectDisposedException">通道已释放时抛出</exception>
        public async Task<TcpDirectResult> AttachAsync(ITcpTransport accepted, CancellationToken cancellationToken)
        {
            if (accepted == null)
            {
                throw new ArgumentNullException(nameof(accepted));
            }

            ThrowIfDisposed();

            var stopwatch = Stopwatch.StartNew();

            lock (_syncRoot)
            {
                if (_connected)
                {
                    throw new InvalidOperationException("通道已经连接，不能再次接受入向连接");
                }

                // 接受侧拿不到对端端点，远端端点保持 null，调用方不能用它反查 pair
                _transport = accepted;
                _localEndPoint = accepted.LocalEndPoint;
            }

            var stream = accepted.Stream;
            if (stream == null)
            {
                CloseTransport();
                return TcpDirectResult.Fail(TcpDirectFailure.TransportError, "已接受的连接没有可读写的字节流", null, stopwatch.Elapsed);
            }

            var completed = 0;
            using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeoutCts.CancelAfter(_checkTimeoutMilliseconds);

                var token = timeoutCts.Token;

                // 读调用可能先阻塞在流上，注册关闭才能让超时与取消立刻打断它
                using (token.Register(() =>
                {
                    if (Interlocked.Exchange(ref completed, 1) != 0)
                    {
                        return;
                    }

                    CloseTransport();
                }))
                {
                    while (true)
                    {
                        var attempt = await ReadAttemptAsync(stream, token).ConfigureAwait(false);
                        if (attempt.Error != null)
                        {
                            if (token.IsCancellationRequested)
                            {
                                return BuildCheckStopResult(cancellationToken, null, stopwatch);
                            }

                            CloseTransport();
                            P2PLogHelper.Debug(LogNode, "入向检查读取失败：" + attempt.Error.GetType().Name);
                            return TcpDirectResult.Fail(TcpDirectFailure.TransportError, "读取失败：" + attempt.Error.Message, null, stopwatch.Elapsed);
                        }

                        if (attempt.Count == 0)
                        {
                            CloseTransport();
                            return TcpDirectResult.Fail(TcpDirectFailure.TransportError, "对端已关闭连接", null, stopwatch.Elapsed);
                        }

                        ReadOnlyMemory<byte> frame;
                        bool hasFrame;
                        try
                        {
                            hasFrame = AppendAndTakeFrame(attempt.Count, out frame);
                        }
                        catch (InvalidDataException ex)
                        {
                            CloseTransport();
                            return TcpDirectResult.Fail(TcpDirectFailure.TransportError, "分帧失败：" + ex.Message, null, stopwatch.Elapsed);
                        }

                        while (hasFrame)
                        {
                            TcpDirectResult result;
                            if (TryCompletePassiveCheck(frame, stopwatch, out result))
                            {
                                // 结果已定，别让残余的定时器再关掉即将转数据面的连接
                                Interlocked.Exchange(ref completed, 1);
                                return result ?? CompleteCheck(null, stopwatch);
                            }

                            hasFrame = _framer.TryReadFrame(out frame);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 把业务载荷封装成帧并在锁内写流。
        /// 载荷不复制到帧外，本方法返回后调用方 buffer 即可释放。
        /// </summary>
        /// <param name="payload">业务载荷，长度不得超过 <see cref="DataPlaneCodec.MaxPayloadLength"/></param>
        /// <exception cref="ObjectDisposedException">通道已释放时抛出</exception>
        /// <exception cref="InvalidOperationException">尚未完成连接检查或连接已断开时抛出</exception>
        /// <exception cref="P2PException">载荷超过单帧上限时抛出，错误码为 PayloadTooLarge</exception>
        /// <exception cref="IOException">写入失败时抛出，抛出前已关闭连接</exception>
        public void Send(ReadOnlySpan<byte> payload)
        {
            ThrowIfDisposed();

            if (!_connected)
            {
                throw new InvalidOperationException("通道尚未完成连接检查或连接已断开，不能发送业务帧");
            }

            var stream = GetStream();
            if (stream == null)
            {
                throw new InvalidOperationException("通道尚未完成连接检查或连接已断开，不能发送业务帧");
            }

            var frame = Rfc4571Framer.Encode(DataPlaneCodec.Wrap(payload));

            try
            {
                WriteFrameLocked(stream, frame);
            }
            catch (Exception ex)
            {
                // 写失败说明连接已不可用，主动收尾，避免调用方继续往死连接上写
                CloseTransport();
                P2PLogHelper.Debug(LogNode, "发送业务帧失败：" + ex.GetType().Name);
                throw;
            }
        }

        /// <summary>
        /// 释放通道：取消后台读循环，释放传输与内部检查实例，幂等
        /// </summary>
        public void Dispose()
        {
            CancellationTokenSource loopCts;
            ITcpTransport transport;
            ConnectivityCheck check;
            Task loop;

            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _connected = false;
                loopCts = _loopCts;
                _loopCts = null;
                transport = _transport;
                _transport = null;
                check = _check;
                loop = _readLoopTask;
                _readLoopTask = null;
            }

            if (loopCts != null)
            {
                // 不立即释放取消源：读循环可能还在它上面注册等待，释放会让下一次注册抛 ObjectDisposedException
                try { loopCts.Cancel(); } catch { }
            }

            if (transport != null)
            {
                try { transport.Dispose(); } catch { }
            }

            if (loop != null)
            {
                // 读循环不向外抛异常，等它收敛只是为了让传输释放与循环退出不交错
                try { loop.Wait(100); } catch { }
            }

            try { check.Dispose(); } catch { }
        }

        /// <summary>
        /// 主动侧检查阶段：发一次 Binding 请求，读到有效成功应答后转数据面
        /// </summary>
        /// <param name="remoteUfrag">对端 ufrag</param>
        /// <param name="remotePassword">对端口令</param>
        /// <param name="role">本地角色</param>
        /// <param name="tiebreaker">角色冲突判定用的 64 位随机数</param>
        /// <param name="priority">检查请求携带的候选优先级</param>
        /// <param name="useCandidate">是否提名</param>
        /// <param name="stopwatch">计时器，起点为开始建链</param>
        /// <param name="cancellationToken">调用方取消令牌</param>
        /// <returns>检查结果；失败时连接已被关闭</returns>
        private async Task<TcpDirectResult> RunActiveCheckAsync(
            string remoteUfrag,
            string remotePassword,
            IceRole role,
            ulong tiebreaker,
            uint priority,
            bool useCandidate,
            Stopwatch stopwatch,
            CancellationToken cancellationToken)
        {
            var remoteEndPoint = _remoteEndPoint;
            var stream = GetStream();
            if (stream == null)
            {
                CloseTransport();
                return TcpDirectResult.Fail(TcpDirectFailure.TransportError, "建链后没有可读写的字节流", remoteEndPoint, stopwatch.Elapsed);
            }

            var request = new StunMessage(StunMessageType.Binding, StunClass.Request);
            request.SetAttributes(_check.BuildRequestAttributes(remoteUfrag, role, tiebreaker, priority, useCandidate));

            // 请求用对端口令签，对端按自己的本端口令校验
            StunIntegrity.Append(request, StunIntegrity.CreateShortTermKey(remotePassword));

            var requestTransactionId = request.TransactionId.ToArray();

            try
            {
                WriteFrameLocked(stream, Rfc4571Framer.Encode(request.ToArray()));
            }
            catch (Exception ex)
            {
                CloseTransport();
                return TcpDirectResult.Fail(TcpDirectFailure.TransportError, "发送检查请求失败：" + ex.Message, remoteEndPoint, stopwatch.Elapsed);
            }

            var completed = 0;
            using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeoutCts.CancelAfter(_checkTimeoutMilliseconds);

                var token = timeoutCts.Token;

                // 读调用可能先阻塞在流上，注册关闭才能让超时与取消立刻打断它
                using (token.Register(() =>
                {
                    if (Interlocked.Exchange(ref completed, 1) != 0)
                    {
                        return;
                    }

                    CloseTransport();
                }))
                {
                    while (true)
                    {
                        var attempt = await ReadAttemptAsync(stream, token).ConfigureAwait(false);
                        if (attempt.Error != null)
                        {
                            if (token.IsCancellationRequested)
                            {
                                return BuildCheckStopResult(cancellationToken, remoteEndPoint, stopwatch);
                            }

                            CloseTransport();
                            P2PLogHelper.Debug(LogNode, "检查阶段读取失败：" + attempt.Error.GetType().Name);
                            return TcpDirectResult.Fail(TcpDirectFailure.TransportError, "读取失败：" + attempt.Error.Message, remoteEndPoint, stopwatch.Elapsed);
                        }

                        if (attempt.Count == 0)
                        {
                            CloseTransport();
                            return TcpDirectResult.Fail(TcpDirectFailure.TransportError, "对端已关闭连接", remoteEndPoint, stopwatch.Elapsed);
                        }

                        ReadOnlyMemory<byte> frame;
                        bool hasFrame;
                        try
                        {
                            hasFrame = AppendAndTakeFrame(attempt.Count, out frame);
                        }
                        catch (InvalidDataException ex)
                        {
                            CloseTransport();
                            return TcpDirectResult.Fail(TcpDirectFailure.TransportError, "分帧失败：" + ex.Message, remoteEndPoint, stopwatch.Elapsed);
                        }

                        while (hasFrame)
                        {
                            TcpDirectResult result;
                            IPEndPoint mappedAddress;

                            if (TryCompleteActiveCheck(frame, requestTransactionId, remotePassword, remoteEndPoint, stopwatch, out result, out mappedAddress))
                            {
                                // 结果已定，别让残余的定时器再关掉即将转数据面的连接
                                Interlocked.Exchange(ref completed, 1);
                                if (result != null)
                                {
                                    return result;
                                }

                                return CompleteCheck(mappedAddress, stopwatch);
                            }

                            // 检查阶段读到的后续帧仍留在分帧器里，交给后台读循环接着取
                            hasFrame = _framer.TryReadFrame(out frame);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 处理主动侧检查阶段的一帧：应答对向检查、校验本次应答或给出失败结果
        /// </summary>
        /// <param name="frame">一帧载荷</param>
        /// <param name="requestTransactionId">本次请求的事务 ID</param>
        /// <param name="remotePassword">对端口令</param>
        /// <param name="remoteEndPoint">远端端点，用于填失败结果</param>
        /// <param name="stopwatch">计时器</param>
        /// <param name="result">已得出结果时非 null，为 null 表示检查通过</param>
        /// <param name="mappedAddress">检查通过时的映射地址</param>
        /// <returns>是否已经得出结果</returns>
        private bool TryCompleteActiveCheck(
            ReadOnlyMemory<byte> frame,
            byte[] requestTransactionId,
            string remotePassword,
            IPEndPoint remoteEndPoint,
            Stopwatch stopwatch,
            out TcpDirectResult result,
            out IPEndPoint mappedAddress)
        {
            result = null;
            mappedAddress = null;

            var payload = frame.Span;
            StunMessage message;
            StunParseError parseError;

            if (!StunMessage.TryParse(payload, out message, out parseError))
            {
                // 首个响应帧就必须是 STUN：TCP 上没有重传机会，继续等只会拖到超时
                P2PLogHelper.Debug(LogNode, "检查阶段收到非 STUN 帧：" + parseError);
                CloseTransport();
                result = TcpDirectResult.Fail(TcpDirectFailure.NonStunFrame, "首个响应帧不是 STUN 消息", remoteEndPoint, stopwatch.Elapsed);
                return true;
            }

            if (message.Class == StunClass.Request)
            {
                // 对端可能在同一连接上发起对向检查，先应答再继续等自己的应答
                TryAnswerRequest(message);
                return false;
            }

            if (message.Class != StunClass.SuccessResponse && message.Class != StunClass.ErrorResponse)
            {
                return false;
            }

            if (!IsSameTransaction(message, requestTransactionId))
            {
                // 事务 ID 不一致的帧属于别的请求，忽略
                return false;
            }

            if (!StunIntegrity.TryVerify(message, StunIntegrity.CreateShortTermKey(remotePassword)))
            {
                CloseTransport();
                result = TcpDirectResult.Fail(TcpDirectFailure.IntegrityFailed, "应答的 MESSAGE-INTEGRITY 校验失败", remoteEndPoint, stopwatch.Elapsed);
                return true;
            }

            if (message.Class == StunClass.ErrorResponse)
            {
                int errorCode;
                string errorReason;
                if (!StunErrorCode.TryGet(message, out errorCode, out errorReason))
                {
                    errorCode = 0;
                    errorReason = "错误应答缺少 ERROR-CODE";
                }

                CloseTransport();
                result = TcpDirectResult.Fail(TcpDirectFailure.Rejected, errorCode, errorReason, remoteEndPoint, stopwatch.Elapsed);
                return true;
            }

            IPEndPoint decoded;
            if (!TryDecodeMappedAddress(message, out decoded))
            {
                CloseTransport();
                result = TcpDirectResult.Fail(TcpDirectFailure.TransportError, "成功应答缺少映射地址", remoteEndPoint, stopwatch.Elapsed);
                return true;
            }

            mappedAddress = decoded;
            return true;
        }

        /// <summary>
        /// 处理被动侧检查阶段的一帧：只应答入向检查请求，其他帧忽略
        /// </summary>
        /// <param name="frame">一帧载荷</param>
        /// <param name="stopwatch">计时器</param>
        /// <param name="result">已得出结果时非 null，为 null 表示检查通过</param>
        /// <returns>是否已经得出结果</returns>
        private bool TryCompletePassiveCheck(ReadOnlyMemory<byte> frame, Stopwatch stopwatch, out TcpDirectResult result)
        {
            result = null;

            var payload = frame.Span;
            StunMessage message;
            StunParseError parseError;

            if (!StunMessage.TryParse(payload, out message, out parseError))
            {
                P2PLogHelper.Debug(LogNode, "入向连接的首帧不是 STUN 消息：" + parseError);
                CloseTransport();
                result = TcpDirectResult.Fail(TcpDirectFailure.NonStunFrame, "首个响应帧不是 STUN 消息", null, stopwatch.Elapsed);
                return true;
            }

            if (message.Class != StunClass.Request)
            {
                // 被动侧只应答入向检查，不主动发检查也不提名
                return false;
            }

            // 请求由对端用「远端口令」签发，也就是本端口令
            if (!StunIntegrity.TryVerify(message, StunIntegrity.CreateShortTermKey(_localPassword)))
            {
                CloseTransport();
                result = TcpDirectResult.Fail(TcpDirectFailure.IntegrityFailed, "检查请求的 MESSAGE-INTEGRITY 校验失败", null, stopwatch.Elapsed);
                return true;
            }

            if (!WriteBindingResponse(message))
            {
                CloseTransport();
                result = TcpDirectResult.Fail(TcpDirectFailure.TransportError, "回送检查应答失败", null, stopwatch.Elapsed);
                return true;
            }

            return true;
        }

        /// <summary>
        /// 校验并应答一个入向检查请求
        /// </summary>
        /// <param name="request">入向检查请求</param>
        /// <returns>校验通过且应答写出时返回 true</returns>
        private bool TryAnswerRequest(StunMessage request)
        {
            if (!StunIntegrity.TryVerify(request, StunIntegrity.CreateShortTermKey(_localPassword)))
            {
                // 校验不通过说明不是当前会话发来的检查，不回包也不破坏已有连接
                P2PLogHelper.Debug(LogNode, "检查请求的 MESSAGE-INTEGRITY 校验失败，已忽略");
                return false;
            }

            return WriteBindingResponse(request);
        }

        /// <summary>
        /// 回一个成功应答，属性只放 XOR-MAPPED-ADDRESS，地址取本端端点
        /// </summary>
        /// <param name="request">对应的检查请求，用于取事务 ID</param>
        /// <returns>应答写出时返回 true</returns>
        private bool WriteBindingResponse(StunMessage request)
        {
            var localEndPoint = _localEndPoint;
            var stream = GetStream();
            if (localEndPoint == null || stream == null)
            {
                return false;
            }

            var transactionId = request.TransactionId.ToArray();
            var response = new StunMessage(StunMessageType.Binding, StunClass.SuccessResponse, transactionId);

            var attributes = new StunAttribute[]
            {
                StunAddressCodec.EncodeAddress(StunAttributeType.XorMappedAddress, localEndPoint, true, transactionId)
            };
            response.SetAttributes(attributes);

            // 应答用本端口令签，对端按它看到的「远端口令」也就是本端口令校验
            StunIntegrity.Append(response, StunIntegrity.CreateShortTermKey(_localPassword));

            try
            {
                WriteFrameLocked(stream, Rfc4571Framer.Encode(response.ToArray()));
                return true;
            }
            catch (Exception ex)
            {
                P2PLogHelper.Debug(LogNode, "回送检查应答失败：" + ex.GetType().Name);
                return false;
            }
        }

        /// <summary>
        /// 后台读循环：从流上读字节、分帧、解复用成业务帧或 STUN 帧，直到连接结束或被取消
        /// </summary>
        /// <param name="token">取消令牌</param>
        /// <returns>循环结束的任务，不向外抛异常</returns>
        private async Task ReadLoopAsync(CancellationToken token)
        {
            try
            {
                var stream = GetStream();
                if (stream == null)
                {
                    return;
                }

                while (!token.IsCancellationRequested)
                {
                    var attempt = await ReadAttemptAsync(stream, token).ConfigureAwait(false);
                    if (attempt.Error != null)
                    {
                        if (!token.IsCancellationRequested)
                        {
                            P2PLogHelper.Debug(LogNode, "数据面读取结束：" + attempt.Error.GetType().Name);
                        }

                        return;
                    }

                    if (attempt.Count == 0)
                    {
                        return;
                    }

                    ReadOnlyMemory<byte> frame;
                    bool hasFrame;
                    try
                    {
                        hasFrame = AppendAndTakeFrame(attempt.Count, out frame);
                    }
                    catch (InvalidDataException ex)
                    {
                        P2PLogHelper.Debug(LogNode, "数据面帧长异常：" + ex.Message);
                        return;
                    }

                    while (hasFrame)
                    {
                        DispatchFrame(frame);
                        hasFrame = _framer.TryReadFrame(out frame);
                    }
                }
            }
            catch (Exception ex)
            {
                P2PLogHelper.Debug(LogNode, "数据面读循环异常结束：" + ex.GetType().Name);
            }
            finally
            {
                _connected = false;
            }
        }

        /// <summary>
        /// 处理数据面的一帧：业务帧复制后投递，STUN 请求就地应答，其他忽略
        /// </summary>
        /// <param name="frame">一帧载荷</param>
        private void DispatchFrame(ReadOnlyMemory<byte> frame)
        {
            var payload = frame.Span;
            ReadOnlySpan<byte> body;

            if (DataPlaneCodec.TryUnwrap(payload, out body))
            {
                // 帧切片只在下次分帧调用前有效，订阅者可能跨回调保存，必须先复制
                var copy = body.ToArray();
                Interlocked.Increment(ref _receivedFrameCount);

                var handler = FrameReceived;
                if (handler != null)
                {
                    try
                    {
                        handler(new ReadOnlyMemory<byte>(copy));
                    }
                    catch
                    {
                        // 订阅者异常不能打断读循环，否则后续帧全部丢失
                    }
                }

                return;
            }

            if (DataPlaneCodec.Classify(payload) != DataPlaneKind.Stun)
            {
                return;
            }

            StunMessage message;
            StunParseError parseError;
            if (!StunMessage.TryParse(payload, out message, out parseError))
            {
                return;
            }

            if (message.Class == StunClass.Request)
            {
                // 数据面期间对端仍可能发来检查，应答后连接保持可用
                TryAnswerRequest(message);
            }
        }

        /// <summary>
        /// 检查通过后启动后台读循环并给出成功结果
        /// </summary>
        /// <param name="mappedAddress">检查阶段拿到的映射地址，可为 null</param>
        /// <param name="stopwatch">计时器</param>
        /// <returns>检查结果</returns>
        private TcpDirectResult CompleteCheck(IPEndPoint mappedAddress, Stopwatch stopwatch)
        {
            TcpDirectResult result;

            lock (_syncRoot)
            {
                if (_disposed)
                {
                    // 释放与检查完成撞在一起时不再启动读循环
                    result = TcpDirectResult.Fail(TcpDirectFailure.TransportError, "通道已释放", _remoteEndPoint, stopwatch.Elapsed);
                }
                else
                {
                    _connected = true;

                    var loopCts = new CancellationTokenSource();
                    _loopCts = loopCts;
                    _readLoopTask = Task.Run(() => ReadLoopAsync(loopCts.Token));

                    result = TcpDirectResult.Success(mappedAddress, _remoteEndPoint, stopwatch.Elapsed);
                }
            }

            if (!result.Succeeded)
            {
                CloseTransport();
            }

            return result;
        }

        /// <summary>
        /// 把超时或取消映射成结果；此时连接已由取消回调关闭
        /// </summary>
        /// <param name="cancellationToken">调用方取消令牌</param>
        /// <param name="remoteEndPoint">远端端点，可为 null</param>
        /// <param name="stopwatch">计时器</param>
        /// <returns>Timeout 或 Cancelled 结果</returns>
        private TcpDirectResult BuildCheckStopResult(CancellationToken cancellationToken, IPEndPoint remoteEndPoint, Stopwatch stopwatch)
        {
            CloseTransport();

            if (cancellationToken.IsCancellationRequested)
            {
                P2PLogHelper.Debug(LogNode, "检查已被调用方取消");
                return TcpDirectResult.Fail(TcpDirectFailure.Cancelled, "检查已被调用方取消", remoteEndPoint, stopwatch.Elapsed);
            }

            P2PLogHelper.Debug(LogNode, "检查超时：" + _checkTimeoutMilliseconds + " 毫秒");
            return TcpDirectResult.Fail(TcpDirectFailure.Timeout, "等待检查应答超时", remoteEndPoint, stopwatch.Elapsed);
        }

        /// <summary>
        /// 按已失败次数计算退避毫秒数，每次翻倍且不超过 int 上限
        /// </summary>
        /// <param name="attempt">已失败的尝试序号，从 1 开始</param>
        /// <returns>本次退避毫秒数</returns>
        private int ComputeRetryDelay(int attempt)
        {
            if (_retryDelayMilliseconds <= 0)
            {
                return 0;
            }

            var shift = attempt - 1 > 30 ? 30 : attempt - 1;
            var delay = (long)_retryDelayMilliseconds << shift;
            return delay > int.MaxValue ? int.MaxValue : (int)delay;
        }

        /// <summary>
        /// 在两次建链尝试之间按退避等待，最后一次失败后不再等待
        /// </summary>
        /// <param name="attempt">已失败的尝试序号</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>等待任务</returns>
        /// <exception cref="OperationCanceledException">等待期间被调用方取消</exception>
        private async Task DelayBeforeRetryAsync(int attempt, CancellationToken cancellationToken)
        {
            if (attempt >= _connectAttempts)
            {
                return;
            }

            var delay = ComputeRetryDelay(attempt);
            if (delay <= 0)
            {
                return;
            }

            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 从流上读一次，异常转成返回值，供调用方按令牌状态区分超时与传输错误
        /// </summary>
        /// <param name="stream">字节流</param>
        /// <param name="token">取消令牌</param>
        /// <returns>本次读的结果</returns>
        private async Task<ReadAttempt> ReadAttemptAsync(Stream stream, CancellationToken token)
        {
            try
            {
                var count = await stream.ReadAsync(_readBuffer, 0, _readBuffer.Length, token).ConfigureAwait(false);
                return new ReadAttempt { Count = count };
            }
            catch (Exception ex)
            {
                return new ReadAttempt { Count = -1, Error = ex };
            }
        }

        /// <summary>
        /// 追加本轮读到的字节并取出一帧；只用同步方法碰 Span，避免 async 方法里出现 Span 局部
        /// </summary>
        /// <param name="count">本轮读到的字节数</param>
        /// <param name="frame">取出的帧，切片只在下次分帧调用前有效</param>
        /// <returns>取到完整帧时返回 true</returns>
        /// <exception cref="InvalidDataException">前缀声明的帧长超过上限时抛出</exception>
        private bool AppendAndTakeFrame(int count, out ReadOnlyMemory<byte> frame)
        {
            _framer.Append(new ReadOnlySpan<byte>(_readBuffer, 0, count));
            return _framer.TryReadFrame(out frame);
        }

        /// <summary>
        /// 锁内写完一帧：业务发送与读循环回包会并发写同一条流，交错写会破坏帧边界
        /// </summary>
        /// <param name="stream">字节流</param>
        /// <param name="frame">已编码好的帧</param>
        private void WriteFrameLocked(Stream stream, byte[] frame)
        {
            lock (_syncRoot)
            {
                stream.Write(frame, 0, frame.Length);
                stream.Flush();
            }
        }

        /// <summary>
        /// 判断应答事务 ID 是否与请求一致
        /// </summary>
        /// <param name="message">应答消息</param>
        /// <param name="requestTransactionId">请求事务 ID</param>
        /// <returns>一致时返回 true</returns>
        private static bool IsSameTransaction(StunMessage message, byte[] requestTransactionId)
        {
            return message.TransactionId.Span.SequenceEqual(new ReadOnlySpan<byte>(requestTransactionId));
        }

        /// <summary>
        /// 取应答里的映射地址，优先 XOR-MAPPED-ADDRESS，其次 MAPPED-ADDRESS
        /// </summary>
        /// <param name="message">成功应答</param>
        /// <param name="mappedAddress">解码出的端点</param>
        /// <returns>两种属性都没有或都解码失败时返回 false</returns>
        private static bool TryDecodeMappedAddress(StunMessage message, out IPEndPoint mappedAddress)
        {
            var transactionId = message.TransactionId.Span;
            StunAttribute attribute;

            if (message.TryGet(StunAttributeType.XorMappedAddress, out attribute)
                && StunAddressCodec.TryDecodeAddress(attribute, transactionId, out mappedAddress))
            {
                return true;
            }

            if (message.TryGet(StunAttributeType.MappedAddress, out attribute)
                && StunAddressCodec.TryDecodeAddress(attribute, transactionId, out mappedAddress))
            {
                return true;
            }

            mappedAddress = null;
            return false;
        }

        /// <summary>
        /// 取当前字节流，未连接时为 null
        /// </summary>
        /// <returns>字节流或 null</returns>
        private Stream GetStream()
        {
            var transport = _transport;
            return transport == null ? null : transport.Stream;
        }

        /// <summary>
        /// 关闭并丢弃底层传输，幂等；不取消后台循环，调用方按需自行取消
        /// </summary>
        private void CloseTransport()
        {
            _connected = false;

            ITcpTransport transport;
            lock (_syncRoot)
            {
                transport = _transport;
                _transport = null;
            }

            if (transport != null)
            {
                try { transport.Dispose(); } catch { }
            }
        }

        /// <summary>
        /// 已释放时拒绝新的操作
        /// </summary>
        /// <exception cref="ObjectDisposedException">通道已释放时抛出</exception>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        /// <summary>
        /// 一次读的结果：字节数或异常，用于把读取失败与超时分别映射成结果
        /// </summary>
        private struct ReadAttempt
        {
            /// <summary>
            /// 读到的字节数，0 表示对端已关闭
            /// </summary>
            public int Count;

            /// <summary>
            /// 读取异常，成功时为 null
            /// </summary>
            public Exception Error;
        }
    }
}