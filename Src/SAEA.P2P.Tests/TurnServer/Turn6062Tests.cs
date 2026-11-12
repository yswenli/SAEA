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
*命名空间：SAEA.P2P.Tests.TurnServer
*文件名： Turn6062Tests
*版本号： net10.0
*唯一标识：e66136e3-d9d3-4014-8090-b33628ce10e0
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 06:00:00
*描述：TURN over TCP 的 Connect 与 ConnectionBind 验收
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 13:54:41
*修改人： yswenli
*版本号： net10.0
*描述：新增对端在 ConnectionBind 之前写入数据时 Bind 应答先于数据面字节的回归用例
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

using SAEA.P2P.Stun;
using SAEA.P2P.Turn;
using SAEA.P2P.TurnServer;

using TurnServerHost = SAEA.P2P.TurnServer.TurnServer;
using TurnTcpListenerHost = SAEA.P2P.TurnServer.TurnTcpListener;

namespace SAEA.P2P.Tests.TurnServer
{
    /// <summary>
    /// TURN over TCP 的 Connect 与 ConnectionBind：Connect 成功后另开一条连接绑定为数据连接并双向中继，
    /// 同一对端重复 Connect 返回 446，等待 ConnectionBind 超时的中继连接由服务端关闭；
    /// 对端主动连入中继端口时控制连接收到 ConnectionAttempt，TLS 分配下的明文数据连接按加密层不一致回 403。
    /// </summary>
    [TestClass]
    public class Turn6062Tests
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
        /// 测试证书口令；证书只写入临时文件，用完即删，不进入版本库
        /// </summary>
        private const string CertificatePassword = "saea-turn-tls-test-password";

        /// <summary>
        /// 单次请求与中继等待上限
        /// </summary>
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// TCP 中继分配成功后 Connect 指向的对端可另开一条连接绑定为数据连接，双向字节一致
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task Turn6062_Connect后另开连接Binding_双向数据一致()
        {
            var relayPort = ReserveTcpPort();
            using (var harness = Turn6062Harness.Start(relayPort))
            using (var peer = new TcpPeer(true))
            using (var client = await Turn6062Client.ConnectAsync(harness.TcpControlEndPoint))
            {
                var allocation = await AllocateAsync(harness.Server, client, relayPort);

                var connectResponse = await client.ExchangeAsync(
                    Sign(TurnMessage.CreateConnect(peer.EndPoint), allocation.Credential, allocation.Nonce),
                    Timeout);
                Assert.IsNotNull(connectResponse, "Connect 应得到应答");
                Assert.AreEqual(StunClass.SuccessResponse, connectResponse.Class, "Connect 应成功");

                uint connectionId;
                Assert.IsTrue(TurnMessage.TryGetConnectionId(connectResponse, out connectionId), "Connect 应答应携带 CONNECTION-ID");

                // 数据连接用裸 socket：接管后这条连接只承载应用字节，不能再按 TURN 帧解复用
                using (var dataSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    await dataSocket.ConnectAsync(harness.TcpControlEndPoint);
                    await BindDataConnectionAsync(dataSocket, allocation.Credential, connectionId, Timeout);

                    var payload = new byte[64];
                    for (var i = 0; i < payload.Length; i++)
                    {
                        payload[i] = (byte)(i + 1);
                    }

                    await dataSocket.SendAsync(new ArraySegment<byte>(payload), SocketFlags.None);
                    var echoed = await ReadExactlyAsync(dataSocket, payload.Length, Timeout);
                    CollectionAssert.AreEqual(payload, echoed, "数据连接收到的字节应与经对端回显的字节一致");
                }
            }
        }

        /// <summary>
        /// 同一控制连接上对同一对端重复 Connect 时，第二次返回 446
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task Turn6062_重复Connect同一peer_返回446()
        {
            var relayPort = ReserveTcpPort();
            using (var harness = Turn6062Harness.Start(relayPort))
            using (var peer = new TcpPeer(true))
            using (var client = await Turn6062Client.ConnectAsync(harness.TcpControlEndPoint))
            {
                var allocation = await AllocateAsync(harness.Server, client, relayPort);

                var first = await client.ExchangeAsync(
                    Sign(TurnMessage.CreateConnect(peer.EndPoint), allocation.Credential, allocation.Nonce),
                    Timeout);
                Assert.IsNotNull(first, "第一次 Connect 应得到应答");
                Assert.AreEqual(StunClass.SuccessResponse, first.Class, "第一次 Connect 应成功");

                uint connectionId;
                Assert.IsTrue(TurnMessage.TryGetConnectionId(first, out connectionId), "第一次 Connect 应答应携带 CONNECTION-ID");

                var second = await client.ExchangeAsync(
                    Sign(TurnMessage.CreateConnect(peer.EndPoint), allocation.Credential, allocation.Nonce),
                    Timeout);
                Assert.IsNotNull(second, "重复 Connect 也应得到应答");
                Assert.AreEqual(StunClass.ErrorResponse, second.Class, "同一对端重复 Connect 应被拒绝");

                int code;
                string reason;
                Assert.IsTrue(StunErrorCode.TryGet(second, out code, out reason), "重复 Connect 的应答应携带 ERROR-CODE");
                Assert.AreEqual(446, code, "同一对端已有连接时应返回 446");
            }
        }

