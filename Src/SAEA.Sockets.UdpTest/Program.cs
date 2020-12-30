using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using System;
using System.Text;

namespace SAEA.Sockets.UdpTest
{
    class Program
    {

        static IServerSokcet _udpServer;

        static BaseUnpacker _baseUnpacker;

        static void Main(string[] args)
        {
            Console.Title = "SAEA.Sockets.UdpTest";

            //udpserver
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
            _udpServer.Start();


            //udpclient
            var bContext = new BaseContext();

            var udpClient = SocketFactory.CreateClientSocket(SocketOptionBuilder.Instance.SetSocket(SAEASocketType.Udp)
                .SetIP("127.0.0.1")
                .SetPort(39656)
                .UseIocp(bContext)
                .SetReadBufferSize(SocketOption.UDPMaxLength)
                .SetWriteBufferSize(SocketOption.UDPMaxLength)
                .Build());

            udpClient.OnDisconnected += UdpClient_OnDisconnected;
            udpClient.OnReceive += UdpClient_OnReceive;
            udpClient.OnError += UdpClient_OnError;
            udpClient.Connect();

            //send msg
            _baseUnpacker = (BaseUnpacker)bContext.Unpacker;

            var msg1 = BaseSocketProtocal.Parse(Encoding.UTF8.GetBytes("hello udpserver"), SocketProtocalType.ChatMessage);

            for (int i = 0; i < 10; i++)
            {
                udpClient.SendAsync(msg1.ToBytes());
            }

            Console.ReadLine();
        }

        private static void UdpServer_OnReceive(Interface.ISession currentSession, byte[] data)
        {
            var userToken = (IUserToken)currentSession;
            userToken.Unpacker.Unpack(data, (msg) =>
            {
                Console.WriteLine($"udp服务器收到消息：{Encoding.UTF8.GetString(msg.Content)}");

                var msg2 = BaseSocketProtocal.Parse(Encoding.UTF8.GetBytes("hello udpclient"), Model.SocketProtocalType.ChatMessage);

                _udpServer.SendAsync(userToken.ID, msg2.ToBytes());
            });
        }

        private static void UdpServer_OnError(string ID, Exception ex)
        {
            Console.WriteLine($"UdpServer_OnError: {ID} :" + ex.Message);
        }

        private static void UdpServer_OnDisconnected(string ID, Exception ex)
        {
            Console.WriteLine($"UdpServer_OnDisconnected:{ID}");
        }

        private static void UdpServer_OnAccepted(object obj)
        {
            Console.WriteLine($"UdpServer_OnAccepted:{((IUserToken)obj).ID}");
        }

        private static void UdpClient_OnError(string ID, Exception ex)
        {
            Console.WriteLine($"UdpClient_OnError {ID} :" + ex.Message);
        }

        private static void UdpClient_OnReceive(byte[] data)
        {
            _baseUnpacker.Unpack(data, (msg) =>
            {
                Console.WriteLine($"udp客户端收到消息：{Encoding.UTF8.GetString(msg.Content)}");
            });
        }

        private static void UdpClient_OnDisconnected(string ID, Exception ex)
        {
            Console.WriteLine($"UdpClient_OnDisconnected {ID} :" + ex.Message);
        }


    }
}
