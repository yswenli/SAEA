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
*文件名： TurnClientTests
*版本号： net10.0
*唯一标识：08657882-f442-4944-97b9-dcf60de53bd5
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 14:20:00
*描述：TURN 客户端鉴权重试、权限续期与断线重连验收
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 16:40:00
*修改人： yswenli
*版本号： net10.0
*描述：去掉压短 RTO 的规避手段，新增释放即返回的回归用例
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
using TurnClientHost = SAEA.P2P.Turn.TurnClient;
using TurnServerHost = SAEA.P2P.TurnServer.TurnServer;

namespace SAEA.P2P.Tests.Turn
{
    /// <summary>
    /// TURN 客户端：401 挑战后同一凭据只重试一次、permission 在到期前按一次性定时器自动续期、
    /// 服务端断线后按指数退避重建传输并重新 Allocate。
    /// </summary>
    [TestClass]
    public class TurnClientTests
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
        /// 假服务端挑战使用的认证域
        /// </summary>
        private const string FakeRealm = "saea.test";

        /// <summary>
        /// 假服务端挑战使用的 nonce，按协议取 20 字节
        /// </summary>
        private const string FakeNonce = "0123456789abcdefghij";

        /// <summary>
        /// 服务端重建时等待同一端口释放的上限
        /// </summary>
        private static readonly TimeSpan RestartTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 释放应答的轮询等待上限
        /// </summary>
        private static readonly TimeSpan ReleaseObserveTimeout = TimeSpan.FromSeconds(2);

        /// <summary>
        /// 重连完成事件的等待上限
        /// </summary>
        private static readonly TimeSpan ReallocatedTimeout = TimeSpan.FromSeconds(15);

        /// <summary>
        /// 服务端永远回 401：客户端只能用同一凭据重试一次，第二次仍失败就抛异常，不得发第三次请求。
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task TurnClient_401后重试_只重试一次()
        {
            // 按事务 ID 计数：同一事务的重传不算新请求，只关心是否出现了第三次
            var received = new ConcurrentDictionary<string, byte>();

            using (var fakeServer = new SaeaUdpTransport())
            using (var clientUdp = new SaeaUdpTransport())
            {
                await fakeServer.BindAsync(new IPEndPoint(Host, 0), CancellationToken.None);
                fakeServer.StartReceive();

                Action<IPEndPoint, ReadOnlyMemory<byte>> onDatagram = (from, data) =>
                {
                    StunMessage request;
                    if (!TryParseRequest(data.ToArray(), out request))
                    {
                        return;
                    }

                    received.TryAdd(GetRequestKey(request), 0);
                    SendQuietly(fakeServer, from, BuildChallenge(request, FakeRealm, FakeNonce));
                };
                fakeServer.DatagramReceived += onDatagram;

                await clientUdp.BindAsync(new IPEndPoint(Host, 0), CancellationToken.None);

                // 注入已绑定的 UDP 传输：包装实例不拥有也不绑定底层传输，释放时只退订接收事件
                var transport = new UdpTurnClientTransport(clientUdp, fakeServer.LocalEndPoint);
                var options = new TurnClientOptions
                {
                    ServerEndPoint = fakeServer.LocalEndPoint,
                    TransportFactory = () => transport,
                    Username = "user",
                    Password = "pass",
                    RtoMilliseconds = 100
                };

                try
                {
                    using (var client = new TurnClientHost(options))
                    {
                        var error = await Assert.ThrowsExceptionAsync<TurnClientException>(
                            () => client.AllocateAsync(null, CancellationToken.None));

                        Assert.IsTrue(
                            error.ErrorCode == TurnErrorCode.Unauthorized || error.ErrorCode == TurnErrorCode.StaleNonce,
                            "两次挑战后应以 401 或 438 失败，实际错误码 " + error.ErrorCode + "，原因 " + error.Reason);

                        Assert.AreEqual(2, received.Count, "未鉴权一次加重鉴权一次，共两个请求，不得出现第三次");
                    }
                }
                finally
                {
                    transport.Dispose();
                    fakeServer.DatagramReceived -= onDatagram;
                }
            }
        }

