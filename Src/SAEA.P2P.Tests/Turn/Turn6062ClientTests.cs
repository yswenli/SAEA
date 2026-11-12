/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.P2P.Tests.Turn
*文件名： Turn6062ClientTests
*版本号： net10.0
*唯一标识：aca709a6-5000-415a-93f1-c5045040e66e
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 14:10:00
*描述：TURN over TCP 客户端控制面验收：TCP 中继申请、Connect 与 ConnectionAttempt
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 14:06:33
*修改人： yswenli
*版本号： net10.0
*描述：新增数据面 ConnectionBind 验收：绑定后明文 TCP 双向裸字节收发，以及非 TCP 分配拒绝绑定
*
*****************************************************************************/
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.StunServer;
using SAEA.P2P.Turn;
using SAEA.P2P.TurnServer;

using StunServerHost = SAEA.P2P.StunServer.StunServer;
using TurnServerHost = SAEA.P2P.TurnServer.TurnServer;

namespace SAEA.P2P.Tests.Turn
{
    /// <summary>
    /// TURN over TCP 客户端：按 REQUESTED-TRANSPORT 申请 TCP 中继、Connect 取连接号、
    /// 同一对端重复 Connect 返回 446、UDP 控制连接不能申请 TCP 中继、对端主动连入触发 ConnectionAttempt，
    /// 并在另开的数据连接上完成 ConnectionBind 后双向收发裸字节。
    /// </summary>
    [TestClass]
    public class Turn6062ClientTests
    {
        /// <summary>
        /// 测试统一使用 IPv4 回环地址
        /// </summary>
        private static readonly IPAddress Host = IPAddress.Loopback;

        /// <summary>
        /// 用例共享的共享密钥
        /// </summary>
        private const string SharedSecret = "shared-secret-for-tests";

        /// <summary>
        /// 用例共享的认证域
        /// </summary>
        private const string Realm = "saea.p2p";

        /// <summary>
        /// 等待应答与事件的统一上限
        /// </summary>
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// TCP 中继分配后 Connect 指向的对端应被服务端连上，并返回大于 0 的连接号
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task TurnClient6062_TCP分配_Connect返回连接号()
        {
            var relayPort = ReserveTcpPort();
            using (var harness = Tcp6062Harness.Start(relayPort))
            using (var peerListener = new TcpListener(Host, 0))
            using (var client = new TurnClient(CreateOptions(harness.TcpControlEndPoint, harness.Server, TurnServerHost.TcpTransport)))
            {
                peerListener.Start();
                var peerEndPoint = (IPEndPoint)peerListener.LocalEndpoint;

                var allocation = await client.AllocateAsync(null, CancellationToken.None);
                Assert.IsNotNull(allocation, "TCP 中继分配应成功");
                Assert.IsNotNull(allocation.RelayedEndPoint, "TCP 中继分配应返回中继地址");

                var connectionId = await client.ConnectAsync(peerEndPoint, CancellationToken.None);
                Assert.IsTrue(connectionId > 0, "Connect 应答应携带大于 0 的连接号，实际 " + connectionId);

                using (var accepted = await AcceptWithTimeoutAsync(peerListener, Timeout))
                {
                    Assert.IsNotNull(accepted, "对端应收到服务端发起的中继连接");
                }
            }
        }

        /// <summary>
        /// 同一控制连接上对同一对端重复 Connect 时，第二次返回 446
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task TurnClient6062_重复Connect同一peer_抛446()
        {
            var relayPort = ReserveTcpPort();
            using (var harness = Tcp6062Harness.Start(relayPort))
            using (var peerListener = new TcpListener(Host, 0))
            using (var client = new TurnClient(CreateOptions(harness.TcpControlEndPoint, harness.Server, TurnServerHost.TcpTransport)))
            {
                peerListener.Start();
                var peerEndPoint = (IPEndPoint)peerListener.LocalEndpoint;

                var allocation = await client.AllocateAsync(null, CancellationToken.None);
                Assert.IsNotNull(allocation.RelayedEndPoint, "TCP 中继分配应返回中继地址");

                var connectionId = await client.ConnectAsync(peerEndPoint, CancellationToken.None);
                Assert.IsTrue(connectionId > 0, "第一次 Connect 应返回大于 0 的连接号");

                var error = await Assert.ThrowsExceptionAsync<TurnClientException>(
                    () => client.ConnectAsync(peerEndPoint, CancellationToken.None));
                Assert.AreEqual(
                    TurnErrorCode.ConnectionAlreadyExists,
                    error.ErrorCode,
                    "同一对端已有连接时应返回 446，实际 " + error.ErrorCode);
            }
        }

