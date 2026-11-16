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
*文件名： TurnServerRelayTests
*版本号： net10.0
*唯一标识：d9274a31-2936-4edf-b689-1ec6f92e97ec
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 01:10:00
*描述：TURN over UDP 的分配、权限、中继与通道验收
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 01:10:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 TURN over UDP 端到端中继用例
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Stun;
using SAEA.P2P.StunServer;
using SAEA.P2P.Turn;
using SAEA.P2P.TurnServer;
using SAEA.P2P.Transport;

using StunServerHost = SAEA.P2P.StunServer.StunServer;
using TurnServerHost = SAEA.P2P.TurnServer.TurnServer;

namespace SAEA.P2P.Tests.TurnServer
{
    /// <summary>
    /// TURN over UDP：未带凭据返回 401、Allocate 给出中继地址、CreatePermission 与 Send 指示双向中继、
    /// ChannelBind 后的 ChannelData 双向中继、Refresh 生命周期为 0 立即删除分配。
    /// </summary>
    [TestCategory(P2PTestSuite.Unit)]
    [TestClass]
    public class TurnServerRelayTests
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
        /// 请求经鉴权与中继的总等待上限
        /// </summary>
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 未携带鉴权属性的 Allocate 必须返回 401，并附 REALM 与 NONCE
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public async Task TurnServer_未带凭据_返回401挑战()
        {
            var relayPort = ReserveUdpPorts(1)[0];
            using (var harness = await TurnHarness.StartAsync(relayPort))
            using (var client = new SaeaUdpTransport())
            {
                await client.BindAsync(new IPEndPoint(Host, 0), CancellationToken.None);
                using (var inbox = new Inbox(client))
                {
                    var response = await ExchangeAsync(client, inbox, harness.EndPoint, TurnMessage.CreateAllocate(TurnServerHost.UdpTransport), Timeout);

                    Assert.IsNotNull(response, "服务端应回应答");
                    Assert.AreEqual(StunClass.ErrorResponse, response.Class, "未带凭据应返回错误应答");

                    int code;
                    string reason;
                    Assert.IsTrue(StunErrorCode.TryGet(response, out code, out reason), "应携带 ERROR-CODE");
                    Assert.AreEqual(401, code, "凭据缺失应返回 401");
                    Assert.AreEqual(Realm, ReadString(response, StunAttributeType.Realm), "401 必须带 REALM");
                    Assert.IsFalse(string.IsNullOrEmpty(ReadString(response, StunAttributeType.Nonce)), "401 必须带 NONCE");
                }
            }
        }

