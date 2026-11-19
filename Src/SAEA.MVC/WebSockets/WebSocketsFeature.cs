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
*命名空间：SAEA.MVC.WebSockets
*文件名： WebSocketsFeature
*版本号： v26.11.5.1
*唯一标识：43984f7b-eba6-4ed8-ae54-2052cc5cbfa8
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/5 11:56:30
*描述：MVC WebSocket 能力实现
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 11:16:00
*修改人： yswenli
*版本号： v26.11.16.1
*描述：文件移入 WebSockets/ 并改用命名空间 SAEA.MVC.WebSockets，无逻辑变更
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Http;
using SAEA.Http.Model;
using SAEA.WebSocket.Model;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MVC.WebSockets
{
    /// <summary>
    /// 每个 HTTP 上下文一个的 WebSocket 能力入口。
    /// </summary>
    internal sealed class WebSocketsFeature : IWebSocketsFeature
    {
        const string AcceptGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        static readonly ConcurrentDictionary<string, MVCWebSocketConnection> _connections =
            new ConcurrentDictionary<string, MVCWebSocketConnection>();

        static Timer _sweeper;

        readonly IHttpContext _context;

        readonly object _gate = new object();

        WSController _controller;

        MVCWebSocketConnection _connection;

        readonly List<byte[]> _pending = new List<byte[]>();

        bool _draining;

        Exception _earlyError;

        public WebSocketsFeature(IHttpContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 由 MvcInvoker 在调用 action 前设置，消息分发时回调到同一个控制器实例
        /// </summary>
        internal void Attach(WSController controller)
        {
            _controller = controller;
        }

        /// <summary>
        /// 当前请求是否为 WebSocket 升级请求（只读请求头，不产生副作用）
        /// </summary>
        public bool IsWebSocketRequest
        {
            get
            {
                var headers = _context?.Request?.Headers;

                if (headers == null) return false;

                if (!headers.TryGetValue("Upgrade", out var upgrade) || string.IsNullOrEmpty(upgrade))
                    return false;

                if (upgrade.IndexOf("websocket", StringComparison.OrdinalIgnoreCase) < 0)
                    return false;

                if (!headers.TryGetValue("Connection", out var connection) || string.IsNullOrEmpty(connection))
                    return false;

                if (connection.IndexOf("upgrade", StringComparison.OrdinalIgnoreCase) < 0)
                    return false;

                if (!headers.TryGetValue("Sec-WebSocket-Key", out var key) || string.IsNullOrWhiteSpace(key))
                    return false;

                return true;
            }
        }

        /// <summary>
        /// 已接受的连接；未接受前为 null
        /// </summary>
        public IWebSocketConnection Current => _connection;

        /// <summary>
        /// 接受 WebSocket 连接：完成握手并写 101，之后连接交给调用方使用
        /// </summary>
        public Task<IWebSocketConnection> AcceptWebSocketAsync(string subProtocol = null)
        {
            if (_connection != null)
                throw new InvalidOperationException("当前上下文已经接受过 WebSocket 连接，不能重复 Accept");

            if (_controller == null)
                throw new InvalidOperationException("WebSocket 入口必须声明在 WSController 派生类上");

            var request = _context.Request;
            var response = _context.Response;

            // 1. 基础校验：只接受 GET + 合法升级头
            if (string.Compare(request.Method, ConstHelper.GET, StringComparison.OrdinalIgnoreCase) != 0)
            {
                WriteError(response, 400, "Bad Request", null);
                return Task.FromResult<IWebSocketConnection>(null);
            }

            if (!IsWebSocketRequest)
            {
                WriteError(response, 400, "Bad Request", null);
                return Task.FromResult<IWebSocketConnection>(null);
            }

            // 2. 版本必须为 13
            if (!request.Headers.TryGetValue("Sec-WebSocket-Version", out var version) || version != "13")
            {
                WriteError(response, 426, "Upgrade Required", "Sec-WebSocket-Version: 13\r\n");
                return Task.FromResult<IWebSocketConnection>(null);
            }

            // 3. 拼 101（不复用旧握手令牌，避免它固定回显子协议）
            var acceptKey = ComputeAcceptKey(request.Headers["Sec-WebSocket-Key"]);

            var handshake = BuildHandshake(acceptKey, subProtocol);

            // 4. 升级底层连接。101 与客户端首帧可能同段到达：
            //    Upgrade 内部 drain 出的字节先暂存，连接对象建好后再补喂
            IWebSocketTransport transport;

            try
            {
                transport = response.Upgrade(handshake, OnRawData, OnRawError);
            }
            catch (Exception ex)
            {
                WriteError(response, 400, "Bad Request: " + ex.Message, null);
                return Task.FromResult<IWebSocketConnection>(null);
            }

            // 5. 建连接对象并发布；发布后新到的字节统一进 _pending，
            //    由 _draining 串行排空，避免与早到字节交叉导致乱序
            //    单条消息上限与待处理队列预算保持一致，避免一条消息在队列里就超限
            var coder = new WSCoder { RequireMask = true, MaxMessageSize = MVCWebSocketConnection.MaxQueuedBytes };

            MVCWebSocketConnection connection;

            Exception pendingError;

            lock (_gate)
            {
                connection = new MVCWebSocketConnection(transport, coder, _context, _controller);
                _connection = connection;

                pendingError = _earlyError;
                _earlyError = null;

                if (pendingError != null)
                {
                    _pending.Clear();
                }
                else
                {
                    _draining = true;
                }
            }

            Register(connection);

            try
            {
                transport.SetNoTimeout(true);
            }
            catch
            {
            }

            if (pendingError != null)
            {
                connection.Fail(pendingError, WSCloseReason.ProtocolError);
            }
            else
            {
                DrainPending();
            }

            return Task.FromResult<IWebSocketConnection>(connection);
        }

        /// <summary>
        /// 由 MvcInvoker 调用：处理 [WebSocket] action 内部的异常
        /// </summary>
        internal void HandleActionException(Exception exception)
        {
            var connection = _connection;

            if (connection != null)
            {
                connection.Fail(exception, WSCloseReason.ServerShutdown);
                return;
            }

            // 握手失败已经写过原始响应（IsHandled），这里不再二次写
            if (_context.Response.IsUpgraded || _context.Response.IsHandled) return;

            WriteError(_context.Response, 500, "Internal Server Error", null);
        }

        void OnRawData(byte[] data)
        {
            MVCWebSocketConnection connection;

            lock (_gate)
            {
                connection = _connection;

                // 握手排空期间也走队列，保证与早到字节的先后顺序
                if (connection == null || _draining)
                {
                    _pending.Add(data);
                    return;
                }
            }

            connection.OnData(data);
        }

        /// <summary>
        /// 按入队顺序排空握手期间暂存的字节；排空期间新到的数据继续入队
        /// </summary>
        void DrainPending()
        {
            while (true)
            {
                byte[] data;

                lock (_gate)
                {
                    if (_pending.Count == 0)
                    {
                        _draining = false;
                        return;
                    }

                    data = _pending[0];
                    _pending.RemoveAt(0);
                }

                _connection.OnData(data);
            }
        }

        void OnRawError(Exception exception)
        {
            MVCWebSocketConnection connection;

            lock (_gate)
            {
                connection = _connection;

                if (connection == null)
                {
                    _earlyError = exception;
                    return;
                }
            }

            connection.Fail(exception, WSCloseReason.ProtocolError);
        }

        #region 连接表

        static void Register(MVCWebSocketConnection connection)
        {
            if (connection == null || string.IsNullOrEmpty(connection.Id)) return;

            _connections[connection.Id] = connection;

            EnsureSweeper();

            // 断开可能发生在发布之前（ConnectionClosed 早于 Register 到达），
            // 此时 TryRemove 会落空；注册后补一次检查，避免死连接一直留在表里
            if (!connection.IsAlive && _connections.TryRemove(connection.Id, out var dead))
            {
                dead.HandleDisconnected();
            }
        }

        internal static void Unregister(string id)
        {
            if (string.IsNullOrEmpty(id)) return;

            _connections.TryRemove(id, out _);
        }

        /// <summary>
        /// 底层断开通知：只有表里认识这个 sessionId 才处理，未知 id 直接忽略
        /// </summary>
        internal static void OnDisconnected(string id, Exception exception)
        {
            if (string.IsNullOrEmpty(id)) return;

            if (_connections.TryRemove(id, out var connection))
            {
                connection.HandleDisconnected();
            }
        }

        /// <summary>
        /// 应用停止时中止全部连接
        /// </summary>
        internal static void StopAll()
        {
            foreach (var item in _connections)
            {
                item.Value.Abort();
            }

            _connections.Clear();
        }

        static void EnsureSweeper()
        {
            if (_sweeper != null) return;

            var timer = new Timer(_ => Sweep(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            if (Interlocked.CompareExchange(ref _sweeper, timer, null) != null)
            {
                timer.Dispose();
            }
        }

        static void Sweep()
        {
            // State == Closed 覆盖正常收尾；IsAlive == false 覆盖错过断开通知的残项
            // （例如连接在登记前就已经断开，通知落到空表上）
            foreach (var item in _connections)
            {
                var connection = item.Value;

                if (connection.State != WebSocketState.Closed && connection.IsAlive) continue;

                if (_connections.TryRemove(item.Key, out var removed))
                {
                    // 幂等：已关闭的连接直接 no-op，仍在 Open 的死连接补一次收尾
                    removed.HandleDisconnected();
                }
            }
        }

        #endregion

        #region 握手与错误响应

        static string ComputeAcceptKey(string key)
        {
            using (var sha1 = SHA1.Create())
            {
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(key + AcceptGuid));
                return Convert.ToBase64String(hash);
            }
        }

        static byte[] BuildHandshake(string acceptKey, string subProtocol)
        {
            var builder = new StringBuilder();

            builder.Append("HTTP/1.1 101 Switching Protocols\r\n");
            builder.Append("Upgrade: websocket\r\n");
            builder.Append("Connection: Upgrade\r\n");
            builder.Append("Sec-WebSocket-Accept: ").Append(acceptKey).Append("\r\n");

            if (!string.IsNullOrEmpty(subProtocol))
                builder.Append("Sec-WebSocket-Protocol: ").Append(subProtocol).Append("\r\n");

            builder.Append("\r\n");

            return Encoding.UTF8.GetBytes(builder.ToString());
        }

        static void WriteError(HttpResponse response, int status, string message, string extraHeader)
        {
            if (response == null || response.IsUpgraded || response.IsHandled) return;

            var body = Encoding.UTF8.GetBytes(message ?? string.Empty);

            var builder = new StringBuilder();

            builder.Append("HTTP/1.1 ").Append(status).Append(' ');

            switch (status)
            {
                case 426:
                    builder.Append("Upgrade Required");
                    break;
                case 500:
                    builder.Append("Internal Server Error");
                    break;
                default:
                    builder.Append("Bad Request");
                    break;
            }

            builder.Append("\r\n");
            builder.Append("Content-Type: text/plain; charset=utf-8\r\n");
            builder.Append("Content-Length: ").Append(body.Length).Append("\r\n");
            builder.Append("Connection: close\r\n");

            if (!string.IsNullOrEmpty(extraHeader))
                builder.Append(extraHeader);

            builder.Append("\r\n");

            var head = Encoding.UTF8.GetBytes(builder.ToString());

            var all = new byte[head.Length + body.Length];

            Buffer.BlockCopy(head, 0, all, 0, head.Length);
            Buffer.BlockCopy(body, 0, all, head.Length, body.Length);

            response.WriteRaw(all, true);
        }

        #endregion
    }
}