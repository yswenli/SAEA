/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Sockets.Shortcut
*文件名： TCPClient
*版本号： v26.4.23.1
*唯一标识：a478b77f-6323-44fd-851a-19e57236373c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/09/21 19:36:08
*描述：TCPClient接口
*
*=====================================================================
*修改标记
*修改时间：2021/09/21 19:36:08
*修改人： yswenli
*版本号： v26.4.23.1
*描述：TCPClient接口
*
*****************************************************************************/
using System;
using System.Net;
using System.Text;

using SAEA.Sockets.Base;
using SAEA.Sockets.Core;
using SAEA.Sockets.Interface;

namespace SAEA.Sockets.Shortcut
{
    /// <summary>
    /// TCPClient
    /// </summary>
    /// <typeparam name="Coder">IUnpacker</typeparam>
    public class TCPClient<Coder> : IDisposable where Coder : class, ICoder
    {
        IClientSocket _clientSokcet;

        /// <summary>
        /// 接收数据事件（ReadOnlyMemory 版本）。底层由 Span 事件驱动，回调内数据已完成复制，可跨回调保存。
        /// </summary>
        public event Action<TCPClient<Coder>, ReadOnlyMemory<byte>> OnReceive;

        public event Action<TCPClient<Coder>, Exception> OnError;

        public event Action<TCPClient<Coder>, Exception> OnDisconnect;

        /// <summary>
        /// 流
        /// </summary>
        public SocketStream SocketStream { get; private set; }

        /// <summary>
        /// TCPClient
        /// </summary>
        public TCPClient(IPEndPoint endPoint)
        {
            _clientSokcet = SocketFactory.CreateClientSocket(SocketOptionBuilder.Instance
               .SetSocket(Model.SAEASocketType.Tcp)
               .SetIPEndPoint(endPoint)
               .UseIocp<Coder>()
               .Build());

            _clientSokcet.OnClientReceiveSpan += ClientSokcet_OnReceiveSpan;
            _clientSokcet.OnDisconnected += ClientSokcet_OnDisconnected;
            _clientSokcet.OnError += ClientSokcet_OnError;

            SocketStream = new SocketStream(_clientSokcet);
        }

        /// <summary>
        /// TCPClient
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        public TCPClient(string ip, int port) : this(new IPEndPoint(IPAddress.Parse(ip), port))
        {

        }

        /// <summary>
        /// Connect
        /// </summary>
        public void Connect()
        {
            _clientSokcet.ConnectAsync();
        }
        /// <summary>
        /// SendAsync
        /// </summary>
        /// <param name="data"></param>
        public void SendAsync(byte[] data)
        {
            _clientSokcet.SendAsync(new ReadOnlyMemory<byte>(data));
        }
        /// <summary>
        /// SendAsync
        /// </summary>
        /// <param name="data"></param>
        /// <remarks>
        /// 零拷贝契约：数组支撑的 ReadOnlyMemory 在快速路径上零拷贝发送，发送完成前不得修改或复用该缓冲区；
        /// 非数组内存会在边界处发生一次复制。
        /// </remarks>
        public void SendAsync(ReadOnlyMemory<byte> data)
        {
            _clientSokcet.SendAsync(data);
        }
        /// <summary>
        /// Send
        /// </summary>
        /// <param name="data"></param>
        /// <remarks>
        /// 零拷贝契约：数组支撑的 ReadOnlySpan 在快速路径上零拷贝发送，发送完成前不得修改或复用该缓冲区；
        /// 非数组内存或需要缓冲的实现会在边界处发生一次复制。
        /// </remarks>
        public void Send(ReadOnlySpan<byte> data)
        {
            _clientSokcet.Send(data);
        }
        /// <summary>
        /// SendAsync
        /// </summary>
        /// <param name="str"></param>
        public void SendAsync(string str)
        {
            _clientSokcet.SendAsync(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(str)));
        }


        private void ClientSokcet_OnError(string ID, Exception ex)
        {
            OnError?.Invoke(this, ex);
        }

        private void ClientSokcet_OnDisconnected(string ID, Exception ex)
        {
            OnDisconnect?.Invoke(this, ex);
        }

        private void ClientSokcet_OnReceiveSpan(ReadOnlySpan<byte> data)
        {
            OnReceive?.Invoke(this, data.ToArray());
        }
        /// <summary>
        /// Disconnect
        /// </summary>
        public void Disconnect()
        {
            _clientSokcet.Disconnect();
        }
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            _clientSokcet.Dispose();
        }
    }

    /// <summary>
    /// TCPClient
    /// </summary>
    public class TCPClient : TCPClient<BaseCoder>
    {
        /// <summary>
        /// TCPClient
        /// </summary>
        /// <param name="endPoint"></param>
        public TCPClient(IPEndPoint endPoint) : base(endPoint)
        {

        }

        /// <summary>
        /// TCPClient
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        public TCPClient(string ip, int port) : base(ip, port)
        {

        }
    }
}
