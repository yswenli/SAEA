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
*文件名： TurnDualModeTests
*版本号： net10.0
*唯一标识：3f5c8a71-2d64-4b1e-9c07-8a5d6e2f4b19
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/16 11:00:00
*描述：TURN TCP/TLS 双栈监听回归用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/16 11:00:00
*修改人： yswenli
*版本号： net10.0
*描述：TURN TCP/TLS 双栈监听回归用例
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
using SAEA.P2P.Transport;

using TurnServerHost = SAEA.P2P.TurnServer.TurnServer;

namespace SAEA.P2P.Tests.TurnServer
{
    /// <summary>
    /// TURN over TCP/TLS 的双栈监听：监听 IPv6 任意地址时必须打开 DualMode，
    /// IPv4 客户端才能连上并完成 Allocate；监听具体 IPv4 地址时保持单栈语义。
    /// </summary>
    [TestCategory(P2PTestSuite.Unit)]
    [TestClass]
    public class TurnDualModeTests
    {
        /// <summary>
        /// 测试统一使用 IPv4 回环地址作为中继地址与客户端来源
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
        private const string CertificatePassword = "saea-turn-dualmode-test-password";

        /// <summary>
        /// 单次请求与连接建立的等待上限
        /// </summary>
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// TCP 监听绑定 IPv6Any 时，IPv4 客户端连 127.0.0.1 也能完成 Allocate
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task TurnTcpListener_绑IPv6Any_接受IPv4客户端并完成Allocate()
        {
            var relayPort = ReserveUdpPorts(1)[0];
            var options = new TurnServerOptions
            {
                SharedSecret = SharedSecret,
                Realm = Realm,
                RelayAddress = Host,
                RelayPortMin = relayPort,
                RelayPortMax = relayPort
            };

            using (var server = new TurnServerHost(options))
            using (var listener = new TurnTcpListener(new IPEndPoint(IPAddress.IPv6Any, 0), server))
            {
                listener.Start();
                var localEndPoint = listener.LocalEndPoint;
                Assert.IsNotNull(localEndPoint, "监听启动后应能取到实际端点");
                Assert.AreEqual(IPAddress.IPv6Any, localEndPoint.Address, "监听地址应为 IPv6Any");

                using (var channel = await StreamChannel.ConnectTcpAsync(new IPEndPoint(Host, localEndPoint.Port), Timeout))
                {
                    await AssertAllocateSucceedsAsync(server, channel, relayPort);
                }
            }
        }

        /// <summary>
        /// TLS 监听绑定 IPv6Any 时，IPv4 客户端能连上并完成 TLS 握手与 Allocate
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task TurnTlsListener_绑IPv6Any_接受IPv4客户端并完成TLS握手()
        {
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
                using (var listener = new TurnTlsListener(new IPEndPoint(IPAddress.IPv6Any, 0), server,
                    certificatePath, CertificatePassword))
                {
                    listener.Start();
                    var localEndPoint = listener.LocalEndPoint;
                    Assert.IsNotNull(localEndPoint, "监听启动后应能取到实际端点");
                    Assert.AreEqual(IPAddress.IPv6Any, localEndPoint.Address, "监听地址应为 IPv6Any");

                    using (var channel = await StreamChannel.ConnectTlsAsync(
                        new IPEndPoint(Host, localEndPoint.Port), Timeout))
                    {
                        Assert.IsTrue(channel.IsAuthenticated, "IPv4 客户端应完成 TLS 握手");

                        // 握手之后还能跑完整业务流，说明双栈下的控制连接确实被 TURN 会话接管
                        await AssertAllocateSucceedsAsync(server, channel, relayPort);
                    }
                }
            }
            finally
            {
                TryDelete(certificatePath);
            }
        }

        /// <summary>
        /// TCP 监听绑定具体 IPv4 地址时不开启双栈，IPv6 客户端连 ::1 应被拒绝
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task TurnTcpListener_绑IPv4_不接受IPv6客户端()
        {
            var options = new TurnServerOptions
            {
                SharedSecret = SharedSecret,
                Realm = Realm,
                RelayAddress = Host
            };

            using (var server = new TurnServerHost(options))
            using (var listener = new TurnTcpListener(new IPEndPoint(Host, 0), server))
            {
                listener.Start();
                var port = listener.LocalEndPoint.Port;

                // 正方向先确认真实可用，避免「端口本就没起来」也被当成双栈未误开
                using (var v4 = await StreamChannel.ConnectTcpAsync(new IPEndPoint(Host, port), Timeout))
                {
                    Assert.IsTrue(v4.IsConnected, "IPv4 客户端应能连上 IPv4 监听");
                }

                var failed = false;
                try
                {
                    using (var v6 = await StreamChannel.ConnectTcpAsync(
                        new IPEndPoint(IPAddress.IPv6Loopback, port), Timeout))
                    {
                        failed = !v6.IsConnected;
                    }
                }
                catch (SocketException)
                {
                    failed = true;
                }
                catch (IOException)
                {
                    failed = true;
                }

                Assert.IsTrue(failed, "IPv4 监听不应接受 IPv6 客户端");
            }
        }

