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
*文件名： UDPClient
*版本号： v26.4.23.1
*唯一标识：ab903354-bc3c-4e69-bc2e-b561c411df16
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/09/20 01:32:10
*描述：UDPClient接口
*
*=====================================================================
*修改标记
*修改时间：2021/09/20 01:32:10
*修改人： yswenli
*版本号： v26.4.23.1
*描述：UDPClient接口
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
    /// UDPClient
    /// </summary>
    public class UDPClient<Coder> : IDisposable where Coder : class, ICoder
    {
        IClientSocket _udpClient;

        BaseCoder _baseUnpacker;

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

            _baseUnpacker = (BaseCoder)bContext.Unpacker;
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
            var msgs = _baseUnpacker.Decode(data);
            if (msgs == null || msgs.Count < 1) return;
            foreach (var msg in msgs)
            {
                OnReceive?.Invoke(this, msg);
            }
        }

        private void UdpClient_OnDisconnected(string id, Exception ex)
        {
            if (string.IsNullOrEmpty(id)) return;
            Console.WriteLine($"UdpClient_OnDisconnected {id} :" + ex.Message);
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
    public class UDPClient : UDPClient<BaseCoder>
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
