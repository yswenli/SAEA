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
*命名空间：SAEA.P2P.Stun
*文件名： StunClient
*版本号： v26.11.5.1
*唯一标识：6057ec89-fac7-48b1-999e-d812e883319a
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 17:30:00
*描述：STUN 客户端——Binding 请求、凭据重试与地址解析
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 16:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增首个请求即带 MESSAGE-INTEGRITY 并可校验响应完整性的内部重载，供 ICE 连通性检查使用
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Common;
using SAEA.P2P.Transport;

namespace SAEA.P2P.Stun
{
    /// <summary>
    /// STUN 客户端：在调用方给定的传输上发出 Binding 请求，解析映射地址与响应来源。
    /// UDP 走事务层重传；TCP 按 RFC 4571 的 2 字节长度分帧且不做 STUN 层重传。
    /// 本类不拥有传输，释放本类不会关闭传输；同一实例在 TCP 上同一时刻只应有一个未决请求。
    /// </summary>
    public sealed class StunClient : IDisposable
    {
        /// <summary>
        /// TCP 分帧长度字段占用的字节数
        /// </summary>
        private const int TcpLengthFieldLength = 2;

        /// <summary>
        /// UDP 传输；TCP 模式下为 null
        /// </summary>
        private readonly IUdpTransport _udp;

        /// <summary>
        /// TCP 传输；UDP 模式下为 null
        /// </summary>
        private readonly ITcpTransport _tcp;

        /// <summary>
        /// 计时时钟，决定重传与超时
        /// </summary>
        private readonly IRetryClock _clock;

        /// <summary>
        /// 事务层，负责重传、匹配应答与超时
        /// </summary>
        private readonly StunTransactionLayer _transactions;

        /// <summary>
        /// SOFTWARE 属性值；为 null 时不携带
        /// </summary>
        private readonly string _software;

        /// <summary>
        /// 终止 TCP 读取循环的令牌源
        /// </summary>
        private readonly CancellationTokenSource _tcpCancellation = new CancellationTokenSource();

        /// <summary>
        /// 初始重传间隔，单位毫秒
        /// </summary>
        private int _rtoMilliseconds = StunTransactionTiming.RtoMilliseconds;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 创建 UDP 模式客户端
        /// </summary>
        /// <param name="udpTransport">已绑定的 UDP 传输；本类只订阅其接收事件，不负责释放</param>
        /// <param name="clock">计时时钟；为 null 时使用系统时钟</param>
        /// <param name="software">SOFTWARE 属性值；为 null 时不携带</param>
        public StunClient(IUdpTransport udpTransport, IRetryClock clock = null, string software = null)
        {
            if (udpTransport == null)
            {
                throw new ArgumentNullException(nameof(udpTransport));
            }

            _udp = udpTransport;
            _clock = clock ?? new SystemRetryClock();
            _software = software;
            _transactions = new StunTransactionLayer(_clock, TimeSpan.FromMilliseconds(50));
            _udp.DatagramReceived += OnDatagramReceived;
        }

        /// <summary>
        /// 创建 TCP 模式客户端；传输必须已完成连接
        /// </summary>
        /// <param name="tcpTransport">已连接的 TCP 传输；本类只读其字节流，不负责释放</param>
        /// <param name="clock">计时时钟；为 null 时使用系统时钟</param>
        /// <param name="software">SOFTWARE 属性值；为 null 时不携带</param>
        public StunClient(ITcpTransport tcpTransport, IRetryClock clock = null, string software = null)
        {
            if (tcpTransport == null)
            {
                throw new ArgumentNullException(nameof(tcpTransport));
            }
            if (!tcpTransport.IsConnected)
            {
                throw new ArgumentException("TCP 传输尚未连接", nameof(tcpTransport));
            }

            _tcp = tcpTransport;
            _clock = clock ?? new SystemRetryClock();
            _software = software;
            _transactions = new StunTransactionLayer(_clock, TimeSpan.FromMilliseconds(50));
            _ = ReadTcpLoopAsync(_tcpCancellation.Token);
        }

