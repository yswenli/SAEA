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
*文件名： TurnTransportMatrixTests
*版本号： net10.0
*唯一标识：3f7c1a52-8d64-4a5e-9c1b-2e6f0b7d4a13
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 09:00:00
*描述：TURN 三种控制传输（UDP/TCP/TLS）的中继集成矩阵与释放后端口回收验收
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 09:00:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 TurnTransportMatrixTests
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.StunServer;
using SAEA.P2P.Turn;
using SAEA.P2P.TurnServer;
using SAEA.P2P.Transport;

using StunServerHost = SAEA.P2P.StunServer.StunServer;
using TurnClientHost = SAEA.P2P.Turn.TurnClient;
using TurnServerHost = SAEA.P2P.TurnServer.TurnServer;

namespace SAEA.P2P.Tests.Turn
{
    /// <summary>
    /// TURN 控制传输矩阵：同一个高层 <see cref="TurnClientHost"/> 分别跑在 UDP、TCP、TLS 三种控制连接上，
    /// 每格都完成 Allocate、CreatePermission、ChannelBind 与双向 ChannelData 字节校验；
    /// 另有一格验证 LIFETIME=0 释放后中继端口回到可用状态。
    /// </summary>
    [TestClass]
    public class TurnTransportMatrixTests
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
        /// 矩阵统一的通道号
        /// </summary>
        private const ushort ChannelNumber = 0x4000;

        /// <summary>
        /// 测试证书口令；证书只写入临时文件，用完即删，不进入版本库
        /// </summary>
        private const string CertificatePassword = "saea-turn-matrix-test-password";

        /// <summary>
        /// 中继等待上限：单个请求与单个方向的数据等待都用它
        /// </summary>
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// UDP 控制传输：分配后按通道号双向中继，两端收到的字节与发送内容一致。
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task TurnMatrix_UDP控制_双向中继成功()
        {
            var relayPort = ReserveUdpPorts(1)[0];

            MatrixServer server = null;
            Socket peer = null;
            SaeaUdpTransport udp = null;
            UdpTurnClientTransport transport = null;
            TurnClientHost client = null;
            try
            {
                server = await MatrixServer.StartUdpAsync(relayPort);
                peer = CreatePeerSocket();

                udp = new SaeaUdpTransport();
                await udp.BindAsync(new IPEndPoint(Host, 0), CancellationToken.None);

                // 注入已绑定的 UDP 传输：本用例不重连，工厂每次返回同一实例
                var boundTransport = new UdpTurnClientTransport(udp, server.EndPoint);
                transport = boundTransport;
                client = CreateClient(server, () => boundTransport);

                var allocation = await client.AllocateAsync(300, CancellationToken.None);
                Assert.IsNotNull(allocation, "UDP 控制下 Allocate 应成功");
                AssertRelayEndPoint(allocation, relayPort);

                await RelayBidirectionalAsync(client, allocation, peer);
            }
            finally
            {
                if (client != null)
                {
                    client.Dispose();
                }
                if (transport != null)
                {
                    transport.Dispose();
                }
                if (udp != null)
                {
                    udp.Dispose();
                }
                if (peer != null)
                {
                    peer.Dispose();
                }
                if (server != null)
                {
                    server.Dispose();
                }
            }
        }

        /// <summary>
        /// TCP 控制传输：控制连接按 TURN 长度字段分帧，通道数据在可靠传输上补齐后仍双向一致。
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task TurnMatrix_TCP控制_双向中继成功()
        {
            var relayPort = ReserveUdpPorts(1)[0];

            MatrixServer server = null;
            Socket peer = null;
            TurnClientHost client = null;
            try
            {
                server = MatrixServer.StartTcp(relayPort);
                peer = CreatePeerSocket();

                // 每次调用都新建实例：TurnClient 在重连路径上会再次调用工厂
                var serverEndPoint = server.EndPoint;
                client = CreateClient(server, () => new TcpTurnClientTransport(serverEndPoint));

                var allocation = await client.AllocateAsync(300, CancellationToken.None);
                Assert.IsNotNull(allocation, "TCP 控制下 Allocate 应成功");
                AssertRelayEndPoint(allocation, relayPort);

                await RelayBidirectionalAsync(client, allocation, peer);
            }
            finally
            {
                if (client != null)
                {
                    client.Dispose();
                }
                if (peer != null)
                {
                    peer.Dispose();
                }
                if (server != null)
                {
                    server.Dispose();
                }
            }
        }