        /// <summary>
        /// 分配成功后经 CreatePermission 与 Send 指示可双向中继，回程为 Data 指示
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public async Task TurnServer_投递Send指示_对端收到数据()
        {
            var relayPort = ReserveUdpPorts(1)[0];
            using (var harness = await TurnHarness.StartAsync(relayPort))
            using (var client = new SaeaUdpTransport())
            using (var peer = new SaeaUdpTransport())
            {
                await client.BindAsync(new IPEndPoint(Host, 0), CancellationToken.None);
                await peer.BindAsync(new IPEndPoint(Host, 0), CancellationToken.None);

                using (var clientInbox = new Inbox(client))
                using (var peerInbox = new Inbox(peer))
                {
                    var relayed = await AllocateAsync(harness, client, clientInbox);
                    Assert.AreEqual(relayPort, relayed.Port, "中继地址端口应来自配置区间");

                    var credential = harness.CreateCredential("learner");
                    var nonce = await GetNonceAsync(harness, client, clientInbox);

                    var permission = Sign(TurnMessage.CreateCreatePermission(new[] { peer.LocalEndPoint }), credential, nonce);
                    var permissionResponse = await ExchangeAsync(client, clientInbox, harness.EndPoint, permission, Timeout);
                    Assert.AreEqual(StunClass.SuccessResponse, permissionResponse.Class, "CreatePermission 应成功");

                    var payload = Encoding.UTF8.GetBytes("turn-send-from-client");
                    var send = TurnMessage.CreateSend(peer.LocalEndPoint, payload);
                    await client.SendToAsync(harness.EndPoint, new ReadOnlyMemory<byte>(send.ToArray()), CancellationToken.None);

                    var atPeer = await peerInbox.WaitAsync(data => data.Length == payload.Length, Timeout);
                    Assert.IsNotNull(atPeer, "对端应收到中继数据");
                    CollectionAssert.AreEqual(payload, atPeer.Data, "对端收到的数据应与发送内容一致");
                    Assert.AreEqual(relayed, atPeer.From, "对端看到的来源应为中继地址");

                    var reply = Encoding.UTF8.GetBytes("turn-reply-from-peer");
                    await peer.SendToAsync(relayed, new ReadOnlyMemory<byte>(reply), CancellationToken.None);

                    var atClient = await clientInbox.WaitAsync(IsStunMessage, Timeout);
                    Assert.IsNotNull(atClient, "客户端应收到 Data 指示");

                    StunMessage indication;
                    StunParseError error;
                    Assert.IsTrue(StunMessage.TryParse(atClient.Data, out indication, out error), "回程报文应是 STUN 消息，错误 " + error);
                    Assert.AreEqual(StunMessageType.Data, indication.Method, "回程方法应为 Data");
                    Assert.AreEqual(StunClass.Indication, indication.Class, "回程类别应为 Indication");

                    IPEndPoint indicatedPeer;
                    Assert.IsTrue(TurnMessage.TryGetPeerAddress(indication, out indicatedPeer), "Data 指示应带 XOR-PEER-ADDRESS");
                    Assert.AreEqual(peer.LocalEndPoint, indicatedPeer, "XOR-PEER-ADDRESS 应为对端地址");

                    ReadOnlyMemory<byte> indicatedData;
                    Assert.IsTrue(TurnMessage.TryGetData(indication, out indicatedData), "Data 指示应带 DATA");
                    CollectionAssert.AreEqual(reply, indicatedData.ToArray(), "DATA 应与对端发送内容一致");
                }
            }
        }

        /// <summary>
        /// ChannelBind 后双向数据都走 ChannelData，不再包成 STUN 消息
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public async Task TurnServer_通道绑定后_双向ChannelData()
        {
            const ushort channelNumber = 0x4001;

            var relayPort = ReserveUdpPorts(1)[0];
            using (var harness = await TurnHarness.StartAsync(relayPort))
            using (var client = new SaeaUdpTransport())
            using (var peer = new SaeaUdpTransport())
            {
                await client.BindAsync(new IPEndPoint(Host, 0), CancellationToken.None);
                await peer.BindAsync(new IPEndPoint(Host, 0), CancellationToken.None);

                using (var clientInbox = new Inbox(client))
                using (var peerInbox = new Inbox(peer))
                {
                    var relayed = await AllocateAsync(harness, client, clientInbox);
                    var credential = harness.CreateCredential("learner");
                    var nonce = await GetNonceAsync(harness, client, clientInbox);

                    var bind = Sign(TurnMessage.CreateChannelBind(channelNumber, peer.LocalEndPoint), credential, nonce);
                    var bindResponse = await ExchangeAsync(client, clientInbox, harness.EndPoint, bind, Timeout);
                    Assert.AreEqual(StunClass.SuccessResponse, bindResponse.Class, "ChannelBind 应成功并同时创建权限");

                    var outbound = Encoding.UTF8.GetBytes("channel-data-outbound");
                    var channelData = ChannelData.Encode(channelNumber, outbound, false);
                    await client.SendToAsync(harness.EndPoint, new ReadOnlyMemory<byte>(channelData), CancellationToken.None);

                    var atPeer = await peerInbox.WaitAsync(data => data.Length == outbound.Length, Timeout);
                    Assert.IsNotNull(atPeer, "对端应收到 ChannelData 解出的数据");
                    CollectionAssert.AreEqual(outbound, atPeer.Data, "对端收到的数据应与发送内容一致");
                    Assert.AreEqual(relayed, atPeer.From, "对端看到的来源应为中继地址");

                    var inbound = Encoding.UTF8.GetBytes("channel-data-inbound");
                    await peer.SendToAsync(relayed, new ReadOnlyMemory<byte>(inbound), CancellationToken.None);

                    var atClient = await clientInbox.WaitAsync(IsChannelData, Timeout);
                    Assert.IsNotNull(atClient, "客户端应收到 ChannelData");

                    ushort receivedChannel;
                    var receivedPayload = DecodeChannelData(atClient.Data, out receivedChannel);
                    Assert.IsNotNull(receivedPayload, "ChannelData 应能解码");
                    Assert.AreEqual(channelNumber, receivedChannel, "通道号应一致");
                    CollectionAssert.AreEqual(inbound, receivedPayload, "ChannelData 负载应与对端发送内容一致");
                }
            }
        }

