/***************************************************************************** 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c) 2018-2022yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets.Shortcut
*文件名： UDPClient
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
    /// UDPClient
    /// </summary>
    public class UDPClient<Coder> : IDisposable where Coder : class, IUnpacker
    {
        IClientSocket _udpClient;

        BaseUnpacker _baseUnpacker;

        public event Action<UDPClient<Coder>, ISocketProtocal> OnReceive;

        public event Action<UDPClient<Coder>, Exception> OnError;

        public event Action<UDPClient<Coder>> OnDisconnected;

        /// <summary>
        /// UDPClient
        /// </summary>
        /// <param name="endPoint"></param>
        public UDPClient(IPEndPoint endPoint)
        {
            var bContext = new BaseContext<Coder>();

            _udpClient = SocketFactory.CreateClientSocket(SocketOptionBuilder.Instance.SetSocket(SAEASocketType.Udp)
                .SetIPEndPoint(endPoint)
                .UseIocp(bContext)
                .SetReadBufferSize(SocketOption.UDPMaxLength)
                .SetWriteBufferSize(SocketOption.UDPMaxLength)
                .Build());

            _udpClient.OnDisconnected += UdpClient_OnDisconnected;
            _udpClient.OnReceive += UdpClient_OnReceive;
            _udpClient.OnError += UdpClient_OnError;

            _baseUnpacker = (BaseUnpacker)bContext.Unpacker;
        }

        /// <summary>
        /// UDPClient
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        public UDPClient(string ip, int port) : this(new IPEndPoint(IPAddress.Parse(ip), port))
        {

        }

        /// <summary>
        /// Connect
        /// </summary>
        public void Connect()
        {
            _udpClient.Connect();
        }

        /// <summary>
        /// SendAsync
        /// </summary>
        /// <param name="protocal"></param>
        protected void SendAsync(BaseSocketProtocal protocal)
        {
            _udpClient.SendAsync(protocal.ToBytes());
        }

        /// <summary>
        /// SendAsync
        /// </summary>
        /// <param name="data"></param>
        /// <param name="socketProtocalType"></param>
        public void SendAsync(byte[] data, SocketProtocalType socketProtocalType = SocketProtocalType.ChatMessage)
        {
            SendAsync(BaseSocketProtocal.Parse(data, socketProtocalType));
        }

        /// <summary>
        /// SendAsync
        /// </summary>
        /// <param name="msg"></param>
        public void SendAsync(string msg)
        {
            SendAsync(Encoding.UTF8.GetBytes(msg));
        }

        private void UdpClient_OnError(string ID, Exception ex)
        {
            Console.WriteLine($"UdpClient_OnError {ID} :" + ex.Message);
            OnError?.Invoke(this, ex);
        }

        private void UdpClient_OnReceive(byte[] data)
        {
            _baseUnpacker.Unpack(data, (msg) =>
            {
                OnReceive?.Invoke(this, msg);
            });
        }

        private void UdpClient_OnDisconnected(string ID, Exception ex)
        {
            Console.WriteLine($"UdpClient_OnDisconnected {ID} :" + ex.Message);
            OnDisconnected?.Invoke(this);
        }

        /// <summary>
        /// Disconnect
        /// </summary>
        public void Disconnect()
        {
            _udpClient.Disconnect();
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            _udpClient?.Dispose();
        }
    }

    /// <summary>
    /// UDPClient
    /// </summary>
    public class UDPClient : UDPClient<BaseUnpacker>
    {
        /// <summary>
        /// UDPClient
        /// </summary>
        /// <param name="endPoint"></param>
        public UDPClient(IPEndPoint endPoint) : base(endPoint)
        {

        }
        /// <summary>
        /// UDPClient
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        public UDPClient(string ip, int port) : base(ip, port)
        {

        }
    }
}