        /// <summary>
        /// UDP 控制连接申请 TCP 中继承载不了数据连接，客户端在发请求前就应拒绝
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task TurnClient6062_UDP控制连接_申请TCP中继_抛异常()
        {
            var relayPort = ReserveUdpPort();
            using (var harness = await UdpTurnHarness.StartAsync(relayPort))
            using (var client = new TurnClient(new TurnClientOptions
            {
                ServerEndPoint = harness.EndPoint,
                TransportFactory = () => new UdpTurnClientTransport(new IPEndPoint(Host, 0), harness.EndPoint),
                Credential = harness.Server.CredentialService.Create("udp-control-user"),
                RequestedTransportProtocol = TurnServerHost.TcpTransport,
                RtoMilliseconds = 100,
                RefreshTimeout = TimeSpan.FromSeconds(10)
            }))
            {
                var error = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => client.AllocateAsync(null, CancellationToken.None));
                StringAssert.Contains(error.Message, "TCP 中继", "拒绝原因应说明 TCP 中继需要可靠控制连接");
            }
        }

        /// <summary>
        /// 对端直接连入中继端口时，控制连接收到 ConnectionAttempt 并触发事件
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task TurnClient6062_对端主动连入_触发ConnectionAttempt()
        {
            var relayPort = ReserveTcpPort();
            using (var harness = Tcp6062Harness.Start(relayPort))
            using (var client = new TurnClient(CreateOptions(harness.TcpControlEndPoint, harness.Server, TurnServerHost.TcpTransport)))
            {
                var allocation = await client.AllocateAsync(null, CancellationToken.None);
                Assert.IsNotNull(allocation.RelayedEndPoint, "TCP 中继分配应返回中继地址");

                var attempt = new TaskCompletionSource<TurnConnectionAttempt>(TaskCreationOptions.RunContinuationsAsynchronously);
                client.ConnectionAttemptReceived += received => attempt.TrySetResult(received);

                using (var peerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    // 对端主动连入：不先发 Connect，由服务端在中继监听上 accept 后回通知
                    await peerSocket.ConnectAsync(allocation.RelayedEndPoint);

                    var completed = await Task.WhenAny(attempt.Task, Task.Delay(Timeout));
                    Assert.AreSame(attempt.Task, completed, "对端连入中继端口后应触发 ConnectionAttemptReceived");

                    var received = await attempt.Task;
                    Assert.IsTrue(received.ConnectionId > 0, "ConnectionAttempt 应携带大于 0 的连接号");
                    Assert.AreEqual(Host, received.PeerEndPoint.Address, "对端地址应是本机回环地址");
                }
            }
        }