        /// <summary>
        /// 初始重传间隔，单位毫秒；ICE 检查期间按公式覆盖，默认 500ms
        /// </summary>
        public int RtoMilliseconds
        {
            get { return _rtoMilliseconds; }
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                _rtoMilliseconds = value;
            }
        }

        /// <summary>
        /// 发出 Binding 请求；不需要鉴权
        /// </summary>
        /// <param name="server">STUN 服务端端点</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>Binding 结果，失败时带原因与错误码</returns>
        public Task<StunBindingResult> BindingAsync(IPEndPoint server, CancellationToken cancellationToken = default(CancellationToken))
        {
            return BindingAsync(server, null, null, cancellationToken);
        }

        /// <summary>
        /// 发出 Binding 请求；服务端回 401 或 438 时用挑战里的认证域与 nonce 重试一次
        /// </summary>
        /// <param name="server">STUN 服务端端点</param>
        /// <param name="credential">凭据；为 null 时不重试鉴权</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>Binding 结果，失败时带原因与错误码</returns>
        public Task<StunBindingResult> BindingAsync(IPEndPoint server, StunClientCredential credential, CancellationToken cancellationToken = default(CancellationToken))
        {
            return BindingAsync(server, credential, null, cancellationToken);
        }

        /// <summary>
        /// 发出 Binding 请求，并在请求里附带额外属性（如 CHANGE-REQUEST、ICE 的 PRIORITY）；
        /// 额外属性会插在 SOFTWARE 与鉴权属性之前
        /// </summary>
        /// <param name="server">STUN 服务端端点</param>
        /// <param name="credential">凭据；为 null 时不重试鉴权</param>
        /// <param name="additionalAttributes">额外属性；为 null 时与公开重载等价</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>Binding 结果，失败时带原因与错误码</returns>
        internal async Task<StunBindingResult> BindingAsync(IPEndPoint server, StunClientCredential credential, IReadOnlyList<StunAttribute> additionalAttributes, CancellationToken cancellationToken)
        {
            if (server == null)
            {
                throw new ArgumentNullException(nameof(server));
            }

            ThrowIfDisposed();

            var startedAt = _clock.UtcNow;
            var attempts = 0;

            var first = await RunTransactionAsync(CreateRequest(null, null, null, null, additionalAttributes), server, cancellationToken).ConfigureAwait(false);
            attempts += first.Attempts;

            if (first.Response != null && first.Response.Class == StunClass.SuccessResponse)
            {
                return ParseSuccess(first, attempts, startedAt);
            }

            if (first.Response != null && first.Response.Class == StunClass.ErrorResponse)
            {
                int code;
                string reason;
                StunErrorCode.TryGet(first.Response, out code, out reason);

                var requestedRealm = ReadString(first.Response, StunAttributeType.Realm);
                var nonce = ReadString(first.Response, StunAttributeType.Nonce);

                if ((code == 401 || code == 438) && credential != null && !string.IsNullOrEmpty(nonce))
                {
                    // 认证域以服务端挑战为准，未给认证域时只能按短凭据处理
                    var realm = !string.IsNullOrEmpty(requestedRealm) ? requestedRealm : credential.Realm;
                    var key = string.IsNullOrEmpty(realm)
                        ? StunIntegrity.CreateShortTermKey(credential.Password)
                        : StunIntegrity.CreateLongTermKey(credential.Username, realm, credential.Password);

                    var retry = await RunTransactionAsync(
                        CreateRequest(credential.Username, realm, nonce, key, additionalAttributes),
                        server,
                        cancellationToken).ConfigureAwait(false);
                    attempts += retry.Attempts;

                    if (retry.Response != null && retry.Response.Class == StunClass.SuccessResponse)
                    {
                        return ParseSuccess(retry, attempts, startedAt);
                    }

                    if (retry.Response != null && retry.Response.Class == StunClass.ErrorResponse)
                    {
                        int retryCode;
                        string retryReason;
                        StunErrorCode.TryGet(retry.Response, out retryCode, out retryReason);
                        return CreateErrorResult(retry, retryCode, retryReason, attempts, startedAt);
                    }

                    return CreateTransactionFailure(retry, attempts, startedAt);
                }

                return CreateErrorResult(first, code, reason, attempts, startedAt);
            }

            return CreateTransactionFailure(first, attempts, startedAt);
        }

