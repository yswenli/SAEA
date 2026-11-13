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
*命名空间：SAEA.P2P.Tests.Integration
*文件名： LocalNetTests
*版本号： net10.0
*唯一标识：a4d9e245-caf3-4b6a-bf4e-cbbb9461b583
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 19:00:00
*描述：无信令局域网直连的双向收发与节点过期用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 19:00:00
*修改人： yswenli
*版本号： net10.0
*描述：新增无信令局域网直连的双向收发与节点过期用例
*
*****************************************************************************/
using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Channel;
using SAEA.P2P.Core;
using SAEA.P2P.Discovery;
using SAEA.P2P.Ice;

namespace SAEA.P2P.Tests.Integration
{
    /// <summary>
    /// 无信令局域网集成用例：两端只靠组播发现交换 ICE 参数，同机建立 Local 通道后双向收发；
    /// 节点停止补发后应在过期阈值外从发现列表移除并触发过期事件。
    /// </summary>
    [TestClass]
    public class LocalNetTests
    {
        /// <summary>
        /// 发起方客户端节点标识
        /// </summary>
        private const string PeerA = "localnet-a";

        /// <summary>
        /// 应答方客户端节点标识
        /// </summary>
        private const string PeerB = "localnet-b";

        /// <summary>
        /// 过期用例中发起方发现实例的节点标识
        /// </summary>
        private const string NodeA = "localnet-node-a";

        /// <summary>
        /// 过期用例中应答方发现实例的节点标识
        /// </summary>
        private const string NodeB = "localnet-node-b";

        /// <summary>
        /// 互相发现与业务帧到达的等待预算
        /// </summary>
        private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 双向建连的等待预算，需高于两端 ICE 会话自身的总预算
        /// </summary>
        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(60);

