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
*文件名： WebSocketHandshake
*版本号： v26.11.16.1
*唯一标识：d1f92e18-0565-487d-926a-bf08fdd34269
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 14:18:04
*描述：WebSocket 握手校验与 101 响应拼装
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 14:18:04
*修改人： yswenli
*版本号： v26.11.16.1
*描述：新增，承接 WebSocketsFeature 的握手校验与错误响应
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Http;

using System;
using System.Security.Cryptography;
using System.Text;

namespace SAEA.MVC.WebSockets
{
    /// <summary>
    /// WebSocket 握手：校验升级请求并拼装 101 响应
    /// </summary>
    internal static class WebSocketHandshake
    {
        const string AcceptGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        /// <summary>
        /// 校验升级请求并拼装 101 响应，失败时写出对应的错误响应
        /// </summary>
        /// <param name="request">当前 HTTP 请求</param>
        /// <param name="response">当前 HTTP 响应</param>
        /// <param name="isWebSocketRequest">调用方判定的升级请求标志</param>
        /// <param name="subProtocol">协商到的子协议，可为 null</param>
        /// <param name="handshake">成功时为 101 响应字节，失败时为 null</param>
        /// <returns>校验通过并成功拼装时为 true</returns>
        internal static bool TryBuildHandshake(HttpRequest request, HttpResponse response, bool isWebSocketRequest, string subProtocol, out byte[] handshake)
        {
            handshake = null;

            // 1. 基础校验：只接受 GET + 合法升级头
            if (string.Compare(request.Method, ConstHelper.GET, StringComparison.OrdinalIgnoreCase) != 0)
            {
                WriteError(response, 400, "Bad Request", null);
                return false;
            }

            if (!isWebSocketRequest)
            {
                WriteError(response, 400, "Bad Request", null);
                return false;
            }

            // 2. 版本必须为 13
            if (!request.Headers.TryGetValue("Sec-WebSocket-Version", out var version) || version != "13")
            {
                WriteError(response, 426, "Upgrade Required", "Sec-WebSocket-Version: 13\r\n");
                return false;
            }

            // 3. 拼 101（不复用旧握手令牌，避免它固定回显子协议）
            var acceptKey = ComputeAcceptKey(request.Headers["Sec-WebSocket-Key"]);

            handshake = BuildHandshake(acceptKey, subProtocol);

            return true;
        }

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

        internal static void WriteError(HttpResponse response, int status, string message, string extraHeader)
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
    }
}