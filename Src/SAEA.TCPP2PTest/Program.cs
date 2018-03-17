
using SAEA.Commom;
using SAEA.TcpP2P;
using System;

namespace SAEA.TCPP2PTest
{
    class Program
    {
        static void Main(string[] args)
        {
            ConsoleHelper.WriteLine("输入s启动服务器");

            var input = ConsoleHelper.ReadLine();

            if (string.IsNullOrEmpty(input) || input.ToLower() != "s")
            {
                ConsoleHelper.WriteLine("输入服务器地址，例如：180.122.325.21:39654");

                var ipPort = ConsoleHelper.ReadLine();

                Peer peer = new Peer(ipPort);
                peer.OnConnected += Peer_OnConnected;
                peer.OnMessage += Peer_OnMessage;


                ConsoleHelper.WriteLine("输入PeerB地址，例如：180.122.325.21:21541");

                var pIPPort = ConsoleHelper.ReadLine();

                peer.RequestP2P(pIPPort);
            }
            else
            {
                P2PServer p2pServer = new P2PServer();
                p2pServer.Start();
                ConsoleHelper.WriteLine("回车关闭测试");
            }

            ConsoleHelper.ReadLine();
        }
        private static void Peer_OnConnected(Peer peer)
        {
            ConsoleHelper.WriteLine("p2p连接成功！");
            peer.SendMessage("hello world");

        }
        private static void Peer_OnMessage(string msg)
        {
            ConsoleHelper.WriteLine("收到p2p信息:"+ msg);
        }


    }
}
