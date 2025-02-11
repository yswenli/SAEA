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
*文件名： UDPServer
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
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

namespace SAEA.Sockets.Shortcut
{
    /// <summary>
    /// UDPServer
    /// </summary>
    public class UDPServer<Coder> where Coder : class, ICoder
    {
        IServerSocket _udpServer;

        public event Action<UDPServer<Coder>, string, ISocketProtocal> OnReceive;

        public event Action<string, Exception> OnError;

        public event Action<string> OnAccepted;

        public event Action<string> OnDisconnected;

        /// <summary>
        /// UDPServer
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="timeOut"></param>
        public UDPServer(IPEndPoint endPoint, int timeOut = 5000)
        {
            _udpServer = SocketFactory.CreateServerSocket(SocketOptionBuilder.Instance.SetSocket(SAEASocketType.Udp)
                .SetIPEndPoint(endPoint)
                .UseIocp<Coder>()
                .SetReadBufferSize(SocketOption.UDPMaxLength)
                .SetWriteBufferSize(SocketOption.UDPMaxLength)
                .SetTimeOut(timeOut)
                .Build());

            _udpServer.OnAccepted += UdpServer_OnAccepted;
            _udpServer.OnDisconnected += UdpServer_OnDisconnected;
            _udpServer.OnError += UdpServer_OnError;
            _udpServer.OnReceive += UdpServer_OnReceive;
        }

        /// <summary>
        /// UDPServer
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="timeOut"></param>
        public UDPServer(int port, int timeOut = 5000) : this(new IPEndPoint(IPAddress.Any, port), timeOut)
        {

        }

        /// <summary>
        /// Start
        /// </summary>
        public void Start()
        {
            _udpServer.Start();
        }

        /// <summary>
        /// SendAsync
        /// </summary>
        /// <param name="id"></param>
        /// <param name="baseSocketProtocal"></param>
        protected void SendAsync(string id, BaseSocketProtocal baseSocketProtocal)
        {
            _udpServer.SendAsync(id, baseSocketProtocal.ToBytes());
        }

        /// <summary>
        /// SendAsync
        /// </summary>
        /// <param name="id"></param>
        /// <param name="data"></param>
        /// <param name="socketProtocalType"></param>
        public void SendAsync(string id, byte[] data, SocketProtocalType socketProtocalType = SocketProtocalType.ChatMessage)
        {
            SendAsync(id, BaseSocketProtocal.Parse(data, socketProtocalType));
        }

        /// <summary>
        /// SendAsync
        /// </summary>
        /// <param name="id"></param>
        /// <param name="msg"></param>
        public void SendAsync(string id, string msg)
        {
            SendAsync(id, Encoding.UTF8.GetBytes(msg));
        }

        private void UdpServer_OnReceive(Interface.ISession currentSession, byte[] data)
        {
            var userToken = (IUserToken)currentSession;
            var msgs = userToken.Coder.Decode(data);
            if (msgs == null || msgs.Count == 0) return;
            foreach (var msg in msgs)
            {
                OnReceive?.Invoke(this, userToken.ID, msg);
            }
        }

        private void UdpServer_OnError(string id, Exception ex)
        {
            OnError?.Invoke(id, ex);
        }

        private void UdpServer_OnDisconnected(string id, Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"UdpServer_OnDisconnected:{id}");
            OnDisconnected?.Invoke(id);
        }

        private void UdpServer_OnAccepted(object obj)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"UdpServer_OnAccepted:{((IUserToken)obj).ID}");
            OnAccepted?.Invoke(((IUserToken)obj).ID);
        }

    }

    /// <summary>
    /// UDPServer
    /// </summary>
    public class UDPServer : UDPServer<BaseCoder>
    {
        /// <summary>
        /// UDPServer
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="timeOut"></param>
        public UDPServer(IPEndPoint endPoint, int timeOut = 5000) : base(endPoint, timeOut)
        {

        }
        /// <summary>
        /// UDPServer
        /// </summary>
        /// <param name="port"></param>
        /// <param name="timeOut"></param>
        public UDPServer(int port, int timeOut = 5000) : base(port, timeOut)
        {

        }
    }
}
