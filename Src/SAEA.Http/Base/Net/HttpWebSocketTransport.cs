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
*唯一标识：f5c99ebd-29a6-4ee0-886c-3e56170a04e0
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/5 11:56:30
*描述：基于 IWebHost + IUserToken 的 WebSocket 传输实现
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 13:11:19
*修改人： yswenli
*版本号： v26.11.5.1
*描述：规范文件头 banner
*
*****************************************************************************/
using SAEA.Http.Model;
using SAEA.Sockets.Interface;

using System.Threading;

namespace SAEA.Http.Base.Net
{
    /// <summary>
    /// 基于 IWebHost + IUserToken 的传输实现，仅供 HttpResponse.Upgrade 使用
    /// </summary>
    internal class HttpWebSocketTransport : IWebSocketTransport
    {
        readonly IWebHost _webHost;

        IUserToken _userToken;

        public string SessionId { get; }

        public bool IsConnected
        {
            get
            {
                var userToken = Volatile.Read(ref _userToken);

                // UserTokenPool.Enqueue 会把 Socket 置为 null，可作为会话已回收的判定
                return userToken != null && userToken.Socket != null;
            }
        }

        public HttpWebSocketTransport(IWebHost webHost, IUserToken userToken)
        {
            _webHost = webHost;
            _userToken = userToken;
            SessionId = userToken?.ID;
        }

        public void Send(byte[] data)
        {
            if (data == null || data.Length == 0) return;

            var userToken = Volatile.Read(ref _userToken);

            if (userToken == null) return;

            try
            {
                _webHost.Send(userToken, data);
            }
            catch
            {
                // 断开过程中的发送失败由 CleanupAsync 统一收尾，不能抛回 saea 接收线程
            }
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
            var userToken = Volatile.Read(ref _userToken);

            if (userToken == null) return true;

            if (timeoutMs < 0) timeoutMs = 0;

            var watch = System.Diagnostics.Stopwatch.StartNew();

            while (userToken.IsSending)
            {
                if (watch.ElapsedMilliseconds >= timeoutMs) return false;

                Thread.Sleep(5);
            }

            return true;
        }

        public void Disconnect()
        {
            var userToken = Interlocked.Exchange(ref _userToken, null);

            if (userToken == null) return;

            try
            {
                _webHost.Disconnect(userToken);
            }
            catch
            {
            }
        }

        /// <summary>
        /// 关闭发送方向：先 FIN，让客户端读完 Close 帧；直接 Close 在存在挂起接收时会被内核 reset
        /// </summary>
        public void ShutdownSend()
        {
            var userToken = Volatile.Read(ref _userToken);

            if (userToken == null) return;

            try
            {
                userToken.Socket?.Shutdown(System.Net.Sockets.SocketShutdown.Send);
            }
            catch
            {
            }
        }
    }
}