        /// <summary>
        /// ConnectionBind 成功后数据连接只承载业务字节：对端写入的字节原样到达数据连接，反向同样成立
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task TurnClient6062_TCP中继_Bind后双向数据一致()
        {
            const int payloadSize = 1024;
            var relayPort = ReserveTcpPort();
            using (var harness = Tcp6062Harness.Start(relayPort))
            using (var peerListener = new TcpListener(Host, 0))
            using (var client = new TurnClient(CreateOptions(harness.TcpControlEndPoint, harness.Server, TurnServerHost.TcpTransport)))
            {
                peerListener.Start();
                var peerEndPoint = (IPEndPoint)peerListener.LocalEndpoint;

                var allocation = await client.AllocateAsync(null, CancellationToken.None);
                Assert.IsNotNull(allocation.RelayedEndPoint, "TCP 中继分配应返回中继地址");

                var connectionId = await client.ConnectAsync(peerEndPoint, CancellationToken.None);
                Assert.IsTrue(connectionId > 0, "Connect 应答应携带大于 0 的连接号，实际 " + connectionId);

                var peerSocket = await AcceptWithTimeoutAsync(peerListener, Timeout);
                Assert.IsNotNull(peerSocket, "对端应收到服务端发起的中继连接");

                using (peerSocket)
                using (var peerStream = new NetworkStream(peerSocket, false))
                {
                    var data = await client.ConnectionBindAsync(connectionId, CancellationToken.None);
                    Assert.IsNotNull(data, "ConnectionBind 成功应返回数据连接");
                    using (data)
                    {
                        Assert.IsTrue(data.IsConnected, "绑定成功后数据连接应保持连接");

                        // 下行：对端写、数据连接读；不含任何长度前缀，字节必须逐一对齐
                        var fromPeer = new byte[payloadSize];
                        for (var i = 0; i < fromPeer.Length; i++)
                        {
                            fromPeer[i] = 0x11;
                        }
                        await peerStream.WriteAsync(fromPeer, 0, fromPeer.Length, CancellationToken.None);

                        var received = new byte[payloadSize];
                        await data.Stream.ReadExactlyAsync(received, CancellationToken.None);
                        CollectionAssert.AreEqual(fromPeer, received, "数据连接收到的字节应与对端写入内容一致");

                        // 上行：数据连接写、对端读；同样按裸字节中继，不带前缀
                        var fromData = new byte[payloadSize];
                        for (var i = 0; i < fromData.Length; i++)
                        {
                            fromData[i] = 0x22;
                        }
                        await data.Stream.WriteAsync(fromData, 0, fromData.Length, CancellationToken.None);
                        await data.Stream.FlushAsync(CancellationToken.None);

                        var atPeer = new byte[payloadSize];
                        await peerStream.ReadExactlyAsync(atPeer, CancellationToken.None);
                        CollectionAssert.AreEqual(fromData, atPeer, "对端收到的字节应与数据连接写入内容一致");
                    }
                }
            }
        }

