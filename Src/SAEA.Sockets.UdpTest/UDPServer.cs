/****************************************************************************
*项目名称：SAEA.Sockets.UdpTest
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Sockets.UdpTest
*类 名 称：UDPServer
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/1/5 11:09:17
*描述：
*=====================================================================
*修改时间：2021/1/5 11:09:17
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
    public class UDPServer
    {
        IServerSocket _udpServer;

        public event Action<UDPServer, string, ISocketProtocal> OnReceive;

        public UDPServer()
        {
            _udpServer = SocketFactory.CreateServerSocket(SocketOptionBuilder.Instance.SetSocket(SAEASocketType.Udp)
                .SetIP("127.0.0.1")
                .SetPort(39656)
                .UseIocp<BaseContext>()
                .SetReadBufferSize(SocketOption.UDPMaxLength)
                .SetWriteBufferSize(SocketOption.UDPMaxLength)
                .SetTimeOut(5000)
                .Build());

            _udpServer.OnAccepted += UdpServer_OnAccepted;
            _udpServer.OnDisconnected += UdpServer_OnDisconnected;
            _udpServer.OnError += UdpServer_OnError;
            _udpServer.OnReceive += UdpServer_OnReceive;
            
        }

        public void Start()
        {
            _udpServer.Start();
        }

        public void SendAsync(string id, BaseSocketProtocal baseSocketProtocal)
        {
            _udpServer.SendAsync(id, baseSocketProtocal.ToBytes());
        }



        private void UdpServer_OnReceive(Interface.ISession currentSession, byte[] data)
        {
            var userToken = (IUserToken)currentSession;
            userToken.Unpacker.Unpack(data, (msg) =>
            {
                OnReceive?.Invoke(this, userToken.ID, msg);
            });
        }

        private void UdpServer_OnError(string ID, Exception ex)
        {
            Console.WriteLine($"UdpServer_OnError: {ID} :" + ex.Message);
        }

        private void UdpServer_OnDisconnected(string ID, Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"UdpServer_OnDisconnected:{ID}");
        }

        private void UdpServer_OnAccepted(object obj)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"UdpServer_OnAccepted:{((IUserToken)obj).ID}");
        }

    }
}