        /// <summary>
        /// TLS 控制传输：自签证书握手后复用 TCP 的分帧与分派，双向中继内容一致。
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task TurnMatrix_TLS控制_双向中继成功()
        {
            var certificatePath = CreateCertificateFile();
            var relayPort = ReserveUdpPorts(1)[0];

            MatrixServer server = null;
            Socket peer = null;
            TurnClientHost client = null;
            try
            {
                server = MatrixServer.StartTls(relayPort, certificatePath, CertificatePassword);
                peer = CreatePeerSocket();

                // 自签证书不在系统信任链里，构造参数第三位放开证书校验才能握手成功
                var serverEndPoint = server.EndPoint;
                client = CreateClient(server, () => new TurnTlsClientTransport(serverEndPoint, "localhost", true));

                var allocation = await client.AllocateAsync(300, CancellationToken.None);
                Assert.IsNotNull(allocation, "TLS 控制下 Allocate 应成功");
                AssertRelayEndPoint(allocation, relayPort);

                await RelayBidirectionalAsync(client, allocation, peer);
            }
            finally
            {
                if (client != null)
                {
                    client.Dispose();
                }
                if (peer != null)
                {
                    peer.Dispose();
                }
                if (server != null)
                {
                    server.Dispose();
                }
                TryDelete(certificatePath);
            }
        }

        /// <summary>
        /// LIFETIME=0 释放后服务端删除分配、本地分配标记失效，中继端口可被重新绑定。
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task TurnMatrix_释放后_中继端口回收()
        {
            var relayPort = ReserveUdpPorts(1)[0];

            MatrixServer server = null;
            Socket peer = null;
            SaeaUdpTransport udp = null;
            UdpTurnClientTransport transport = null;
            TurnClientHost client = null;
            Socket reclaimed = null;
            try
            {
                server = await MatrixServer.StartUdpAsync(relayPort);
                peer = CreatePeerSocket();

                udp = new SaeaUdpTransport();
                await udp.BindAsync(new IPEndPoint(Host, 0), CancellationToken.None);

                var boundTransport = new UdpTurnClientTransport(udp, server.EndPoint);
                transport = boundTransport;
                client = CreateClient(server, () => boundTransport);

                var allocation = await client.AllocateAsync(300, CancellationToken.None);
                Assert.IsNotNull(allocation, "UDP 控制下 Allocate 应成功");
                AssertRelayEndPoint(allocation, relayPort);

                var peerEndPoint = (IPEndPoint)peer.LocalEndPoint;
                await allocation.CreatePermissionAsync(peerEndPoint, CancellationToken.None);
                await allocation.ChannelBindAsync(ChannelNumber, peerEndPoint, CancellationToken.None);
                Assert.AreEqual(1, server.Server.AllocationCount, "释放前服务端应有一条分配记录");

                await allocation.ReleaseAsync(CancellationToken.None);

                // 释放只把本地分配标记为失效，实例仍由 TurnClient 持有
                Assert.IsNotNull(client.Allocation, "释放后 TurnClient 仍持有分配实例");
                Assert.IsTrue(client.Allocation.IsExpired, "释放后分配应标记为失效");
                Assert.IsTrue(
                    await WaitUntilAsync(() => server.Server.AllocationCount == 0, Timeout),
                    "释放后服务端应删除分配，实际剩余 " + server.Server.AllocationCount);

                // 服务端关闭中继 socket 后才归还端口，绑定按 50ms 轮询重试
                reclaimed = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                var error = await BindWithRetryAsync(reclaimed, relayPort, Timeout);
                Assert.IsNull(error, "释放后中继端口应可再次绑定，实际错误：" + error);
            }
            finally
            {
                if (reclaimed != null)
                {
                    reclaimed.Dispose();
                }
                if (client != null)
                {
                    client.Dispose();
                }
                if (transport != null)
                {
                    transport.Dispose();
                }
                if (udp != null)
                {
                    udp.Dispose();
                }
                if (peer != null)
                {
                    peer.Dispose();
                }
                if (server != null)
                {
                    server.Dispose();
                }
            }
        }

