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
*命名空间：SAEA.MVC.Tests.Tests.Support
*文件名： WebSocketProbe
*版本号： net10.0
*唯一标识：131f388d-2ec0-4bdc-81b2-77db2734e4fd
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 18:05:27
*描述：WebSocket 客户端探针
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 18:05:27
*修改人： yswenli
*版本号： net10.0
*描述：新增，封装握手重试、文本收发与正常关闭
*
*****************************************************************************/
using System;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MVC.Tests.Tests.Support
{
    /// <summary>
    /// WebSocket 客户端探针：握手就绪重试、单条文本消息的发送与接收、正常关闭。
    /// </summary>
    public static class WebSocketProbe
    {
        private const int ReadyIntervalMilliseconds = 100;

        private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(10);

        private const int ReceiveBufferSize = 8 * 1024;

        /// <summary>
        /// 连接服务端：连接被拒绝时在 10 秒内重试，其余失败原样抛出。
        /// </summary>
        /// <param name="url">ws 或 wss 地址</param>
        /// <param name="acceptAnyCertificate">true 时跳过服务端证书校验，仅用于自签证书的 WSS 用例</param>
        /// <param name="token">取消令牌</param>
        /// <returns>已建立的客户端连接</returns>
        public static async Task<ClientWebSocket> ConnectAsync(string url, bool acceptAnyCertificate, CancellationToken token)
        {
            var deadline = DateTime.UtcNow + ReadyTimeout;

            while (true)
            {
                var socket = new ClientWebSocket();

                if (acceptAnyCertificate)
                    socket.Options.RemoteCertificateValidationCallback = (message, certificate, chain, errors) => true;

                try
                {
                    await socket.ConnectAsync(new Uri(url), token).ConfigureAwait(false);

                    return socket;
                }
                catch (Exception ex) when (IsConnectionRefused(ex) && DateTime.UtcNow < deadline)
                {
                    socket.Dispose();

                    await Task.Delay(ReadyIntervalMilliseconds, token).ConfigureAwait(false);
                }
                catch
                {
                    socket.Dispose();

                    throw;
                }
            }
        }

        /// <summary>
        /// 发送一条文本消息并读取一条完整回包。
        /// </summary>
        /// <param name="socket">已建立的连接</param>
        /// <param name="text">待发送文本</param>
        /// <param name="token">取消令牌</param>
        /// <returns>收到的文本</returns>
        public static async Task<string> SendAndReceiveTextAsync(ClientWebSocket socket, string text, CancellationToken token)
        {
            var payload = Encoding.UTF8.GetBytes(text);

            await socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, token)
                .ConfigureAwait(false);

            var buffer = new byte[ReceiveBufferSize];

            var builder = new StringBuilder();

            while (true)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                    throw new InvalidOperationException("服务端在回包前关闭了连接，关闭状态：" + result.CloseStatus);

                builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                if (result.EndOfMessage)
                    return builder.ToString();
            }
        }

        /// <summary>
        /// 正常关闭连接：失败不影响用例结论。
        /// </summary>
        /// <param name="socket">已建立的连接</param>
        /// <param name="token">取消令牌</param>
        /// <returns>关闭完成的任务</returns>
        public static async Task CloseAsync(ClientWebSocket socket, CancellationToken token)
        {
            try
            {
                if (socket.State == WebSocketState.Open)
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", token).ConfigureAwait(false);
            }
            catch (WebSocketException)
            {
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>
        /// 判断异常链里是否含连接被拒绝的套接字错误。
        /// </summary>
        /// <param name="exception">待判定的异常</param>
        /// <returns>true 表示可以重试连接</returns>
        private static bool IsConnectionRefused(Exception exception)
        {
            var current = exception;

            while (current != null)
            {
                if (current is SocketException socketException && socketException.SocketErrorCode == SocketError.ConnectionRefused)
                    return true;

                current = current.InnerException;
            }

            return false;
        }
    }
}