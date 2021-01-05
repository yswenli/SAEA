using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using System;
using System.Text;

namespace SAEA.Sockets.UdpTest
{
    class Program
    {
        static object _locker = new object();

        static void Main(string[] args)
        {
            Console.Title = "SAEA.Sockets.UdpTest";

            //udpserver
            UDPServer server = new UDPServer();
            server.OnReceive += Server_OnReceive;
            server.Start();

            //udpclient
            var bContext = new BaseContext();

            UDPClient client = new UDPClient();
            client.OnReceive += Client_OnReceive;
            client.Connect();

            //send msg
            for (int i = 0; i < 10; i++)
            {
                client.SendAsync(BaseSocketProtocal.Parse(Encoding.UTF8.GetBytes($"hello udpserver {i}"), SocketProtocalType.ChatMessage));
            }

            Console.ReadLine();
        }        

        private static void Server_OnReceive(UDPServer arg1, string arg2, ISocketProtocal arg3)
        {
            lock (_locker)
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine($"udp服务器收到消息：{Encoding.UTF8.GetString(arg3.Content)}");
            }

            var msg2 = BaseSocketProtocal.Parse(Encoding.UTF8.GetBytes($"udpserver reply:{Encoding.UTF8.GetString(arg3.Content)}"), SocketProtocalType.ChatMessage);

            arg1.SendAsync(arg2, msg2);
        }

        private static void Client_OnReceive(UDPClient arg1, ISocketProtocal arg2)
        {
            lock (_locker)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"udp客户端收到消息：{Encoding.UTF8.GetString(arg2.Content)}");
            }
        }
    }
}