        /// <summary>
        /// Refresh 携带 LIFETIME=0 时立即删除分配
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public async Task TurnServer_Refresh生命周期为0_删除分配()
        {
            var relayPort = ReserveUdpPorts(1)[0];
            using (var harness = await TurnHarness.StartAsync(relayPort))
            using (var client = new SaeaUdpTransport())
            {
                await client.BindAsync(new IPEndPoint(Host, 0), CancellationToken.None);

                using (var clientInbox = new Inbox(client))
                {
                    await AllocateAsync(harness, client, clientInbox);
                    Assert.AreEqual(1, harness.Server.AllocationCount, "分配后应有一条记录");

                    var credential = harness.CreateCredential("learner");
                    var nonce = await GetNonceAsync(harness, client, clientInbox);

                    var refresh = Sign(TurnMessage.CreateRefresh(0), credential, nonce);
                    var response = await ExchangeAsync(client, clientInbox, harness.EndPoint, refresh, Timeout);

                    Assert.IsNotNull(response, "服务端应回应答");
                    Assert.AreEqual(StunClass.SuccessResponse, response.Class, "删除分配应成功");
                    Assert.AreEqual(0, harness.Server.AllocationCount, "立即删除后不应残留分配");
                }
            }
        }

        /// <summary>
        /// 同一用户的并发分配达到上限时，后续 Allocate 返回 486
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public async Task TurnServer_超配额_返回486()
        {
            var relayPort = ReserveUdpPorts(1)[0];
            using (var harness = await TurnHarness.StartAsync(relayPort, 1))
            using (var first = new SaeaUdpTransport())
            using (var second = new SaeaUdpTransport())
            {
                await first.BindAsync(new IPEndPoint(Host, 0), CancellationToken.None);
                await second.BindAsync(new IPEndPoint(Host, 0), CancellationToken.None);

                using (var firstInbox = new Inbox(first))
                using (var secondInbox = new Inbox(second))
                {
                    // 两个请求共用同一个凭据对象，保证用户名完全一致，才能命中单用户上限
                    var credential = harness.CreateCredential("learner");

                    var firstNonce = await GetNonceAsync(harness, first, firstInbox);
                    var firstAllocate = Sign(TurnMessage.CreateAllocate(TurnServerHost.UdpTransport), credential, firstNonce);
                    var firstResponse = await ExchangeAsync(first, firstInbox, harness.EndPoint, firstAllocate, Timeout);
                    Assert.IsNotNull(firstResponse, "第一个客户端应得到应答");
                    Assert.AreEqual(StunClass.SuccessResponse, firstResponse.Class, "未超配额时 Allocate 应成功");

                    var secondNonce = await GetNonceAsync(harness, second, secondInbox);
                    var secondAllocate = Sign(TurnMessage.CreateAllocate(TurnServerHost.UdpTransport), credential, secondNonce);
                    var secondResponse = await ExchangeAsync(second, secondInbox, harness.EndPoint, secondAllocate, Timeout);

                    Assert.IsNotNull(secondResponse, "第二个客户端应得到应答");
                    Assert.AreEqual(StunClass.ErrorResponse, secondResponse.Class, "超出单用户上限时应返回错误应答");

                    int code;
                    string reason;
                    Assert.IsTrue(StunErrorCode.TryGet(secondResponse, out code, out reason), "应携带 ERROR-CODE");
                    Assert.AreEqual(486, code, "单用户并发分配超限应返回 486");
                    Assert.AreEqual(1, harness.Server.AllocationCount, "被拒绝的请求不应产生新分配");
                }
            }
        }

