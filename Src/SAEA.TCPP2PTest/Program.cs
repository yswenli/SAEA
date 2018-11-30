
using SAEA.Common;
using SAEA.TcpP2P;
using System;
using System.Text;

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

                Peer peer = new Peer();
                peer.OnPeerListResponse += Peer_OnPeerListResponse;
                peer.OnP2PSucess += Peer_OnP2PSucess;
                peer.OnMessage += Peer_OnMessage;
                peer.ConnectPeerServer(ipPort);

                ConsoleHelper.WriteLine("输入PeerB地址，例如：180.122.325.21:21541");

                var pIPPort = ConsoleHelper.ReadLine();

                peer.RequestP2p(pIPPort);

                TaskHelper.Start(() =>
                {
                    while (peer.IsConnected)
                    {
                        peer.SendMessage("hello p2p");

                        ThreadHelper.Sleep(1000);
                    }
                });
            }
            else
            {
                P2PServer p2pServer = new P2PServer();
                p2pServer.Start();
                ConsoleHelper.WriteLine("回车关闭测试");
            }

            ConsoleHelper.ReadLine();
        }

        private static void Peer_OnMessage(byte[] obj)
        {
            ConsoleHelper.WriteLine($"收到数据:{Encoding.UTF8.GetString(obj)}");
        }

        private static void Peer_OnP2PSucess(Tuple<string, int> obj)
        {
            ConsoleHelper.WriteLine($"p2p连接成功 ip:{obj.Item1},port:{obj.Item2}");
        }

        private static void Peer_OnPeerListResponse(System.Collections.Generic.List<string> obj)
        {
            ConsoleHelper.WriteLine("收到p2p服务器列表 " + string.Join(",", obj));
        }


    }
}
