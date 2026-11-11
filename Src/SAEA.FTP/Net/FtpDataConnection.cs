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
*命名空间：SAEA.FTP.Net
*文件名： FtpDataConnection
*版本号： v26.11.5.1
*唯一标识：a5abd92b-19c1-4081-8b65-9e960219f305
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 18:20:00
*描述：FTP 数据连接门面，向传输代码提供同步 Stream 与显式收口
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 17:52:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：构造改为私有，新增服务端接受与客户端连接两个工厂，关闭动作由工厂注入
*
*****************************************************************************/
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using SAEA.Sockets;

namespace SAEA.FTP.Net
{
    /// <summary>
    /// 一条数据连接的同步门面：读写走 <see cref="Stream"/>，收口走半关闭加关闭。
    /// <para>
    /// 生命周期与所有权：连接由传输层持有，本对象只借用底层 <see cref="Socket"/> 做半关闭，
    /// 绝不主动释放它；关闭时先释放适配器让入站泵停止，再执行工厂注入的关闭动作，
    /// 这样被背压阻塞的泵线程能先解除等待，不会被收口路径反过来卡住。
    /// </para>
    /// <para>
    /// 创建入口：服务端由监听器创建、客户端由 <see cref="FtpControlConnection"/> 创建，
    /// 两者只在断开动作上不同，读写语义完全一致。
    /// </para>
    /// <para>
    /// 关闭语义与旧实现一致：先 <see cref="ShutdownSend"/> 发出 FIN，再 <see cref="Close"/> 断开；
    /// 两者都幂等，重复调用不会抛异常。
    /// </para>
    /// </summary>
    public sealed class FtpDataConnection : IDisposable
    {
        private readonly Socket _socket;

        private readonly ChannelStreamAdapter _adapter;

        private readonly Action _closeTransport;

        private int _sendShutdown;

        private int _closed;

        /// <summary>
        /// 由两个内部工厂创建，不供外部直接构造。
        /// </summary>
        /// <param name="socket">该连接的底层套接字，为 null 抛 <see cref="ArgumentNullException"/></param>
        /// <param name="adapter">数据通道流适配器，为 null 抛 <see cref="ArgumentNullException"/></param>
        /// <param name="closeTransport">断开底层会话的动作，为 null 抛 <see cref="ArgumentNullException"/></param>
        private FtpDataConnection(Socket socket, ChannelStreamAdapter adapter, Action closeTransport)
        {
            if (socket == null)
                throw new ArgumentNullException(nameof(socket));
            if (adapter == null)
                throw new ArgumentNullException(nameof(adapter));
            if (closeTransport == null)
                throw new ArgumentNullException(nameof(closeTransport));

            _socket = socket;
            _adapter = adapter;
            _closeTransport = closeTransport;
        }

        /// <summary>
        /// 服务端工厂：由数据监听器在链启动后创建，关闭时断开该会话。
        /// </summary>
        /// <param name="owner">底层传输层服务器，为 null 抛 <see cref="ArgumentNullException"/></param>
        /// <param name="channelId">连接标识，为空或 null 抛 <see cref="ArgumentException"/></param>
        /// <param name="socket">该连接的底层套接字，为 null 抛 <see cref="ArgumentNullException"/></param>
        /// <param name="adapter">数据通道流适配器，为 null 抛 <see cref="ArgumentNullException"/></param>
        /// <returns>可读写的数据连接门面</returns>
        internal static FtpDataConnection CreateForServer(IServerSocket owner, string channelId, Socket socket, ChannelStreamAdapter adapter)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (string.IsNullOrEmpty(channelId))
                throw new ArgumentException("连接标识不能为空。", nameof(channelId));

            return new FtpDataConnection(socket, adapter, () =>
            {
                try { owner.Disconnect(channelId); } catch (Exception) { }
            });
        }

        /// <summary>
        /// 客户端工厂：由控制连接在数据链启动后创建，关闭时释放客户端实例。
        /// <para>
        /// <see cref="IClientSocket.Dispose"/> 内部已先断开再释放，这里不再单独调用 <see cref="IClientSocket.Disconnect"/>。
        /// </para>
        /// </summary>
        /// <param name="client">承载该数据连接的客户端，为 null 抛 <see cref="ArgumentNullException"/></param>
        /// <param name="socket">该连接的底层套接字，为 null 抛 <see cref="ArgumentNullException"/></param>
        /// <param name="adapter">数据通道流适配器，为 null 抛 <see cref="ArgumentNullException"/></param>
        /// <returns>可读写的数据连接门面</returns>
        internal static FtpDataConnection CreateForClient(IClientSocket client, Socket socket, ChannelStreamAdapter adapter)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            return new FtpDataConnection(socket, adapter, () =>
            {
                try { client.Dispose(); } catch (Exception) { }
            });
        }

        /// <summary>
        /// 数据读写流：读在对端 FIN 后返回 0，写完成表示数据已交给传输层。
        /// </summary>
        public Stream Stream
        {
            get { return _adapter; }
        }

        /// <summary>
        /// 单次读等待数据的上限（毫秒），小于等于 0 表示无限等待；超时抛 <see cref="IOException"/>。
        /// </summary>
        public int ReadTimeout
        {
            get { return _adapter.ReadTimeout; }
            set { _adapter.ReadTimeout = value; }
        }

        /// <summary>
        /// 单次写等待传输层交接的上限（毫秒），小于等于 0 表示无限等待；超时抛 <see cref="IOException"/>。
        /// </summary>
        public int WriteTimeout
        {
            get { return _adapter.WriteTimeout; }
            set { _adapter.WriteTimeout = value; }
        }

        /// <summary>
        /// 半关闭发送方向，向对端发出 FIN；已关闭或已半关闭时静默忽略。
        /// <para>
        /// 只能在全部写完成交接之后调用，否则缓冲区里未发出的数据会被截断。
        /// </para>
        /// </summary>
        public void ShutdownSend()
        {
            if (Interlocked.Exchange(ref _sendShutdown, 1) != 0)
                return;

            try { _socket.Shutdown(SocketShutdown.Send); } catch (Exception) { }
        }

        /// <summary>
        /// 关闭数据连接：释放适配器、断开发送出的底层会话；可重复调用。
        /// </summary>
        public void Close()
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0)
                return;

            // 先停适配器：它会把入站泵从背压等待中唤醒，随后断开才不会和泵的收口互相等待。
            try { _adapter.Dispose(); } catch (Exception) { }

            try { _closeTransport(); } catch (Exception) { }
        }

        /// <summary>
        /// 等价于 <see cref="Close"/>，便于在 using 语句中收口。
        /// </summary>
        public void Dispose()
        {
            Close();
        }
    }
}