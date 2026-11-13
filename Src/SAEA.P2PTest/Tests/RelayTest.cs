using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Common;
using SAEA.P2P.StunServer;
using SAEA.P2P.Turn;
using SAEA.P2P.TurnServer;

using StunServerHost = SAEA.P2P.StunServer.StunServer;
using TurnClientHost = SAEA.P2P.Turn.TurnClient;
using TurnServerHost = SAEA.P2P.TurnServer.TurnServer;

namespace SAEA.P2PTest.Tests
{
    /// <summary>
    /// 中继基础自测：TURN 分配给出中继地址、权限登记、Send 指示与 Data 指示双向中继、释放后分配失效。
    /// 每个用例自起自停一套进程内 STUN + TURN 宿主，端口全部随机取用，不依赖外部服务。
    /// </summary>
    public static class RelayTest
    {
        /// <summary>
        /// 进程内 TURN 服务端使用的共享密钥
        /// </summary>
        private const string SharedSecret = "saea-p2p-relay-selftest-secret";

        /// <summary>
        /// 进程内 TURN 服务端使用的认证域
        /// </summary>
        private const string Realm = "saea.p2p.relay.selftest";

        /// <summary>
        /// 自测统一使用 IPv4 回环地址
        /// </summary>
        private static readonly IPAddress Loopback = IPAddress.Loopback;

        /// <summary>
        /// 单个异步等待的上限，避免服务端异常时整个自测卡死
        /// </summary>
        private static readonly TimeSpan IoTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 依次校验中继地址、权限登记、去程中继、回程中继与释放语义
        /// </summary>
        /// <returns>异步任务，完成后该组场景与断言已全部执行</returns>
        public static async Task RunAsync()
        {
            TestHarness.Section("RelayTest");

            await AllocationReportsRelayAddressAsync();
            await CreatePermissionRegistersPeerAsync();
            await SendIndicationReachesPeerAsync();
            await PeerReplyReachesClientAsync();
            await ReleaseMarksAllocationExpiredAsync();

            TestHarness.WriteSummary("RelayTest");
        }

        /// <summary>
        /// 分配成功后应给出中继地址与生命周期，客户端进入已连接状态
        /// </summary>
        private static async Task AllocationReportsRelayAddressAsync()
        {
            TestHarness.Section("relay allocate");

            using (var host = await RelayHost.StartAsync())
            using (var client = CreateClient(host))
            {
                var allocation = await AllocateAsync(client);
                if (allocation == null)
                {
                    return;
                }

                var relayed = allocation.RelayedEndPoint;
                ConsoleHelper.WriteLine($"RelayedEndPoint: {relayed}");
                ConsoleHelper.WriteLine($"MappedEndPoint: {allocation.MappedEndPoint}");
                ConsoleHelper.WriteLine($"Lifetime: {allocation.Lifetime}s");

                TestHarness.Expect(relayed != null, "分配给出中继地址");
                TestHarness.Expect(
                    relayed != null && relayed.Port >= host.RelayPortMin && relayed.Port <= host.RelayPortMax,
                    "中继端口落在服务端配置区间",
                    relayed == null ? "null" : $"{relayed} 不在 [{host.RelayPortMin},{host.RelayPortMax}]");
                TestHarness.Expect(allocation.Lifetime > 0, "生命周期大于零", allocation.Lifetime.ToString());
                TestHarness.Expect(!allocation.IsExpired, "分配未失效");
                TestHarness.Expect(client.IsConnected, "分配完成后客户端处于已连接状态");
                TestHarness.Expect(host.Server.AllocationCount == 1, "服务端记录一条分配", host.Server.AllocationCount.ToString());
            }
        }

        /// <summary>
        /// CreatePermission 指向裸 UDP 对端后，客户端侧应登记一条权限
        /// </summary>
        private static async Task CreatePermissionRegistersPeerAsync()
        {
            TestHarness.Section("relay create permission");

            using (var host = await RelayHost.StartAsync())
            using (var client = CreateClient(host))
            using (var peer = new UdpPeer())
            {
                var allocation = await AllocateAsync(client);
                if (allocation == null)
                {
                    return;
                }

                ConsoleHelper.WriteLine($"PeerEndPoint: {peer.EndPoint}");
                TestHarness.Expect(allocation.PermissionCount == 0, "登记权限前权限数为零", allocation.PermissionCount.ToString());

                if (!await TryAsync(allocation.CreatePermissionAsync(peer.EndPoint, CancellationToken.None), "CreatePermission 成功"))
                {
                    return;
                }

                TestHarness.Expect(allocation.PermissionCount == 1, "登记权限后权限数为一", allocation.PermissionCount.ToString());
            }
        }