        /// <summary>
        /// 按凭据创建高层客户端；三层刷新间隔拉长到测试窗口之外，避免续期请求与中继校验交叉
        /// </summary>
        /// <param name="server">服务端宿主</param>
        /// <param name="factory">传输工厂</param>
        /// <returns>尚未分配的客户端</returns>
        private static TurnClientHost CreateClient(MatrixServer server, Func<ITurnClientTransport> factory)
        {
            var credential = server.Server.CredentialService.Create("learner");
            var options = new TurnClientOptions
            {
                ServerEndPoint = server.EndPoint,
                TransportFactory = factory,
                Username = credential.Username,
                Password = credential.Password,
                RtoMilliseconds = 100,
                AllocationRefreshInterval = TimeSpan.FromSeconds(30),
                PermissionRefreshInterval = TimeSpan.FromSeconds(30),
                ChannelRefreshInterval = TimeSpan.FromSeconds(30),
                RefreshTimeout = TimeSpan.FromSeconds(5)
            };

            return new TurnClientHost(options);
        }

        /// <summary>
        /// 断言中继地址落在预留的中继端口上
        /// </summary>
        /// <param name="allocation">分配</param>
        /// <param name="relayPort">预留的中继端口</param>
        private static void AssertRelayEndPoint(TurnAllocationClient allocation, int relayPort)
        {
            Assert.IsNotNull(allocation.RelayedEndPoint, "Allocate 应给出中继地址");
            Assert.AreEqual(Host, allocation.RelayedEndPoint.Address, "中继地址应为配置的中继地址");
            Assert.AreEqual(relayPort, allocation.RelayedEndPoint.Port, "中继端口应来自配置区间");
        }

        /// <summary>
        /// 走完权限、通道绑定与双向 ChannelData：客户端到对端单向直收，对端到客户端走事件回调
        /// </summary>
        /// <param name="client">TURN 客户端</param>
        /// <param name="allocation">分配</param>
        /// <param name="peer">对端 socket</param>
        /// <returns>双向校验完成的任务</returns>
        private static async Task RelayBidirectionalAsync(TurnClientHost client, TurnAllocationClient allocation, Socket peer)
        {
            var peerEndPoint = (IPEndPoint)peer.LocalEndPoint;
            await allocation.CreatePermissionAsync(peerEndPoint, CancellationToken.None);
            await allocation.ChannelBindAsync(ChannelNumber, peerEndPoint, CancellationToken.None);

            // 客户端 -> 对端
            var outbound = Encoding.UTF8.GetBytes("turn-matrix-outbound");
            await allocation.SendChannelDataAsync(ChannelNumber, outbound, CancellationToken.None);

            var buffer = new byte[2048];
            int read;
            using (var receiveCts = new CancellationTokenSource(Timeout))
            {
                read = await peer.ReceiveAsync(new Memory<byte>(buffer), SocketFlags.None, receiveCts.Token);
            }

            Assert.AreEqual(outbound.Length, read, "对端收到的字节数应与发送内容一致");
            var atPeer = new byte[read];
            Array.Copy(buffer, atPeer, read);
            CollectionAssert.AreEqual(outbound, atPeer, "对端收到的数据应与发送内容一致");

            // 对端 -> 客户端；事件在传输层线程上触发，必须用可跨线程完成的信号量等待
            ushort observedChannel = 0;
            var inboundReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            Action<ushort, byte[]> handler = (channelNumber, data) =>
            {
                observedChannel = channelNumber;
                inboundReceived.TrySetResult(data);
            };
            client.ChannelDataReceived += handler;
            try
            {
                var inbound = Encoding.UTF8.GetBytes("turn-matrix-inbound");
                peer.SendTo(inbound, allocation.RelayedEndPoint);

                var completed = await Task.WhenAny(inboundReceived.Task, Task.Delay(Timeout));
                Assert.AreSame(inboundReceived.Task, completed, "客户端应收到 ChannelData");
                Assert.AreEqual(ChannelNumber, observedChannel, "通道号应一致");
                CollectionAssert.AreEqual(inbound, await inboundReceived.Task, "ChannelData 负载应与对端发送内容一致");
            }
            finally
            {
                client.ChannelDataReceived -= handler;
            }
        }

