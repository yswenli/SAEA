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
    /// 中继进阶自测：通道绑定后的 ChannelData 双向中继、错误口令鉴权失败、
    /// 权限登记随对端增长、两个客户端各自拿到不同中继地址。
    /// 每个用例自起自停一套进程内 STUN + TURN 宿主，端口全部随机取用，不共享端口。
    /// </summary>
    public static class RelayAdvancedTest
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
        /// 通道绑定用例使用的通道号
        /// </summary>
        private const ushort ChannelNumber = 0x4001;

        /// <summary>
        /// 自测统一使用 IPv4 回环地址
        /// </summary>
        private static readonly IPAddress Loopback = IPAddress.Loopback;

        /// <summary>
        /// 第二个权限登记使用的回环地址，与 127.0.0.1 属于不同 IP
        /// </summary>
        private static readonly IPAddress LoopbackAlias = IPAddress.Parse("127.0.0.2");

        /// <summary>
        /// 单个异步等待的上限，避免服务端异常时整个自测卡死
        /// </summary>
        private static readonly TimeSpan IoTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 依次校验通道数据双向、错误口令、权限增长与多客户端中继地址
        /// </summary>
        /// <returns>异步任务，完成后该组场景与断言已全部执行</returns>
        public static async Task RunAsync()
        {
            TestHarness.Section("RelayAdvancedTest");

            await ChannelBindCarriesDataBothWaysAsync();
            await WrongPasswordFailsAllocateAsync();
            await PermissionCountGrowsWithPeersAsync();
            await TwoClientsGetDistinctRelayAddressesAsync();

            TestHarness.WriteSummary("RelayAdvancedTest");
        }

        /// <summary>
        /// ChannelBind 后去程与回程都走 ChannelData，不再包成 Data 指示
        /// </summary>
        private static async Task ChannelBindCarriesDataBothWaysAsync()
        {
            TestHarness.Section("relay channel bind");

            using (var host = await RelayHost.StartAsync())
            using (var client = CreateClient(host))
            using (var peer = new UdpPeer())
            {
                var allocation = await AllocateAsync(client);
                if (allocation == null)
                {
                    return;
                }

                if (!await TryAsync(allocation.ChannelBindAsync(ChannelNumber, peer.EndPoint, CancellationToken.None), "ChannelBind 成功"))
                {
                    return;
                }

                TestHarness.Expect(allocation.ChannelCount == 1, "绑定后通道数为一", allocation.ChannelCount.ToString());
                TestHarness.Expect(allocation.PermissionCount == 1, "绑定同时登记一条权限", allocation.PermissionCount.ToString());

                // 先订阅再收发，避免回程 ChannelData 比订阅更早到达
                var inbox = new TaskCompletionSource<Datagram>(TaskCreationOptions.RunContinuationsAsynchronously);
                ushort receivedChannel = 0;
                var sawDataIndication = false;
                client.ChannelDataReceived += (channel, data) =>
                {
                    receivedChannel = channel;
                    inbox.TrySetResult(new Datagram(null, data));
                };
                client.DataReceived += (from, data) => sawDataIndication = true;

                var outbound = Encoding.UTF8.GetBytes("channel-data-outbound");
                if (!await TryAsync(allocation.SendChannelDataAsync(ChannelNumber, outbound, CancellationToken.None), "ChannelData 发送成功"))
                {
                    return;
                }

                var atPeer = await peer.WaitAsync(data => data.Length == outbound.Length, IoTimeout);
                TestHarness.Expect(atPeer != null, "对端收到 ChannelData 解出的数据");
                TestHarness.Expect(atPeer != null && IsSamePayload(atPeer.Data, outbound), "去程载荷与发送内容一致");
                TestHarness.Expect(
                    atPeer != null && Equals(atPeer.From, allocation.RelayedEndPoint),
                    "对端看到的来源为中继地址",
                    atPeer == null ? "未收到数据" : $"{atPeer.From} vs {allocation.RelayedEndPoint}");

                var inbound = Encoding.UTF8.GetBytes("channel-data-inbound");
                peer.Send(inbound, allocation.RelayedEndPoint);

                var atClient = await AwaitAsync(inbox.Task, IoTimeout, "客户端收到 ChannelData 事件");
                TestHarness.Expect(atClient != null, "客户端收到 ChannelData 事件");
                TestHarness.Expect(atClient != null && IsSamePayload(atClient.Data, inbound), "回程载荷与对端发送内容一致");
                TestHarness.Expect(atClient != null && receivedChannel == ChannelNumber, "回程通道号与绑定通道一致", receivedChannel.ToString("X4"));
                TestHarness.Expect(!sawDataIndication, "通道绑定后回程不再走 Data 指示");
            }
        }

        /// <summary>
        /// 口令错误时 AllocateAsync 必须以 401 或 438 失败，且服务端不产生分配
        /// </summary>
        private static async Task WrongPasswordFailsAllocateAsync()
        {
            TestHarness.Section("relay wrong password");

            using (var host = await RelayHost.StartAsync())
            using (var client = CreateClient(host, "wrong-password"))
            {
                TurnClientException? error = null;
                try
                {
                    await client.AllocateAsync(null, CancellationToken.None).WaitAsync(IoTimeout);
                }
                catch (TurnClientException ex)
                {
                    error = ex;
                }
                catch (Exception ex)
                {
                    TestHarness.Expect(false, "错误口令抛出 TurnClientException", Describe(ex));
                }

                TestHarness.Expect(error != null, "错误口令时 AllocateAsync 抛出 TurnClientException");
                TestHarness.Expect(
                    error != null && (error.ErrorCode == 401 || error.ErrorCode == 438),
                    "错误口令对应 401 或 438",
                    error == null ? "未抛出" : error.ErrorCode + ": " + error.Reason);
                TestHarness.Expect(host.Server.AllocationCount == 0, "鉴权失败不产生分配记录", host.Server.AllocationCount.ToString());
            }
        }

        /// <summary>
        /// 对不同对端 IP 申请权限时登记数递增，同一 IP 重复申请不重复计数
        /// </summary>
        private static async Task PermissionCountGrowsWithPeersAsync()
        {
            TestHarness.Section("relay permission growth");

            using (var host = await RelayHost.StartAsync())
            using (var client = CreateClient(host))
            {
                var allocation = await AllocateAsync(client);
                if (allocation == null)
                {
                    return;
                }

                TestHarness.Expect(allocation.PermissionCount == 0, "初始没有权限登记", allocation.PermissionCount.ToString());

                var first = new IPEndPoint(Loopback, TestHarness.GetFreeUdpPort());
                if (!await TryAsync(allocation.CreatePermissionAsync(first, CancellationToken.None), "第一份权限申请成功"))
                {
                    return;
                }
                TestHarness.Expect(allocation.PermissionCount == 1, "第一份权限登记后数量为一", allocation.PermissionCount.ToString());

                var sameAddress = new IPEndPoint(Loopback, TestHarness.GetFreeUdpPort());
                if (!await TryAsync(allocation.CreatePermissionAsync(sameAddress, CancellationToken.None), "同 IP 不同端口权限申请成功"))
                {
                    return;
                }
                TestHarness.Expect(allocation.PermissionCount == 1, "同 IP 不同端口不重复计数", allocation.PermissionCount.ToString());

                var second = new IPEndPoint(LoopbackAlias, TestHarness.GetFreeUdpPort());
                if (!await TryAsync(allocation.CreatePermissionAsync(second, CancellationToken.None), "第二份权限申请成功"))
                {
                    return;
                }
                TestHarness.Expect(allocation.PermissionCount == 2, "新 IP 登记后数量为二", allocation.PermissionCount.ToString());
            }
        }

        /// <summary>
        /// 两个客户端各自分配，中继地址必须互不相同且都落在服务端配置区间内
        /// </summary>
        private static async Task TwoClientsGetDistinctRelayAddressesAsync()
        {
            TestHarness.Section("relay distinct allocations");

            using (var host = await RelayHost.StartAsync())
            using (var first = CreateClient(host))
            using (var second = CreateClient(host))
            {
                var firstAllocation = await AllocateAsync(first, "第一个客户端分配成功");
                if (firstAllocation == null)
                {
                    return;
                }

                var secondAllocation = await AllocateAsync(second, "第二个客户端分配成功");
                if (secondAllocation == null)
                {
                    return;
                }

                var firstRelayed = firstAllocation.RelayedEndPoint;
                var secondRelayed = secondAllocation.RelayedEndPoint;
                ConsoleHelper.WriteLine($"First RelayedEndPoint: {firstRelayed}");
                ConsoleHelper.WriteLine($"Second RelayedEndPoint: {secondRelayed}");

                TestHarness.Expect(firstRelayed != null && secondRelayed != null, "两次分配都给出中继地址");
                TestHarness.Expect(!Equals(firstRelayed, secondRelayed), "两个客户端的中继地址不同", $"{firstRelayed} vs {secondRelayed}");
                TestHarness.Expect(
                    firstRelayed != null && secondRelayed != null
                    && firstRelayed.Port >= host.RelayPortMin && firstRelayed.Port <= host.RelayPortMax
                    && secondRelayed.Port >= host.RelayPortMin && secondRelayed.Port <= host.RelayPortMax,
                    "两个中继端口都落在服务端配置区间",
                    $"[{host.RelayPortMin},{host.RelayPortMax}]");
                TestHarness.Expect(host.Server.AllocationCount == 2, "服务端记录两条分配", host.Server.AllocationCount.ToString());
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
        /// <param name="name">断言名称</param>
        /// <returns>分配实例；失败时为 null</returns>
        private static async Task<TurnAllocationClient> AllocateAsync(TurnClientHost client, string name = "Allocate 成功")
        {
            try
            {
                return await client.AllocateAsync(null, CancellationToken.None).WaitAsync(IoTimeout);
            }
            catch (Exception ex)
            {
                TestHarness.Expect(false, name, Describe(ex));
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
            /// <param name="from">来源端点；来自客户端事件时由事件参数给出，ChannelData 事件不携带来源</param>
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