        /// <summary>
        /// Send 指示把数据投递给对端，对端看到的来源应为中继地址
        /// </summary>
        private static async Task SendIndicationReachesPeerAsync()
        {
            TestHarness.Section("relay send indication");

            using (var host = await RelayHost.StartAsync())
            using (var client = CreateClient(host))
            using (var peer = new UdpPeer())
            {
                var allocation = await AllocateAsync(client);
                if (allocation == null)
                {
                    return;
                }

                if (!await TryAsync(allocation.CreatePermissionAsync(peer.EndPoint, CancellationToken.None), "CreatePermission 成功"))
                {
                    return;
                }

                var payload = Encoding.UTF8.GetBytes("relay-payload-outbound");
                if (!await TryAsync(allocation.SendAsync(peer.EndPoint, payload, CancellationToken.None), "Send 指示发送成功"))
                {
                    return;
                }

                var atPeer = await peer.WaitAsync(data => data.Length == payload.Length, IoTimeout);
                TestHarness.Expect(atPeer != null, "对端收到中继数据");
                TestHarness.Expect(atPeer != null && IsSamePayload(atPeer.Data, payload), "对端载荷与发送内容一致");
                TestHarness.Expect(
                    atPeer != null && Equals(atPeer.From, allocation.RelayedEndPoint),
                    "对端看到的来源为中继地址",
                    atPeer == null ? "未收到数据" : $"{atPeer.From} vs {allocation.RelayedEndPoint}");
            }
        }

        /// <summary>
        /// 对端把数据发到中继地址后，客户端应通过 Data 指示收到同一份数据
        /// </summary>
        private static async Task PeerReplyReachesClientAsync()
        {
            TestHarness.Section("relay peer reply");

            using (var host = await RelayHost.StartAsync())
            using (var client = CreateClient(host))
            using (var peer = new UdpPeer())
            {
                var allocation = await AllocateAsync(client);
                if (allocation == null)
                {
                    return;
                }

                if (!await TryAsync(allocation.CreatePermissionAsync(peer.EndPoint, CancellationToken.None), "CreatePermission 成功"))
                {
                    return;
                }

                // 先订阅再发回程，避免回程报文比订阅更早到达
                var inbox = new TaskCompletionSource<Datagram>(TaskCreationOptions.RunContinuationsAsynchronously);
                client.DataReceived += (from, data) => inbox.TrySetResult(new Datagram(from, data));

                var reply = Encoding.UTF8.GetBytes("relay-payload-inbound");
                peer.Send(reply, allocation.RelayedEndPoint);

                var atClient = await AwaitAsync(inbox.Task, IoTimeout, "客户端收到 Data 指示");
                TestHarness.Expect(atClient != null, "客户端收到 Data 指示");
                TestHarness.Expect(atClient != null && IsSamePayload(atClient.Data, reply), "回程载荷与对端发送内容一致");
                TestHarness.Expect(
                    atClient != null && Equals(atClient.From, peer.EndPoint),
                    "Data 指示携带对端地址",
                    atClient == null ? "未收到数据" : $"{atClient.From} vs {peer.EndPoint}");
            }
        }

        /// <summary>
        /// Release 后本地立即标记失效，服务端随后删除分配
        /// </summary>
        private static async Task ReleaseMarksAllocationExpiredAsync()
        {
            TestHarness.Section("relay release");

            using (var host = await RelayHost.StartAsync())
            using (var client = CreateClient(host))
            using (var peer = new UdpPeer())
            {
                var allocation = await AllocateAsync(client);
                if (allocation == null)
                {
                    return;
                }

                if (!await TryAsync(allocation.CreatePermissionAsync(peer.EndPoint, CancellationToken.None), "CreatePermission 成功"))
                {
                    return;
                }

                await TryAsync(allocation.ReleaseAsync(CancellationToken.None), "ReleaseAsync 成功");

                TestHarness.Expect(allocation.IsExpired, "释放后分配标记为失效");
                TestHarness.Expect(!client.IsConnected, "释放后客户端不再是已连接状态");

                var removed = await TestHarness.WaitUntil(() => host.Server.AllocationCount == 0, 3000);
                TestHarness.Expect(removed, "服务端已删除分配记录", host.Server.AllocationCount.ToString());
            }
        }

        /// <summary>
        /// 按进程内服务端签发的临时凭据创建 TURN 客户端
        /// </summary>
        /// <param name="host">服务端宿主</param>
        /// <param name="password">覆盖口令；为 null 时使用凭据自带口令</param>
        /// <returns>TURN 客户端</returns>
        private static TurnClientHost CreateClient(RelayHost host, string? password = null)
        {
            var credential = host.Server.CredentialService.Create("learner");

            return new TurnClientHost(new TurnClientOptions
            {
                ServerEndPoint = host.EndPoint,

                // 断线重连会重复调用工厂，每次都返回独立实例
                TransportFactory = () => new UdpTurnClientTransport(new IPEndPoint(Loopback, 0), host.EndPoint),
                Username = credential.Username,
                Password = password ?? credential.Password,
                RtoMilliseconds = 100
            });
        }