        /// <summary>
        /// 非 TCP 中继分配没有数据连接可绑，客户端在建立任何连接前就应拒绝
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task TurnClient6062_非TCP分配_ConnectionBind抛异常()
        {
            // 只有协议号参与判定，这里的端点与传输工厂都不会被用到，因此不需要起服务端
            using (var client = new TurnClient(new TurnClientOptions
            {
                ServerEndPoint = new IPEndPoint(Host, 1),
                TransportFactory = () => new TcpTurnClientTransport(new IPEndPoint(Host, 1)),
                Username = "tcp-relay-user",
                Password = "tcp-relay-password",
                RequestedTransportProtocol = 17,
                RtoMilliseconds = 100
            }))
            {
                var error = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                    () => client.ConnectionBindAsync(1, CancellationToken.None));
                StringAssert.Contains(error.Message, "ConnectionBind", "拒绝原因应说明只有 TCP 中继分配才能绑定数据连接");
            }
        }

        /// <summary>
        /// 按 TCP 控制连接创建客户端配置
        /// </summary>
        /// <param name="controlEndPoint">TCP 控制监听端点</param>
        /// <param name="server">TURN 服务端</param>
        /// <param name="protocol">REQUESTED-TRANSPORT 协议号</param>
        /// <returns>客户端配置</returns>
        private static TurnClientOptions CreateOptions(IPEndPoint controlEndPoint, TurnServerHost server, byte protocol)
        {
            return new TurnClientOptions
            {
                ServerEndPoint = controlEndPoint,
                TransportFactory = () => new TcpTurnClientTransport(controlEndPoint),
                Credential = server.CredentialService.Create("tcp-relay-user"),
                RequestedTransportProtocol = protocol,
                RtoMilliseconds = 100,
                RefreshTimeout = TimeSpan.FromSeconds(10)
            };
        }

        /// <summary>
        /// 接受一条连接；超时返回 null
        /// </summary>
        /// <param name="listener">对端监听</param>
        /// <param name="timeout">等待上限</param>
        /// <returns>已接受的连接</returns>
        private static async Task<Socket> AcceptWithTimeoutAsync(TcpListener listener, TimeSpan timeout)
        {
            using (var cts = new CancellationTokenSource(timeout))
            {
                try
                {
                    return await listener.AcceptSocketAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// 预占一个尚未使用的 TCP 端口：TCP 中继监听按 TCP 绑定，用 UDP 预占的端口不具排他性
        /// </summary>
        /// <returns>端口号</returns>
        private static int ReserveTcpPort()
        {
            var probe = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                probe.Bind(new IPEndPoint(Host, 0));
                return ((IPEndPoint)probe.LocalEndPoint).Port;
            }
            finally
            {
                probe.Dispose();
            }
        }

        /// <summary>
        /// 预占一个尚未使用的 UDP 端口
        /// </summary>
        /// <returns>端口号</returns>
        private static int ReserveUdpPort()
        {
            var probe = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            try
            {
                probe.Bind(new IPEndPoint(Host, 0));
                return ((IPEndPoint)probe.LocalEndPoint).Port;
            }
            finally
            {
                probe.Dispose();
            }
        }

        /// <summary>
        /// TURN 服务端与 TCP 控制监听；中继端口区间收窄到单个端口
        /// </summary>
        private sealed class Tcp6062Harness : IDisposable
        {
            /// <summary>
            /// TCP 控制监听
            /// </summary>
            private readonly TurnTcpListener _listener;

            /// <summary>
            /// 创建宿主
            /// </summary>
            /// <param name="server">TURN 服务端</param>
            /// <param name="listener">已启动的监听</param>
            private Tcp6062Harness(TurnServerHost server, TurnTcpListener listener)
            {
                Server = server;
                _listener = listener;
            }

            /// <summary>
            /// TURN 服务端
            /// </summary>
            public TurnServerHost Server { get; private set; }

            /// <summary>
            /// 控制连接应访问的端点
            /// </summary>
            public IPEndPoint TcpControlEndPoint
            {
                get { return _listener.LocalEndPoint; }
            }

            /// <summary>
            /// 按配置创建并启动服务端与监听
            /// </summary>
            /// <param name="relayPort">中继端口</param>
            /// <returns>宿主</returns>
            public static Tcp6062Harness Start(int relayPort)
            {
                var server = new TurnServerHost(new TurnServerOptions
                {
                    SharedSecret = SharedSecret,
                    Realm = Realm,
                    RelayAddress = Host,
                    RelayPortMin = relayPort,
                    RelayPortMax = relayPort
                });
                var listener = new TurnTcpListener(new IPEndPoint(Host, 0), server);
                listener.Start();
                return new Tcp6062Harness(server, listener);
            }

            /// <summary>
            /// 先停监听再停服务端：监听关闭会断开控制连接，服务端随后清掉剩余分配与中继资源
            /// </summary>
            public void Dispose()
            {
                _listener.Dispose();
                Server.Dispose();
            }
        }

        /// <summary>
        /// TURN 服务端与承接其报文的 UDP STUN 监听端点
        /// </summary>
        private sealed class UdpTurnHarness : IDisposable
        {
            /// <summary>
            /// STUN 监听
            /// </summary>
            private readonly StunServerHost _stun;

            /// <summary>
            /// 创建宿主
            /// </summary>
            /// <param name="stun">已启动的 STUN 服务端</param>
            /// <param name="server">TURN 服务端</param>
            /// <param name="endPoint">客户端应访问的端点</param>
            private UdpTurnHarness(StunServerHost stun, TurnServerHost server, IPEndPoint endPoint)
            {
                _stun = stun;
                Server = server;
                EndPoint = endPoint;
            }

            /// <summary>
            /// TURN 服务端
            /// </summary>
            public TurnServerHost Server { get; private set; }

            /// <summary>
            /// 客户端应访问的端点
            /// </summary>
            public IPEndPoint EndPoint { get; private set; }

            /// <summary>
            /// 按配置创建并启动服务端与 STUN 监听
            /// </summary>
            /// <param name="relayPort">中继端口</param>
            /// <returns>宿主</returns>
            public static async Task<UdpTurnHarness> StartAsync(int relayPort)
            {
                var server = new TurnServerHost(new TurnServerOptions
                {
                    SharedSecret = SharedSecret,
                    Realm = Realm,
                    RelayAddress = Host,
                    RelayPortMin = relayPort,
                    RelayPortMax = relayPort
                });

                var options = new StunServerOptions(new IPEndPoint(Host, 0))
                {
                    RequestHandler = server.HandleRequest,
                    DatagramHandler = server.HandleDatagram
                };
                var stun = new StunServerHost(options);
                await stun.StartAsync();
                return new UdpTurnHarness(stun, server, stun.Endpoints[0].LocalEndPoint);
            }

            /// <summary>
            /// 先停 TURN 服务端再停 STUN 监听
            /// </summary>
            public void Dispose()
            {
                Server.Dispose();
                _stun.Dispose();
            }
        }
    }
}