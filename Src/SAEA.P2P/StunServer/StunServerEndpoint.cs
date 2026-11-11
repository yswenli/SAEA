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
*命名空间：SAEA.P2P.StunServer
*文件名： StunServerEndpoint
*版本号： v26.11.5.1
*唯一标识：d727871f-b807-42cf-b540-e1a1737da0c9
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 19:00:00
*描述：STUN 服务端单个监听端点
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 19:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 STUN 服务端监听端点
*
*****************************************************************************/
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Transport;

namespace SAEA.P2P.StunServer
{
    /// <summary>
    /// STUN 服务端的一个已绑定监听端点，封装底层 UDP 传输与响应发送。
    /// 端点由 <see cref="StunServer"/> 创建并释放，调用方只读取地址。
    /// </summary>
    public sealed class StunServerEndpoint : IDisposable
    {
        /// <summary>
        /// 底层 UDP 传输
        /// </summary>
        private readonly IUdpTransport _transport;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 包装一个已绑定的传输；只在同一程序集内创建
        /// </summary>
        /// <param name="transport">已绑定的 UDP 传输</param>
        internal StunServerEndpoint(IUdpTransport transport)
        {
            if (transport == null)
            {
                throw new ArgumentNullException(nameof(transport));
            }

            _transport = transport;
        }

        /// <summary>
        /// 本地监听端点；已释放时为 null
        /// </summary>
        public IPEndPoint LocalEndPoint
        {
            get { return _disposed ? null : _transport.LocalEndPoint; }
        }

        /// <summary>
        /// 底层传输，供服务端订阅接收事件
        /// </summary>
        internal IUdpTransport Transport
        {
            get { return _transport; }
        }

        /// <summary>
        /// 启动接收循环
        /// </summary>
        internal void StartReceive()
        {
            _transport.StartReceive();
        }

        /// <summary>
        /// 从本端点向远端发送一个数据报
        /// </summary>
        /// <param name="remoteEndPoint">目标端点</param>
        /// <param name="data">待发送数据</param>
        /// <returns>发送完成的任务</returns>
        internal Task SendAsync(IPEndPoint remoteEndPoint, byte[] data)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            return _transport.SendToAsync(remoteEndPoint, new ReadOnlyMemory<byte>(data), CancellationToken.None);
        }

        /// <summary>
        /// 释放底层传输
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _transport.Dispose();
        }
    }
}