        /// <summary>
        /// 发出首个请求就携带 MESSAGE-INTEGRITY 的 Binding 请求，并按需校验响应完整性；
        /// 不做 401/438 挑战重试，也不读取挑战里的认证域与 nonce
        /// </summary>
        /// <param name="server">STUN 服务端端点</param>
        /// <param name="username">USERNAME 属性值；为 null 时不携带</param>
        /// <param name="integrityKey">完整性密钥；短凭据直接传口令的 UTF-8 字节</param>
        /// <param name="verifyResponseIntegrity">是否用同一密钥校验响应完整性</param>
        /// <param name="additionalAttributes">额外属性；为 null 时不追加</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>Binding 结果，完整性校验失败时带协议错误</returns>
        internal async Task<StunBindingResult> BindingAsync(IPEndPoint server, string username, byte[] integrityKey, bool verifyResponseIntegrity, IReadOnlyList<StunAttribute> additionalAttributes, CancellationToken cancellationToken)
        {
            if (server == null)
            {
                throw new ArgumentNullException(nameof(server));
            }
            if (integrityKey == null || integrityKey.Length == 0)
            {
                throw new ArgumentException("完整性密钥不能为空", nameof(integrityKey));
            }

            ThrowIfDisposed();

            var startedAt = _clock.UtcNow;

            // ICE 没有挑战流程，首个请求必须自带完整性，因此只发一次事务且不做鉴权重试
            var result = await RunTransactionAsync(
                CreateRequest(username, null, null, integrityKey, additionalAttributes),
                server,
                cancellationToken).ConfigureAwait(false);

            if (result.Response != null && result.Response.Class == StunClass.SuccessResponse)
            {
                var bindingResult = ParseSuccess(result, result.Attempts, startedAt);

                if (verifyResponseIntegrity)
                {
                    // 未经完整性校验的应答可能是伪造或错配的，按协议错误上报而不是当作成功
                    if (!StunIntegrity.TryVerify(result.Response, integrityKey))
                    {
                        return StunBindingResult.Failed(StunBindingFailure.ProtocolError, 0, "MESSAGE-INTEGRITY 校验失败", _clock.UtcNow - startedAt, result.Attempts);
                    }

                    bindingResult.MarkIntegrityVerified();
                }

                return bindingResult;
            }

            if (result.Response != null && result.Response.Class == StunClass.ErrorResponse)
            {
                int code;
                string reason;
                StunErrorCode.TryGet(result.Response, out code, out reason);
                return CreateErrorResult(result, code, reason, result.Attempts, startedAt);
            }

            return CreateTransactionFailure(result, result.Attempts, startedAt);
        }

        /// <summary>
        /// 释放客户端：退订接收事件、停止 TCP 读取循环并结束未决事务；不释放调用方的传输
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            if (_udp != null)
            {
                _udp.DatagramReceived -= OnDatagramReceived;
            }

            try
            {
                _tcpCancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // 令牌源已释放时取消为空操作
            }