        /// <summary>
        /// 中继端口区间耗尽时，Allocate 返回 508
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public async Task TurnServer_端口耗尽_返回508()
        {
            var relayPort = ReserveUdpPorts(1)[0];
            using (var harness = await TurnHarness.StartAsync(relayPort))
            using (var first = new SaeaUdpTransport())
            using (var second = new SaeaUdpTransport())
            {
                await first.BindAsync(new IPEndPoint(Host, 0), CancellationToken.None);
                await second.BindAsync(new IPEndPoint(Host, 0), CancellationToken.None);

                using (var firstInbox = new Inbox(first))
                using (var secondInbox = new Inbox(second))
                {
                    var relayed = await AllocateAsync(harness, first, firstInbox);
                    Assert.AreEqual(relayPort, relayed.Port, "中继地址应占用区间内唯一端口");

                    // 换一个用户，避免命中单用户配额而拿到 486
                    var credential = harness.CreateCredential("other");
                    var nonce = await GetNonceAsync(harness, second, secondInbox);
                    var allocate = Sign(TurnMessage.CreateAllocate(TurnServerHost.UdpTransport), credential, nonce);
                    var response = await ExchangeAsync(second, secondInbox, harness.EndPoint, allocate, Timeout);

                    Assert.IsNotNull(response, "端口耗尽时也应回应答");
                    Assert.AreEqual(StunClass.ErrorResponse, response.Class, "端口不足时应返回错误应答");

                    int code;
                    string reason;
                    Assert.IsTrue(StunErrorCode.TryGet(response, out code, out reason), "应携带 ERROR-CODE");
                    Assert.AreEqual(508, code, "中继端口耗尽应返回 508");
                    Assert.AreEqual(1, harness.Server.AllocationCount, "被拒绝的请求不应产生新分配");
                }
            }
        }

        /// <summary>
        /// 完成一次带鉴权的 Allocate 并返回中继地址
        /// </summary>
        /// <param name="harness">服务端宿主</param>
        /// <param name="client">客户端传输</param>
        /// <param name="inbox">客户端收件箱</param>
        /// <returns>中继地址</returns>
        private static async Task<IPEndPoint> AllocateAsync(TurnHarness harness, SaeaUdpTransport client, Inbox inbox)
        {
            var credential = harness.CreateCredential("learner");
            var nonce = await GetNonceAsync(harness, client, inbox);

            var allocate = Sign(TurnMessage.CreateAllocate(TurnServerHost.UdpTransport), credential, nonce);
            var response = await ExchangeAsync(client, inbox, harness.EndPoint, allocate, Timeout);

            Assert.IsNotNull(response, "Allocate 应得到应答");
            Assert.AreEqual(StunClass.SuccessResponse, response.Class, "Allocate 应成功");
            Assert.AreEqual(600u, ReadUInt32(response, StunAttributeType.Lifetime), "默认生命周期应为 600 秒");

            var relayed = DecodeAddress(response, StunAttributeType.XorRelayedAddress);
            Assert.IsNotNull(relayed, "Allocate 应答应携带 XOR-RELAYED-ADDRESS");
            Assert.AreEqual(Host, relayed.Address, "中继地址应为配置的中继地址");
            return relayed;
        }

