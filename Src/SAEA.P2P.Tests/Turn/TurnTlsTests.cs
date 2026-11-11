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
*文件名： TurnTlsTests
*版本号： net10.0
*唯一标识：9e1e3f80-b38b-4be8-9408-6d9c627fcad6
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 02:30:00
*描述：TURN over TLS 控制连接的分配、通道绑定与证书校验验收
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 02:30:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 TURN over TLS 端到端中继与证书校验用例
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Stun;
using SAEA.P2P.Turn;
using SAEA.P2P.TurnServer;
using SAEA.P2P.Transport;

using TurnServerHost = SAEA.P2P.TurnServer.TurnServer;

namespace SAEA.P2P.Tests.Turn
{
    /// <summary>
    /// TURN over TLS：PFX 证书完成握手后，控制连接复用 TCP 的分帧与分派；
    /// 绑定通道后 ChannelData 双向中继内容一致，自签证书在默认校验策略下必须被拒绝。
    /// </summary>
    [TestClass]
    public class TurnTlsTests
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
        /// TLS 控制连接完成 Allocate 与 ChannelBind 后，ChannelData 双向中继内容一致；
        /// 连接关闭后该连接上的分配应被释放。
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task TurnTls_Allocate后双向中继_数据一致()
        {
            const ushort channelNumber = 0x4001;

            var certificatePath = CreateCertificateFile();
            var relayPort = ReserveUdpPorts(1)[0];
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
                using (var server = new TurnServerHost(options))
                using (var listener = new TurnTlsListener(new IPEndPoint(Host, 0), server, certificatePath, CertificatePassword))
                using (var peer = new SaeaUdpTransport())
                {
                    listener.Start();
                    Assert.IsNotNull(listener.LocalEndPoint, "监听启动后应能取到实际端点");
                    await peer.BindAsync(new IPEndPoint(Host, 0), CancellationToken.None);

                    using (var peerInbox = new UdpInbox(peer))
                    {
                        var client = await TlsChannel.ConnectAsync(listener.LocalEndPoint);
                        try
                        {
                            var credential = server.CredentialService.Create("learner");
                            var nonce = await GetNonceAsync(client);

                            var allocate = Sign(TurnMessage.CreateAllocate(TurnServerHost.UdpTransport), credential, nonce);
                            var allocateResponse = await client.ExchangeAsync(allocate, Timeout);
                            Assert.IsNotNull(allocateResponse, "Allocate 应得到应答");
                            Assert.AreEqual(StunClass.SuccessResponse, allocateResponse.Class, "带鉴权的 Allocate 应成功");

                            var relayed = DecodeAddress(allocateResponse, StunAttributeType.XorRelayedAddress);
                            Assert.IsNotNull(relayed, "Allocate 应答应携带 XOR-RELAYED-ADDRESS");
                            Assert.AreEqual(Host, relayed.Address, "中继地址应为配置的中继地址");
                            Assert.AreEqual(relayPort, relayed.Port, "中继端口应来自配置区间");
                            Assert.AreEqual(1, server.AllocationCount, "分配成功后应有一条记录");

                            var permission = Sign(TurnMessage.CreateCreatePermission(new[] { peer.LocalEndPoint }), credential, nonce);
                            var permissionResponse = await client.ExchangeAsync(permission, Timeout);
                            Assert.IsNotNull(permissionResponse, "CreatePermission 应得到应答");
                            Assert.AreEqual(StunClass.SuccessResponse, permissionResponse.Class, "CreatePermission 应成功");

                            var bind = Sign(TurnMessage.CreateChannelBind(channelNumber, peer.LocalEndPoint), credential, nonce);
                            var bindResponse = await client.ExchangeAsync(bind, Timeout);
                            Assert.IsNotNull(bindResponse, "ChannelBind 应得到应答");
                            Assert.AreEqual(StunClass.SuccessResponse, bindResponse.Class, "ChannelBind 应成功并同时创建权限");

                            // 客户端到对端：ChannelData 在 TLS 流上按 4 字节补齐，服务端需按长度字段解出应用数据
                            var outbound = Encoding.UTF8.GetBytes("turn-tls-outbound");
                            await client.SendAsync(ChannelData.Encode(channelNumber, outbound, true));

                            var atPeer = await peerInbox.WaitAsync(data => data.Length == outbound.Length, Timeout);
                            Assert.IsNotNull(atPeer, "对端应收到 ChannelData 解出的数据");
                            CollectionAssert.AreEqual(outbound, atPeer.Data, "对端收到的数据应与发送内容一致");
                            Assert.AreEqual(relayed, atPeer.From, "对端看到的来源应为中继地址");

                            // 对端到客户端：服务端回送的 ChannelData 必须补齐，客户端才能按流继续切帧
                            var inbound = Encoding.UTF8.GetBytes("turn-tls-inbound");
                            await peer.SendToAsync(relayed, new ReadOnlyMemory<byte>(inbound), CancellationToken.None);

                            var frameData = await client.ReadAsync(frame => frame.Kind == TurnStreamFrameKind.ChannelData, Timeout);
                            Assert.IsNotNull(frameData, "客户端应收到 ChannelData 帧");

                            ushort receivedChannel;
                            var receivedPayload = DecodeChannelData(frameData, out receivedChannel);
                            Assert.IsNotNull(receivedPayload, "ChannelData 应能解码");
                            Assert.AreEqual(channelNumber, receivedChannel, "通道号应一致");
                            CollectionAssert.AreEqual(inbound, receivedPayload, "ChannelData 负载应与对端发送内容一致");
                        }
                        finally
                        {
                            client.Dispose();
                        }

                        // 控制连接断开后由监听器回收该连接上的分配，否则中继 socket 与端口会一直泄漏
                        Assert.IsTrue(
                            await WaitUntilAsync(() => server.AllocationCount == 0, Timeout),
                            "控制连接断开后应释放该连接上的分配");
                    }
                }
            }
            finally
            {
                TryDelete(certificatePath);
            }
        }

        /// <summary>
        /// 自签证书在默认校验策略下必须让客户端握手失败，且客户端不得进入已认证状态。
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task TurnTls_证书校验失败_连接中止()
        {
            var certificatePath = CreateCertificateFile();
            var options = new TurnServerOptions
            {
                SharedSecret = SharedSecret,
                Realm = Realm,
                RelayAddress = Host
            };

            try
            {
                using (var server = new TurnServerHost(options))
                using (var listener = new TurnTlsListener(new IPEndPoint(Host, 0), server, certificatePath, CertificatePassword))
                {
                    listener.Start();

                    using (var client = new TurnTlsClientTransport(listener.LocalEndPoint, "localhost", false))
                    {
                        Exception error = null;
                        try
                        {
                            await client.ConnectAsync(CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            error = ex;
                        }

                        Assert.IsNotNull(error, "自签证书应被默认校验策略拒绝");
                        Assert.IsFalse(client.IsAuthenticated, "握手失败后不应处于已认证状态");
                    }
                }
            }
            finally
            {
                TryDelete(certificatePath);
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
        /// 先发无凭据 Allocate 拿到 401，再取出 NONCE
        /// </summary>
        /// <param name="client">TLS 控制连接</param>
        /// <returns>nonce</returns>
        private static async Task<string> GetNonceAsync(TlsChannel client)
        {
            var challenge = await client.ExchangeAsync(TurnMessage.CreateAllocate(TurnServerHost.UdpTransport), Timeout);

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
        /// 解码 ChannelData 并取出应用数据；失败时返回 null
        /// </summary>
        /// <param name="data">完整 ChannelData 报文，尾部可能含补齐字节</param>
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
        /// 一条 TLS 控制连接：写入整条报文，按分帧器读取自定界帧
        /// </summary>
        private sealed class TlsChannel : IDisposable
        {
            /// <summary>
            /// 客户端 TLS 承载
            /// </summary>
            private readonly TurnTlsClientTransport _transport;

            /// <summary>
            /// 连接上的字节流
            /// </summary>
            private readonly Stream _stream;

            /// <summary>
            /// 流式分帧器；半包状态跨多次读取保留
            /// </summary>
            private readonly TurnStreamFramer _framer = new TurnStreamFramer();

            /// <summary>
            /// 单次读取的缓冲
            /// </summary>
            private readonly byte[] _buffer = new byte[8192];

            /// <summary>
            /// 包装已连接的承载
            /// </summary>
            /// <param name="transport">已完成握手的承载</param>
            private TlsChannel(TurnTlsClientTransport transport)
            {
                _transport = transport;
                _stream = transport.Stream;
            }

            /// <summary>
            /// 连接指定端点并完成 TLS 握手；自签证书只在测试里显式放行
            /// </summary>
            /// <param name="target">目标端点</param>
            /// <returns>控制连接</returns>
            public static async Task<TlsChannel> ConnectAsync(IPEndPoint target)
            {
                var transport = new TurnTlsClientTransport(target, "localhost", true);
                try
                {
                    await transport.ConnectAsync(CancellationToken.None);
                    return new TlsChannel(transport);
                }
                catch (Exception)
                {
                    transport.Dispose();
                    throw;
                }
            }

            /// <summary>
            /// 写入一条完整报文
            /// </summary>
            /// <param name="data">报文</param>
            /// <returns>写入任务</returns>
            public async Task SendAsync(byte[] data)
            {
                await _stream.WriteAsync(new ReadOnlyMemory<byte>(data));
                await _stream.FlushAsync();
            }

            /// <summary>
            /// 读取第一条满足条件的帧；不匹配的帧直接丢弃
            /// </summary>
            /// <param name="predicate">帧匹配条件</param>
            /// <param name="timeout">等待上限</param>
            /// <returns>帧的完整字节；超时或连接关闭时为 null</returns>
            public async Task<byte[]> ReadAsync(Func<TurnStreamFrame, bool> predicate, TimeSpan timeout)
            {
                var deadline = DateTime.UtcNow + timeout;
                while (DateTime.UtcNow < deadline)
                {
                    TurnStreamFrame frame;
                    while (_framer.TryReadFrame(out frame))
                    {
                        if (predicate(frame))
                        {
                            return frame.Data;
                        }
                    }

                    using (var cts = new CancellationTokenSource(timeout))
                    {
                        int read;
                        try
                        {
                            read = await _stream.ReadAsync(new Memory<byte>(_buffer), cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            return null;
                        }
                        catch (IOException)
                        {
                            return null;
                        }
                        catch (ObjectDisposedException)
                        {
                            return null;
                        }

                        if (read <= 0)
                        {
                            return null;
                        }

                        _framer.Feed(new ReadOnlySpan<byte>(_buffer, 0, read));
                    }
                }
                return null;
            }

            /// <summary>
            /// 发送请求并等待事务 ID 相同的应答
            /// </summary>
            /// <param name="request">请求</param>
            /// <param name="timeout">等待上限</param>
            /// <returns>应答；超时为 null</returns>
            public async Task<StunMessage> ExchangeAsync(StunMessage request, TimeSpan timeout)
            {
                var transactionId = request.TransactionId.ToArray();
                await SendAsync(request.ToArray());

                var data = await ReadAsync(
                    frame => frame.Kind == TurnStreamFrameKind.Stun && HasTransactionId(frame.Data, transactionId),
                    timeout);
                if (data == null)
                {
                    return null;
                }

                StunMessage message;
                StunParseError error;
                return StunMessage.TryParse(data, out message, out error) ? message : null;
            }

            /// <summary>
            /// 关闭流与连接
            /// </summary>
            public void Dispose()
            {
                try
                {
                    _stream.Dispose();
                }
                catch (ObjectDisposedException)
                {
                }

                _transport.Dispose();
            }

            /// <summary>
            /// 判断报文是否为指定事务的 STUN 消息
            /// </summary>
            /// <param name="data">报文</param>
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
        }

        /// <summary>
        /// 数据报收件箱：常驻订阅并缓存，避免请求间隙丢包
        /// </summary>
        private sealed class UdpInbox : IDisposable
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
            public UdpInbox(IUdpTransport transport)
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