        /// <summary>
        /// 创建绑定了回环随机端口的 UDP 对端 socket
        /// </summary>
        /// <returns>已绑定的对端 socket</returns>
        private static Socket CreatePeerSocket()
        {
            var peer = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            peer.Bind(new IPEndPoint(Host, 0));
            return peer;
        }

        /// <summary>
        /// 在超时内重复尝试绑定指定端口；成功返回 null，超时返回最后一次异常
        /// </summary>
        /// <param name="socket">待绑定的 socket</param>
        /// <param name="port">目标端口</param>
        /// <param name="timeout">等待上限</param>
        /// <returns>成功为 null，否则为最后一次错误</returns>
        private static async Task<Exception> BindWithRetryAsync(Socket socket, int port, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            Exception last = null;
            while (true)
            {
                try
                {
                    socket.Bind(new IPEndPoint(Host, port));
                    return null;
                }
                catch (SocketException ex)
                {
                    last = ex;
                }

                if (DateTime.UtcNow >= deadline)
                {
                    return last;
                }

                await Task.Delay(50);
            }
        }

        /// <summary>
        /// 轮询等待条件成立
        /// </summary>
        /// <param name="condition">条件</param>
        /// <param name="timeout">等待上限</param>
        /// <returns>成立返回 true，超时返回 false</returns>
        private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (condition())
                {
                    return true;
                }
                await Task.Delay(20);
            }
            return condition();
        }

        /// <summary>
        /// 运行时生成自签 PFX 并写入临时文件；主题与 targetHost 一样取 localhost
        /// </summary>
        /// <returns>临时证书文件路径</returns>
        private static string CreateCertificateFile()
        {
            using (var rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
                request.CertificateExtensions.Add(new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));

                using (var certificate = request.CreateSelfSigned(
                    DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1)))
                {
                    var path = Path.Combine(Path.GetTempPath(), "saea-turn-matrix-" + Guid.NewGuid().ToString("N") + ".pfx");
                    File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, CertificatePassword));
                    return path;
                }
            }
        }

        /// <summary>
        /// 删除临时证书文件；文件已不存在时忽略
        /// </summary>
        /// <param name="path">证书文件路径</param>
        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        /// <summary>
        /// 预占一个尚未使用的 UDP 端口；持有套接字期间避免与其他用例拿到同一端口
        /// </summary>
        /// <param name="count">端口数量</param>
        /// <returns>端口数组</returns>
        private static int[] ReserveUdpPorts(int count)
        {
            var probes = new List<SaeaUdpTransport>();
            try
            {
                var ports = new int[count];
                for (var i = 0; i < count; i++)
                {
                    var probe = new SaeaUdpTransport();
                    probe.BindAsync(new IPEndPoint(Host, 0), CancellationToken.None).GetAwaiter().GetResult();
                    probes.Add(probe);
                    ports[i] = probe.LocalEndPoint.Port;
                }
                return ports;
            }
            finally
            {
                for (var i = 0; i < probes.Count; i++)
                {
                    probes[i].Dispose();
                }
            }
        }

        /// <summary>
        /// 服务端宿主：UDP 控制用 STUN 监听端点承接 TURN 请求，TCP 与 TLS 控制各起一个对应监听器，
        /// 三者共享同一个 <see cref="TurnServerHost"/> 与中继端口区间
        /// </summary>
        private sealed class MatrixServer : IDisposable
        {
            /// <summary>
            /// UDP 控制时的 STUN 监听端点；TCP 与 TLS 时为 null
            /// </summary>
            private readonly StunServerHost _stun;

            /// <summary>
            /// TCP 或 TLS 控制监听器；UDP 时为 null
            /// </summary>
            private readonly IDisposable _listener;

            /// <summary>
            /// 创建宿主
            /// </summary>
            /// <param name="server">TURN 服务端</param>
            /// <param name="endPoint">客户端应访问的端点</param>
            /// <param name="stun">STUN 监听端点</param>
            /// <param name="listener">TCP 或 TLS 监听器</param>
            private MatrixServer(TurnServerHost server, IPEndPoint endPoint, StunServerHost stun, IDisposable listener)
            {
                Server = server;
                EndPoint = endPoint;
                _stun = stun;
                _listener = listener;
            }

            /// <summary>
            /// TURN 服务端
            /// </summary>
            public TurnServerHost Server { get; private set; }

            /// <summary>
            /// 客户端应访问的控制端点
            /// </summary>
            public IPEndPoint EndPoint { get; private set; }

            /// <summary>
            /// 起 UDP 控制：STUN 监听随机端口，把 TURN 请求与非 STUN 数据报都交给同一个服务端
            /// </summary>
            /// <param name="relayPort">中继端口</param>
            /// <returns>宿主</returns>
            public static async Task<MatrixServer> StartUdpAsync(int relayPort)
            {
                var server = CreateServer(relayPort);
                var stunOptions = new StunServerOptions(new IPEndPoint(Host, 0))
                {
                    RequestHandler = server.HandleRequest,
                    DatagramHandler = server.HandleDatagram
                };
                var stun = new StunServerHost(stunOptions);
                await stun.StartAsync();
                return new MatrixServer(server, stun.Endpoints[0].LocalEndPoint, stun, null);
            }

            /// <summary>
            /// 起 TCP 控制监听
            /// </summary>
            /// <param name="relayPort">中继端口</param>
            /// <returns>宿主</returns>
            public static MatrixServer StartTcp(int relayPort)
            {
                var server = CreateServer(relayPort);
                var listener = new TurnTcpListener(new IPEndPoint(Host, 0), server);
                listener.Start();
                return new MatrixServer(server, listener.LocalEndPoint, null, listener);
            }

            /// <summary>
            /// 起 TLS 控制监听
            /// </summary>
            /// <param name="relayPort">中继端口</param>
            /// <param name="certificatePath">PFX 证书路径</param>
            /// <param name="certificatePassword">证书口令</param>
            /// <returns>宿主</returns>
            public static MatrixServer StartTls(int relayPort, string certificatePath, string certificatePassword)
            {
                var server = CreateServer(relayPort);
                var listener = new TurnTlsListener(new IPEndPoint(Host, 0), server, certificatePath, certificatePassword);
                listener.Start();
                return new MatrixServer(server, listener.LocalEndPoint, null, listener);
            }

            /// <summary>
            /// 按测试凭据创建服务端配置；中继端口区间收窄到单个端口，便于断言与回收校验
            /// </summary>
            /// <param name="relayPort">中继端口</param>
            /// <returns>TURN 服务端</returns>
            private static TurnServerHost CreateServer(int relayPort)
            {
                return new TurnServerHost(new TurnServerOptions
                {
                    SharedSecret = SharedSecret,
                    Realm = Realm,
                    RelayAddress = Host,
                    RelayPortMin = relayPort,
                    RelayPortMax = relayPort
                });
            }

            /// <summary>
            /// 先释放监听端点再释放服务端：监听关闭时才会回收其控制连接上的分配
            /// </summary>
            public void Dispose()
            {
                if (_listener != null)
                {
                    _listener.Dispose();
                }

                if (_stun != null)
                {
                    _stun.Dispose();
                }

                if (Server != null)
                {
                    Server.Dispose();
                }
            }
        }
    }
}