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
 *命名空间：SAEA.Http.Base.Net
 *文件名： HttpWebSocketTransport
 *版本号： v26.11.5.1
 *唯一标识：6a1c2f0e-3b7d-4e58-9a02-8c4d5e6f7a81
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/9 15:40:00
 *描述：基于连接会话的 WebSocket 传输实现
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/10 10:30:00
 *修改人： yswenli
 *版本号： v26.11.5.1
 *描述：改为按连接会话发送与收口，不再依赖连接令牌与底层 socket 事件
 *
 *****************************************************************************/
using SAEA.Http.Model;

using System.Threading;

namespace SAEA.Http.Base.Net
{
    /// <summary>
    /// 基于连接会话的传输实现，仅供 HttpResponse.Upgrade 使用
    /// </summary>
    internal class HttpWebSocketTransport : IWebSocketTransport
    {
        readonly IWebHost _webHost;

        HttpConnectionSession _current;

        public string SessionId { get; }

        public bool IsConnected
        {
            get
            {
                var session = Volatile.Read(ref _current);
                if (session == null || session.Channel == null)
                    return false;

                return session.Channel.IsActive;
            }
        }

        public HttpWebSocketTransport(IWebHost webHost, HttpConnectionSession session)
        {
            _webHost = webHost;
            _current = session;
            SessionId = session == null || session.Channel == null ? null : session.Channel.Id;
        }

        public void Send(byte[] data)
        {
            if (data == null || data.Length == 0) return;

            var session = Volatile.Read(ref _current);

            if (session == null) return;

            // 断开过程中的发送失败由会话内部记录，不能抛回处理器链的入站回调。
            session.SendRaw(data);
        }

        public void SetNoTimeout(bool noTimeout)
        {
            var host = _webHost as WebHost;
            if (host == null) return;

            try
            {
                host.SetNoTimeout(SessionId, noTimeout);
            }
            catch
            {
            }
        }

        /// <summary>
        /// 等待最后一次发送完成，避免 Close 帧还在内核队列时就 Close 掉 socket（会变成 RST）
        /// </summary>
        public bool WaitSendCompleted(int timeoutMs)
        {
            var session = Volatile.Read(ref _current);

            if (session == null) return true;

            return session.WaitSendCompleted(timeoutMs);
        }

        public void Disconnect()
        {
            var session = Interlocked.Exchange(ref _current, null);

            if (session == null) return;

            session.Close();
        }

        /// <summary>
        /// 关闭发送方向：先 FIN，让客户端读完 Close 帧；直接 Close 在存在挂起接收时会被内核 reset
        /// </summary>
        public void ShutdownSend()
        {
            var session = Volatile.Read(ref _current);

            if (session == null) return;

            var info = session.Info;

            if (info == null || info.ClientSocket == null) return;

            try
            {
                info.ClientSocket.Shutdown(System.Net.Sockets.SocketShutdown.Send);
            }
            catch
            {
            }
        }
    }
}