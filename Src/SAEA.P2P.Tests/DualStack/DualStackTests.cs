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
*命名空间：SAEA.P2P.Tests.DualStack
*文件名： DualStackTests
*版本号： net10.0
*唯一标识：70f51395-9577-41d1-9602-31f5988611de
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 20:00:00
*描述：双栈监听与 IPv6 中继用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 20:00:00
*修改人： yswenli
*版本号： net10.0
*描述：新增双栈监听与 IPv6 中继用例
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
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

namespace SAEA.P2P.Tests.DualStack
{
    /// <summary>
    /// 双栈监听与 IPv6 中继：一个双栈套接字同时服务 IPv4 与 IPv6 的 Binding，
    /// TURN 中继按 RelayAddress 的地址族分配，请求其它地址族返回 440。
    /// </summary>
    [TestClass]
    public class DualStackTests
    {
        /// <summary>
        /// 中继测试统一使用 IPv6 回环地址
        /// </summary>
        private static readonly IPAddress RelayHost = IPAddress.IPv6Loopback;

        /// <summary>
        /// 用例共享的共享密钥
        /// </summary>
        private const string SharedSecret = "shared-secret-for-tests";

        /// <summary>
        /// 用例共享的认证域
        /// </summary>
        private const string Realm = "saea.p2p";

        /// <summary>
        /// 单次请求应答的等待上限
        /// </summary>
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 双栈监听后，IPv4 与 IPv6 客户端都能完成 Binding 并拿到各自地址族的映射地址
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task StunServer_双栈监听_IPv4与IPv6均可Binding()
        {
            var options = new StunServerOptions(new IPEndPoint(IPAddress.IPv6Any, 0))
            {
                EnableDualMode = true
            };

            using (var server = new StunServerHost(options))
            {
                await server.StartAsync();
                var port = server.Endpoints[0].LocalEndPoint.Port;

                using (var v4 = new SaeaUdpTransport())
                {
                    await v4.BindAsync(new IPEndPoint(IPAddress.Loopback, 0), CancellationToken.None);
                    using (var inbox = new Inbox(v4))
                    {
                        var target = new IPEndPoint(IPAddress.Loopback, port);
                        var request = new StunMessage(StunMessageType.Binding, StunClass.Request);
                        var response = await ExchangeAsync(v4, inbox, target, request, Timeout);

                        Assert.IsNotNull(response, "IPv4 客户端未收到 Binding 应答，目标 " + target);
                        Assert.AreEqual(StunClass.SuccessResponse, response.Class, "IPv4 Binding 应返回成功应答");

                        var mapped = DecodeAddress(response, StunAttributeType.XorMappedAddress);
                        Assert.IsNotNull(mapped, "IPv4 应答应携带 XOR-MAPPED-ADDRESS");
                        Assert.AreEqual(
                            AddressFamily.InterNetwork,
                            mapped.AddressFamily,
                            "XOR-MAPPED-ADDRESS 地址族应为 IPv4，实际 " + mapped);
                        Assert.AreEqual(
                            v4.LocalEndPoint,
                            mapped,
                            "XOR-MAPPED-ADDRESS 应为 IPv4 客户端端点 " + v4.LocalEndPoint + "，实际 " + mapped);
                    }
                }

                using (var v6 = new SaeaUdpTransport())
                {
                    await v6.BindAsync(new IPEndPoint(IPAddress.IPv6Loopback, 0), CancellationToken.None);
                    using (var inbox = new Inbox(v6))
                    {
                        var target = new IPEndPoint(IPAddress.IPv6Loopback, port);
                        var request = new StunMessage(StunMessageType.Binding, StunClass.Request);
                        var response = await ExchangeAsync(v6, inbox, target, request, Timeout);

                        Assert.IsNotNull(response, "IPv6 客户端未收到 Binding 应答，目标 " + target);
                        Assert.AreEqual(StunClass.SuccessResponse, response.Class, "IPv6 Binding 应返回成功应答");

                        var mapped = DecodeAddress(response, StunAttributeType.XorMappedAddress);
                        Assert.IsNotNull(mapped, "IPv6 应答应携带 XOR-MAPPED-ADDRESS");
                        Assert.AreEqual(
                            AddressFamily.InterNetworkV6,
                            mapped.AddressFamily,
                            "XOR-MAPPED-ADDRESS 地址族应为 IPv6，实际 " + mapped);
                        Assert.AreEqual(
                            v6.LocalEndPoint,
                            mapped,
                            "XOR-MAPPED-ADDRESS 应为 IPv6 客户端端点 " + v6.LocalEndPoint + "，实际 " + mapped);
                    }
                }
            }
        }

