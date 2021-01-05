/****************************************************************************
*项目名称：SAEA.Sockets.UdpTest
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Sockets.UdpTest
*类 名 称：UDPClient
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/1/5 11:13:46
*描述：
*=====================================================================
*修改时间：2021/1/5 11:13:46
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.Sockets.UdpTest
{
    public class UDPClient
    {
        IClientSocket _udpClient;

        BaseUnpacker _baseUnpacker;

        public event Action<UDPClient, ISocketProtocal> OnReceive;

        public UDPClient()
        {
            //udpclient
            var bContext = new BaseContext();

            _udpClient = SocketFactory.CreateClientSocket(SocketOptionBuilder.Instance.SetSocket(SAEASocketType.Udp)
                .SetIP("127.0.0.1")
                .SetPort(39656)
                .UseIocp(bContext)
                .SetReadBufferSize(SocketOption.UDPMaxLength)
                .SetWriteBufferSize(SocketOption.UDPMaxLength)
                .Build());

            _udpClient.OnDisconnected += UdpClient_OnDisconnected;
            _udpClient.OnReceive += UdpClient_OnReceive;
            _udpClient.OnError += UdpClient_OnError;

            _baseUnpacker = (BaseUnpacker)bContext.Unpacker;

        }

        public void Connect()
        {
            _udpClient.Connect();
        }

        public void SendAsync(BaseSocketProtocal protocal)
        {
            _udpClient.SendAsync(protocal.ToBytes());
        }

        private void UdpClient_OnError(string ID, Exception ex)
        {
            Console.WriteLine($"UdpClient_OnError {ID} :" + ex.Message);
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
        }

    }
}
