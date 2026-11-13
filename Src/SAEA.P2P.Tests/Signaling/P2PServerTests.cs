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
*文件名： P2PServerTests
*版本号： net10.0
*唯一标识：3bda5407-b287-468c-8892-80898fb03aaf
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 18:32:49
*描述：信令服务器用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 18:32:49
*修改人： yswenli
*版本号： net10.0
*描述：信令服务器用例
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Builder;
using SAEA.P2P.Common;
using SAEA.P2P.Core;
using SAEA.P2P.Protocol;
using SAEA.P2P.Signaling;

namespace SAEA.P2P.Tests.Signaling
{
    /// <summary>
    /// 信令服务器：重复节点标识的顶替与拒绝、会话超时清理、目标离线错误码、旧格式裸负载鉴权放行。
    /// 每个用例都在本机回环上起真实服务端，通过裸 TcpClient 收发帧，不依赖任何外部服务。
    /// </summary>
    [TestClass]
    public class P2PServerTests
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
        /// 等待单条报文的默认预算
        /// </summary>
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 默认配置下重复节点标识由新连接顶替：旧连接收到 EP03 并被断开，新连接保持可用。
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public void P2PServer_重复peerId_默认顶替旧连接()
        {
            var port = ReserveTcpPort();
            var server = new P2PServer(CreateOptions(port, false));
            TestSignalingClient clientA = null;
            TestSignalingClient clientB = null;

            try
            {
                server.Start();

                clientA = new TestSignalingClient(port);
                clientB = new TestSignalingClient(port);

                clientA.Send(P2PMessageType.Register, CreateRegister(PeerA));
                var ackA = SignalingJsonCodec.Decode(clientA.ReceiveUntil(P2PMessageType.RegisterAck, DefaultTimeout).Content);
                Assert.AreEqual(P2PMessageType.RegisterAck, ackA.Type, "A 注册应答不是 RegisterAck");
                Assert.AreEqual(PeerA, ackA.PeerId, "注册应答里的节点标识与请求不一致");
                Assert.IsFalse(string.IsNullOrEmpty(ackA.SessionId), "注册应答缺少会话标识");

                clientB.Send(P2PMessageType.Register, CreateRegister(PeerA));

                // 服务端先给旧连接发 Error、断开它，再给新连接回 RegisterAck；
                // 旧连接被断开时是 RST，会丢弃接收缓冲里未读的帧，所以必须先在旧连接上等 Error
                var errorA = SignalingJsonCodec.Decode(clientA.ReceiveUntil(P2PMessageType.Error, DefaultTimeout).Content);
                Assert.AreEqual(P2PMessageType.Error, errorA.Type, "旧连接未收到 Error 报文");
                Assert.AreEqual(ErrorCode.RegisterPeerIdDuplicate, errorA.Code, "旧连接收到的错误码不是节点标识重复");

                var ackB = SignalingJsonCodec.Decode(clientB.ReceiveUntil(P2PMessageType.RegisterAck, DefaultTimeout).Content);
                Assert.AreEqual(P2PMessageType.RegisterAck, ackB.Type, "B 顶替注册未拿到 RegisterAck");
                Assert.AreEqual(PeerA, ackB.PeerId, "B 的注册应答节点标识与请求不一致");

                clientA.WaitUntilClosed(TimeSpan.FromSeconds(3));
                Assert.IsTrue(clientA.IsClosed, "旧连接收到 EP03 后未被服务端断开");

                // 顶替后节点映射应指向 B，心跳应答只能由 B 的会话收到
                clientB.Send(P2PMessageType.Heartbeat, SignalingMessage.Create(P2PMessageType.Heartbeat));
                var heartbeatAck = clientB.ReceiveUntil(P2PMessageType.HeartbeatAck, DefaultTimeout);
                Assert.AreEqual(P2PMessageType.HeartbeatAck, heartbeatAck.Type, "新连接顶替后心跳未收到应答");
            }
            finally
            {
                clientB?.Dispose();
                clientA?.Dispose();
                server.Stop();
            }
        }