        /// <summary>
        /// 中继连接超过 ConnectionBind 超时仍未绑定，服务端关闭这条对端连接
        /// </summary>
        [TestMethod]
        [Timeout(40000)]
        public async Task Turn6062_30秒未Bind_连接关闭()
        {
            // 产品默认值是 30 秒，用例不可能等这么久；这里把同一项配置压到 400ms，
            // 走的是 TurnTcpRelay 的同一条清理路径，等价验证超时后关闭未绑定连接的行为
            var bindTimeout = TimeSpan.FromMilliseconds(400);

            var relayPort = ReserveTcpPort();
            using (var harness = Turn6062Harness.Start(relayPort, bindTimeout))
            using (var peer = new TcpPeer(false))
            using (var client = await Turn6062Client.ConnectAsync(harness.TcpControlEndPoint))
            {
                var allocation = await AllocateAsync(harness.Server, client, relayPort);

                var connectResponse = await client.ExchangeAsync(
                    Sign(TurnMessage.CreateConnect(peer.EndPoint), allocation.Credential, allocation.Nonce),
                    Timeout);
                Assert.IsNotNull(connectResponse, "Connect 应得到应答");
                Assert.AreEqual(StunClass.SuccessResponse, connectResponse.Class, "Connect 应成功");

                var accepted = await WaitForPeerAsync(peer, Timeout);
                Assert.IsNotNull(accepted, "对端应收到服务端发起的中继连接");

                // 对端一直不发数据，只在拿到报文或连接关闭时返回；超时说明服务端没关连接
                accepted.ReceiveTimeout = 3000;
                var buffer = new byte[16];
                var closed = false;
                try
                {
                    closed = accepted.Receive(buffer) <= 0;
                }
                catch (SocketException ex)
                {
                    // 服务端强制关闭时对端看到的是连接重置，与读到 0 等价；读超时不算关闭
                    closed = ex.SocketErrorCode != SocketError.TimedOut;
                }

                Assert.IsTrue(closed, "超过 ConnectionBindTimeout 仍未绑定，服务端应关闭这条中继连接");
                Assert.AreEqual(1, harness.Server.AllocationCount, "关闭未绑定连接不应连带删除分配");
            }
        }