            _transactions.Dispose();
            _tcpCancellation.Dispose();
        }

        /// <summary>
        /// 创建 Binding 请求；提供完整性密钥时追加 MESSAGE-INTEGRITY
        /// </summary>
        /// <param name="username">用户名；为 null 时不带鉴权属性</param>
        /// <param name="realm">认证域</param>
        /// <param name="nonce">一次性随机串；为 null 时不携带，短凭据路径没有挑战</param>
        /// <param name="integrityKey">完整性密钥；为 null 时不计算 MIC</param>
        /// <param name="additionalAttributes">额外属性；为 null 时不追加</param>
        /// <returns>待发送的请求</returns>
        private StunMessage CreateRequest(string username, string realm, string nonce, byte[] integrityKey, IReadOnlyList<StunAttribute> additionalAttributes)
        {
            var request = new StunMessage(StunMessageType.Binding, StunClass.Request);
            var attributes = new List<StunAttribute>();

            if (additionalAttributes != null)
            {
                for (var i = 0; i < additionalAttributes.Count; i++)
                {
                    if (additionalAttributes[i] != null)
                    {
                        attributes.Add(additionalAttributes[i]);
                    }
                }
            }

            if (!string.IsNullOrEmpty(_software))
            {
                attributes.Add(new StunAttribute(StunAttributeType.Software, Encoding.UTF8.GetBytes(_software)));
            }

            if (username != null)
            {
                attributes.Add(new StunAttribute(StunAttributeType.Username, Encoding.UTF8.GetBytes(username)));
                if (!string.IsNullOrEmpty(realm))
                {
                    attributes.Add(new StunAttribute(StunAttributeType.Realm, Encoding.UTF8.GetBytes(realm)));
                }
                // 短凭据路径没有挑战，也就没有 NONCE 可带
                if (!string.IsNullOrEmpty(nonce))
                {
                    attributes.Add(new StunAttribute(StunAttributeType.Nonce, Encoding.UTF8.GetBytes(nonce)));
                }
            }

            request.SetAttributes(attributes);

            if (integrityKey != null)
            {
                StunIntegrity.Append(request, integrityKey);
            }

            return request;
        }

        /// <summary>
        /// 注册并等待一个请求事务；发送动作与传输绑定
        /// </summary>
        /// <param name="request">请求消息</param>
        /// <param name="server">服务端端点</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>事务结果</returns>
        private async Task<StunTransactionResult> RunTransactionAsync(StunMessage request, IPEndPoint server, CancellationToken cancellationToken)
        {
            var reliable = _tcp != null;
            StunTransaction transaction = null;

            // 首发在 TryBegin 内部同步发生，此时局部变量还未赋值；异步发送失败通过 ContinueWith 回填
            Func<StunTransaction> current = () => transaction;
            Action<byte[]> send = bytes => Send(reliable, server, bytes, current);

            StunTransaction created;
            if (!_transactions.TryBegin(request, reliable, send, out created, _rtoMilliseconds, cancellationToken))
            {
                return StunTransactionResult.Failed(StunTransactionFailure.TransportError, 0);
            }

            transaction = created;
            return await transaction.Completion.ConfigureAwait(false);
        }

        /// <summary>
        /// 发送一次请求字节；发送失败由事务标记为传输错误
        /// </summary>
        /// <param name="reliable">是否可靠传输</param>
        /// <param name="server">服务端端点，UDP 模式使用</param>
        /// <param name="bytes">已编码的 STUN 报文</param>
        /// <param name="current">取当前事务的委托，发送发起时事务可能尚未赋值</param>
        private void Send(bool reliable, IPEndPoint server, byte[] bytes, Func<StunTransaction> current)
        {
            if (reliable)
            {
                var framed = new byte[TcpLengthFieldLength + bytes.Length];
                framed[0] = (byte)(bytes.Length >> 8);
                framed[1] = (byte)bytes.Length;
                Buffer.BlockCopy(bytes, 0, framed, TcpLengthFieldLength, bytes.Length);
                ObserveSendFailure(_tcp.Stream.WriteAsync(framed, 0, framed.Length, _tcpCancellation.Token), current);
                return;
            }

            ObserveSendFailure(_udp.SendToAsync(server, new ReadOnlyMemory<byte>(bytes), CancellationToken.None), current);
        }

        /// <summary>
        /// 观察异步发送任务；只有失败才结束事务，避免影响正常重传节奏
        /// </summary>
        /// <param name="task">发送任务</param>
        /// <param name="current">取当前事务的委托</param>
        private static void ObserveSendFailure(Task task, Func<StunTransaction> current)
        {
            if (task.IsCompleted)
            {
                if (task.IsFaulted)
                {
                    var ignored = task.Exception;
                    var transaction = current();
                    if (transaction != null)
                    {
                        transaction.Fail(StunTransactionFailure.TransportError);
                    }
                }
                return;
            }

            task.ContinueWith(
                t =>
                {
                    var ignored = t.Exception;
                    var transaction = current();
                    if (transaction != null)
                    {
                        transaction.Fail(StunTransactionFailure.TransportError);
                    }
                },
                TaskContinuationOptions.OnlyOnFaulted);
        }

        /// <summary>
        /// 解析成功应答；缺少映射地址时按协议错误处理
        /// </summary>
        /// <param name="result">事务结果</param>
        /// <param name="attempts">累计发送次数</param>
        /// <param name="startedAt">起始时刻</param>
        /// <returns>Binding 结果</returns>
        private StunBindingResult ParseSuccess(StunTransactionResult result, int attempts, DateTime startedAt)
        {
            var response = result.Response;
            if (response.Method != StunMessageType.Binding)
            {
                return StunBindingResult.Failed(StunBindingFailure.ProtocolError, 0, "应答方法不是 Binding", _clock.UtcNow - startedAt, attempts);
            }

            var transactionId = response.TransactionId.Span;
            var xorMapped = DecodeAddress(response, StunAttributeType.XorMappedAddress, transactionId);
            var mapped = DecodeAddress(response, StunAttributeType.MappedAddress, transactionId);
            var responseOrigin = DecodeAddress(response, StunAttributeType.ResponseOrigin, transactionId);
            var otherAddress = DecodeAddress(response, StunAttributeType.OtherAddress, transactionId);

            if (xorMapped == null && mapped == null)
            {
                return StunBindingResult.Failed(StunBindingFailure.ProtocolError, 0, "成功应答缺少映射地址", _clock.UtcNow - startedAt, attempts);
            }

            return StunBindingResult.Success(xorMapped, mapped, responseOrigin, otherAddress, _clock.UtcNow - startedAt, attempts);
        }

        /// <summary>
        /// 按错误应答构造失败结果；401/438 标记为鉴权失败，5xx 标记为服务端错误
        /// </summary>
        /// <param name="result">事务结果</param>
        /// <param name="errorCode">STUN 错误码</param>
        /// <param name="errorReason">错误原因短语</param>
        /// <param name="attempts">累计发送次数</param>
        /// <param name="startedAt">起始时刻</param>
        /// <returns>Binding 结果</returns>
        private StunBindingResult CreateErrorResult(StunTransactionResult result, int errorCode, string errorReason, int attempts, DateTime startedAt)
        {
            StunBindingFailure failure;
            if (errorCode == 401 || errorCode == 438)
            {
                failure = StunBindingFailure.Unauthorized;
            }
            else if (errorCode >= 500 && errorCode <= 599)
            {
                failure = StunBindingFailure.ServerError;
            }
            else
            {
                failure = StunBindingFailure.ProtocolError;
            }

            var bindingResult = StunBindingResult.Failed(failure, errorCode, errorReason, _clock.UtcNow - startedAt, attempts);
            bindingResult.SetChallenge(
                ReadString(result.Response, StunAttributeType.Realm),
                ReadString(result.Response, StunAttributeType.Nonce));
            return bindingResult;
        }

        /// <summary>
        /// 把事务层失败映射为 Binding 失败
        /// </summary>
        /// <param name="result">事务结果</param>
        /// <param name="attempts">累计发送次数</param>
        /// <param name="startedAt">起始时刻</param>
        /// <returns>Binding 结果</returns>
        private StunBindingResult CreateTransactionFailure(StunTransactionResult result, int attempts, DateTime startedAt)
        {
            StunBindingFailure failure;
            switch (result.Failure)
            {
                case StunTransactionFailure.Timeout:
                    failure = StunBindingFailure.Timeout;
                    break;
                case StunTransactionFailure.Cancelled:
                    failure = StunBindingFailure.Cancelled;
                    break;
                default:
                    failure = StunBindingFailure.TransportError;
                    break;
            }

            return StunBindingResult.Failed(failure, 0, null, _clock.UtcNow - startedAt, attempts);
        }

        /// <summary>
        /// 解码地址属性；缺失或格式错误时返回 null
        /// </summary>
        /// <param name="message">应答消息</param>
        /// <param name="type">属性类型</param>
        /// <param name="transactionId">事务 ID，异或类属性必须提供</param>
        /// <returns>解码出的端点</returns>
        private static IPEndPoint DecodeAddress(StunMessage message, StunAttributeType type, ReadOnlySpan<byte> transactionId)
        {
            StunAttribute attribute;
            if (!message.TryGet(type, out attribute))
            {
                return null;
            }

            IPEndPoint endPoint;
            return StunAddressCodec.TryDecodeAddress(attribute, transactionId, out endPoint) ? endPoint : null;
        }

        /// <summary>
        /// 读取字符串属性；缺失时返回 null
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="type">属性类型</param>
        /// <returns>UTF-8 字符串</returns>
        private static string ReadString(StunMessage message, StunAttributeType type)
        {
            StunAttribute attribute;
            if (!message.TryGet(type, out attribute))
            {
                return null;
            }

            return Encoding.UTF8.GetString(attribute.Value.ToArray());
        }

        /// <summary>
        /// UDP 数据报入口：只处理应答类消息，其余交给事务层按事务 ID 匹配
        /// </summary>
        /// <param name="remoteEndPoint">来源端点</param>
        /// <param name="data">数据报内容</param>
        private void OnDatagramReceived(IPEndPoint remoteEndPoint, ReadOnlyMemory<byte> data)
        {
            StunMessage message;
            StunParseError error;
            if (!StunMessage.TryParse(data.Span, out message, out error))
            {
                return;
            }

            if (message.Class != StunClass.SuccessResponse && message.Class != StunClass.ErrorResponse)
            {
                return;
            }

            _transactions.HandleResponse(message);
        }

        /// <summary>
        /// TCP 读取循环：按 2 字节长度分帧，读满一帧后交给事务层
        /// </summary>
        /// <param name="cancellationToken">终止令牌</param>
        /// <returns>读取循环结束的任务</returns>
        private async Task ReadTcpLoopAsync(CancellationToken cancellationToken)
        {
            var lengthBuffer = new byte[TcpLengthFieldLength];
            try
            {
                var stream = _tcp.Stream;
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (!await ReadExactAsync(stream, lengthBuffer, TcpLengthFieldLength, cancellationToken).ConfigureAwait(false))
                    {
                        return;
                    }

                    var length = (lengthBuffer[0] << 8) | lengthBuffer[1];
                    if (length < StunConstants.HeaderLength)
                    {
                        // 帧长不足以容纳 STUN 头，字节流已不可信，停止读取避免无限错位
                        P2PLogHelper.Debug("StunClient", "TCP 帧长非法，停止读取");
                        return;
                    }

                    var payload = new byte[length];
                    if (!await ReadExactAsync(stream, payload, length, cancellationToken).ConfigureAwait(false))
                    {
                        return;
                    }

                    StunMessage message;
                    StunParseError error;
                    if (!StunMessage.TryParse(payload, out message, out error))
                    {
                        continue;
                    }

                    if (message.Class == StunClass.SuccessResponse || message.Class == StunClass.ErrorResponse)
                    {
                        _transactions.HandleResponse(message);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    P2PLogHelper.Debug("StunClient", "TCP 读取循环结束：" + ex.Message);
                }
            }
        }

        /// <summary>
        /// 从流里读满指定字节数；提前结束返回 false
        /// </summary>
        /// <param name="stream">字节流</param>
        /// <param name="buffer">目标缓冲</param>
        /// <param name="count">需要读取的字节数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>读满返回 true</returns>
        private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, int count, CancellationToken cancellationToken)
        {
            var offset = 0;
            while (offset < count)
            {
                var read = await stream.ReadAsync(buffer, offset, count - offset, cancellationToken).ConfigureAwait(false);
                if (read <= 0)
                {
                    return false;
                }
                offset += read;
            }
            return true;
        }

        /// <summary>
        /// 已释放时拒绝新请求
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}