        /// <summary>
        /// 真实服务端下 permission 续期间隔调到 150ms：700ms 内应至少续期一次，而 allocation 间隔 5s 不应触发。
        /// 计数只统计带 MESSAGE-INTEGRITY 的请求，避免把 401 握手的两次 Allocate 当成两次续期；
        /// RTO 取协议常规的 100ms，释放路径的耗时只由它自己的释放超时决定。
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task TurnClient_permission到期前_自动刷新()
        {
            var ports = ReserveUdpPorts(2);
            using (var harness = await TurnHarness.StartAsync(0, ports[0]))
            {
                var credential = harness.Server.CredentialService.Create("learner");
                var counting = new CountingClientTransport(new UdpTurnClientTransport(new IPEndPoint(Host, 0), harness.EndPoint));
                var options = new TurnClientOptions
                {
                    ServerEndPoint = harness.EndPoint,
                    TransportFactory = () => counting,
                    Username = credential.Username,
                    Password = credential.Password,
                    RtoMilliseconds = 100,
                    PermissionRefreshInterval = TimeSpan.FromMilliseconds(150),
                    AllocationRefreshInterval = TimeSpan.FromSeconds(5),
                    RefreshTimeout = TimeSpan.FromSeconds(10)
                };

                using (var client = new TurnClientHost(options))
                {
                    var allocation = await client.AllocateAsync(null, CancellationToken.None);
                    Assert.IsNotNull(allocation, "Allocate 应成功");

                    // 对端只用于登记 permission，全程不做真实收发
                    var peer = new IPEndPoint(Host, ports[1]);
                    await allocation.CreatePermissionAsync(peer, CancellationToken.None);

                    await Task.Delay(700);

                    var permissions = counting.GetCount(StunMessageType.CreatePermission);
                    Assert.IsTrue(permissions >= 2, "permission 应在到期前自动续期，实际计数 " + permissions);
                    Assert.AreEqual(1, counting.GetCount(StunMessageType.Allocate), "allocation 间隔 5s，测试窗口内不应续期");

                    Assert.IsNotNull(client.Allocation, "续期期间分配实例不应丢失");
                    Assert.IsFalse(client.Allocation.IsExpired, "permission 续期成功后分配不应失效");
                    Assert.AreEqual(1, client.Allocation.PermissionCount, "同一对端 IP 只应保留一条 permission 登记");
                }
            }
        }

        /// <summary>
        /// 服务端在同一端口重建后，刷新失败应触发退避重连并重新 Allocate；
        /// 重连复用同一分配实例并原地更新中继地址。
        /// </summary>
        [TestMethod]
        [Timeout(40000)]
        public async Task TurnClient_断线_按退避重连并重新Allocate()
        {
            var ports = ReserveUdpPorts(2);
            using (var harness = await TurnHarness.StartAsync(ports[0], ports[1]))
            {
                var credential = harness.Server.CredentialService.Create("learner");
                var options = new TurnClientOptions
                {
                    ServerEndPoint = harness.EndPoint,

                    // 重连路径必然重建传输，工厂每次都要返回新实例
                    TransportFactory = () => new UdpTurnClientTransport(new IPEndPoint(IPAddress.Any, 0), harness.EndPoint),
                    Username = credential.Username,
                    Password = credential.Password,
                    RtoMilliseconds = 100,
                    AllocationRefreshInterval = TimeSpan.FromMilliseconds(200),
                    RefreshTimeout = TimeSpan.FromMilliseconds(400),
                    ReconnectBackoffInitial = TimeSpan.FromMilliseconds(100),
                    ReconnectBackoffMax = TimeSpan.FromMilliseconds(400)
                };

                using (var client = new TurnClientHost(options))
                {
                    var reallocated = new TaskCompletionSource<TurnAllocationClient>(TaskCreationOptions.RunContinuationsAsynchronously);
                    client.Reallocated += allocation => reallocated.TrySetResult(allocation);

                    var first = await client.AllocateAsync(null, CancellationToken.None);
                    Assert.IsNotNull(first, "首次 Allocate 应成功");

                    // 先停服务端并留出一次刷新超时，保证重连循环在服务端恢复前已经启动
                    harness.Stop();
                    await Task.Delay(900);
                    await harness.RestartAsync();

                    var completed = await Task.WhenAny(reallocated.Task, Task.Delay(ReallocatedTimeout));
                    Assert.AreSame(reallocated.Task, completed, "断线后应在 15s 内重连并重新 Allocate");

                    Assert.IsTrue(client.IsConnected, "重连完成后应处于已连接状态");
                    Assert.IsNotNull(client.Allocation, "重连后应保留分配实例");
                    Assert.AreSame(first, client.Allocation, "重连应复用同一分配实例");
                    Assert.IsNotNull(client.Allocation.RelayedEndPoint, "重新 Allocate 后应拿到中继地址");
                    Assert.IsFalse(client.Allocation.IsExpired, "重连重放完成后分配不应处于失效状态");
                }
            }
        }

