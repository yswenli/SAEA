using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;
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
            //udpserver
            _udpServer = SocketFactory.CreateServerSocket(SocketOptionBuilder.Instance.SetSocket(Model.SAEASocketType.Udp)
                .SetIP("127.0.0.1")
                .SetPort(39656)
                .UseIocp<BaseContext>()
                .Build());

            _udpServer.OnDisconnected += UdpServer_OnDisconnected;
            _udpServer.OnError += UdpServer_OnError;
            _udpServer.OnReceive += UdpServer_OnReceive;
            _udpServer.Start();


            //udpclient
            var bContext = new BaseContext();

            var udpClient = SocketFactory.CreateClientSocket(SocketOptionBuilder.Instance.SetSocket(Model.SAEASocketType.Udp)
                .SetIP("127.0.0.1")
                .SetPort(39656)
                .UseIocp(bContext)
                .Build());

            udpClient.OnDisconnected += UdpClient_OnDisconnected;
            udpClient.OnReceive += UdpClient_OnReceive;
            udpClient.OnError += UdpClient_OnError;
            udpClient.Connect();

            _baseUnpacker = (BaseUnpacker)bContext.Unpacker;

            var msg = BaseSocketProtocal.Parse(Encoding.UTF8.GetBytes("hello udpserver"), Model.SocketProtocalType.RequestSend);

            udpClient.SendAsync(msg.ToBytes());

            Console.ReadLine();
        }

        private static void UdpServer_OnReceive(Interface.ISession currentSession, byte[] data)
        {
            var userToken = (IUserToken)currentSession;
            userToken.Unpacker.Unpack(data, (msg) =>
            {
                Console.WriteLine($"udp服务器收到消息：{Encoding.UTF8.GetString(msg.Content)}");

                var msg2 = BaseSocketProtocal.Parse(Encoding.UTF8.GetBytes("hello udpclient"), Model.SocketProtocalType.RequestSend);

                _udpServer.SendAsync(userToken.ID, msg2.ToBytes());
            });
        }

        private static void UdpServer_OnError(string ID, Exception ex)
        {
            throw new NotImplementedException();
        }

        private static void UdpServer_OnDisconnected(string ID, Exception ex)
        {
            Console.WriteLine(ID);
        }

        private static void UdpServer_OnAccepted(object obj)
        {
            Console.WriteLine(obj.ToString());
        }

        private static void UdpClient_OnError(string ID, Exception ex)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }


    }
}
