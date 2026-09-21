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
*文件名： TCPServer
*版本号： v26.4.23.1
*唯一标识：6c46016f-1e63-4f6d-a4ef-5e520d3e9ac6
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/09/21 19:36:08
*描述：TCPServer服务端类
*
*=====================================================================
*修改标记
*修改时间：2021/09/21 19:36:08
*修改人： yswenli
*版本号： v26.4.23.1
*描述：TCPServer服务端类
*
*****************************************************************************/
using System;
using System.Net;
using System.Text;

using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

namespace SAEA.Sockets.Shortcut
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="Coder"></typeparam>
    public class TCPServer<Coder> where Coder : class, ICoder
    {
        IServerSocket _serverSokcet;


        public event Action<TCPServer<Coder>, string> OnAccept;

        /// <summary>
        /// 接收数据事件（ReadOnlyMemory 版本）。底层由 Span 事件驱动，回调内数据已完成复制，可跨回调保存。
        /// </summary>
        public event Action<TCPServer<Coder>, IUserToken, ReadOnlyMemory<byte>> OnReceive;

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
            _serverSokcet.OnServerReceiveSpan += ServerSokcet_OnReceiveSpan;
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
            _serverSokcet.SendAsync(id, new ReadOnlyMemory<byte>(data));
        }

        /// <summary>
        /// SendAsync
        /// </summary>
        /// <param name="id"></param>
        /// <param name="data"></param>
        public void SendAsync(string id, ReadOnlyMemory<byte> data)
        {
            _serverSokcet.SendAsync(id, data);
        }

        /// <summary>
        /// Send
        /// </summary>
        /// <param name="id"></param>
        /// <param name="data"></param>
        public void Send(string id, ReadOnlySpan<byte> data)
        {
            _serverSokcet.Send(id, data);
        }

        /// <summary>
        /// SendAsync
        /// </summary>
        /// <param name="id"></param>
        /// <param name="str"></param>
        public void SendAsync(string id, string str)
        {
            _serverSokcet.SendAsync(id, new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(str)));
        }

        private void ServerSokcet_OnReceiveSpan(IUserToken userToken, ReadOnlySpan<byte> data)
        {
            OnReceive?.Invoke(this, userToken, data.ToArray());
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
            var userToken = ResolveUserToken(obj);

            if (userToken != null)
            {
                OnAccept?.Invoke(this, userToken.ID);
            }
            else if (obj is ChannelInfo channelInfo)
            {
                OnAccept?.Invoke(this, channelInfo.ID);
            }
        }

        private static IUserToken ResolveUserToken(object obj)
        {
            if (obj is IUserToken userToken) return userToken;

            if (obj is ChannelInfo channelInfo) return channelInfo.UserToken;

            return null;
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
    public class TCPServer : TCPServer<BaseCoder>
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
