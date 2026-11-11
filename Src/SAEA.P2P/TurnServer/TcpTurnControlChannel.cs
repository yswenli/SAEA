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
*命名空间：SAEA.P2P.TurnServer
*文件名： TcpTurnControlChannel
*版本号： v26.11.5.1
*唯一标识：797a1c90-15f1-4441-aeff-f7742852d15f
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 01:30:00
*描述：基于已连接 socket 的 TCP/TLS 控制连接
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 05:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：接管为数据连接后的 NetworkStream 改由自己拥有 socket，避免中继泵结束时连接泄漏
*
*****************************************************************************/
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

using SAEA.P2P.Common;

namespace SAEA.P2P.TurnServer
{
    /// <summary>
    /// 流式控制连接：把一条已连接的 TCP socket 包装成 TURN 控制连接。
    /// 同一连接上可能有中继接收循环与请求处理线程同时写，发送必须串行化，否则会切碎 ChannelData 帧。
    /// </summary>
    internal sealed class TcpTurnControlChannel : ITurnControlChannel
    {
        /// <summary>
        /// 已连接的对端 socket
        /// </summary>
        private readonly Socket _socket;

        /// <summary>
        /// 串行化写操作的同步对象
        /// </summary>
        private readonly object _sendSync = new object();

        /// <summary>
        /// 连接是否已关闭；关闭后发送直接丢弃
        /// </summary>
        private bool _closed;

        /// <summary>
        /// 接管为数据连接后使用的字节流；首次访问时创建并缓存
        /// </summary>
        private NetworkStream _dataStream;

        /// <summary>
        /// 包装一条已连接的 socket
        /// </summary>
        /// <param name="socket">已连接的 socket</param>
        /// <param name="localEndPoint">连接本地端点</param>
        /// <param name="remoteEndPoint">连接对端端点</param>
        /// <exception cref="ArgumentNullException">任一参数为 null</exception>
        public TcpTurnControlChannel(Socket socket, IPEndPoint localEndPoint, IPEndPoint remoteEndPoint)
        {
            if (socket == null)
            {
                throw new ArgumentNullException(nameof(socket));
            }
            if (localEndPoint == null)
            {
                throw new ArgumentNullException(nameof(localEndPoint));
            }
            if (remoteEndPoint == null)
            {
                throw new ArgumentNullException(nameof(remoteEndPoint));
            }

            _socket = socket;
            LocalEndPoint = localEndPoint;
            RemoteEndPoint = remoteEndPoint;
        }

        /// <summary>
        /// 连接对端端点
        /// </summary>
        public IPEndPoint RemoteEndPoint { get; private set; }

        /// <summary>
        /// 连接本地端点
        /// </summary>
        public IPEndPoint LocalEndPoint { get; private set; }

        /// <summary>
        /// 流式连接，ChannelData 必须补齐到 4 字节倍数
        /// </summary>
        public bool IsStream
        {
            get { return true; }
        }

        /// <summary>
        /// 明文 TCP 控制连接
        /// </summary>
        public bool IsTls
        {
            get { return false; }
        }

        /// <summary>
        /// 接管为数据连接时使用的基础流；首次访问时创建并缓存。
        /// 中继泵结束时必须能连带关闭连接，否则 socket 泄漏；接管后这条连接不再承载 TURN 报文，不会与 Send 抢用
        /// </summary>
        public Stream DataStream
        {
            get
            {
                // 与发送共用同一把锁，避免接管瞬间创建两个 NetworkStream 包同一个 socket
                lock (_sendSync)
                {
                    if (_dataStream == null)
                    {
                        _dataStream = new NetworkStream(_socket, true);
                    }
                    return _dataStream;
                }
            }
        }

        /// <summary>
        /// 把一条完整报文写回连接；写失败只标记关闭，不向上抛
        /// </summary>
        /// <param name="data">待发送的完整报文</param>
        public void Send(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return;
            }

            lock (_sendSync)
            {
                if (_closed)
                {
                    return;
                }

                try
                {
                    var offset = 0;
                    while (offset < data.Length)
                    {
                        var sent = _socket.Send(data, offset, data.Length - offset, SocketFlags.None);
                        if (sent <= 0)
                        {
                            _closed = true;
                            return;
                        }
                        offset += sent;
                    }
                }
                catch (ObjectDisposedException)
                {
                    _closed = true;
                }
                catch (SocketException ex)
                {
                    _closed = true;
                    P2PLogHelper.Debug("TurnServer", "流式控制连接回送失败：" + ex.SocketErrorCode);
                }
            }
        }
    }
}