/***************************************************************************** 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c) 2018-2021yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets.Shortcut
*文件名： TCPServer
*版本号： v6.0.0.1
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
*版本号： v6.0.0.1
*描述：
*****************************************************************************/
using System;
using System.Net;
using System.Text;

using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;

namespace SAEA.Sockets.Shortcut
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="Coder"></typeparam>
    public class TCPServer<Coder> where Coder : class, IUnpacker
    {
        IServerSocket _serverSokcet;


        public event Action<TCPServer<Coder>, string> OnAccept;

        public event Action<TCPServer<Coder>, IUserToken, byte[]> OnReceive;

        public event Action<TCPServer<Coder>, string, Exception> OnError;

        public event Action<TCPServer<Coder>, string, Exception> OnDisconnect;

        /// <summary>
        /// TCPServer
        /// </summary>
        /// <param name="endpoint"></param>
        public TCPServer(IPEndPoint endpoint)
        {
            //tcpserver
            _serverSokcet = SocketFactory.CreateServerSocket(SocketOptionBuilder.Instance
                .SetSocket(Model.SAEASocketType.Tcp)
                .SetIPEndPoint(endpoint)
                .UseIocp<Coder>()
                .Build());

            _serverSokcet.OnAccepted += ServerSokcet_OnAccepted;
            _serverSokcet.OnDisconnected += ServerSokcet_OnDisconnected;
            _serverSokcet.OnError += ServerSokcet_OnError;
            _serverSokcet.OnReceive += ServerSokcet_OnReceive;
        }

        /// <summary>
        /// TCPServer
        /// </summary>
        /// <param name="port"></param>
        public TCPServer(int port) : this(new IPEndPoint(IPAddress.Any, port))
        {

        }

        /// <summary>
        /// Start
        /// </summary>
        public void Start()
        {
            _serverSokcet.Start();
        }

        /// <summary>
        /// Stop
        /// </summary>
        public void Stop()
        {
            _serverSokcet.Stop();
        }

        /// <summary>
        /// SendAsync
        /// </summary>
        /// <param name="id"></param>
        /// <param name="data"></param>
        public void SendAsync(string id, byte[] data)
        {
            _serverSokcet.SendAsync(id, data);
        }

        /// <summary>
        /// SendAsync
        /// </summary>
        /// <param name="id"></param>
        /// <param name="str"></param>
        public void SendAsync(string id, string str)
        {
            _serverSokcet.SendAsync(id, Encoding.UTF8.GetBytes(str));
        }

        private void ServerSokcet_OnReceive(Interface.ISession currentSession, byte[] data)
        {
            var userToken = (IUserToken)currentSession;

            OnReceive?.Invoke(this, userToken, data);
        }

        private void ServerSokcet_OnError(string id, Exception ex)
        {
            OnError?.Invoke(this, id, ex);
        }

        private void ServerSokcet_OnDisconnected(string id, Exception ex)
        {
            OnDisconnect?.Invoke(this, id, ex);
        }

        private void ServerSokcet_OnAccepted(object obj)
        {
            OnAccept?.Invoke(this, ((IUserToken)obj).ID);
        }

        /// <summary>
        /// Disconnect
        /// </summary>
        /// <param name="id"></param>
        public void Disconnect(string id)
        {
            _serverSokcet.Disconnect(id);
        }
    }

    /// <summary>
    /// TCPServer
    /// </summary>
    public class TCPServer : TCPServer<BaseUnpacker>
    {
        /// <summary>
        /// TCPServer
        /// </summary>
        /// <param name="endPoint"></param>
        public TCPServer(IPEndPoint endPoint) : base(endPoint)
        {

        }
        /// <summary>
        /// TCPServer
        /// </summary>
        /// <param name="port"></param>
        public TCPServer(int port) : base(port)
        {

        }
    }
}
