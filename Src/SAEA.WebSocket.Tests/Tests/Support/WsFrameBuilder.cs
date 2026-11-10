/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.WebSocket.Tests.Tests.Support
*文件名： WsFrameBuilder
*版本号： net10.0
*唯一标识：7e4c5a26-8b1d-4f0a-9a63-5b2c8d1e4f72
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 18:32:00
*描述：WebSocket 线格式与握手报文的测试拼装器
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 18:32:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，WebSocket 线格式与握手报文的测试拼装器
*
*****************************************************************************/
using System;
using System.Security.Cryptography;
using System.Text;

namespace SAEA.WebSocket.Tests.Tests.Support
{
    /// <summary>
    /// RFC 6455 wire frame 与握手报文的测试拼装器，只用于构造输入。
    /// </summary>
    public static class WsFrameBuilder
    {
        /// <summary>测试固定使用 4 字节掩码键，保证掩码结果可复现。</summary>
        public static readonly byte[] MaskKey = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        /// <summary>RFC 6455 规定的握手固定后缀。</summary>
        public const string HandshakeGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        /// <summary>
        /// 拼出一条完整 wire frame。
        /// </summary>
        /// <param name="opcode">帧操作码。</param>
        /// <param name="payload">帧体，可为 null 表示空帧体。</param>
        /// <param name="masked">是否带掩码；带掩码时用固定掩码键。</param>
        /// <param name="fin">是否消息末帧。</param>
        /// <param name="rsv">是否置 RSV1，用于构造非法帧。</param>
        /// <returns>含帧头与帧体的完整帧。</returns>
        public static byte[] Frame(byte opcode, byte[] payload, bool masked = true, bool fin = true, bool rsv = false)
        {
            var body = payload ?? Array.Empty<byte>();
            var length = body.Length;

            var extLength = length < 126 ? 0 : (length < 65536 ? 2 : 8);
            var headerLength = 2 + extLength + (masked ? 4 : 0);

            var frame = new byte[headerLength + length];

            frame[0] = (byte)((fin ? 0x80 : 0) | (rsv ? 0x40 : 0) | (opcode & 0x0f));

            if (length < 126)
            {
                frame[1] = (byte)((masked ? 0x80 : 0) | length);
            }
            else if (length < 65536)
            {
                frame[1] = (byte)((masked ? 0x80 : 0) | 126);
                frame[2] = (byte)(length >> 8);
                frame[3] = (byte)length;
            }
            else
            {
                frame[1] = (byte)((masked ? 0x80 : 0) | 127);
                var value = (ulong)length;
                for (var i = 0; i < 8; i++)
                    frame[2 + i] = (byte)(value >> (56 - 8 * i));
            }

            var payloadOffset = headerLength;
            if (masked)
            {
                Buffer.BlockCopy(MaskKey, 0, frame, 2 + extLength, 4);
            }

            for (var i = 0; i < length; i++)
            {
                var value = body[i];
                if (masked)
                    value = (byte)(value ^ MaskKey[i & 3]);

                frame[payloadOffset + i] = value;
            }

            return frame;
        }

        /// <summary>
        /// 拼出一段指定长度的可预测帧体：第 i 个字节为 <c>(byte)i</c>。
        /// </summary>
        /// <param name="length">帧体长度。</param>
        /// <returns>填充后的帧体。</returns>
        public static byte[] Payload(int length)
        {
            var body = new byte[length];
            for (var i = 0; i < length; i++)
                body[i] = (byte)i;

            return body;
        }

        /// <summary>
        /// 生成一个服务端可接受的握手请求报文。
        /// </summary>
        /// <param name="key">客户端 <c>Sec-WebSocket-Key</c>，为 null 时用固定值。</param>
        /// <param name="subProtocol">子协议，可为空。</param>
        /// <returns>ASCII 编码的完整请求报文。</returns>
        public static byte[] HandshakeRequest(string key = null, string subProtocol = null)
        {
            var actualKey = string.IsNullOrEmpty(key) ? "dGhlIHNhbXBsZSBub25jZQ==" : key;

            var builder = new StringBuilder(192);
            builder.Append("GET /chat HTTP/1.1\r\n");
            builder.Append("Host: server.example.com\r\n");
            builder.Append("Upgrade: websocket\r\n");
            builder.Append("Connection: Upgrade\r\n");
            builder.Append("Sec-WebSocket-Version: 13\r\n");

            if (!string.IsNullOrEmpty(subProtocol))
                builder.Append("Sec-WebSocket-Protocol: ").Append(subProtocol).Append("\r\n");

            builder.Append("Sec-WebSocket-Key: ").Append(actualKey).Append("\r\n");
            builder.Append("\r\n");
            return Encoding.ASCII.GetBytes(builder.ToString());
        }

        /// <summary>
        /// 生成一个客户端可接受的 101 握手应答。
        /// </summary>
        /// <param name="accept">应答里的 <c>Sec-WebSocket-Accept</c>。</param>
        /// <returns>ASCII 编码的完整应答报文。</returns>
        public static byte[] HandshakeResponse(string accept)
        {
            var builder = new StringBuilder(160);
            builder.Append("HTTP/1.1 101 Switching Protocols\r\n");
            builder.Append("Upgrade: websocket\r\n");
            builder.Append("Connection: Upgrade\r\n");
            builder.Append("Sec-WebSocket-Accept: ").Append(accept).Append("\r\n\r\n");
            return Encoding.ASCII.GetBytes(builder.ToString());
        }

        /// <summary>
        /// 由客户端 Key 计算 <c>Sec-WebSocket-Accept</c>。
        /// </summary>
        /// <param name="key">客户端 <c>Sec-WebSocket-Key</c>。</param>
        /// <returns>base64 编码的 SHA1 摘要。</returns>
        public static string ComputeAccept(string key)
        {
            using (var sha1 = SHA1.Create())
            {
                var hash = sha1.ComputeHash(Encoding.ASCII.GetBytes(key + HandshakeGuid));
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>
        /// 从客户端发出的握手请求里取出 <c>Sec-WebSocket-Key</c>。
        /// </summary>
        /// <param name="request">完整请求报文字节。</param>
        /// <returns>取到的 Key；未找到返回 null。</returns>
        public static string ExtractKey(byte[] request)
        {
            var text = Encoding.ASCII.GetString(request);
            var lines = text.Split(new[] { "\r\n" }, StringSplitOptions.None);

            foreach (var line in lines)
            {
                var colon = line.IndexOf(':');
                if (colon <= 0)
                    continue;

                if (!string.Equals(line.Substring(0, colon).Trim(), "Sec-WebSocket-Key",
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                return line.Substring(colon + 1).Trim();
            }

            return null;
        }
    }
}