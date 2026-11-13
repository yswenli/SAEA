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
*命名空间：SAEA.P2P.Tests.Signaling
*文件名： SignalingClientTests
*版本号： net10.0
*唯一标识：e957db7e-1df6-4a04-b9b5-84775159a0a5
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 10:02:23
*描述：信令客户端用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 10:02:23
*修改人： yswenli
*版本号： net10.0
*描述：信令客户端用例
*
 *****************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Builder;
using SAEA.P2P.Core;
using SAEA.P2P.Protocol;
using SAEA.P2P.Signaling;

namespace SAEA.P2P.Tests.Signaling
{
    /// <summary>
    /// 信令客户端：注册应答、断线重连后补发未完成候选、服务端下发的 TURN 临时凭据字段透传。
    /// 每个用例都在本机回环上起真实服务端，裸 TcpClient 只作为对端辅助，不依赖任何外部服务。
    /// </summary>
    [TestClass]
    public class SignalingClientTests
    {
        /// <summary>
        /// 发起方节点标识
        /// </summary>
        private const string PeerA = "peer-a";

        /// <summary>
        /// 应答方节点标识
        /// </summary>
        private const string PeerB = "peer-b";

        /// <summary>
        /// 用例内共用的信令会话标识
        /// </summary>
        private const string SessionId = "sess-1";

        /// <summary>
        /// 等待单条报文的默认预算
        /// </summary>
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 注册成功后 RegisterAsync 正常返回，连接态、服务端会话标识与 OnRegistered 报文都应一致。
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task SignalingClient_注册_收到RegisterAck()
        {
            var port = ReserveTcpPort();
            var server = new P2PServer(CreateOptions(port, false));
            SignalingClient client = null;
            var registeredTcs = new TaskCompletionSource<SignalingMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                server.Start();

                client = new SignalingClient(new SignalingOptions
                {
                    PeerId = PeerA,
                    ServerPort = port,
                    ReconnectIntervalMs = 200,
                    HeartbeatIntervalMs = 2000
                });
                client.OnRegistered += message => registeredTcs.TrySetResult(message);

                await client.RegisterAsync();

                Assert.IsTrue(client.IsRegistered, "RegisterAsync 返回后 IsRegistered 应为 true");
                Assert.IsTrue(client.IsConnected, "RegisterAsync 返回后 IsConnected 应为 true");
                Assert.IsFalse(string.IsNullOrEmpty(client.ServerSessionId), "注册后应记录服务端会话标识");

                var completed = await Task.WhenAny(registeredTcs.Task, Task.Delay(DefaultTimeout));
                Assert.IsTrue(ReferenceEquals(completed, registeredTcs.Task), "5s 内未收到 OnRegistered");

                var registered = await registeredTcs.Task;
                Assert.AreEqual(client.ServerSessionId, registered.SessionId,
                    "OnRegistered 报文的会话标识与服务端会话标识不一致");
                Assert.IsTrue(server.NodeCount >= 1, "注册后服务端在线节点数应至少为 1，实际 " + server.NodeCount);
            }
            finally
            {
                client?.Dispose();
                server.Stop();
            }
        }