        /// <summary>
        /// 节点过期事件的等待预算
        /// </summary>
        private static readonly TimeSpan ExpireTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 关闭会话后断开事件的等待预算
        /// </summary>
        private static readonly TimeSpan DisconnectTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 两端无信令启动后应互相发现，并发建连得到 Local 通道，且两个方向的业务帧都能完整到达
        /// </summary>
        [TestMethod]
        [Timeout(180000)]
        public async Task LocalNet_无信令_双向业务帧成功()
        {
            var port = ReserveUdpPort();
            var optionsA = new LocalDiscoveryOptions { Port = port, IntervalMilliseconds = 200 };
            var optionsB = new LocalDiscoveryOptions { Port = port, IntervalMilliseconds = 200 };

            // 同机用例只收集回环候选；两端各用独立实例，避免共享配置被并发改写
            var iceA = new IceSessionOptions { Ice = new IceOptions { AllowLoopbackCandidates = true } };
            var iceB = new IceSessionOptions { Ice = new IceOptions { AllowLoopbackCandidates = true } };

            using (var clientA = new P2PClient(PeerA, optionsA, iceA))
            using (var clientB = new P2PClient(PeerB, optionsB, iceB))
            {
                var seenA = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var seenB = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                clientA.OnPeerDiscovered += node =>
                {
                    if (node != null && node.NodeId == PeerB) seenA.TrySetResult(true);
                };
                clientB.OnPeerDiscovered += node =>
                {
                    if (node != null && node.NodeId == PeerA) seenB.TrySetResult(true);
                };

                var localInfoA = await clientA.StartLocalAsync().ConfigureAwait(false);
                var localInfoB = await clientB.StartLocalAsync().ConfigureAwait(false);
                Assert.IsNotNull(localInfoA, "A 侧启动发现后应返回本端节点信息");
                Assert.IsNotNull(localInfoB, "B 侧启动发现后应返回本端节点信息");
                Assert.AreEqual(PeerA, localInfoA.NodeId, "A 侧节点信息应带本端标识");
                Assert.AreEqual(PeerB, localInfoB.NodeId, "B 侧节点信息应带本端标识");
                Assert.IsTrue(clientA.IsRegistered, "启动发现后 A 侧应处于已注册状态：" + Describe(clientA));
                Assert.IsTrue(clientB.IsRegistered, "启动发现后 B 侧应处于已注册状态：" + Describe(clientB));

                var bothSeen = Task.WhenAll(seenA.Task, seenB.Task);
                await WaitAsync(
                    bothSeen,
                    EventTimeout,
                    "10 秒内未互相发现（端口 " + port + "）：" + Describe(clientA) + "；" + Describe(clientB)).ConfigureAwait(false);

                // 无信令模式没有请求/应答，两端必须同时发起，否则互相等待候选会超时
                var connectA = clientA.ConnectToPeerAsync(PeerB);
                var connectB = clientB.ConnectToPeerAsync(PeerA);
                var bothConnected = Task.WhenAll(connectA, connectB);
                await WaitAsync(
                    bothConnected,
                    ConnectTimeout,
                    "60 秒内未建立连接：" + Describe(clientA) + "；" + Describe(clientB)).ConfigureAwait(false);

                var connectionA = await connectA.ConfigureAwait(false);
                var connectionB = await connectB.ConfigureAwait(false);

                Assert.AreEqual(ChannelType.Local, connectionA.Channel, "A 侧通道应为 Local：" + Describe(clientA));
                Assert.AreEqual(PeerB, connectionA.PeerId, "A 侧连接的对端标识应为 B");
                Assert.AreEqual(ChannelType.Local, connectionB.Channel, "B 侧通道应为 Local：" + Describe(clientB));
                Assert.AreEqual(PeerA, connectionB.PeerId, "B 侧连接的对端标识应为 A");

                Assert.IsNotNull(connectionA.LocalEndPoint, "A 侧连接的本地端点不应为空");
                Assert.IsNotNull(connectionA.RemoteEndPoint, "A 侧连接的远端端点不应为空");
                Assert.IsNotNull(connectionB.LocalEndPoint, "B 侧连接的本地端点不应为空");
                Assert.IsNotNull(connectionB.RemoteEndPoint, "B 侧连接的远端端点不应为空");

                var aReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                var bReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

                // data 是只在回调期间有效的 Span，必须先复制再交给断言逻辑
                clientA.OnDataReceived += (peerId, data) =>
                {
                    if (peerId == PeerB) aReceived.TrySetResult(data.ToArray());
                };
                clientB.OnDataReceived += (peerId, data) =>
                {
                    if (peerId == PeerA) bReceived.TrySetResult(data.ToArray());
                };

                var fromAToB = Encoding.UTF8.GetBytes("localnet-a-发给-b-的业务帧");
                clientA.Send(PeerB, fromAToB);
                var receivedByB = await WaitAsync(
                    bReceived.Task,
                    EventTimeout,
                    "10 秒内 B 未收到 A 的业务帧：" + Describe(clientA) + "；" + Describe(clientB)).ConfigureAwait(false);
                CollectionAssert.AreEqual(fromAToB, receivedByB, "B 侧收到的载荷应与 A 侧发送的一致");

                var fromBToA = new byte[256];
                for (var i = 0; i < fromBToA.Length; i++)
                {
                    fromBToA[i] = (byte)(i % 251);
                }

                clientB.Send(PeerA, fromBToA);
                var receivedByA = await WaitAsync(
                    aReceived.Task,
                    EventTimeout,
                    "10 秒内 A 未收到 B 的业务帧：" + Describe(clientA) + "；" + Describe(clientB)).ConfigureAwait(false);
                CollectionAssert.AreEqual(fromBToA, receivedByA, "A 侧收到的载荷应与 B 侧发送的一致");

                var disconnectedA = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var disconnectedB = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                clientA.OnPeerDisconnected += (peerId, reason) =>
                {
                    if (peerId == PeerB) disconnectedA.TrySetResult(true);
                };
                clientB.OnPeerDisconnected += (peerId, reason) =>
                {
                    if (peerId == PeerA) disconnectedB.TrySetResult(true);
                };

                await clientA.DisconnectPeerAsync(PeerB).ConfigureAwait(false);
                await clientB.DisconnectPeerAsync(PeerA).ConfigureAwait(false);

                await WaitAsync(
                    disconnectedA.Task,
                    DisconnectTimeout,
                    "A 侧关闭会话后应触发 OnPeerDisconnected：" + Describe(clientA)).ConfigureAwait(false);
                await WaitAsync(
                    disconnectedB.Task,
                    DisconnectTimeout,
                    "B 侧关闭会话后应触发 OnPeerDisconnected：" + Describe(clientB)).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 一端停止补发后，另一端应在过期阈值后把该节点从列表移除，并经 OnNodeExpired 上报节点标识
        /// </summary>
        [TestMethod]
        [Timeout(60000)]
        public async Task LocalNet_过期节点_从列表移除()
        {
            var port = ReserveUdpPort();
            var optionsA = new LocalDiscoveryOptions { Port = port, IntervalMilliseconds = 100, TimeoutMilliseconds = 600 };
            var optionsB = new LocalDiscoveryOptions { Port = port, IntervalMilliseconds = 100, TimeoutMilliseconds = 600 };

            using (var discoveryA = new LocalDiscovery(NodeA, optionsA))
            using (var discoveryB = new LocalDiscovery(NodeB, optionsB))
            {
                var seen = new TaskCompletionSource<DiscoveredNode>(TaskCreationOptions.RunContinuationsAsynchronously);
                discoveryA.OnNodeDiscovered += node =>
                {
                    if (node != null && node.NodeId == NodeB) seen.TrySetResult(node);
                };

                discoveryA.Start();
                discoveryB.Start();
                Assert.IsNotNull(discoveryA.LocalEndPoint, "A 侧启动后应有本地发现端点");
                Assert.IsNotNull(discoveryB.LocalEndPoint, "B 侧启动后应有本地发现端点");

                var discovered = await WaitAsync(
                    seen.Task,
                    EventTimeout,
                    "10 秒内 A 未发现 B（端口 " + port + "）：" + Describe(discoveryA) + "；" + Describe(discoveryB)).ConfigureAwait(false);
                Assert.AreEqual(NodeB, discovered.NodeId, "发现事件应上报 B 的节点标识");

                var expired = new TaskCompletionSource<DiscoveredNode>(TaskCreationOptions.RunContinuationsAsynchronously);
                discoveryA.OnNodeExpired += node => expired.TrySetResult(node);

                // 只停 B 的周期补发，A 侧不会再收到 B 的报文，节点应在过期阈值后被清理
                discoveryB.Stop();

                // 内部计时器有抖动，这里每 100ms 主动清理一次；总预算 10 秒，超时按失败处理
                var deadline = DateTime.UtcNow + ExpireTimeout;
                while (!expired.Task.IsCompleted && DateTime.UtcNow < deadline)
                {
                    discoveryA.CleanupExpiredNodes();
                    await Task.Delay(100).ConfigureAwait(false);
                }

                if (!expired.Task.IsCompleted)
                {
                    Assert.Fail(
                        "10 秒内 A 未触发节点过期（阈值 600 ms）：" + Describe(discoveryA) + "；" + Describe(discoveryB));
                }

                var expiredNode = await expired.Task.ConfigureAwait(false);
                Assert.AreEqual(NodeB, expiredNode.NodeId, "过期事件应上报 B 的节点标识");
                Assert.IsFalse(
                    discoveryA.DiscoveredNodes.ContainsKey(NodeB),
                    "过期后 A 的已发现列表不应再包含 B：" + Describe(discoveryA));
            }
        }

        /// <summary>
        /// 预占一个尚未使用的 UDP 端口：先绑定取到系统分配端口，再立即释放交给被测实例
        /// </summary>
        /// <returns>端口号</returns>
        private static int ReserveUdpPort()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Bind(new IPEndPoint(IPAddress.Any, 0));
                return ((IPEndPoint)socket.LocalEndPoint).Port;
            }
        }

