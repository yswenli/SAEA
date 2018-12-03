
using SAEA.Common;
using SAEA.TcpP2P;
using SAEA.TcpP2P.Net;
using System.Linq;
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
                peer.OnP2pSucess += Peer_OnP2PSucess;
                peer.OnP2pFailed += Peer_OnP2pFailed;
                peer.OnMessage += Peer_OnMessage;
                peer.OnServerDisconnected += Peer_OnServerDisconnected;
                peer.OnP2pDisconnected += Peer_OnP2pDisconnected;
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

        private static void Peer_OnP2pFailed(string obj)
        {
            ConsoleHelper.WriteLine($"P2p 连接失败:{obj}");
        }

        private static void Peer_OnP2pDisconnected(string ID, System.Exception ex)
        {
            ConsoleHelper.WriteLine($"与P2p 连接已断开:{ex.Message}");
        }

        private static void Peer_OnServerDisconnected(string ID, System.Exception ex)
        {
            ConsoleHelper.WriteLine($"与P2pServer 连接已断开:{ex.Message}");
        }

        private static void Peer_OnMessage(byte[] obj)
        {
            ConsoleHelper.WriteLine($"收到数据:{Encoding.UTF8.GetString(obj)}");
        }

        private static void Peer_OnP2PSucess(NatInfo obj)
        {
            ConsoleHelper.WriteLine($"p2p连接成功 ip:{obj.IP},port:{obj.Port}");
        }

        private static void Peer_OnPeerListResponse(System.Collections.Generic.List<NatInfo> obj)
        {
            ConsoleHelper.WriteLine("收到p2p服务器列表 " + string.Join(",", obj.Select(b => b.ToString())));
        }


    }
}