        /// <summary>
        /// 未注册时发送的候选进入待发队列，重连成功并用同一会话标识重建会话后按序补发到对端。
        /// </summary>
        [TestMethod]
        [Timeout(90000)]
        public async Task SignalingClient_断线重连_重发未完成候选()
        {
            var port = ReserveTcpPort();
            var server = new P2PServer(CreateOptions(port, false));
            SignalingClient clientA = null;
            TestSignalingClient clientB = null;
            TestSignalingClient clientC = null;

            var aErrors = new List<string>();
            var aDisconnects = new List<string>();
            var aRegisteredCount = 0;
            var aSendConnectError = (string)null;
            var aConnectRequestTcs = new TaskCompletionSource<SignalingMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                server.Start();

                clientA = new SignalingClient(new SignalingOptions
                {
                    PeerId = PeerA,
                    ServerPort = port,
                    ReconnectIntervalMs = 200,
                    RegisterTimeoutMs = 5000,
                    HeartbeatIntervalMs = 2000
                });
                clientA.OnError += (code, message) =>
                {
                    lock (aErrors)
                    {
                        aErrors.Add(code + "：" + (message ?? "<空>"));
                    }
                };
                clientA.OnDisconnected += reason =>
                {
                    lock (aDisconnects)
                    {
                        aDisconnects.Add(reason ?? "<空>");
                    }
                };
                clientA.OnConnectRequest += message => aConnectRequestTcs.TrySetResult(message);
                clientA.OnRegistered += message =>
                {
                    var count = Interlocked.Increment(ref aRegisteredCount);

                    // 首次注册只为建链；重连成功后才用同一 sessionId 重建会话，随后客户端会补发候选
                    if (count < 2)
                    {
                        return;
                    }

                    try
                    {
                        var request = SignalingMessage.Create(P2PMessageType.ConnectRequest);
                        request.TargetPeerId = PeerB;
                        request.SessionId = SessionId;
                        clientA.SendAsync(request).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        // 事件在接收线程上同步执行，异常不能冒出去，否则会打断后续帧的处理
                        Interlocked.CompareExchange(ref aSendConnectError, ex.GetType().Name + "：" + ex.Message, null);
                    }
                };

                await clientA.RegisterAsync();
                Assert.IsTrue(clientA.IsRegistered, "A 首次注册后 IsRegistered 应为 true");

                clientB = new TestSignalingClient(port);
                clientB.Send(P2PMessageType.Register, CreateRegister(PeerB));
                var ackB = SignalingJsonCodec.Decode(clientB.ReceiveUntil(P2PMessageType.RegisterAck, DefaultTimeout).Content);
                Assert.AreEqual(PeerB, ackB.PeerId, "B 注册应答里的节点标识与请求不一致");

                var connect = SignalingMessage.Create(P2PMessageType.ConnectRequest);
                connect.TargetPeerId = PeerA;
                connect.SessionId = SessionId;
                clientB.Send(P2PMessageType.ConnectRequest, connect);

                var connectCompleted = await Task.WhenAny(aConnectRequestTcs.Task, Task.Delay(DefaultTimeout));
                Assert.IsTrue(ReferenceEquals(connectCompleted, aConnectRequestTcs.Task),
                    "5s 内 A 未收到 B 发起的 ConnectRequest；A 错误：" + Snapshot(aErrors));

                // 基线：已注册状态下候选应立刻转发给会话另一端
                await clientA.SendAsync(CreateCandidate(SessionId, 1u, 40001));
                var baseline = SignalingJsonCodec.Decode(clientB.ReceiveUntil(P2PMessageType.TrickleCandidate, DefaultTimeout).Content);
                Assert.AreEqual(SessionId, baseline.SessionId, "基线候选的会话标识被改写");
                Assert.IsNotNull(baseline.Candidates, "基线候选的候选列表为空");
                Assert.IsTrue(baseline.Candidates.Length > 0, "基线候选的候选列表长度为 0");
                Assert.AreEqual(40001, baseline.Candidates[0].Port, "基线候选端口与发送值不一致");

                // 用同一 peerId 顶替 A，制造 A 掉线
                clientC = new TestSignalingClient(port);
                clientC.Send(P2PMessageType.Register, CreateRegister(PeerA));
                var ackC = SignalingJsonCodec.Decode(clientC.ReceiveUntil(P2PMessageType.RegisterAck, DefaultTimeout).Content);
                Assert.AreEqual(PeerA, ackC.PeerId, "顶替注册的应答节点标识与请求不一致");

                var unregisteredStopwatch = Stopwatch.StartNew();
                while (clientA.IsRegistered && unregisteredStopwatch.Elapsed < TimeSpan.FromSeconds(5))
                {
                    await Task.Delay(50);
                }

                Assert.IsFalse(clientA.IsRegistered,
                    "C 顶替后 5s 内 A 仍处于已注册状态；A 错误：" + Snapshot(aErrors)
                    + "；A 断开记录：" + Snapshot(aDisconnects));

                // 掉线窗口内发候选：候选类报文只入队，不应抛异常
                try
                {
                    await clientA.SendAsync(CreateCandidate(SessionId, 2u, 40002));
                }
                catch (Exception ex)
                {
                    Assert.Fail("未注册时发送候选不应抛异常，实际 " + ex.GetType().Name + "：" + ex.Message
                        + "；A 错误：" + Snapshot(aErrors));
                }

                clientC.Dispose();
                clientC = null;

                var reconnectedStopwatch = Stopwatch.StartNew();
                while (!clientA.IsRegistered && reconnectedStopwatch.Elapsed < TimeSpan.FromSeconds(10))
                {
                    await Task.Delay(50);
                }

                Assert.IsTrue(clientA.IsRegistered,
                    "C 断开后 10s 内 A 未重新注册；A 错误：" + Snapshot(aErrors)
                    + "；A 断开记录：" + Snapshot(aDisconnects)
                    + "；发送重建会话异常：" + (aSendConnectError ?? "无"));

                // 重连后 A 先重建 sess-1，再补发队列里的候选；B 先到的其它帧一律跳过继续等
                var observed = new List<string>();
                var resent = ReceiveTrickleWithPort(clientB, SessionId, 40002, TimeSpan.FromSeconds(10), observed);

                Assert.IsNotNull(resent, "10s 内 B 未收到补发的候选；期间收到：" + Snapshot(observed)
                    + "；A 错误：" + Snapshot(aErrors)
                    + "；发送重建会话异常：" + (aSendConnectError ?? "无"));
                Assert.AreEqual(SessionId, resent.SessionId, "补发候选的会话标识被改写");
                Assert.IsNotNull(resent.Candidates, "补发候选的候选列表为空");
                Assert.IsTrue(resent.Candidates.Length > 0, "补发候选的候选列表长度为 0");
                Assert.AreEqual(40002, resent.Candidates[0].Port, "补发候选端口与掉线期间入队的值不一致");
            }
            finally
            {
                clientC?.Dispose();
                clientB?.Dispose();
                clientA?.Dispose();
                server.Stop();
            }
        }

        /// <summary>
        /// 配置共享密钥与 TURN 地址时，客户端完成鉴权后收到 TurnCredential，领域、用户名、密码、有效期与地址列表完整透传。
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task SignalingClient_TurnCredential_透传字段完整()
        {
            var port = ReserveTcpPort();
            var turnUrls = new[]
            {
                "turn:127.0.0.1:3478?transport=udp",
                "turns:127.0.0.1:5349?transport=tcp"
            };
            var options = CreateOptions(port, false);
            options.SharedSecret = "signaling-secret";
            options.Realm = "saea-turn";
            options.CredentialTtlSeconds = 300;
            options.TurnUrls = turnUrls;

            var server = new P2PServer(options);
            SignalingClient client = null;
            var credentialTcs = new TaskCompletionSource<SignalingMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                server.Start();

                client = new SignalingClient(new SignalingOptions
                {
                    PeerId = "peer-turn",
                    ServerPort = port,
                    SharedSecret = "signaling-secret",
                    ReconnectIntervalMs = 200,
                    HeartbeatIntervalMs = 2000
                });
                client.OnTurnCredential += message => credentialTcs.TrySetResult(message);

                await client.RegisterAsync();

                var completed = await Task.WhenAny(credentialTcs.Task, Task.Delay(TimeSpan.FromSeconds(15)));
                Assert.IsTrue(ReferenceEquals(completed, credentialTcs.Task), "15s 内未收到 TurnCredential");

                var credential = await credentialTcs.Task;

                Assert.AreEqual("saea-turn", credential.Realm, "TURN 领域与服务端配置不一致");
                Assert.IsFalse(string.IsNullOrEmpty(credential.Username), "TURN 临时用户名为空");
                Assert.IsFalse(string.IsNullOrEmpty(credential.Password), "TURN 临时密码为空");
                Assert.IsTrue(credential.Ttl > 0 && credential.Ttl <= 300,
                    "凭据有效期应为大于 0 且不超过 300 秒，实际 " + credential.Ttl);
                Assert.IsNotNull(credential.Urls, "TURN 地址列表为空");
                Assert.AreEqual(2, credential.Urls.Length, "TURN 地址数量与服务端配置不一致");
                CollectionAssert.Contains(credential.Urls, turnUrls[0], "TURN 地址列表缺少 " + turnUrls[0]);
                CollectionAssert.Contains(credential.Urls, turnUrls[1], "TURN 地址列表缺少 " + turnUrls[1]);

                // 密码必须是由共享密钥派生的临时凭据，不能把服务端密钥直接当密码下发
                Assert.AreNotEqual("signaling-secret", credential.Password, "TURN 密码不应等于共享密钥");
                Assert.IsTrue(client.IsRegistered, "收到 TurnCredential 后 IsRegistered 应为 true");
            }
            finally
            {
                client?.Dispose();
                server.Stop();
            }
        }

        /// <summary>
        /// 构造只监听回环的信令服务器配置，日志压到错误级、关闭中继，避免用例输出与无关模块干扰
        /// </summary>
        /// <param name="port">预占的监听端口</param>
        /// <param name="rejectDuplicatePeer">重复节点标识时是否拒绝新连接</param>
        /// <returns>服务器配置</returns>
        private static P2PServerOptions CreateOptions(int port, bool rejectDuplicatePeer)
        {
            var options = new P2PServerOptions
            {
                BindIP = "127.0.0.1",
                Port = port,
                SharedSecret = null,
                RejectDuplicatePeer = rejectDuplicatePeer
            };
            options.Logging.Level = 4;
            return options;
        }

        /// <summary>
        /// 构造新格式注册报文
        /// </summary>
        /// <param name="peerId">节点标识</param>
        /// <returns>注册报文</returns>
        private static SignalingMessage CreateRegister(string peerId)
        {
            var message = SignalingMessage.Create(P2PMessageType.Register);
            message.PeerId = peerId;
            message.NodeName = peerId + "-node";
            message.Capabilities = new[] { "udp", "tcp" };
            return message;
        }

        /// <summary>
        /// 构造只含一个主机候选的 TrickleCandidate 报文
        /// </summary>
        /// <param name="sessionId">信令会话标识</param>
        /// <param name="priority">候选优先级</param>
        /// <param name="candidatePort">候选端口，用于区分不同批次的候选</param>
        /// <returns>候选报文</returns>
        private static SignalingMessage CreateCandidate(string sessionId, uint priority, int candidatePort)
        {
            var message = SignalingMessage.Create(P2PMessageType.TrickleCandidate);
            message.SessionId = sessionId;
            message.Candidates = new[]
            {
                new SignalingCandidate
                {
                    Foundation = "f1",
                    Component = 1,
                    Transport = "udp",
                    Priority = priority,
                    Address = "127.0.0.1",
                    Port = candidatePort,
                    Type = "host"
                }
            };
            return message;
        }

        /// <summary>
        /// 在时间预算内等待指定端口的候选；期间其它帧（含 NodeList、会话拆除通知）只记录并丢弃
        /// </summary>
        /// <param name="client">对端裸客户端</param>
        /// <param name="sessionId">期望的信令会话标识</param>
        /// <param name="candidatePort">期望的候选端口</param>
        /// <param name="timeout">等待预算</param>
        /// <param name="observed">已收到的帧描述，供失败文案定位</param>
        /// <returns>命中的候选报文，超时为 null</returns>
        private static SignalingMessage ReceiveTrickleWithPort(TestSignalingClient client, string sessionId,
            int candidatePort, TimeSpan timeout, List<string> observed)
        {
            var stopwatch = Stopwatch.StartNew();

            while (true)
            {
                var remaining = timeout - stopwatch.Elapsed;
                if (remaining < TimeSpan.Zero)
                {
                    remaining = TimeSpan.Zero;
                }

                if (!client.TryReceive(remaining, out var frame))
                {
                    return null;
                }

                if (frame.Type != P2PMessageType.TrickleCandidate)
                {
                    observed.Add(frame.Type.ToString());
                    continue;
                }

                var message = SignalingJsonCodec.Decode(frame.Content);
                var port = message.Candidates != null && message.Candidates.Length > 0
                    ? message.Candidates[0].Port
                    : -1;
                observed.Add(frame.Type + "(sessionId=" + (message.SessionId ?? "<空>") + ", port=" + port + ")");

                if (string.Equals(message.SessionId, sessionId, StringComparison.Ordinal) && port == candidatePort)
                {
                    return message;
                }
            }
        }

        /// <summary>
        /// 预占一个尚未使用的 TCP 端口：先监听取到系统分配端口，再立即停止交给被测服务端
        /// </summary>
        /// <returns>端口号</returns>
        private static int ReserveTcpPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        /// <summary>
        /// 在锁内取记录快照拼成失败文案；空记录写成「无」
        /// </summary>
        /// <param name="items">跨线程写入的记录</param>
        /// <returns>文案</returns>
        private static string Snapshot(List<string> items)
        {
            lock (items)
            {
                if (items.Count == 0)
                {
                    return "无";
                }

                var builder = new StringBuilder();
                for (var i = 0; i < items.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append("；");
                    }
                    builder.Append(items[i]);
                }
                return builder.ToString();
            }
        }

        /// <summary>
        /// 最小信令客户端：按 P2P 帧格式收发，收到的帧先复制负载再离开解码批次
        /// </summary>
        private sealed class TestSignalingClient : IDisposable
        {
            /// <summary>
            /// 单次读取的缓冲区，够放一个用例里的任意信令帧
            /// </summary>
            private readonly byte[] _buffer = new byte[8 * 1024];

            /// <summary>
            /// 解码器保留了半包缓存，必须每客户端独立持有，不能跨连接共享
            /// </summary>
            private readonly P2PCoder _coder = new P2PCoder();

            /// <summary>
            /// 已解码但还没被取走的帧，按到达顺序排列
            /// </summary>
            private readonly List<ReceivedFrame> _pending = new List<ReceivedFrame>();

            /// <summary>
            /// 已解码帧数与字节数，仅用于失败文案定位「到底有没有数据回来」
            /// </summary>
            private int _decodedFrames;
            private int _decodedBytes;

            private readonly TcpClient _client;
            private readonly NetworkStream _stream;

            /// <summary>
            /// 建立到服务端的回环连接
            /// </summary>
            /// <param name="port">服务端监听端口</param>
            public TestSignalingClient(int port)
            {
                _client = new TcpClient();
                _client.NoDelay = true;
                _client.Connect(IPAddress.Loopback, port);
                _stream = _client.GetStream();
                _stream.ReadTimeout = 3000;
            }

            /// <summary>
            /// 对端已关闭连接：读返回 0 或读失败后置 true
            /// </summary>
            public bool IsClosed { get; private set; }

            /// <summary>
            /// 最近一次读失败的原因，仅用于失败文案定位
            /// </summary>
            public string LastError { get; private set; }

            /// <summary>
            /// 按信令报文编码后发送
            /// </summary>
            /// <param name="type">消息类型</param>
            /// <param name="message">信令报文，负载由 JSON 编解码器生成</param>
            public void Send(P2PMessageType type, SignalingMessage message)
            {
                SendRaw(type, SignalingJsonCodec.Encode(message));
            }

            /// <summary>
            /// 直接发送原始负载，用于旧格式的裸字符串报文
            /// </summary>
            /// <param name="type">消息类型</param>
            /// <param name="content">原始负载</param>
            public void SendRaw(P2PMessageType type, byte[] content)
            {
                var packet = _coder.EncodeP2P(type, content);
                _stream.Write(packet, 0, packet.Length);
                _stream.Flush();
            }

            /// <summary>
            /// 等到目标类型的帧；NodeList 是注册后必然到达的广播帧，直接丢弃继续等。
            /// 其他非目标类型记录在失败文案里，便于定位实际收到了什么。
            /// </summary>
            /// <param name="expected">目标消息类型</param>
            /// <param name="timeout">等待预算</param>
            /// <returns>目标帧</returns>
            public ReceivedFrame ReceiveUntil(P2PMessageType expected, TimeSpan timeout)
            {
                var stopwatch = Stopwatch.StartNew();
                var unexpected = new List<P2PMessageType>();

                while (true)
                {
                    var remaining = timeout - stopwatch.Elapsed;
                    if (remaining < TimeSpan.Zero)
                    {
                        remaining = TimeSpan.Zero;
                    }

                    if (!TryReceive(remaining, out var frame))
                    {
                        Assert.Fail("等待 " + expected + " 超时（" + (long)timeout.TotalMilliseconds
                            + " ms），期间收到：" + DescribeTypes(unexpected)
                            + "；已解码帧 " + _decodedFrames + " 个/" + _decodedBytes + " 字节"
                            + "；读取错误：" + (LastError ?? "无"));
                        return null;
                    }

                    if (frame.Type == expected)
                    {
                        return frame;
                    }

                    if (frame.Type == P2PMessageType.NodeList)
                    {
                        continue;
                    }

                    unexpected.Add(frame.Type);
                }
            }

            /// <summary>
            /// 在时间预算内取一帧：先取已解码的积压帧，再按剩余预算读套接字
            /// </summary>
            /// <param name="timeout">等待预算</param>
            /// <param name="frame">取到的帧，失败时为 null</param>
            /// <returns>取到帧时返回 true</returns>
            public bool TryReceive(TimeSpan timeout, out ReceivedFrame frame)
            {
                frame = null;
                var deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;

                while (true)
                {
                    if (_pending.Count > 0)
                    {
                        frame = _pending[0];
                        _pending.RemoveAt(0);
                        return true;
                    }

                    if (IsClosed)
                    {
                        return false;
                    }

                    var remaining = deadline - Environment.TickCount64;
                    if (remaining <= 0)
                    {
                        return false;
                    }

                    _stream.ReadTimeout = (int)Math.Min(int.MaxValue, Math.Max(1, remaining));

                    int read;
                    try
                    {
                        read = _stream.Read(_buffer, 0, _buffer.Length);
                    }
                    catch (IOException ex) when (IsReadTimeout(ex))
                    {
                        // 读超时只是这次没数据，连接仍可能是通的，回到循环按剩余预算判断
                        continue;
                    }
                    catch (IOException ex)
                    {
                        LastError = "IOException: " + ex.Message + (ex.InnerException != null ? " / " + ex.InnerException.Message : "");
                        IsClosed = true;
                        return false;
                    }
                    catch (SocketException ex)
                    {
                        LastError = "SocketException: " + ex.SocketErrorCode;
                        IsClosed = true;
                        return false;
                    }
                    catch (ObjectDisposedException)
                    {
                        IsClosed = true;
                        return false;
                    }

                    if (read <= 0)
                    {
                        LastError = "对端关闭（read=" + read + "）";
                        IsClosed = true;
                        return false;
                    }

                    _decodedBytes += read;
                    Decode(read);
                }
            }

            /// <summary>
            /// 释放连接
            /// </summary>
            public void Dispose()
            {
                try { _stream?.Dispose(); } catch { }
                try { _client?.Dispose(); } catch { }
            }

            /// <summary>
            /// 把本次读到的字节解成帧，负载必须在解码批次释放前复制出来
            /// </summary>
            /// <param name="count">本次读到的字节数</param>
            private void Decode(int count)
            {
                using (var frames = _coder.DecodeP2P(new ReadOnlySpan<byte>(_buffer, 0, count)))
                {
                    foreach (var item in frames.Frames)
                    {
                        _decodedFrames++;
                        _pending.Add(new ReceivedFrame
                        {
                            Type = (P2PMessageType)item.Type,
                            Content = item.Content.ToArray()
                        });
                    }
                }
            }

            /// <summary>
            /// 判断读失败是否只是套接字读超时
            /// </summary>
            /// <param name="ex">读取抛出的异常</param>
            /// <returns>超时返回 true</returns>
            private static bool IsReadTimeout(IOException ex)
            {
                var socketException = ex.InnerException as SocketException;
                if (socketException == null)
                {
                    return false;
                }
                return socketException.SocketErrorCode == SocketError.TimedOut
                    || socketException.SocketErrorCode == SocketError.WouldBlock;
            }

            /// <summary>
            /// 把已收到的类型列表拼成失败文案；空列表写成「无」
            /// </summary>
            /// <param name="types">已收到的类型</param>
            /// <returns>文案</returns>
            private static string DescribeTypes(List<P2PMessageType> types)
            {
                if (types.Count == 0)
                {
                    return "无";
                }

                var builder = new StringBuilder();
                for (var i = 0; i < types.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(", ");
                    }
                    builder.Append(types[i]);
                }
                return builder.ToString();
            }
        }

        /// <summary>
        /// 一帧的类型与已复制出来的负载
        /// </summary>
        private sealed class ReceivedFrame
        {
            /// <summary>
            /// 消息类型
            /// </summary>
            public P2PMessageType Type;

            /// <summary>
            /// 帧体负载，已脱离解码批次的池化缓冲
            /// </summary>
            public byte[] Content;
        }
    }
}