        /// <summary>
        /// 先发无凭据请求拿到 401，再取出 NONCE
        /// </summary>
        /// <param name="harness">服务端宿主</param>
        /// <param name="client">客户端传输</param>
        /// <param name="inbox">客户端收件箱</param>
        /// <returns>nonce</returns>
        private static async Task<string> GetNonceAsync(TurnHarness harness, SaeaUdpTransport client, Inbox inbox)
        {
            var challenge = await ExchangeAsync(client, inbox, harness.EndPoint, TurnMessage.CreateAllocate(TurnServerHost.UdpTransport), Timeout);

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
        /// 发送请求并等待事务 ID 相同的应答
        /// </summary>
        /// <param name="client">客户端传输</param>
        /// <param name="inbox">客户端收件箱</param>
        /// <param name="target">服务端端点</param>
        /// <param name="request">请求</param>
        /// <param name="timeout">等待上限</param>
        /// <returns>应答；超时为 null</returns>
        private static async Task<StunMessage> ExchangeAsync(SaeaUdpTransport client, Inbox inbox, IPEndPoint target, StunMessage request, TimeSpan timeout)
        {
            var transactionId = request.TransactionId.ToArray();
            await client.SendToAsync(target, new ReadOnlyMemory<byte>(request.ToArray()), CancellationToken.None);

            var received = await inbox.WaitAsync(data => HasTransactionId(data, transactionId), timeout);
            if (received == null)
            {
                return null;
            }

            StunMessage message;
            StunParseError error;
            return StunMessage.TryParse(received.Data, out message, out error) ? message : null;
        }

        /// <summary>
        /// 判断数据报是否为指定事务的 STUN 消息
        /// </summary>
        /// <param name="data">数据报</param>
        /// <param name="transactionId">事务 ID</param>
        /// <returns>相同返回 true</returns>
        private static bool HasTransactionId(byte[] data, byte[] transactionId)
        {
            StunMessage message;
            StunParseError error;
            if (!StunMessage.TryParse(data, out message, out error))
            {
                return false;
            }

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
        /// 判断数据报是否为 STUN 消息
        /// </summary>
        /// <param name="data">数据报</param>
        /// <returns>是则返回 true</returns>
        private static bool IsStunMessage(byte[] data)
        {
            StunMessage message;
            StunParseError error;
            return StunMessage.TryParse(data, out message, out error);
        }

        /// <summary>
        /// 解码 ChannelData 并取出应用数据；失败时返回 null
        /// </summary>
        /// <param name="data">数据报</param>
        /// <param name="channelNumber">通道号</param>
        /// <returns>应用数据副本</returns>
        private static byte[] DecodeChannelData(byte[] data, out ushort channelNumber)
        {
            ReadOnlySpan<byte> payload;
            if (!ChannelData.TryDecode(data, out channelNumber, out payload))
            {
                return null;
            }
            return payload.ToArray();
        }

        /// <summary>
        /// 判断数据报是否为 ChannelData
        /// </summary>
        /// <param name="data">数据报</param>
        /// <returns>是则返回 true</returns>
        private static bool IsChannelData(byte[] data)
        {
            return data.Length >= ChannelData.HeaderLength && data[0] >= 0x40 && data[0] <= 0x4F;
        }

        /// <summary>
        /// 解码地址属性；缺失或格式错误时返回 null
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="type">属性类型</param>
        /// <returns>解码出的端点</returns>
        private static IPEndPoint DecodeAddress(StunMessage message, StunAttributeType type)
        {
            StunAttribute attribute;
            if (!message.TryGet(type, out attribute))
            {
                return null;
            }

            IPEndPoint endPoint;
            return StunAddressCodec.TryDecodeAddress(attribute, message.TransactionId.Span, out endPoint) ? endPoint : null;
        }

        /// <summary>
        /// 读取 32 位无符号整型属性；缺失时返回 0
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="type">属性类型</param>
        /// <returns>属性值</returns>
        private static uint ReadUInt32(StunMessage message, StunAttributeType type)
        {
            StunAttribute attribute;
            uint value;
            return message.TryGet(type, out attribute) && StunAttributeCodec.TryGetUInt32(attribute, out value) ? value : 0;
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
        /// 预占若干个尚未使用的 UDP 端口；同时持有套接字可避免返回重复端口
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
        /// TURN 服务端、STUN 监听端点与时钟的组合宿主
        /// </summary>
        private sealed class TurnHarness : IDisposable
        {
            /// <summary>
            /// 底层 STUN 服务端，负责监听与回送
            /// </summary>
            private readonly StunServerHost _stun;

            /// <summary>
            /// 创建宿主并启动监听
            /// </summary>
            /// <param name="stun">STUN 服务端</param>
            /// <param name="server">TURN 服务端</param>
            private TurnHarness(StunServerHost stun, TurnServerHost server)
            {
                _stun = stun;
                Server = server;
            }

            /// <summary>
            /// TURN 服务端
            /// </summary>
            public TurnServerHost Server { get; private set; }

            /// <summary>
            /// 客户端应访问的端点
            /// </summary>
            public IPEndPoint EndPoint
            {
                get { return _stun.Endpoints[0].LocalEndPoint; }
            }

            /// <summary>
            /// 按配置创建并启动服务端；中继端口区间收窄到单个端口，便于断言中继地址
            /// </summary>
            /// <param name="relayPort">中继端口</param>
            /// <param name="maxAllocationsPerUser">单用户并发分配上限；为 null 时用服务端默认值</param>
            /// <returns>宿主</returns>
            public static async Task<TurnHarness> StartAsync(int relayPort, int? maxAllocationsPerUser = null)
            {
                var options = new TurnServerOptions
                {
                    SharedSecret = SharedSecret,
                    Realm = Realm,
                    RelayAddress = Host,
                    RelayPortMin = relayPort,
                    RelayPortMax = relayPort
                };
                if (maxAllocationsPerUser.HasValue)
                {
                    options.MaxAllocationsPerUser = maxAllocationsPerUser.Value;
                }
                var server = new TurnServerHost(options);

                var stunOptions = new StunServerOptions(new IPEndPoint(Host, 0))
                {
                    RequestHandler = server.HandleRequest,
                    DatagramHandler = server.HandleDatagram
                };
                var stun = new StunServerHost(stunOptions);
                await stun.StartAsync();
                return new TurnHarness(stun, server);
            }

            /// <summary>
            /// 按用户名签发临时凭据
            /// </summary>
            /// <param name="userId">用户标识</param>
            /// <returns>临时凭据</returns>
            public TurnCredential CreateCredential(string userId)
            {
                return Server.CredentialService.Create(userId);
            }

            /// <summary>
            /// 依次释放 STUN 服务端与 TURN 服务端
            /// </summary>
            public void Dispose()
            {
                Server.Dispose();
                _stun.Dispose();
            }
        }

        /// <summary>
        /// 数据报收件箱：常驻订阅并缓存，避免请求间隙丢包
        /// </summary>
        private sealed class Inbox : IDisposable
        {
            /// <summary>
            /// 订阅的传输
            /// </summary>
            private readonly IUdpTransport _transport;

            /// <summary>
            /// 已收到但尚未取走的数据报
            /// </summary>
            private readonly List<Received> _items = new List<Received>();

            /// <summary>
            /// 保护收件列表的同步对象
            /// </summary>
            private readonly object _sync = new object();

            /// <summary>
            /// 订阅传输
            /// </summary>
            /// <param name="transport">传输</param>
            public Inbox(IUdpTransport transport)
            {
                _transport = transport;
                _transport.DatagramReceived += OnDatagramReceived;
            }

            /// <summary>
            /// 等待第一条满足条件的数据报；超时返回 null
            /// </summary>
            /// <param name="predicate">匹配条件</param>
            /// <param name="timeout">等待上限</param>
            /// <returns>收到的数据报</returns>
            public async Task<Received> WaitAsync(Func<byte[], bool> predicate, TimeSpan timeout)
            {
                var deadline = DateTime.UtcNow + timeout;
                while (DateTime.UtcNow < deadline)
                {
                    lock (_sync)
                    {
                        for (var i = 0; i < _items.Count; i++)
                        {
                            if (predicate(_items[i].Data))
                            {
                                var item = _items[i];
                                _items.RemoveAt(i);
                                return item;
                            }
                        }
                    }

                    await Task.Delay(20);
                }
                return null;
            }

            /// <summary>
            /// 复制回调数据并缓存；回调返回后原始内存不再有效
            /// </summary>
            /// <param name="from">来源端点</param>
            /// <param name="data">数据</param>
            private void OnDatagramReceived(IPEndPoint from, ReadOnlyMemory<byte> data)
            {
                var copy = data.ToArray();
                lock (_sync)
                {
                    _items.Add(new Received(from, copy));
                }
            }

            /// <summary>
            /// 取消订阅
            /// </summary>
            public void Dispose()
            {
                _transport.DatagramReceived -= OnDatagramReceived;
            }
        }

        /// <summary>
        /// 一条收到的数据报
        /// </summary>
        private sealed class Received
        {
            /// <summary>
            /// 创建数据报记录
            /// </summary>
            /// <param name="from">来源端点</param>
            /// <param name="data">已复制的数据</param>
            public Received(IPEndPoint from, byte[] data)
            {
                From = from;
                Data = data;
            }

            /// <summary>
            /// 来源端点
            /// </summary>
            public IPEndPoint From { get; private set; }

            /// <summary>
            /// 已复制的数据
            /// </summary>
            public byte[] Data { get; private set; }
        }
    }
}