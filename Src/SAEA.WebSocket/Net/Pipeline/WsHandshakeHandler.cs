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
*命名空间：SAEA.WebSocket
*文件名： WsHandshakeHandler
*版本号： v26.11.5.1
*唯一标识：9f2efa00-046f-41ed-b017-f65f8e51c3d6
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 17:36:00
*描述：WebSocket 握手处理器
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 14:12:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：握手完成后到达的帧改为直接交下游，修复紧随握手的首帧被丢弃
*
*****************************************************************************/
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using SAEA.Sockets.Pipeline;

namespace SAEA.WebSocket
{
    /// <summary>
    /// WebSocket 握手处理器：服务端应答 101、客户端发起并校验握手，成功后把分帧器切成帧分帧器。
    /// <para>
    /// 握手块由 <see cref="WsHandshakeSplitter"/> 以 <c>\r\n\r\n</c> 切出并停切，本处理器在协议切换前
    /// 独占该块、不向下游派发；切换完成后同一路径上的后续帧由 <see cref="WsFrameDecoder"/> 解释。
    /// </para>
    /// <para>
    /// 服务端失败时按 431 / 400 回写后关闭连接；客户端失败时只向上抛出协议错误，不写任何字节。
    /// 实例只服务一条连接，不跨连接复用。
    /// </para>
    /// </summary>
    public sealed class WsHandshakeHandler : InboundHandlerAdapter
    {
        /// <summary>RFC 6455 规定的握手固定后缀。</summary>
        private const string HandshakeGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        private const string HttpVersion = "HTTP/1.1";

        private const string PlainTextContentType = "text/plain; charset=utf-8";

        private const string UserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4103.61 Safari/537.36";

        private readonly bool _isServer;
        private readonly SwitchableSplitter _barrier;
        private readonly IFrameSplitter _frameSplitter;
        private readonly Action<string> _onCompleted;
        private readonly WsHandshakeState _state;
        private readonly string _requestTarget;
        private readonly string _host;
        private readonly string _subProtocol;
        private readonly string _origin;
        private readonly string _clientKey;
        private readonly string _expectedAccept;

        private bool _completed;
        private bool _requestSent;

        /// <summary>
        /// 服务端方向的握手处理器。
        /// </summary>
        /// <param name="barrier">协议切换屏障，为 null 时只应答不切分帧器</param>
        /// <param name="frameSplitter">握手成功后生效的帧分帧器，为 null 时只应答不切分帧器</param>
        /// <param name="onCompleted">握手成功回调，参数为连接标识，可为 null</param>
        /// <param name="state">握手完成标记，可为 null</param>
        private WsHandshakeHandler(
            SwitchableSplitter barrier,
            IFrameSplitter frameSplitter,
            Action<string> onCompleted,
            WsHandshakeState state)
        {
            _isServer = true;
            _barrier = barrier;
            _frameSplitter = frameSplitter;
            _onCompleted = onCompleted;
            _state = state;
        }

        /// <summary>
        /// 客户端方向的握手处理器。
        /// </summary>
        /// <param name="barrier">协议切换屏障，为 null 时只校验不切分帧器</param>
        /// <param name="frameSplitter">握手成功后生效的帧分帧器，为 null 时只校验不切分帧器</param>
        /// <param name="requestTarget">握手请求行里的 origin-form 目标路径</param>
        /// <param name="host">握手请求的 Host 头</param>
        /// <param name="subProtocol">子协议，可为空</param>
        /// <param name="origin">请求来源，可为空</param>
        /// <param name="onCompleted">握手成功回调，参数为连接标识，可为 null</param>
        /// <param name="state">握手完成标记，可为 null</param>
        private WsHandshakeHandler(
            SwitchableSplitter barrier,
            IFrameSplitter frameSplitter,
            string requestTarget,
            string host,
            string subProtocol,
            string origin,
            Action<string> onCompleted,
            WsHandshakeState state)
        {
            _isServer = false;
            _barrier = barrier;
            _frameSplitter = frameSplitter;
            _requestTarget = string.IsNullOrEmpty(requestTarget) ? "/" : requestTarget;
            _host = host ?? string.Empty;
            _subProtocol = subProtocol;
            _origin = origin;
            _onCompleted = onCompleted;
            _state = state;

            _clientKey = CreateKey();
            _expectedAccept = ComputeAccept(_clientKey);
        }