        /// <summary>
        /// 中继地址为 IPv6 回环时，带 REQUESTED-ADDRESS-FAMILY=IPv6 的 Allocate 返回该地址与预留端口
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task TurnServer_请求IPv6中继_返回v6地址()
        {
            var relayPort = ReserveUdpPorts(RelayHost, 1)[0];
            var options = new TurnServerOptions
            {
                SharedSecret = SharedSecret,
                Realm = Realm,
                RelayAddress = RelayHost,
                RelayPortMin = relayPort,
                RelayPortMax = relayPort
            };

            using (var turn = new TurnServerHost(options))
            {
                var stunOptions = new StunServerOptions(new IPEndPoint(RelayHost, 0))
                {
                    RequestHandler = turn.HandleRequest,
                    DatagramHandler = turn.HandleDatagram
                };

                using (var stun = new StunServerHost(stunOptions))
                using (var client = new SaeaUdpTransport())
                {
                    await stun.StartAsync();
                    var endPoint = stun.Endpoints[0].LocalEndPoint;
                    await client.BindAsync(new IPEndPoint(RelayHost, 0), CancellationToken.None);

                    using (var inbox = new Inbox(client))
                    {
                        var nonce = await GetNonceAsync(client, inbox, endPoint);
                        var credential = turn.CredentialService.Create("learner");

                        var allocate = CreateAllocate(TurnAddressFamily.IPv6);
                        Sign(allocate, credential, nonce);
                        var response = await ExchangeAsync(client, inbox, endPoint, allocate, Timeout);

                        Assert.IsNotNull(response, "IPv6 中继 Allocate 未收到应答，目标 " + endPoint);
                        Assert.AreEqual(StunClass.SuccessResponse, response.Class, "IPv6 中继 Allocate 应成功");
                        Assert.AreEqual(600u, ReadUInt32(response, StunAttributeType.Lifetime), "默认生命周期应为 600 秒");

                        var relayed = DecodeAddress(response, StunAttributeType.XorRelayedAddress);
                        Assert.IsNotNull(relayed, "Allocate 应答应携带 XOR-RELAYED-ADDRESS");
                        Assert.AreEqual(
                            AddressFamily.InterNetworkV6,
                            relayed.AddressFamily,
                            "中继地址族应为 IPv6，实际 " + relayed);
                        Assert.AreEqual(IPAddress.IPv6Loopback, relayed.Address, "中继地址应为 ::1，实际 " + relayed.Address);
                        Assert.AreEqual(relayPort, relayed.Port, "中继端口应为预留端口 " + relayPort + "，实际 " + relayed.Port);
                        Assert.AreEqual(1, turn.AllocationCount, "成功分配后应有 1 条记录");
                    }
                }
            }
        }

        /// <summary>
        /// 中继只支持 ::1 时，带 REQUESTED-ADDRESS-FAMILY=IPv4 的 Allocate 返回 440
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task TurnServer_不支持地址族_返回440()
        {
            var relayPort = ReserveUdpPorts(RelayHost, 1)[0];
            var options = new TurnServerOptions
            {
                SharedSecret = SharedSecret,
                Realm = Realm,
                RelayAddress = RelayHost,
                RelayPortMin = relayPort,
                RelayPortMax = relayPort
            };

            using (var turn = new TurnServerHost(options))
            {
                var stunOptions = new StunServerOptions(new IPEndPoint(RelayHost, 0))
                {
                    RequestHandler = turn.HandleRequest,
                    DatagramHandler = turn.HandleDatagram
                };

                using (var stun = new StunServerHost(stunOptions))
                using (var client = new SaeaUdpTransport())
                {
                    await stun.StartAsync();
                    var endPoint = stun.Endpoints[0].LocalEndPoint;
                    await client.BindAsync(new IPEndPoint(RelayHost, 0), CancellationToken.None);

                    using (var inbox = new Inbox(client))
                    {
                        var nonce = await GetNonceAsync(client, inbox, endPoint);
                        var credential = turn.CredentialService.Create("learner");

                        var allocate = CreateAllocate(TurnAddressFamily.IPv4);
                        Sign(allocate, credential, nonce);
                        var response = await ExchangeAsync(client, inbox, endPoint, allocate, Timeout);

                        Assert.IsNotNull(response, "IPv4 地址族请求未收到应答，目标 " + endPoint);
                        Assert.AreEqual(StunClass.ErrorResponse, response.Class, "地址族不符应返回错误应答");

                        int code;
                        string reason;
                        Assert.IsTrue(StunErrorCode.TryGet(response, out code, out reason), "应携带 ERROR-CODE");
                        Assert.AreEqual(440, code, "地址族不符应返回 440，实际 " + code);
                        Assert.AreEqual(0, turn.AllocationCount, "被拒绝的请求不应产生分配");
                    }
                }
            }
        }

