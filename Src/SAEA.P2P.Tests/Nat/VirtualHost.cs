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
*命名空间：SAEA.P2P.Tests.Nat
*文件名： VirtualHost
*版本号： net10.0
*唯一标识：d7cc3542-f95d-4dec-8a1f-f5d45ef02d8e
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 11:50:00
*描述：测试用虚拟主机，在指定回环地址上创建并绑定套接字
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 11:50:00
*修改人： yswenli
*版本号： net10.0
*描述：新建虚拟主机夹具
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace SAEA.P2P.Tests.Nat
{
    /// <summary>
    /// 测试用虚拟主机：把一台"主机"固定到某个回环地址上，按需创建并绑定自己的套接字。
    /// 释放时统一关闭本主机创建的套接字，避免用例之间争抢端口。
    /// </summary>
    public sealed class VirtualHost : IDisposable
    {
        /// <summary>
        /// 本主机创建的全部套接字，Dispose 时逐个关闭
        /// </summary>
        private readonly List<Socket> _sockets = new List<Socket>();

        /// <summary>
        /// 本主机的地址
        /// </summary>
        public IPAddress Address { get; }

        /// <summary>
        /// 用点分十进制地址串创建虚拟主机
        /// </summary>
        /// <param name="address">主机地址，测试中取 127.0.0.2 起的回环地址</param>
        public VirtualHost(string address)
            : this(IPAddress.Parse(address))
        {
        }

        /// <summary>
        /// 用地址对象创建虚拟主机
        /// </summary>
        /// <param name="address">主机地址，测试中取 127.0.0.2 起的回环地址</param>
        public VirtualHost(IPAddress address)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            Address = address;
        }

        /// <summary>
        /// 在本主机地址上绑定一个 UDP 套接字，端口由系统分配
        /// </summary>
        /// <returns>已绑定的 UDP 套接字，由本主机负责关闭</returns>
        public Socket BindUdp()
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            try
            {
                socket.Bind(new IPEndPoint(Address, 0));
            }
            catch
            {
                socket.Dispose();
                throw;
            }
            _sockets.Add(socket);
            return socket;
        }

        /// <summary>
        /// 在本主机地址上绑定一个 TCP 监听套接字，端口由系统分配
        /// </summary>
        /// <param name="backlog">等待接受的连接队列长度</param>
        /// <returns>已绑定并开始监听的 TCP 套接字，由本主机负责关闭</returns>
        public Socket BindTcpListener(int backlog = 128)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                socket.Bind(new IPEndPoint(Address, 0));
                socket.Listen(backlog);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
            _sockets.Add(socket);
            return socket;
        }

        /// <summary>
        /// 获取套接字实际绑定的本地端点
        /// </summary>
        /// <param name="socket">本主机创建的套接字</param>
        /// <returns>该套接字的本地端点</returns>
        public static IPEndPoint EndPointOf(Socket socket)
        {
            if (socket == null) throw new ArgumentNullException(nameof(socket));
            return (IPEndPoint)socket.LocalEndPoint;
        }

        /// <summary>
        /// 关闭本主机创建的全部套接字，可重复调用
        /// </summary>
        public void Dispose()
        {
            lock (_sockets)
            {
                foreach (var socket in _sockets)
                {
                    try { socket.Dispose(); } catch { }
                }
                _sockets.Clear();
            }
        }
    }
}