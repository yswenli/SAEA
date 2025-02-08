using System;
using System.Linq;
using System.Text;
using System.Threading;

using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using SAEA.Sockets.Shortcut;

namespace SAEA.Sockets.UdpTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "SAEA.Sockets.UdpTest";

            //udpserver
            UDPServer server = new UDPServer(39656);
            server.OnReceive += Server_OnReceive;
            server.Start();

            //udpclient
            UDPClient client = new UDPClient("127.0.0.1", 39656);
            client.OnReceive += Client_OnReceive;
            client.Connect();

            //send msg
            for (int i = 1; i <= 100; i++)
            {
                client.SendAsync(Encoding.UTF8.GetBytes($"{i}、hello udpserver"), SocketProtocalType.ChatMessage);
                Thread.Sleep(100);
            }

            client.Disconnect();

            Console.ReadLine();
        }



        private static void Server_OnReceive(UDPServer<BaseUnpacker> arg1, string arg2, ISocketProtocal arg3)
        {
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine($"udp server received a message：{Encoding.UTF8.GetString(arg3.Content)}");


            arg1.SendAsync(arg2, Encoding.UTF8.GetBytes($"udpserver reply:{Encoding.UTF8.GetString(arg3.Content)}"));
        }

        private static void Client_OnReceive(UDPClient<BaseUnpacker> arg1, ISocketProtocal arg2)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"udp client received a message：{Encoding.UTF8.GetString(arg2.Content)}");
        }
    }
}