        /// <summary>
        /// 在预算内等待有结果的任务，超时以断言失败结束并带上定位信息
        /// </summary>
        /// <typeparam name="T">任务结果类型</typeparam>
        /// <param name="task">被等待的任务</param>
        /// <param name="timeout">等待预算</param>
        /// <param name="message">超时时的定位信息</param>
        /// <returns>任务结果</returns>
        private static async Task<T> WaitAsync<T>(Task<T> task, TimeSpan timeout, string message)
        {
            var completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
            if (!ReferenceEquals(completed, task))
            {
                Assert.Fail(message);
            }

            return await task.ConfigureAwait(false);
        }

        /// <summary>
        /// 在预算内等待无结果的任务，超时以断言失败结束并带上定位信息
        /// </summary>
        /// <param name="task">被等待的任务</param>
        /// <param name="timeout">等待预算</param>
        /// <param name="message">超时时的定位信息</param>
        private static async Task WaitAsync(Task task, TimeSpan timeout, string message)
        {
            var completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
            if (!ReferenceEquals(completed, task))
            {
                Assert.Fail(message);
            }

            await task.ConfigureAwait(false);
        }

        /// <summary>
        /// 拼出客户端标识、注册状态与内部发现器的端点、已发现节点数，供失败断言定位问题
        /// </summary>
        /// <param name="client">客户端</param>
        /// <returns>可读描述</returns>
        private static string Describe(P2PClient client)
        {
            var builder = new StringBuilder();
            builder.Append("PeerId=").Append(client.PeerId).Append(", IsRegistered=").Append(client.IsRegistered);

            // 客户端未公开发现端点，失败定位时用反射读取；取不到就只保留标识与注册状态
            LocalDiscovery discovery;
            try
            {
                var field = typeof(P2PClient).GetField("_discovery", BindingFlags.Instance | BindingFlags.NonPublic);
                discovery = field == null ? null : field.GetValue(client) as LocalDiscovery;
            }
            catch (Exception)
            {
                discovery = null;
            }

            if (discovery == null)
            {
                return builder.ToString();
            }

            builder.Append(", LocalEndPoint=").Append(discovery.LocalEndPoint == null ? "null" : discovery.LocalEndPoint.ToString());
            builder.Append(", Nodes=").Append(discovery.DiscoveredNodes.Count);
            return builder.ToString();
        }

        /// <summary>
        /// 拼出发现实例的端点与已发现节点，供失败断言定位问题
        /// </summary>
        /// <param name="discovery">发现实例</param>
        /// <returns>可读描述</returns>
        private static string Describe(LocalDiscovery discovery)
        {
            var builder = new StringBuilder();
            builder.Append("LocalEndPoint=").Append(discovery.LocalEndPoint == null ? "null" : discovery.LocalEndPoint.ToString());
            builder.Append(", Nodes=").Append(discovery.DiscoveredNodes.Count);

            foreach (var node in discovery.DiscoveredNodes.Values)
            {
                builder.Append(", [").Append(node.NodeId).Append('@').Append(node.LocalAddress).Append(']');
            }

            return builder.ToString();
        }
    }
}