        /// <summary>
        /// 创建服务端握手处理器。
        /// </summary>
        /// <param name="barrier">协议切换屏障，可为 null</param>
        /// <param name="frameSplitter">握手成功后生效的帧分帧器，可为 null</param>
        /// <param name="onCompleted">握手成功回调，可为 null</param>
        /// <param name="state">握手完成标记，可为 null</param>
        /// <returns>服务端方向的处理器实例</returns>
        public static WsHandshakeHandler CreateServer(
            SwitchableSplitter barrier,
            IFrameSplitter frameSplitter,
            Action<string> onCompleted,
            WsHandshakeState state = null)
        {
            return new WsHandshakeHandler(barrier, frameSplitter, onCompleted, state);
        }

        /// <summary>
        /// 创建客户端握手处理器；握手请求在连接激活时自动发出。
        /// </summary>
        /// <param name="barrier">协议切换屏障，可为 null</param>
        /// <param name="frameSplitter">握手成功后生效的帧分帧器，可为 null</param>
        /// <param name="requestTarget">握手请求行里的 origin-form 目标路径</param>
        /// <param name="host">握手请求的 Host 头</param>
        /// <param name="subProtocol">子协议，可为空</param>
        /// <param name="origin">请求来源，可为空</param>
        /// <param name="onCompleted">握手成功回调，可为 null</param>
        /// <param name="state">握手完成标记，可为 null</param>
        /// <returns>客户端方向的处理器实例</returns>
        public static WsHandshakeHandler CreateClient(
            SwitchableSplitter barrier,
            IFrameSplitter frameSplitter,
            string requestTarget,
            string host,
            string subProtocol,
            string origin,
            Action<string> onCompleted,
            WsHandshakeState state = null)
        {
            return new WsHandshakeHandler(
                barrier,
                frameSplitter,
                requestTarget,
                host,
                subProtocol,
                origin,
                onCompleted,
                state);
        }

        /// <summary>是否为服务端方向。</summary>
        public bool IsServer
        {
            get { return _isServer; }
        }

        /// <summary>握手是否已经成功完成。</summary>
        public bool Completed
        {
            get { return _completed; }
        }

        /// <summary>客户端方向期望收到的 <c>Sec-WebSocket-Accept</c>；服务端方向为空字符串。</summary>
        public string ExpectedAccept
        {
            get { return _expectedAccept ?? string.Empty; }
        }

        /// <summary>
        /// 客户端在此发出握手请求；服务端不做任何事，等对端首帧。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文</param>
        /// <returns>请求写出完成后结束的任务</returns>
        public override async Task ChannelActiveAsync(IChannelHandlerContext ctx)
        {
            if (!_isServer && !_requestSent)
            {
                _requestSent = true;

                var request = new RawFrame(Encoding.ASCII.GetBytes(BuildRequest()), null);
                try
                {
                    await ctx.Pipeline.WriteAsync(request).ConfigureAwait(false);
                }
                catch
                {
                    // 没交出去的那一份由本处理器归还；已移交时 Dispose 是 no-op。
                    request.Dispose();
                    throw;
                }
            }
        }

