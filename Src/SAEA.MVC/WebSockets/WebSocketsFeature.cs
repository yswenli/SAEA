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
*修改时间：2026/11/19 14:18:04
*修改人： yswenli
*版本号： v26.11.16.1
*描述：握手与连接表外提到 WebSocketHandshake 与 WebSocketConnectionRegistry，StopAll 释放清扫计时器
*
*****************************************************************************/
using SAEA.Http.Model;
using SAEA.MVC.Controllers;
using SAEA.WebSocket.Model;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAEA.MVC.WebSockets
{
    /// <summary>
    /// 每个 HTTP 上下文一个的 WebSocket 能力入口。
    /// </summary>
    internal sealed class WebSocketsFeature : IWebSocketsFeature
    {
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

            // 1~3. 基础校验与 101 拼装：失败时已写好错误响应
            byte[] handshake;

            if (!WebSocketHandshake.TryBuildHandshake(request, response, IsWebSocketRequest, subProtocol, out handshake))
                return Task.FromResult<IWebSocketConnection>(null);

            // 4. 升级底层连接。101 与客户端首帧可能同段到达：
            //    Upgrade 内部 drain 出的字节先暂存，连接对象建好后再补喂
            IWebSocketTransport transport;

            try
            {
                transport = response.Upgrade(handshake, OnRawData, OnRawError);
            }
            catch (Exception ex)
            {
                WebSocketHandshake.WriteError(response, 400, "Bad Request: " + ex.Message, null);
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

            WebSocketConnectionRegistry.Register(connection);

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

            WebSocketHandshake.WriteError(_context.Response, 500, "Internal Server Error", null);
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
    }
}