        /// <summary>
        /// 创建带 REQUESTED-ADDRESS-FAMILY 的 Allocate 请求
        /// </summary>
        /// <param name="family">请求的地址族</param>
        /// <returns>请求</returns>
        private static StunMessage CreateAllocate(TurnAddressFamily family)
        {
            var request = TurnMessage.CreateAllocate(TurnServerHost.UdpTransport);
            var attributes = new List<StunAttribute>(request.Attributes)
            {
                new StunAttribute(
                    StunAttributeType.RequestedAddressFamily,
                    new byte[] { (byte)family, 0x00, 0x00, 0x00 })
            };
            request.SetAttributes(attributes);
            return request;
        }

        /// <summary>
        /// 先发无凭据请求拿到 401，再取出 NONCE
        /// </summary>
        /// <param name="client">客户端传输</param>
        /// <param name="inbox">客户端收件箱</param>
        /// <param name="endPoint">服务端端点</param>
        /// <returns>nonce</returns>
        private static async Task<string> GetNonceAsync(SaeaUdpTransport client, Inbox inbox, IPEndPoint endPoint)
        {
            var challenge = await ExchangeAsync(
                client,
                inbox,
                endPoint,
                TurnMessage.CreateAllocate(TurnServerHost.UdpTransport),
                Timeout);

            Assert.IsNotNull(challenge, "无凭据请求未收到挑战应答，目标 " + endPoint);
            int code;
            string reason;
            Assert.IsTrue(StunErrorCode.TryGet(challenge, out code, out reason), "挑战应携带 ERROR-CODE");
            Assert.AreEqual(401, code, "无凭据请求应返回 401，实际 " + code);

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
        private static void Sign(StunMessage request, TurnCredential credential, string nonce)
        {
            var attributes = new List<StunAttribute>(request.Attributes)
            {
                new StunAttribute(StunAttributeType.Username, Encoding.UTF8.GetBytes(credential.Username)),
                new StunAttribute(StunAttributeType.Realm, Encoding.UTF8.GetBytes(credential.Realm)),
                new StunAttribute(StunAttributeType.Nonce, Encoding.UTF8.GetBytes(nonce))
            };
            request.SetAttributes(attributes);
            StunIntegrity.Append(request, StunIntegrity.CreateLongTermKey(credential.Username, credential.Realm, credential.Password));
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
        private static async Task<StunMessage> ExchangeAsync(
            SaeaUdpTransport client,
            Inbox inbox,
            IPEndPoint target,
            StunMessage request,
            TimeSpan timeout)
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
        /// 在指定地址上预占若干个尚未使用的 UDP 端口；同时持有套接字可避免返回重复端口
        /// </summary>
        /// <param name="host">绑定地址</param>
        /// <param name="count">端口数量</param>
        /// <returns>端口数组</returns>
        private static int[] ReserveUdpPorts(IPAddress host, int count)
        {
            var probes = new List<SaeaUdpTransport>();
            try
            {
                var ports = new int[count];
                for (var i = 0; i < count; i++)
                {
                    var probe = new SaeaUdpTransport();
                    probe.BindAsync(new IPEndPoint(host, 0), CancellationToken.None).GetAwaiter().GetResult();
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