        /// <summary>
        /// 释放时先发 LIFETIME=0 再立即返回：应答在释放阶段必须仍交给事务层，
        /// 否则只能等满释放事务自身的超时。阈值取 300ms，因为回环往返通常不到 20ms，
        /// 而修复前最坏要等满 500ms 的 <c>ReleaseTransactionTimeout</c>，两者差距足够大。
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task TurnClient_释放_发送LIFETIME零并立即返回()
        {
            var ports = ReserveUdpPorts(2);
            using (var harness = await TurnHarness.StartAsync(0, ports[0]))
            {
                var credential = harness.Server.CredentialService.Create("learner");
                var options = new TurnClientOptions
                {
                    ServerEndPoint = harness.EndPoint,
                    TransportFactory = () => new UdpTurnClientTransport(new IPEndPoint(Host, 0), harness.EndPoint),
                    Username = credential.Username,
                    Password = credential.Password,
                    RtoMilliseconds = 100,
                    AllocationRefreshInterval = TimeSpan.FromSeconds(5),
                    RefreshTimeout = TimeSpan.FromSeconds(10)
                };

                using (var client = new TurnClientHost(options))
                {
                    var allocation = await client.AllocateAsync(null, CancellationToken.None);
                    Assert.IsNotNull(allocation, "Allocate 应成功");
                    Assert.AreEqual(1, harness.Server.AllocationCount, "分配成功后服务端应有一条记录");

                    // 对端只用于登记 permission，全程不做真实收发
                    var peer = new IPEndPoint(Host, ports[1]);
                    await allocation.CreatePermissionAsync(peer, CancellationToken.None);

                    var stopwatch = Stopwatch.StartNew();
                    client.Dispose();
                    stopwatch.Stop();

                    Assert.IsTrue(
                        stopwatch.ElapsedMilliseconds < 300,
                        "释放应收到 LIFETIME=0 的应答后立即返回，实际耗时 " + stopwatch.ElapsedMilliseconds + "ms");

                    // 删除在服务端是异步生效的，这里按 2s 轮询等待
                    var deleted = await WaitUntilAsync(() => harness.Server.AllocationCount == 0, ReleaseObserveTimeout);
                    Assert.IsTrue(deleted, "释放请求应删除服务端分配，实际剩余 " + harness.Server.AllocationCount);
                }
            }
        }

        /// <summary>
        /// 解析并判断是否为 STUN 请求；Span 解析放在同步助手里，异步方法内不声明 Span 局部变量
        /// </summary>
        /// <param name="data">数据报</param>
        /// <param name="request">解析出的请求</param>
        /// <returns>是请求时返回 true</returns>
        private static bool TryParseRequest(byte[] data, out StunMessage request)
        {
            StunParseError error;
            return StunMessage.TryParse(data, out request, out error) && request.Class == StunClass.Request;
        }

        /// <summary>
        /// 生成请求的计数键：事务 ID 加是否已鉴权。
        /// 同一事务的重传键相同，而 401 前后共用同一事务 ID 的未鉴权与鉴权请求会被区分开
        /// </summary>
        /// <param name="request">请求</param>
        /// <returns>计数键</returns>
        private static string GetRequestKey(StunMessage request)
        {
            StunAttribute integrity;
            var authenticated = request.TryGet(StunAttributeType.MessageIntegrity, out integrity);
            return Convert.ToHexString(request.TransactionId.Span) + (authenticated ? ":auth" : ":plain");
        }

        /// <summary>
        /// 构造带 REALM 与 NONCE 的 401 挑战应答
        /// </summary>
        /// <param name="request">请求</param>
        /// <param name="realm">认证域</param>
        /// <param name="nonce">nonce</param>
        /// <returns>应答字节</returns>
        private static byte[] BuildChallenge(StunMessage request, string realm, string nonce)
        {
            var response = new StunMessage(request.Method, StunClass.ErrorResponse, request.TransactionId.ToArray());
            var attributes = new List<StunAttribute>
            {
                TurnErrorCode.Create(TurnErrorCode.Unauthorized),
                new StunAttribute(StunAttributeType.Realm, Encoding.UTF8.GetBytes(realm)),
                new StunAttribute(StunAttributeType.Nonce, Encoding.UTF8.GetBytes(nonce))
            };
            response.SetAttributes(attributes);
            return response.ToArray();
        }