        /// <summary>
        /// RejectDuplicatePeer 为 true 时拒绝新连接：新连接收到 EP03 并被断开，旧连接继续可用。
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public void P2PServer_重复peerId_配置拒绝_拒绝新连接()
        {
            var port = ReserveTcpPort();
            var server = new P2PServer(CreateOptions(port, true));
            TestSignalingClient clientA = null;
            TestSignalingClient clientB = null;

            try
            {
                server.Start();

                clientA = new TestSignalingClient(port);
                clientB = new TestSignalingClient(port);

                clientA.Send(P2PMessageType.Register, CreateRegister(PeerA));
                var ackA = SignalingJsonCodec.Decode(clientA.ReceiveUntil(P2PMessageType.RegisterAck, DefaultTimeout).Content);
                Assert.AreEqual(PeerA, ackA.PeerId, "A 注册应答里的节点标识与请求不一致");

                clientB.Send(P2PMessageType.Register, CreateRegister(PeerA));
                var errorB = SignalingJsonCodec.Decode(clientB.ReceiveUntil(P2PMessageType.Error, DefaultTimeout).Content);
                Assert.AreEqual(P2PMessageType.Error, errorB.Type, "新连接未收到 Error 报文");
                Assert.AreEqual(ErrorCode.RegisterPeerIdDuplicate, errorB.Code, "新连接收到的错误码不是节点标识重复");

                clientB.WaitUntilClosed(TimeSpan.FromSeconds(3));
                Assert.IsTrue(clientB.IsClosed, "被拒绝的新连接未被服务端断开");

                // 旧连接不受影响，心跳仍能拿到应答
                clientA.Send(P2PMessageType.Heartbeat, SignalingMessage.Create(P2PMessageType.Heartbeat));
                var heartbeatAck = clientA.ReceiveUntil(P2PMessageType.HeartbeatAck, DefaultTimeout);
                Assert.AreEqual(P2PMessageType.HeartbeatAck, heartbeatAck.Type, "拒绝新连接后旧连接心跳未收到应答");
            }
            finally
            {
                clientB?.Dispose();
                clientA?.Dispose();
                server.Stop();
            }
        }

        /// <summary>
        /// 会话超过 SessionTimeoutMs 未活动时，服务端删除会话并向两端各发一条会话超时的 SessionClose。
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public void P2PServer_会话超时_清理并通知()
        {
            var port = ReserveTcpPort();
            var options = CreateOptions(port, false);
            options.Timeout.SessionTimeoutMs = 800;
            // 连接空闲回收远大于会话超时，保证是会话扫描先命中而不是传输层先断开
            options.Timeout.FreeTimeMs = 60000;
            var server = new P2PServer(options);
            TestSignalingClient clientA = null;
            TestSignalingClient clientB = null;

            try
            {
                server.Start();

                clientA = new TestSignalingClient(port);
                clientB = new TestSignalingClient(port);

                clientA.Send(P2PMessageType.Register, CreateRegister(PeerA));
                clientA.ReceiveUntil(P2PMessageType.RegisterAck, DefaultTimeout);

                clientB.Send(P2PMessageType.Register, CreateRegister(PeerB));
                clientB.ReceiveUntil(P2PMessageType.RegisterAck, DefaultTimeout);

                var request = SignalingMessage.Create(P2PMessageType.ConnectRequest);
                request.TargetPeerId = PeerB;
                request.SessionId = "sess-timeout";
                clientA.Send(P2PMessageType.ConnectRequest, request);

                var forwarded = SignalingJsonCodec.Decode(clientB.ReceiveUntil(P2PMessageType.ConnectRequest, DefaultTimeout).Content);
                Assert.AreEqual(PeerA, forwarded.PeerId, "转发的 ConnectRequest 未回填发起方节点标识");
                Assert.AreEqual("sess-timeout", forwarded.SessionId, "转发的 ConnectRequest 会话标识被改写");

                // 之后两端都不再发报文，只剩超时扫描会触发通知
                var stopwatch = Stopwatch.StartNew();
                var closeA = SignalingJsonCodec.Decode(clientA.ReceiveUntil(P2PMessageType.SessionClose, TimeSpan.FromSeconds(6)).Content);
                var closeB = SignalingJsonCodec.Decode(clientB.ReceiveUntil(P2PMessageType.SessionClose, TimeSpan.FromSeconds(6)).Content);
                stopwatch.Stop();

                Assert.AreEqual(P2PMessageType.SessionClose, closeA.Type, "发起方收到的不是 SessionClose");
                Assert.AreEqual("sess-timeout", closeA.SessionId, "发起方收到的 SessionClose 会话标识不一致");
                Assert.AreEqual("会话超时", closeA.Reason, "发起方收到的 SessionClose 原因不是会话超时");

                Assert.AreEqual(P2PMessageType.SessionClose, closeB.Type, "应答方收到的不是 SessionClose");
                Assert.AreEqual("sess-timeout", closeB.SessionId, "应答方收到的 SessionClose 会话标识不一致");
                Assert.AreEqual("会话超时", closeB.Reason, "应答方收到的 SessionClose 原因不是会话超时");

                Assert.IsTrue(stopwatch.Elapsed <= TimeSpan.FromSeconds(6),
                    "会话超时清理与通知耗时超过 6s，实际 " + stopwatch.Elapsed);
            }
            finally
            {
                clientB?.Dispose();
                clientA?.Dispose();
                server.Stop();
            }
        }