        /// <summary>
        /// 处理握手块：校验、应答（服务端）或校验（客户端），成功后切换分帧器，握手块本身不向下游派发。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文</param>
        /// <param name="msg">拆帧器产出的握手块，必须是 <see cref="PooledMessage"/></param>
        /// <returns>处理完成后结束的任务</returns>
        public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
        {
            var frame = msg as PooledMessage;
            if (frame == null)
            {
                // 不是握手块（例如独立装配的单测直接喂业务消息）：原样交下游。
                await ctx.FireChannelReadAsync(msg).ConfigureAwait(false);
                return;
            }

            // 握手完成后分帧器已切到帧模式，此后到达的每个 PooledMessage 都是 wire frame，必须原样交给解码器。
            if (_completed)
            {
                await ctx.FireChannelReadAsync(msg).ConfigureAwait(false);
                return;
            }

            var block = Encoding.UTF8.GetString(frame.Frame.ToArray());

            if (_isServer)
                await HandleServerAsync(ctx, block).ConfigureAwait(false);
            else
                HandleClient(ctx, block);
        }

        /// <summary>
        /// 握手阶段失败时服务端回写 431 / 400 再关闭；其它异常与客户端方向一律继续向下游传播。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文</param>
        /// <param name="ex">待处理的异常</param>
        /// <returns>回写与收口完成后结束的任务</returns>
        public override async Task ChannelExceptionCaughtAsync(IChannelHandlerContext ctx, Exception ex)
        {
            var protocolError = ex as FrameProtocolException;
            if (!_isServer || _completed || protocolError == null)
            {
                await ctx.FireExceptionCaughtAsync(ex).ConfigureAwait(false);
                return;
            }

            var status = protocolError.Message == WebSocketProtocolErrors.HandshakeHeaderTooLarge ? 431 : 400;
            var reason = status == 431 ? "Request Header Fields Too Large" : "Bad Request";
            var response = new RawFrame(Encoding.ASCII.GetBytes(BuildErrorResponse(status, reason)), null);

            try
            {
                await ctx.Pipeline.WriteAsync(response).ConfigureAwait(false);
            }
            catch
            {
                // 先写后关：写本身失败时仍要走有界收口，写异常不能被吞掉。
                await ctx.CloseAfterWriteAsync("ws-handshake-error").ConfigureAwait(false);
                throw;
            }

            await ctx.CloseAfterWriteAsync("ws-handshake-error").ConfigureAwait(false);
        }

        /// <summary>
        /// 服务端处理一个握手块：校验请求行与必需头，应答 101 后切换分帧器。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文</param>
        /// <param name="block">整块握手报文文本</param>
        /// <returns>应答写出与切换完成后结束的任务</returns>
        private async Task HandleServerAsync(IChannelHandlerContext ctx, string block)
        {
            ValidateRequest(block, out var key, out var requestedProtocol);

            var response = new RawFrame(
                Encoding.ASCII.GetBytes(BuildResponse(ComputeAccept(key), requestedProtocol)),
                null);

            try
            {
                // 必须先等 101 真正交接再切分帧器，否则紧随其后的帧会越过尚未完成的响应。
                await ctx.Pipeline.WriteAsync(response).ConfigureAwait(false);
            }
            catch
            {
                await ctx.CloseAfterWriteAsync("ws-handshake-write-failed").ConfigureAwait(false);
                throw;
            }

            Complete(ctx);
        }

        /// <summary>
        /// 客户端处理握手应答：状态行必须是 101 且 Accept 必须与本地期望值一致。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文</param>
        /// <param name="block">整块握手应答文本</param>
        private void HandleClient(IChannelHandlerContext ctx, string block)
        {
            if (string.IsNullOrEmpty(block))
                throw new FrameProtocolException(WebSocketProtocolErrors.InvalidHandshakeResponse);

            var lineEnd = block.IndexOf("\r\n", StringComparison.Ordinal);
            var statusLine = lineEnd < 0 ? block : block.Substring(0, lineEnd);

            if (statusLine.IndexOf("101", StringComparison.Ordinal) < 0)
                throw new FrameProtocolException(WebSocketProtocolErrors.InvalidHandshakeResponse);

            if (!TryGetHeader(block, "Sec-WebSocket-Accept", out var accept)
                || !string.Equals(accept, _expectedAccept, StringComparison.Ordinal))
                throw new FrameProtocolException(WebSocketProtocolErrors.InvalidHandshakeResponse);

            Complete(ctx);
        }