        /// <summary>
        /// 执行一次分配；失败时按断言计入失败并返回 null
        /// </summary>
        /// <param name="client">TURN 客户端</param>
        /// <returns>分配实例；失败时为 null</returns>
        private static async Task<TurnAllocationClient> AllocateAsync(TurnClientHost client)
        {
            try
            {
                return await client.AllocateAsync(null, CancellationToken.None).WaitAsync(IoTimeout);
            }
            catch (Exception ex)
            {
                TestHarness.Expect(false, "Allocate 成功", Describe(ex));
                return null;
            }
        }

        /// <summary>
        /// 等待任务完成；失败或超时都按断言计入失败
        /// </summary>
        /// <param name="task">待等待任务</param>
        /// <param name="name">断言名称</param>
        /// <returns>任务成功返回 true</returns>
        private static async Task<bool> TryAsync(Task task, string name)
        {
            try
            {
                await task.WaitAsync(IoTimeout);
                return true;
            }
            catch (Exception ex)
            {
                TestHarness.Expect(false, name, Describe(ex));
                return false;
            }
        }

        /// <summary>
        /// 等待带结果的任务完成；失败或超时都按断言计入失败
        /// </summary>
        /// <param name="task">待等待任务</param>
        /// <param name="timeout">等待上限</param>
        /// <param name="name">断言名称</param>
        /// <returns>任务结果；失败时为 null</returns>
        private static async Task<T?> AwaitAsync<T>(Task<T> task, TimeSpan timeout, string name) where T : class
        {
            try
            {
                return await task.WaitAsync(timeout);
            }
            catch (Exception ex)
            {
                TestHarness.Expect(false, name, Describe(ex));
                return null;
            }
        }