        /// <summary>
        /// ConnectRequest 指向不在线的目标节点时回 ES03，而不是静默丢弃。
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public void P2PServer_ConnectRequest目标离线_返回ES03()
        {
            var port = ReserveTcpPort();
            var server = new P2PServer(CreateOptions(port, false));
            TestSignalingClient clientA = null;

            try
            {
                server.Start();

                clientA = new TestSignalingClient(port);

                clientA.Send(P2PMessageType.Register, CreateRegister(PeerA));
                clientA.ReceiveUntil(P2PMessageType.RegisterAck, DefaultTimeout);

                var request = SignalingMessage.Create(P2PMessageType.ConnectRequest);
                request.TargetPeerId = "peer-x";
                request.SessionId = "sess-x";
                clientA.Send(P2PMessageType.ConnectRequest, request);

                var error = SignalingJsonCodec.Decode(clientA.ReceiveUntil(P2PMessageType.Error, DefaultTimeout).Content);
                Assert.AreEqual(P2PMessageType.Error, error.Type, "目标离线未回 Error 报文");
                Assert.AreEqual(ErrorCode.SessionTargetOffline, error.Code, "目标离线返回的错误码不是 ES03");
            }
            finally
            {
                clientA?.Dispose();
                server.Stop();
            }
        }

        /// <summary>
        /// 旧格式注册用裸字符串负载，服务端回裸 OK| 应答与挑战；裸 AuthResponse 直接放行且连接仍可用。
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public void P2PServer_旧格式鉴权_裸字符串应答放行()
        {
            var port = ReserveTcpPort();
            var server = new P2PServer(CreateOptions(port, false));
            TestSignalingClient client = null;

            try
            {
                server.Start();

                client = new TestSignalingClient(port);

                client.SendRaw(P2PMessageType.Register, Encoding.UTF8.GetBytes("peer-legacy"));
                var ack = client.ReceiveUntil(P2PMessageType.RegisterAck, DefaultTimeout);
                var ackText = Encoding.UTF8.GetString(ack.Content);
                Assert.IsTrue(ackText.StartsWith("OK|", StringComparison.Ordinal), "旧格式注册应答不是裸 OK| 负载：" + ackText);

                var challenge = client.ReceiveUntil(P2PMessageType.AuthChallenge, DefaultTimeout);
                var challengeText = Encoding.UTF8.GetString(challenge.Content);
                Assert.IsFalse(string.IsNullOrEmpty(challengeText), "旧格式鉴权挑战负载为空");
                Assert.AreEqual(32, challengeText.Length, "旧格式鉴权挑战应为 32 位十六进制串：" + challengeText);

                client.SendRaw(P2PMessageType.AuthResponse, Encoding.UTF8.GetBytes("anything"));
                var success = client.ReceiveUntil(P2PMessageType.AuthSuccess, DefaultTimeout);
                Assert.AreEqual(P2PMessageType.AuthSuccess, success.Type, "旧格式鉴权应答未放行");
                Assert.AreEqual(0, success.Content.Length, "AuthSuccess 不应携带负载");

                // 放行后连接必须仍然可用，未被鉴权逻辑断开
                client.Send(P2PMessageType.Heartbeat, SignalingMessage.Create(P2PMessageType.Heartbeat));
                var heartbeatAck = client.ReceiveUntil(P2PMessageType.HeartbeatAck, DefaultTimeout);
                Assert.AreEqual(P2PMessageType.HeartbeatAck, heartbeatAck.Type, "旧格式鉴权放行后心跳未收到应答");
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
            /// 轮询等待对端关闭，期间读到的帧一律丢弃
            /// </summary>
            /// <param name="timeout">最长等待时间</param>
            public void WaitUntilClosed(TimeSpan timeout)
            {
                var stopwatch = Stopwatch.StartNew();
                while (!IsClosed && stopwatch.Elapsed < timeout)
                {
                    TryReceive(TimeSpan.FromMilliseconds(100), out _);
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