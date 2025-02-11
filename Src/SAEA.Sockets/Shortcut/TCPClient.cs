/***************************************************************************** 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets.Shortcut
*文件名： TCPClient
*版本号： v7.0.0.1
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 15:54:21
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
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

        public event Action<TCPClient<Coder>, byte[]> OnReceive;

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

            _clientSokcet.OnReceive += ClientSokcet_OnReceive;
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
            _clientSokcet.SendAsync(data);
        }
        /// <summary>
        /// SendAsync
        /// </summary>
        /// <param name="str"></param>
        public void SendAsync(string str)
        {
            _clientSokcet.SendAsync(Encoding.UTF8.GetBytes(str));
        }


        private void ClientSokcet_OnError(string ID, Exception ex)
        {
            OnError?.Invoke(this, ex);
        }

        private void ClientSokcet_OnDisconnected(string ID, Exception ex)
        {
            OnDisconnect?.Invoke(this, ex);
        }

        private void ClientSokcet_OnReceive(byte[] data)
        {
            OnReceive?.Invoke(this, data);
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