        /// <summary>
        /// 逐字节比较两份载荷
        /// </summary>
        /// <param name="left">左侧载荷</param>
        /// <param name="right">右侧载荷</param>
        /// <returns>完全一致返回 true</returns>
        private static bool IsSamePayload(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (var i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 拼出便于定位的异常描述
        /// </summary>
        /// <param name="ex">异常</param>
        /// <returns>异常类型与消息</returns>
        private static string Describe(Exception ex)
        {
            return ex.GetType().Name + ": " + ex.Message;
        }

        /// <summary>
        /// 进程内 STUN 监听端点与 TURN 服务端的组合宿主，中继端口区间随机取用
        /// </summary>
        private sealed class RelayHost : IDisposable
        {
            /// <summary>
            /// 中继端口随机区间宽度；区间内被系统占用的端口由服务端回退探测
            /// </summary>
            private const int RelayPortWindow = 8;

            /// <summary>
            /// 底层 STUN 服务端，负责监听与回送
            /// </summary>
            private readonly StunServerHost _stun;

            /// <summary>
            /// 创建宿主
            /// </summary>
            /// <param name="stun">STUN 服务端</param>
            /// <param name="server">TURN 服务端</param>
            /// <param name="relayPortMin">中继端口下界</param>
            /// <param name="relayPortMax">中继端口上界</param>
            private RelayHost(StunServerHost stun, TurnServerHost server, int relayPortMin, int relayPortMax)
            {
                _stun = stun;
                Server = server;
                RelayPortMin = relayPortMin;
                RelayPortMax = relayPortMax;
            }

            /// <summary>
            /// TURN 服务端
            /// </summary>
            public TurnServerHost Server { get; private set; }

            /// <summary>
            /// 中继端口下界
            /// </summary>
            public int RelayPortMin { get; private set; }

            /// <summary>
            /// 中继端口上界
            /// </summary>
            public int RelayPortMax { get; private set; }

            /// <summary>
            /// 客户端应访问的端点
            /// </summary>
            public IPEndPoint EndPoint
            {
                get { return _stun.Endpoints[0].LocalEndPoint; }
            }

            /// <summary>
            /// 按随机端口启动一套服务端；STUN 端口交由系统分配
            /// </summary>
            /// <returns>宿主</returns>
            public static async Task<RelayHost> StartAsync()
            {
                var min = TestHarness.GetFreeUdpPort();
                if (min > 65535 - (RelayPortWindow - 1))
                {
                    min = 65535 - (RelayPortWindow - 1);
                }
                var max = min + RelayPortWindow - 1;

                var server = new TurnServerHost(new TurnServerOptions
                {
                    SharedSecret = SharedSecret,
                    Realm = Realm,
                    RelayAddress = Loopback,
                    RelayPortMin = min,
                    RelayPortMax = max
                });

                var options = new StunServerOptions(new IPEndPoint(Loopback, 0))
                {
                    RequestHandler = server.HandleRequest,
                    DatagramHandler = server.HandleDatagram
                };
                var stun = new StunServerHost(options);
                await stun.StartAsync();
                return new RelayHost(stun, server, min, max);
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
        /// 裸 UDP 对端：绑定回环随机端口并常驻接收，用于验证中继的去程与回程
        /// </summary>
        private sealed class UdpPeer : IDisposable
        {
            /// <summary>
            /// 底层套接字
            /// </summary>
            private readonly Socket _socket;

            /// <summary>
            /// 停止接收循环的取消源
            /// </summary>
            private readonly CancellationTokenSource _cts = new CancellationTokenSource();

            /// <summary>
            /// 保护收件列表的同步对象
            /// </summary>
            private readonly object _sync = new object();

            /// <summary>
            /// 已收到但尚未取走的数据报
            /// </summary>
            private readonly List<Datagram> _items = new List<Datagram>();

            /// <summary>
            /// 绑定回环随机端口并启动接收循环
            /// </summary>
            public UdpPeer()
            {
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                _socket.Bind(new IPEndPoint(Loopback, 0));

                // 接收循环用后台任务常驻，失败只记在队列里，不影响主流程
                var ignored = Task.Run((Func<Task>)ReceiveLoopAsync);
            }

            /// <summary>
            /// 本地端点
            /// </summary>
            public IPEndPoint EndPoint
            {
                get { return (IPEndPoint)_socket.LocalEndPoint!; }
            }

            /// <summary>
            /// 向目标端点发送一个数据报
            /// </summary>
            /// <param name="data">数据</param>
            /// <param name="target">目标端点</param>
            public void Send(byte[] data, IPEndPoint target)
            {
                _socket.SendTo(data, target);
            }

            /// <summary>
            /// 等待第一条满足条件的数据报；超时返回 null
            /// </summary>
            /// <param name="predicate">匹配条件</param>
            /// <param name="timeout">等待上限</param>
            /// <returns>收到的数据报</returns>
            public async Task<Datagram?> WaitAsync(Func<byte[], bool> predicate, TimeSpan timeout)
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
            /// 释放套接字并停止接收循环
            /// </summary>
            public void Dispose()
            {
                try
                {
                    _cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // 已释放时取消为空操作
                }

                _socket.Dispose();
                _cts.Dispose();
            }

            /// <summary>
            /// 常驻接收循环：复制回调数据后入队，原始缓冲会被下一次接收覆盖
            /// </summary>
            /// <returns>停止接收后完成的任务</returns>
            private async Task ReceiveLoopAsync()
            {
                var buffer = new byte[65536];
                EndPoint any = new IPEndPoint(IPAddress.Any, 0);

                while (!_cts.IsCancellationRequested)
                {
                    SocketReceiveFromResult result;
                    try
                    {
                        result = await _socket.ReceiveFromAsync(new ArraySegment<byte>(buffer), SocketFlags.None, any).ConfigureAwait(false);
                    }
                    catch (ObjectDisposedException)
                    {
                        return;
                    }
                    catch (Exception)
                    {
                        if (_cts.IsCancellationRequested)
                        {
                            return;
                        }

                        // 瞬时错误（如 ICMP 端口不可达）不应终止对端接收
                        await Task.Delay(10).ConfigureAwait(false);
                        continue;
                    }

                    if (result.ReceivedBytes <= 0)
                    {
                        continue;
                    }

                    var copy = new byte[result.ReceivedBytes];
                    Buffer.BlockCopy(buffer, 0, copy, 0, result.ReceivedBytes);
                    var from = result.RemoteEndPoint as IPEndPoint;

                    lock (_sync)
                    {
                        _items.Add(new Datagram(from, copy));
                    }
                }
            }
        }

        /// <summary>
        /// 一条收到的数据报
        /// </summary>
        private sealed class Datagram
        {
            /// <summary>
            /// 创建数据报记录
            /// </summary>
            /// <param name="from">来源端点；由客户端事件参数给出</param>
            /// <param name="data">已复制的数据</param>
            public Datagram(IPEndPoint? from, byte[] data)
            {
                From = from;
                Data = data;
            }

            /// <summary>
            /// 来源端点
            /// </summary>
            public IPEndPoint? From { get; private set; }

            /// <summary>
            /// 已复制的数据
            /// </summary>
            public byte[] Data { get; private set; }
        }
    }
}