        /// <summary>
        /// 走完整的 401 挑战到带鉴权 Allocate，并校验中继地址与端口来自配置
        /// </summary>
        /// <param name="server">TURN 服务端</param>
        /// <param name="channel">已完成连接（TLS 则含握手）的控制连接</param>
        /// <param name="relayPort">配置的中继端口</param>
        /// <returns>完成的任务</returns>
        private static async Task AssertAllocateSucceedsAsync(TurnServerHost server, StreamChannel channel, int relayPort)
        {
            var credential = server.CredentialService.Create("learner");
            var nonce = await GetNonceAsync(channel);

            var allocate = Sign(TurnMessage.CreateAllocate(TurnServerHost.UdpTransport), credential, nonce);
            var response = await channel.ExchangeAsync(allocate, Timeout);

            Assert.IsNotNull(response, "Allocate 应得到应答");
            Assert.AreEqual(StunClass.SuccessResponse, response.Class, "带鉴权的 Allocate 应成功");

            var relayed = DecodeAddress(response, StunAttributeType.XorRelayedAddress);
            Assert.IsNotNull(relayed, "Allocate 应答应携带 XOR-RELAYED-ADDRESS");
            Assert.AreEqual(Host, relayed.Address, "中继地址应为配置的中继地址");
            Assert.AreEqual(relayPort, relayed.Port, "中继端口应来自配置区间");
            Assert.AreEqual(1, server.AllocationCount, "分配成功后应有一条记录");
        }

        /// <summary>
        /// 先发无凭据 Allocate 拿到 401，再取出 NONCE
        /// </summary>
        /// <param name="channel">控制连接</param>
        /// <returns>nonce</returns>
        private static async Task<string> GetNonceAsync(StreamChannel channel)
        {
            var challenge = await channel.ExchangeAsync(TurnMessage.CreateAllocate(TurnServerHost.UdpTransport), Timeout);

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
                    var path = Path.Combine(Path.GetTempPath(), "saea-turn-dualmode-" + Guid.NewGuid().ToString("N") + ".pfx");
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
        /// 一条 TURN 控制连接：写入整条报文，按分帧器读取自定界帧；TCP 与 TLS 共用同一套读写逻辑
        /// </summary>
        private sealed class StreamChannel : IDisposable
        {
            /// <summary>
            /// 流对象；由下层连接或 TLS 承载提供
            /// </summary>
            private readonly Stream _stream;

            /// <summary>
            /// 释放下层连接的回调
            /// </summary>
            private readonly Action _release;

            /// <summary>
            /// 流式分帧器；半包状态跨多次读取保留
            /// </summary>
            private readonly TurnStreamFramer _framer = new TurnStreamFramer();

            /// <summary>
            /// 单次读取的缓冲
            /// </summary>
            private readonly byte[] _buffer = new byte[8192];

            /// <summary>
            /// 包装已连接的流
            /// </summary>
            /// <param name="stream">连接上的字节流</param>
            /// <param name="release">释放下层连接的回调</param>
            /// <param name="authenticated">是否已完成 TLS 握手；明文连接恒为 true</param>
            private StreamChannel(Stream stream, Action release, bool authenticated)
            {
                _stream = stream;
                _release = release;
                IsAuthenticated = authenticated;
            }

            /// <summary>
            /// 是否为已建立且未被释放的连接
            /// </summary>
            public bool IsConnected
            {
                get { return _stream.CanRead; }
            }

            /// <summary>
            /// 是否已完成 TLS 握手；明文连接恒为 true
            /// </summary>
            public bool IsAuthenticated { get; private set; }

            /// <summary>
            /// 建立明文 TCP 连接
            /// </summary>
            /// <param name="target">目标端点；地址族决定客户端套接字类型</param>
            /// <param name="timeout">连接建立的等待上限</param>
            /// <returns>控制连接</returns>
            public static async Task<StreamChannel> ConnectTcpAsync(IPEndPoint target, TimeSpan timeout)
            {
                var client = new TcpClient(target.AddressFamily);
                try
                {
                    using (var cts = new CancellationTokenSource(timeout))
                    {
                        await client.ConnectAsync(target.Address, target.Port, cts.Token);
                    }
                    return new StreamChannel(client.GetStream(), client.Dispose, true);
                }
                catch (Exception)
                {
                    client.Dispose();
                    throw;
                }
            }

            /// <summary>
            /// 建立 TLS 连接并完成握手；自签证书只在测试里显式放行
            /// </summary>
            /// <param name="target">目标端点</param>
            /// <param name="timeout">握手等待上限</param>
            /// <returns>控制连接</returns>
            public static async Task<StreamChannel> ConnectTlsAsync(IPEndPoint target, TimeSpan timeout)
            {
                var transport = new TurnTlsClientTransport(target, "localhost", true);
                try
                {
                    using (var cts = new CancellationTokenSource(timeout))
                    {
                        await transport.ConnectAsync(cts.Token);
                    }
                    return new StreamChannel(transport.Stream, transport.Dispose, transport.IsAuthenticated);
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
            /// 关闭流与下层连接
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

                _release();
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
    }
}