        /// <summary>
        /// 标记握手完成、切换分帧器并通知装配方。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文</param>
        private void Complete(IChannelHandlerContext ctx)
        {
            _completed = true;

            // 先置握手完成再切分帧器：出站编码器据此放行数据帧，早于切换不会让帧发错协议。
            var state = _state;
            if (state != null)
                state.MarkCompleted();

            var callback = _onCompleted;
            if (callback != null)
                callback(ctx.ChannelId);

            SwitchSplitter();
        }

        /// <summary>
        /// 把分帧器切到帧分帧器；屏障或目标缺失时跳过，装配方自行承担后果。
        /// </summary>
        private void SwitchSplitter()
        {
            if (_barrier == null || _frameSplitter == null)
                return;

            _barrier.SwitchTo(_frameSplitter);
        }

        /// <summary>
        /// 校验服务端接收的握手请求：请求行、Upgrade 与 Sec-WebSocket-Key。
        /// </summary>
        /// <param name="block">整块握手报文文本</param>
        /// <param name="key">输出 <c>Sec-WebSocket-Key</c></param>
        /// <param name="requestedProtocol">输出客户端请求的第一个子协议，未请求时为 null</param>
        private static void ValidateRequest(string block, out string key, out string requestedProtocol)
        {
            key = null;
            requestedProtocol = null;

            if (string.IsNullOrEmpty(block))
                throw new FrameProtocolException(WebSocketProtocolErrors.InvalidHandshakeRequest);

            var lineEnd = block.IndexOf("\r\n", StringComparison.Ordinal);
            var requestLine = lineEnd < 0 ? block : block.Substring(0, lineEnd);

            var parts = requestLine.Split(' ');
            if (parts.Length < 3
                || !string.Equals(parts[0], "GET", StringComparison.OrdinalIgnoreCase)
                || parts[parts.Length - 1].IndexOf(HttpVersion, StringComparison.OrdinalIgnoreCase) < 0)
                throw new FrameProtocolException(WebSocketProtocolErrors.InvalidHandshakeRequest);

            if (!TryGetHeader(block, "Upgrade", out var upgrade)
                || upgrade.IndexOf("websocket", StringComparison.OrdinalIgnoreCase) < 0)
                throw new FrameProtocolException(WebSocketProtocolErrors.InvalidHandshakeRequest);

            if (!TryGetHeader(block, "Sec-WebSocket-Key", out key) || string.IsNullOrEmpty(key))
                throw new FrameProtocolException(WebSocketProtocolErrors.InvalidHandshakeRequest);

            if (TryGetHeader(block, "Sec-WebSocket-Protocol", out var protocols) && !string.IsNullOrEmpty(protocols))
            {
                var comma = protocols.IndexOf(',');
                requestedProtocol = (comma < 0 ? protocols : protocols.Substring(0, comma)).Trim();
            }
        }

        /// <summary>
        /// 组装客户端握手请求报文。
        /// </summary>
        /// <returns>可直接写出的 ASCII 请求文本</returns>
        private string BuildRequest()
        {
            var builder = new StringBuilder(256);
            builder.Append("GET ").Append(_requestTarget).Append(" ").Append(HttpVersion).Append("\r\n");
            builder.Append("Host: ").Append(_host).Append("\r\n");
            builder.Append("User-Agent: ").Append(UserAgent).Append("\r\n");

            if (!string.IsNullOrEmpty(_origin))
                builder.Append("Origin: ").Append(_origin).Append("\r\n");

            builder.Append("Upgrade: websocket\r\n");
            builder.Append("Connection: Upgrade\r\n");
            builder.Append("Sec-WebSocket-Version: 13\r\n");

            if (!string.IsNullOrEmpty(_subProtocol))
                builder.Append("Sec-WebSocket-Protocol: ").Append(_subProtocol).Append("\r\n");

            builder.Append("Sec-WebSocket-Key: ").Append(_clientKey).Append("\r\n");
            builder.Append("\r\n");
            return builder.ToString();
        }