        /// <summary>
        /// 对端直接连入中继端口时，控制连接应收到 ConnectionAttempt，
        /// 凭其中的连接 ID 另开数据连接绑定后双向字节一致
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task Turn6062_对端主动连入_收到ConnectionAttempt()
        {
            var relayPort = ReserveTcpPort();
            using (var harness = Turn6062Harness.Start(relayPort))
            using (var client = await Turn6062Client.ConnectAsync(harness.TcpControlEndPoint))
            {
                var allocation = await AllocateAsync(harness.Server, client, relayPort);

                // 对端主动连入：不先发 Connect，由服务端在中继监听上 accept 后回通知
                using (var peerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    await peerSocket.ConnectAsync(allocation.RelayedEndPoint);
                    var peerLocal = (IPEndPoint)peerSocket.LocalEndPoint;

                    // 控制连接上可能先到别的帧，按方法与类别过滤，不依赖到达顺序
                    var attempt = await client.WaitForMessageAsync(
                        message => message.Method == StunMessageType.ConnectionAttempt
                            && message.Class == StunClass.Indication,
                        Timeout);
                    Assert.IsNotNull(attempt, "对端连入中继端口后控制连接应收到 ConnectionAttempt");

                    uint connectionId;
                    Assert.IsTrue(TurnMessage.TryGetConnectionId(attempt, out connectionId),
                        "ConnectionAttempt 应携带 CONNECTION-ID");

                    IPEndPoint peerFromAttempt;
                    Assert.IsTrue(TurnMessage.TryGetPeerAddress(attempt, out peerFromAttempt),
                        "ConnectionAttempt 应携带 XOR-PEER-ADDRESS");
                    Assert.AreEqual(peerLocal, peerFromAttempt, "指示里的对端地址应是发起连入的 socket 本地端点");

                    using (var dataSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                    {
                        await dataSocket.ConnectAsync(harness.TcpControlEndPoint);
                        await BindDataConnectionAsync(dataSocket, allocation.Credential, connectionId, Timeout);

                        var payload = new byte[64];
                        for (var i = 0; i < payload.Length; i++)
                        {
                            payload[i] = (byte)(0xA0 + i);
                        }

                        // 客户端到对端方向
                        await dataSocket.SendAsync(new ArraySegment<byte>(payload), SocketFlags.None);
                        var atPeer = await ReadExactlyAsync(peerSocket, payload.Length, Timeout);
                        CollectionAssert.AreEqual(payload, atPeer, "对端收到的字节应与数据连接发送内容一致");

                        // 对端到客户端方向：对端原样回写，服务端再泵回数据连接
                        await peerSocket.SendAsync(new ArraySegment<byte>(atPeer), SocketFlags.None);
                        var echoed = await ReadExactlyAsync(dataSocket, payload.Length, Timeout);
                        CollectionAssert.AreEqual(payload, echoed, "数据连接收到的字节应与对端回写内容一致");
                    }
                }
            }
        }

        /// <summary>
        /// TLS 分配下用明文数据连接做 ConnectionBind 时，加密层与控制连接不一致，服务端回 403
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task Turn6062_TLS控制连接_明文数据连接被拒()
        {
            var certificatePath = CreateCertificateFile();
            var relayPort = ReserveTcpPort();
            var options = new TurnServerOptions
            {
                SharedSecret = SharedSecret,
                Realm = Realm,
                RelayAddress = Host,
                RelayPortMin = relayPort,
                RelayPortMax = relayPort
            };

            try
            {
                // 明文连接穿不过 TLS 监听：BouncyCastle 握手阶段就把非 TLS 记录判为失败并断开，
                // 报文到不了会话循环。所以明文数据连接走同一服务端下的明文监听，分配与连接表仍共用
                using (var server = new TurnServerHost(options))
                using (var tlsListener = new TurnTlsListener(new IPEndPoint(Host, 0), server, certificatePath, CertificatePassword))
                using (var plainListener = new TurnTcpListenerHost(new IPEndPoint(Host, 0), server))
                using (var peer = new TcpPeer(false))
                {
                    tlsListener.Start();
                    plainListener.Start();

                    using (var client = await Turn6062Client.ConnectTlsAsync(tlsListener.LocalEndPoint))
                    {
                        var allocation = await AllocateAsync(server, client, relayPort);

                        var connectResponse = await client.ExchangeAsync(
                            Sign(TurnMessage.CreateConnect(peer.EndPoint), allocation.Credential, allocation.Nonce),
                            Timeout);
                        Assert.IsNotNull(connectResponse, "Connect 应得到应答");
                        Assert.AreEqual(StunClass.SuccessResponse, connectResponse.Class, "TLS 控制连接上的 Connect 应成功");

                        uint connectionId;
                        Assert.IsTrue(TurnMessage.TryGetConnectionId(connectResponse, out connectionId),
                            "Connect 应答应携带 CONNECTION-ID");

                        using (var dataSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                        {
                            await dataSocket.ConnectAsync(plainListener.LocalEndPoint);

                            // 数据连接是另一条流，源端口与控制连接不同，nonce 与源端点绑定，只能走本条连接的挑战
                            var challenge = await SendAndReadResponseAsync(
                                dataSocket, TurnMessage.CreateConnectionBind(connectionId), Timeout);
                            int code;
                            string reason;
                            Assert.IsTrue(StunErrorCode.TryGet(challenge, out code, out reason),
                                "数据连接首次绑定应得到带 ERROR-CODE 的挑战");
                            Assert.AreEqual(401, code, "数据连接首次绑定应返回 401 挑战");

                            var nonce = ReadString(challenge, StunAttributeType.Nonce);
                            Assert.IsFalse(string.IsNullOrEmpty(nonce), "数据连接的挑战必须带 NONCE");

                            var rejected = await SendAndReadResponseAsync(
                                dataSocket,
                                Sign(TurnMessage.CreateConnectionBind(connectionId), allocation.Credential, nonce),
                                Timeout);
                            Assert.AreEqual(StunClass.ErrorResponse, rejected.Class, "明文数据连接不得接管 TLS 分配");
                            Assert.IsTrue(StunErrorCode.TryGet(rejected, out code, out reason),
                                "拒绝应答应携带 ERROR-CODE");
                            Assert.AreEqual(403, code, "加密层与控制连接不一致时应返回 403");
                        }
                    }
                }
            }
            finally
            {
                TryDelete(certificatePath);
            }
        }

        /// <summary>
        /// 对端在 ConnectionBind 之前就写入 64KB，客户端必须先完整读出 Bind 应答，再读满对端写入的字节
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task Turn6062_对端连入后立即写数据_Bind应答先于数据()
        {
            var relayPort = ReserveTcpPort();
            using (var harness = Turn6062Harness.Start(relayPort))
            using (var peer = new TcpPeer(false))
            using (var client = await Turn6062Client.ConnectAsync(harness.TcpControlEndPoint))
            {
                var allocation = await AllocateAsync(harness.Server, client, relayPort);

                var connectResponse = await client.ExchangeAsync(
                    Sign(TurnMessage.CreateConnect(peer.EndPoint), allocation.Credential, allocation.Nonce),
                    Timeout);
                Assert.IsNotNull(connectResponse, "Connect 应得到应答");
                Assert.AreEqual(StunClass.SuccessResponse, connectResponse.Class, "Connect 应成功");

                uint connectionId;
                Assert.IsTrue(TurnMessage.TryGetConnectionId(connectResponse, out connectionId),
                    "Connect 应答应携带 CONNECTION-ID");

                var accepted = await WaitForPeerAsync(peer, Timeout);
                Assert.IsNotNull(accepted, "对端应收到服务端发起的中继连接");

                // 此刻还没有数据连接，中继不会读这条对端连接：先把数据面字节压进 peer 到服务端的方向，
                // Bind 成功时这些字节一定早于应答路径发出
                var payload = new byte[64 * 1024];
                for (var i = 0; i < payload.Length; i++)
                {
                    payload[i] = 0x5A;
                }

                // 中继在 Bind 之前不读这条连接，放大发送缓冲才能让整段写入立即完成而不必等接收窗口
                accepted.SendBufferSize = payload.Length * 4;
                await SendAllAsync(accepted, payload, Timeout);

                using (var dataSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    await dataSocket.ConnectAsync(harness.TcpControlEndPoint);

                    // 数据连接是另开的一条流，源端口与控制连接不同；nonce 与源端点绑定，
                    // 控制连接上取到的 nonce 在这里必然失效，必须按本条连接的 401 挑战换新的再重签
                    var bindRequest = TurnMessage.CreateConnectionBind(connectionId);
                    var challenge = await SendAndReadResponseAsync(dataSocket, bindRequest, Timeout);

                    if (challenge.Class != StunClass.SuccessResponse)
                    {
                        int code;
                        string reason;
                        Assert.IsTrue(StunErrorCode.TryGet(challenge, out code, out reason),
                            "数据连接的拒绝应答应携带 ERROR-CODE");
                        Assert.AreEqual(401, code, "数据连接首次绑定应返回 401 挑战");

                        var nonce = ReadString(challenge, StunAttributeType.Nonce);
                        Assert.IsFalse(string.IsNullOrEmpty(nonce), "数据连接的挑战必须带 NONCE");

                        bindRequest = Sign(bindRequest, allocation.Credential, nonce);
                    }

                    // 应答按 STUN 头精确读取 20 + length 字节，多出来的字节必须留在流里留给数据面
                    var transactionId = bindRequest.TransactionId.ToArray();
                    var bindResponse = await SendAndReadResponseAsync(dataSocket, bindRequest, Timeout);
                    Assert.AreEqual(StunClass.SuccessResponse, bindResponse.Class, "ConnectionBind 应成功");
                    Assert.AreEqual(StunMessageType.ConnectionBind, bindResponse.Method, "应答方法应为 ConnectionBind");
                    Assert.IsTrue(HasTransactionId(bindResponse, transactionId), "Bind 应答的事务 ID 应与请求一致");

                    var relayed = await ReadExactlyAsync(dataSocket, payload.Length, Timeout);
                    CollectionAssert.AreEqual(payload, relayed, "Bind 应答之后读到的字节应全部来自对端写入的数据");
                }
            }
        }

        /// <summary>
        /// 完成一次带鉴权的 TCP 中继 Allocate 并返回凭据与中继地址
        /// </summary>
        /// <param name="server">TURN 服务端</param>
        /// <param name="client">控制连接客户端</param>
        /// <param name="expectedPort">配置区间唯一允许的中继端口</param>
        /// <returns>本次分配的凭据、nonce 与中继地址</returns>
        private static async Task<Turn6062Allocation> AllocateAsync(TurnServerHost server, Turn6062Client client, int expectedPort)
        {
            var credential = server.CredentialService.Create("learner");
            var nonce = await GetNonceAsync(client);

            var allocate = Sign(TurnMessage.CreateAllocate(TurnServerHost.TcpTransport), credential, nonce);
            var response = await client.ExchangeAsync(allocate, Timeout);

            Assert.IsNotNull(response, "Allocate 应得到应答");
            Assert.AreEqual(StunClass.SuccessResponse, response.Class, "带鉴权的 TCP 中继 Allocate 应成功");

            IPEndPoint relayed;
            Assert.IsTrue(TurnMessage.TryGetRelayedAddress(response, out relayed), "Allocate 应答应携带 XOR-RELAYED-ADDRESS");
            Assert.AreEqual(Host, relayed.Address, "中继地址应为配置的中继地址");
            Assert.AreEqual(expectedPort, relayed.Port, "中继端口应来自配置区间");

            return new Turn6062Allocation(credential, nonce, relayed);
        }

        /// <summary>
        /// 先发无凭据的 Allocate 拿到 401，再取出 NONCE
        /// </summary>
        /// <param name="client">控制连接客户端</param>
        /// <returns>nonce</returns>
        private static async Task<string> GetNonceAsync(Turn6062Client client)
        {
            var challenge = await client.ExchangeAsync(TurnMessage.CreateAllocate(TurnServerHost.TcpTransport), Timeout);

            Assert.IsNotNull(challenge, "无凭据请求应得到挑战应答");
            int code;
            string reason;
            Assert.IsTrue(StunErrorCode.TryGet(challenge, out code, out reason), "挑战应携带 ERROR-CODE");
            Assert.AreEqual(401, code, "无凭据请求应返回 401");

            var nonce = ReadString(challenge, StunAttributeType.Nonce);
            Assert.IsFalse(string.IsNullOrEmpty(nonce), "挑战必须带 NONCE");
            return nonce;
        }

        /// <summary>
        /// 给请求追加鉴权属性并用长期密钥签名
        /// </summary>
        /// <param name="request">请求</param>
        /// <param name="credential">临时凭据</param>
        /// <param name="nonce">nonce</param>
        /// <returns>已签名的请求</returns>
        private static StunMessage Sign(StunMessage request, TurnCredential credential, string nonce)
        {
            var attributes = new List<StunAttribute>(request.Attributes)
            {
                new StunAttribute(StunAttributeType.Username, Encoding.UTF8.GetBytes(credential.Username)),
                new StunAttribute(StunAttributeType.Realm, Encoding.UTF8.GetBytes(credential.Realm)),
                new StunAttribute(StunAttributeType.Nonce, Encoding.UTF8.GetBytes(nonce))
            };
            request.SetAttributes(attributes);
            StunIntegrity.Append(request, StunIntegrity.CreateLongTermKey(credential.Username, credential.Realm, credential.Password));
            return request;
        }

        /// <summary>
        /// 在另开的数据连接上完成 ConnectionBind：先按 401 换 nonce 重签，成功后该连接转为裸字节通道
        /// </summary>
        /// <param name="socket">数据连接</param>
        /// <param name="credential">临时凭据</param>
        /// <param name="connectionId">连接 ID</param>
        /// <param name="timeout">单次应答等待上限</param>
        /// <returns>绑定完成的任务</returns>
        private static async Task BindDataConnectionAsync(Socket socket, TurnCredential credential, uint connectionId, TimeSpan timeout)
        {
            // 数据连接是另开的一条流，源端口与控制连接不同；nonce 与源地址（含端口）绑定，
            // 控制连接上取到的 nonce 在这里必然失效，必须按本条连接的 401 挑战换新的再重签
            var response = await SendAndReadResponseAsync(socket, TurnMessage.CreateConnectionBind(connectionId), timeout);

            if (response.Class != StunClass.SuccessResponse)
            {
                int code;
                string reason;
                Assert.IsTrue(StunErrorCode.TryGet(response, out code, out reason), "数据连接的拒绝应答应携带 ERROR-CODE");
                Assert.AreEqual(401, code, "数据连接首次绑定应返回 401 挑战");

                var nonce = ReadString(response, StunAttributeType.Nonce);
                Assert.IsFalse(string.IsNullOrEmpty(nonce), "数据连接的挑战必须带 NONCE");

                var signed = Sign(TurnMessage.CreateConnectionBind(connectionId), credential, nonce);
                response = await SendAndReadResponseAsync(socket, signed, timeout);
            }

            Assert.AreEqual(StunClass.SuccessResponse, response.Class, "ConnectionBind 应成功");
            Assert.AreEqual(StunMessageType.ConnectionBind, response.Method, "应答方法应为 ConnectionBind");
        }

        /// <summary>
        /// 发送一整条 STUN 报文，再按 20 字节头加长度字段读回一条完整应答
        /// </summary>
        /// <param name="socket">数据连接</param>
        /// <param name="request">请求</param>
        /// <param name="timeout">读取上限</param>
        /// <returns>解析出的应答</returns>
        private static async Task<StunMessage> SendAndReadResponseAsync(Socket socket, StunMessage request, TimeSpan timeout)
        {
            await socket.SendAsync(new ArraySegment<byte>(request.ToArray()), SocketFlags.None).ConfigureAwait(false);

            var header = await ReadExactlyAsync(socket, StunConstants.HeaderLength, timeout).ConfigureAwait(false);
            var bodyLength = (header[2] << 8) | header[3];
            var data = new byte[StunConstants.HeaderLength + bodyLength];
            Buffer.BlockCopy(header, 0, data, 0, header.Length);

            if (bodyLength > 0)
            {
                var body = await ReadExactlyAsync(socket, bodyLength, timeout).ConfigureAwait(false);
                Buffer.BlockCopy(body, 0, data, StunConstants.HeaderLength, bodyLength);
            }

            StunMessage message;
            Assert.IsTrue(TryParseStun(data, out message), "数据连接的应答应是合法的 STUN 报文");
            return message;
        }

        /// <summary>
        /// 从连接上读满指定字节数
        /// </summary>
        /// <param name="socket">连接</param>
        /// <param name="count">字节数</param>
        /// <param name="timeout">读取上限</param>
        /// <returns>读到的字节</returns>
        /// <exception cref="TimeoutException">超时或对端提前关闭</exception>
        private static async Task<byte[]> ReadExactlyAsync(Socket socket, int count, TimeSpan timeout)
        {
            var buffer = new byte[count];
            var offset = 0;
            using (var cts = new CancellationTokenSource(timeout))
            {
                while (offset < count)
                {
                    int read;
                    try
                    {
                        read = await socket.ReceiveAsync(
                            new ArraySegment<byte>(buffer, offset, count - offset),
                            SocketFlags.None,
                            cts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw new TimeoutException("等待数据连接应答超时");
                    }

                    if (read <= 0)
                    {
                        throw new TimeoutException("数据连接已被提前关闭");
                    }

                    offset += read;
                }
            }
            return buffer;
        }

        /// <summary>
        /// 把整段字节全部写入连接
        /// </summary>
        /// <param name="socket">连接</param>
        /// <param name="data">待写入内容</param>
        /// <param name="timeout">写入上限</param>
        /// <returns>写入完成的任务</returns>
        /// <exception cref="TimeoutException">超时或连接已关闭</exception>
        private static async Task SendAllAsync(Socket socket, byte[] data, TimeSpan timeout)
        {
            var offset = 0;
            using (var cts = new CancellationTokenSource(timeout))
            {
                while (offset < data.Length)
                {
                    int sent;
                    try
                    {
                        sent = await socket.SendAsync(
                            new ArraySegment<byte>(data, offset, data.Length - offset),
                            SocketFlags.None,
                            cts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw new TimeoutException("写入对端数据超时");
                    }

                    if (sent <= 0)
                    {
                        throw new TimeoutException("连接已被提前关闭");
                    }

                    offset += sent;
                }
            }
        }

        /// <summary>
        /// 等待对端接受一条连接；超时返回 null
        /// </summary>
        /// <param name="peer">对端宿主</param>
        /// <param name="timeout">等待上限</param>
        /// <returns>已接受的对端 socket</returns>
        private static async Task<Socket> WaitForPeerAsync(TcpPeer peer, TimeSpan timeout)
        {
            var completed = await Task.WhenAny(peer.Accepted, Task.Delay(timeout)).ConfigureAwait(false);
            if (completed != peer.Accepted)
            {
                return null;
            }

            return await peer.Accepted.ConfigureAwait(false);
        }

        /// <summary>
        /// 解析 STUN 报文；Span 形态的入口不能出现在 async 方法里，因此单独收成同步助手
        /// </summary>
        /// <param name="data">完整报文</param>
        /// <param name="message">解析结果</param>
        /// <returns>解析成功返回 true</returns>
        private static bool TryParseStun(byte[] data, out StunMessage message)
        {
            StunParseError error;
            return StunMessage.TryParse(data, out message, out error);
        }

        /// <summary>
        /// 判断报文是否为指定事务的 STUN 消息
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="transactionId">事务 ID</param>
        /// <returns>相同返回 true</returns>
        private static bool HasTransactionId(StunMessage message, byte[] transactionId)
        {
            var actual = message.TransactionId.ToArray();
            if (actual.Length != transactionId.Length)
            {
                return false;
            }

            for (var i = 0; i < actual.Length; i++)
            {
                if (actual[i] != transactionId[i])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 读取字符串属性；缺失时返回 null
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="type">属性类型</param>
        /// <returns>UTF-8 字符串</returns>
        private static string ReadString(StunMessage message, StunAttributeType type)
        {
            StunAttribute attribute;
            if (!message.TryGet(type, out attribute))
            {
                return null;
            }

            return Encoding.UTF8.GetString(attribute.Value.ToArray());
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
        /// 运行时生成自签 PFX 并写入临时文件；同名主题与口令只用于测试
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
                    var path = Path.Combine(Path.GetTempPath(), "saea-turn-tls-" + Guid.NewGuid().ToString("N") + ".pfx");
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
        /// 一次 TCP 中继分配：凭据、nonce 与中继地址
        /// </summary>
        private sealed class Turn6062Allocation
        {
            /// <summary>
            /// 创建分配记录
            /// </summary>
            /// <param name="credential">临时凭据</param>
            /// <param name="nonce">签发时的 nonce</param>
            /// <param name="relayedEndPoint">中继地址</param>
            public Turn6062Allocation(TurnCredential credential, string nonce, IPEndPoint relayedEndPoint)
            {
                Credential = credential;
                Nonce = nonce;
                RelayedEndPoint = relayedEndPoint;
            }

            /// <summary>
            /// 临时凭据
            /// </summary>
            public TurnCredential Credential { get; private set; }

            /// <summary>
            /// 控制连接上取到的 nonce
            /// </summary>
            public string Nonce { get; private set; }

            /// <summary>
            /// 中继地址
            /// </summary>
            public IPEndPoint RelayedEndPoint { get; private set; }
        }

        /// <summary>
        /// TURN 服务端与 TCP 控制连接监听的组合宿主；中继端口区间收窄到单个端口，便于断言中继地址
        /// </summary>
        private sealed class Turn6062Harness : IDisposable
        {
            /// <summary>
            /// TCP 控制连接监听
            /// </summary>
            private readonly TurnTcpListenerHost _listener;

            /// <summary>
            /// 创建宿主
            /// </summary>
            /// <param name="server">TURN 服务端</param>
            /// <param name="listener">已启动的监听</param>
            private Turn6062Harness(TurnServerHost server, TurnTcpListenerHost listener)
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
            /// <param name="connectionBindTimeout">未绑定连接超时；为 null 时用服务端默认值</param>
            /// <returns>宿主</returns>
            public static Turn6062Harness Start(int relayPort, TimeSpan? connectionBindTimeout = null)
            {
                var options = new TurnServerOptions
                {
                    SharedSecret = SharedSecret,
                    Realm = Realm,
                    RelayAddress = Host,
                    RelayPortMin = relayPort,
                    RelayPortMax = relayPort
                };
                if (connectionBindTimeout.HasValue)
                {
                    options.ConnectionBindTimeout = connectionBindTimeout.Value;
                }

                var server = new TurnServerHost(options);
                var listener = new TurnTcpListenerHost(new IPEndPoint(Host, 0), server);
                listener.Start();
                return new Turn6062Harness(server, listener);
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
        /// 控制连接客户端：底层用 TURN 的 TCP 传输收发自定界帧，按事务 ID 匹配应答
        /// </summary>
        private sealed class Turn6062Client : IDisposable
        {
            /// <summary>
            /// 底层控制传输；TCP 与 TLS 共用同一套整帧接口
            /// </summary>
            private readonly ITurnClientTransport _transport;

            /// <summary>
            /// 已收到的整帧；读泵在后台线程写，测试线程逐条取。
            /// 类型写全名是因为 SAEA.P2P 下另有 Channel 命名空间，简单名会被它遮蔽
            /// </summary>
            private readonly System.Threading.Channels.Channel<byte[]> _frames = System.Threading.Channels.Channel.CreateUnbounded<byte[]>();

            /// <summary>
            /// 包装传输并订阅帧事件
            /// </summary>
            /// <param name="transport">已创建的传输</param>
            private Turn6062Client(ITurnClientTransport transport)
            {
                _transport = transport;
                _transport.FrameReceived += OnFrameReceived;
            }

            /// <summary>
            /// 以明文 TCP 连接服务端并启动读泵
            /// </summary>
            /// <param name="target">服务端控制端点</param>
            /// <returns>已打开的控制连接客户端</returns>
            public static async Task<Turn6062Client> ConnectAsync(IPEndPoint target)
            {
                return await OpenAsync(new TcpTurnClientTransport(target)).ConfigureAwait(false);
            }

            /// <summary>
            /// 以 TLS 连接服务端并启动读泵；证书是测试内即时生成的自签证书，只能显式放行无效证书
            /// </summary>
            /// <param name="target">服务端 TLS 控制端点</param>
            /// <returns>已打开的控制连接客户端</returns>
            public static async Task<Turn6062Client> ConnectTlsAsync(IPEndPoint target)
            {
                return await OpenAsync(new TurnTlsClientTransport(target, "localhost", true)).ConfigureAwait(false);
            }

            /// <summary>
            /// 打开传输并包装成客户端；打开失败时释放已创建的传输
            /// </summary>
            /// <param name="transport">已创建的传输</param>
            /// <returns>已打开的控制连接客户端</returns>
            private static async Task<Turn6062Client> OpenAsync(ITurnClientTransport transport)
            {
                var client = new Turn6062Client(transport);
                try
                {
                    await transport.OpenAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // 建链失败时客户端还没有交回调用方，必须就地释放已持有的传输
                    client.Dispose();
                    throw;
                }
                return client;
            }

            /// <summary>
            /// 等待第一条满足条件的消息；不匹配的帧直接丢弃，适用于没有事务的指示
            /// </summary>
            /// <param name="predicate">消息匹配条件</param>
            /// <param name="timeout">等待上限</param>
            /// <returns>匹配的消息；超时或连接关闭时为 null</returns>
            public async Task<StunMessage> WaitForMessageAsync(Func<StunMessage, bool> predicate, TimeSpan timeout)
            {
                var deadline = DateTime.UtcNow + timeout;
                while (true)
                {
                    var remaining = deadline - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero)
                    {
                        return null;
                    }

                    var frame = await NextFrameAsync(remaining).ConfigureAwait(false);
                    if (frame == null)
                    {
                        return null;
                    }

                    StunMessage message;
                    if (TryParseStun(frame, out message) && predicate(message))
                    {
                        return message;
                    }
                }
            }

            /// <summary>
            /// 发送请求并等待事务 ID 相同的应答
            /// </summary>
            /// <param name="request">请求</param>
            /// <param name="timeout">等待上限</param>
            /// <returns>应答</returns>
            /// <exception cref="TimeoutException">超时未收到应答</exception>
            public async Task<StunMessage> ExchangeAsync(StunMessage request, TimeSpan timeout)
            {
                var transactionId = request.TransactionId.ToArray();
                _transport.Send(request.ToArray());

                var deadline = DateTime.UtcNow + timeout;
                while (true)
                {
                    var remaining = deadline - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero)
                    {
                        throw new TimeoutException("等待事务应答超时");
                    }

                    var frame = await NextFrameAsync(remaining).ConfigureAwait(false);
                    if (frame == null)
                    {
                        throw new TimeoutException("等待事务应答超时");
                    }

                    StunMessage message;
                    if (!TryParseStun(frame, out message) || !HasTransactionId(message, transactionId))
                    {
                        continue;
                    }

                    return message;
                }
            }

            /// <summary>
            /// 取消订阅并释放传输
            /// </summary>
            public void Dispose()
            {
                _transport.FrameReceived -= OnFrameReceived;
                _transport.Dispose();
            }

            /// <summary>
            /// 缓存读泵送来的整帧；不复制会与读泵复用缓冲冲突，这里帧本身已是副本
            /// </summary>
            /// <param name="frame">完整帧</param>
            private void OnFrameReceived(byte[] frame)
            {
                _frames.Writer.TryWrite(frame);
            }

            /// <summary>
            /// 取下一帧；超时返回 null
            /// </summary>
            /// <param name="timeout">等待上限</param>
            /// <returns>整帧</returns>
            private async Task<byte[]> NextFrameAsync(TimeSpan timeout)
            {
                using (var cts = new CancellationTokenSource(timeout))
                {
                    try
                    {
                        if (!await _frames.Reader.WaitToReadAsync(cts.Token).ConfigureAwait(false))
                        {
                            return null;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        return null;
                    }
                }

                byte[] frame;
                return _frames.Reader.TryRead(out frame) ? frame : null;
            }
        }

        /// <summary>
        /// 测试用对端：只接受一条连接，按需把收到的字节原样写回
        /// </summary>
        private sealed class TcpPeer : IDisposable
        {
            /// <summary>
            /// 对端监听
            /// </summary>
            private readonly Socket _listener;

            /// <summary>
            /// 是否回显收到的字节
            /// </summary>
            private readonly bool _echo;

            /// <summary>
            /// 接受成功时交出 socket；宿主释放时以 null 结束
            /// </summary>
            private readonly TaskCompletionSource<Socket> _accepted = new TaskCompletionSource<Socket>(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>
            /// 已接受的对端 socket；尚未接受时为 null
            /// </summary>
            private Socket _connection;

            /// <summary>
            /// 是否已释放
            /// </summary>
            private bool _disposed;

            /// <summary>
            /// 创建对端并开始接受连接
            /// </summary>
            /// <param name="echo">true 表示收到数据后原样写回，false 表示只持有连接</param>
            public TcpPeer(bool echo)
            {
                _echo = echo;
                _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _listener.Bind(new IPEndPoint(Host, 0));
                _listener.Listen(4);
                EndPoint = (IPEndPoint)_listener.LocalEndPoint;

                var ignored = Task.Run(() => AcceptLoopAsync());
            }

            /// <summary>
            /// 对端监听端点
            /// </summary>
            public IPEndPoint EndPoint { get; private set; }

            /// <summary>
            /// 接受连接的完成源
            /// </summary>
            public Task<Socket> Accepted
            {
                get { return _accepted.Task; }
            }

            /// <summary>
            /// 释放监听与已接受连接；可重复调用
            /// </summary>
            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;

                DisposeSocket(_listener);
                DisposeSocket(_connection);
                _accepted.TrySetResult(null);
            }

            /// <summary>
            /// 接受一条连接，并按需进入回显循环
            /// </summary>
            /// <returns>接受任务</returns>
            private async Task AcceptLoopAsync()
            {
                Socket accepted;
                try
                {
                    accepted = await _listener.AcceptAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException)
                {
                    return;
                }

                _connection = accepted;
                _accepted.TrySetResult(accepted);

                if (!_echo)
                {
                    return;
                }

                var buffer = new byte[4096];
                while (true)
                {
                    int read;
                    try
                    {
                        read = await accepted.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None).ConfigureAwait(false);
                    }
                    catch (SocketException)
                    {
                        return;
                    }
                    catch (ObjectDisposedException)
                    {
                        return;
                    }

                    if (read <= 0)
                    {
                        return;
                    }

                    var offset = 0;
                    while (offset < read)
                    {
                        int sent;
                        try
                        {
                            sent = await accepted.SendAsync(
                                new ArraySegment<byte>(buffer, offset, read - offset),
                                SocketFlags.None).ConfigureAwait(false);
                        }
                        catch (SocketException)
                        {
                            return;
                        }
                        catch (ObjectDisposedException)
                        {
                            return;
                        }

                        if (sent <= 0)
                        {
                            return;
                        }
                        offset += sent;
                    }
                }
            }

            /// <summary>
            /// 关闭 socket 并忽略重复关闭
            /// </summary>
            /// <param name="socket">socket；可为 null</param>
            private static void DisposeSocket(Socket socket)
            {
                if (socket == null)
                {
                    return;
                }

                try
                {
                    socket.Dispose();
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }
    }
}