        /// <summary>
        /// 从接收回调里发送应答；发送失败只忽略，不能打断接收循环
        /// </summary>
        /// <param name="transport">发送用传输</param>
        /// <param name="target">目标端点</param>
        /// <param name="data">应答字节</param>
        private static void SendQuietly(IUdpTransport transport, IPEndPoint target, byte[] data)
        {
            try
            {
                var sending = transport.SendToAsync(target, new ReadOnlyMemory<byte>(data), CancellationToken.None);
                if (sending.IsFaulted)
                {
                    var ignored = sending.Exception;
                    return;
                }

                if (!sending.IsCompleted)
                {
                    sending.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                }
            }
            catch (Exception)
            {
                // 假服务端的发送失败不影响请求计数
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
        /// 计数装饰器：只统计带 MESSAGE-INTEGRITY 的请求，同一事务的重传只算一次，其余成员全部转发给内部传输
        /// </summary>
        private sealed class CountingClientTransport : ITurnClientTransport
        {
            /// <summary>
            /// 内部 UDP 传输
            /// </summary>
            private readonly UdpTurnClientTransport _inner;

            /// <summary>
            /// 按方法累计的已鉴权请求数
            /// </summary>
            private readonly ConcurrentDictionary<StunMessageType, int> _counts = new ConcurrentDictionary<StunMessageType, int>();

            /// <summary>
            /// 已计数的事务 ID，避免重传把一次请求算成多次
            /// </summary>
            private readonly ConcurrentDictionary<string, byte> _countedIds = new ConcurrentDictionary<string, byte>();

            /// <summary>
            /// 包装内部传输
            /// </summary>
            /// <param name="inner">内部 UDP 传输</param>
            public CountingClientTransport(UdpTurnClientTransport inner)
            {
                _inner = inner;
            }

            /// <summary>
            /// 本地绑定的端点
            /// </summary>
            public IPEndPoint LocalEndPoint
            {
                get { return _inner.LocalEndPoint; }
            }

            /// <summary>
            /// TURN 服务端端点
            /// </summary>
            public IPEndPoint ServerEndPoint
            {
                get { return _inner.ServerEndPoint; }
            }

            /// <summary>
            /// 是否为可靠传输
            /// </summary>
            public bool IsReliable
            {
                get { return _inner.IsReliable; }
            }

            /// <summary>
            /// 是否已打开并可收发
            /// </summary>
            public bool IsConnected
            {
                get { return _inner.IsConnected; }
            }

            /// <summary>
            /// 收到一整帧时触发，直接转发内部事件
            /// </summary>
            public event Action<byte[]> FrameReceived
            {
                add { _inner.FrameReceived += value; }
                remove { _inner.FrameReceived -= value; }
            }

            /// <summary>
            /// 打开内部传输
            /// </summary>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>打开完成的任务</returns>
            public Task OpenAsync(CancellationToken cancellationToken)
            {
                return _inner.OpenAsync(cancellationToken);
            }

            /// <summary>
            /// 先按事务 ID 去重计数再转发发送
            /// </summary>
            /// <param name="frame">完整帧</param>
            public void Send(byte[] frame)
            {
                StunMessageType method;
                string transactionId;
                if (TryGetAuthenticatedRequest(frame, out method, out transactionId) && _countedIds.TryAdd(transactionId, 0))
                {
                    _counts.AddOrUpdate(method, 1, (key, value) => value + 1);
                }

                _inner.Send(frame);
            }

            /// <summary>
            /// 读取指定方法的已鉴权请求计数
            /// </summary>
            /// <param name="method">STUN 方法</param>
            /// <returns>计数；未出现过为 0</returns>
            public int GetCount(StunMessageType method)
            {
                int value;
                return _counts.TryGetValue(method, out value) ? value : 0;
            }

            /// <summary>
            /// 只释放内部传输
            /// </summary>
            public void Dispose()
            {
                _inner.Dispose();
            }

            /// <summary>
            /// 解析帧并取出已鉴权请求的方法与事务 ID；Span 解析放在同步助手里
            /// </summary>
            /// <param name="frame">完整帧</param>
            /// <param name="method">请求方法</param>
            /// <param name="transactionId">事务 ID 的十六进制串</param>
            /// <returns>是已鉴权请求时返回 true</returns>
            private static bool TryGetAuthenticatedRequest(byte[] frame, out StunMessageType method, out string transactionId)
            {
                method = StunMessageType.Binding;
                transactionId = null;

                StunMessage message;
                StunParseError error;
                if (!StunMessage.TryParse(frame, out message, out error))
                {
                    return false;
                }

                StunAttribute integrity;
                if (message.Class != StunClass.Request || !message.TryGet(StunAttributeType.MessageIntegrity, out integrity))
                {
                    return false;
                }

                method = message.Method;
                transactionId = Convert.ToHexString(message.TransactionId.Span);
                return true;
            }
        }

        /// <summary>
        /// TURN 服务端与 STUN 监听端点的组合宿主；端点固定时可停掉后在同一端口重建
        /// </summary>
        private sealed class TurnHarness : IDisposable
        {
            /// <summary>
            /// 中继端口
            /// </summary>
            private readonly int _relayPort;

            /// <summary>
            /// 底层 STUN 服务端，负责监听与回送
            /// </summary>
            private StunServerHost _stun;

            /// <summary>
            /// TURN 服务端
            /// </summary>
            private TurnServerHost _server;

            /// <summary>
            /// 是否已释放
            /// </summary>
            private bool _disposed;

            /// <summary>
            /// 创建宿主
            /// </summary>
            /// <param name="stun">STUN 服务端</param>
            /// <param name="server">TURN 服务端</param>
            /// <param name="endPoint">客户端应访问的端点</param>
            /// <param name="relayPort">中继端口</param>
            private TurnHarness(StunServerHost stun, TurnServerHost server, IPEndPoint endPoint, int relayPort)
            {
                _stun = stun;
                _server = server;
                _relayPort = relayPort;
                EndPoint = endPoint;
            }

            /// <summary>
            /// TURN 服务端
            /// </summary>
            public TurnServerHost Server
            {
                get { return _server; }
            }

            /// <summary>
            /// 客户端应访问的端点
            /// </summary>
            public IPEndPoint EndPoint { get; private set; }

            /// <summary>
            /// 按配置创建并启动服务端；中继端口区间收窄到单个端口
            /// </summary>
            /// <param name="stunPort">STUN 监听端口；为 0 时由系统分配</param>
            /// <param name="relayPort">中继端口</param>
            /// <returns>宿主</returns>
            public static async Task<TurnHarness> StartAsync(int stunPort, int relayPort)
            {
                var server = CreateServer(relayPort);
                var stun = await StartStunAsync(server, stunPort);
                return new TurnHarness(stun, server, stun.Endpoints[0].LocalEndPoint, relayPort);
            }

            /// <summary>
            /// 依次释放 TURN 服务端与 STUN 服务端，保留端口信息以便重建
            /// </summary>
            public void Stop()
            {
                if (_server != null)
                {
                    _server.Dispose();
                    _server = null;
                }

                if (_stun != null)
                {
                    _stun.Dispose();
                    _stun = null;
                }
            }

            /// <summary>
            /// 在同一监听端口重建服务端；旧套接字尚未完全释放时按 100ms 轮询重试
            /// </summary>
            /// <returns>重建完成的任务</returns>
            public async Task RestartAsync()
            {
                Stop();

                var deadline = DateTime.UtcNow + RestartTimeout;
                while (true)
                {
                    try
                    {
                        var server = CreateServer(_relayPort);
                        var stun = await StartStunAsync(server, EndPoint.Port);
                        _server = server;
                        _stun = stun;
                        return;
                    }
                    catch (Exception) when (DateTime.UtcNow < deadline)
                    {
                        // 绑定失败通常是旧套接字还没释放，稍后重试同一端口
                        await Task.Delay(100);
                    }
                }
            }

            /// <summary>
            /// 释放服务端；重复调用为空操作
            /// </summary>
            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                Stop();
            }

            /// <summary>
            /// 按测试凭据创建 TURN 服务端配置
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
            /// 启动承接 TURN 请求与 ChannelData 的 STUN 监听端点
            /// </summary>
            /// <param name="server">TURN 服务端</param>
            /// <param name="port">监听端口</param>
            /// <returns>已启动的 STUN 服务端</returns>
            private static async Task<StunServerHost> StartStunAsync(TurnServerHost server, int port)
            {
                var options = new StunServerOptions(new IPEndPoint(Host, port))
                {
                    RequestHandler = server.HandleRequest,
                    DatagramHandler = server.HandleDatagram
                };
                var stun = new StunServerHost(options);
                await stun.StartAsync();
                return stun;
            }
        }
    }
}