        /// <summary>
        /// 组装服务端 101 应答报文；客户端请求了子协议时回显其中第一个。
        /// </summary>
        /// <param name="accept">由请求 Key 算出的 <c>Sec-WebSocket-Accept</c></param>
        /// <param name="requestedProtocol">客户端请求的第一个子协议，可为 null</param>
        /// <returns>可直接写出的 ASCII 应答文本</returns>
        private static string BuildResponse(string accept, string requestedProtocol)
        {
            var builder = new StringBuilder(160);
            builder.Append("HTTP/1.1 101 Switching Protocols\r\n");
            builder.Append("Upgrade: websocket\r\n");
            builder.Append("Connection: Upgrade\r\n");

            if (!string.IsNullOrEmpty(requestedProtocol))
                builder.Append("Sec-WebSocket-Protocol: ").Append(requestedProtocol).Append("\r\n");

            builder.Append("Sec-WebSocket-Accept: ").Append(accept).Append("\r\n\r\n");
            return builder.ToString();
        }

        /// <summary>
        /// 组装握手失败的 HTTP 应答报文。
        /// </summary>
        /// <param name="status">HTTP 状态码</param>
        /// <param name="reason">状态短语</param>
        /// <returns>可直接写出的 ASCII 应答文本</returns>
        private static string BuildErrorResponse(int status, string reason)
        {
            var body = status + " " + reason;
            var builder = new StringBuilder(160);
            builder.Append(HttpVersion).Append(' ').Append(status).Append(' ').Append(reason).Append("\r\n");
            builder.Append("Content-Type: ").Append(PlainTextContentType).Append("\r\n");
            builder.Append("Content-Length: ").Append(body.Length).Append("\r\n");
            builder.Append("Connection: close\r\n\r\n");
            builder.Append(body);
            return builder.ToString();
        }

        /// <summary>
        /// 在报文里查找指定报头，报头名大小写不敏感。
        /// </summary>
        /// <param name="block">整块报文文本</param>
        /// <param name="name">报头名</param>
        /// <param name="value">输出报头值（已去除首尾空白）</param>
        /// <returns>找到返回 true</returns>
        internal static bool TryGetHeader(string block, string name, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(block) || string.IsNullOrEmpty(name))
                return false;

            var lines = block.Split(new[] { "\r\n" }, StringSplitOptions.None);

            // 第 0 行是请求行/状态行，从第 1 行开始找；空行表示报头结束。
            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Length == 0)
                    return false;

                var colon = line.IndexOf(':');
                if (colon <= 0)
                    continue;

                if (!string.Equals(line.Substring(0, colon).Trim(), name, StringComparison.OrdinalIgnoreCase))
                    continue;

                value = line.Substring(colon + 1).Trim();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 生成 16 字节随机 Key 的 base64 表示。
        /// </summary>
        /// <returns>握手用的 <c>Sec-WebSocket-Key</c></returns>
        private static string CreateKey()
        {
            var src = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(src);
            }

            return Convert.ToBase64String(src);
        }

        /// <summary>
        /// 由 <c>Sec-WebSocket-Key</c> 计算 <c>Sec-WebSocket-Accept</c>。
        /// </summary>
        /// <param name="key">握手 Key</param>
        /// <returns>base64 编码的 SHA1 摘要</returns>
        internal static string ComputeAccept(string key)
        {
            using (var sha1 = SHA1.Create())
            {
                var hash = sha1.ComputeHash(Encoding.ASCII.GetBytes(key + HandshakeGuid));
                return Convert.ToBase64String(hash);
            }
        }
    }
}