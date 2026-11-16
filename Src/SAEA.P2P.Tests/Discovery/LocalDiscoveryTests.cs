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
*命名空间：SAEA.P2P.Tests.Discovery
*文件名： LocalDiscoveryTests
*版本号： net10.0
*唯一标识：277e0395-971f-4d05-b465-e57b0fba48f2
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 17:05:13
*描述：局域网发现器的同机互相发现与 HMAC 隔离用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 18:00:00
*修改人： yswenli
*版本号： net10.0
*描述：新增无信令客户端发现后建立 Local 通道的收发用例
*
*****************************************************************************/
using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Channel;
using SAEA.P2P.Core;
using SAEA.P2P.Discovery;
using SAEA.P2P.Ice;

namespace SAEA.P2P.Tests.Discovery
{
    /// <summary>
    /// 局域网发现器：两个实例绑定同一端口后应互相发现并携带来源端点；
    /// 共享密钥不一致时应因 HMAC 校验失败而互不可见；
    /// 无信令客户端发现后互相发起连接应得到 Local 通道并完成收发。
    /// </summary>
    [TestCategory(P2PTestSuite.LocalNet)]
    [TestClass]
    public class LocalDiscoveryTests
    {
        /// <summary>
        /// 互相发现的等待预算
        /// </summary>
        private static readonly TimeSpan DiscoverTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 两台实例同端口启动后，应在预算内各自发现对方，且记录的来源端点不为空
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public void LocalDiscovery_两台实例_互相发现()
        {
            var port = ReserveUdpPort();
            var optionsA = new LocalDiscoveryOptions { Port = port, IntervalMilliseconds = 200 };
            var optionsB = new LocalDiscoveryOptions { Port = port, IntervalMilliseconds = 200 };

            using (var discoveryA = new LocalDiscovery("discovery-a", optionsA))
            using (var discoveryB = new LocalDiscovery("discovery-b", optionsB))
            {
                discoveryA.Start();
                discoveryB.Start();

                var deadline = DateTime.UtcNow.Add(DiscoverTimeout);
                while (DateTime.UtcNow < deadline)
                {
                    DiscoveredNode nodeA;
                    DiscoveredNode nodeB;

                    var foundA = discoveryA.DiscoveredNodes.TryGetValue("discovery-b", out nodeB);
                    var foundB = discoveryB.DiscoveredNodes.TryGetValue("discovery-a", out nodeA);

                    if (foundA && foundB && nodeA.LocalAddress != null && nodeB.LocalAddress != null)
                    {
                        return;
                    }

                    Thread.Sleep(100);
                }

                Assert.Fail("10 秒内未互相发现：" + Describe(discoveryA) + "；" + Describe(discoveryB));
            }
        }

        /// <summary>
        /// 两端使用不同共享密钥时，收到的报文都应因 HMAC 校验失败被丢弃，双方列表保持为空
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public void LocalDiscovery_HMAC错误_不加入列表()
        {
            var port = ReserveUdpPort();
            var optionsA = new LocalDiscoveryOptions
            {
                Port = port,
                IntervalMilliseconds = 200,
                SharedKey = "key-a"
            };
            var optionsB = new LocalDiscoveryOptions
            {
                Port = port,
                IntervalMilliseconds = 200,
                SharedKey = "key-b"
            };

            using (var discoveryA = new LocalDiscovery("hmac-a", optionsA))
            using (var discoveryB = new LocalDiscovery("hmac-b", optionsB))
            {
                discoveryA.Start();
                discoveryB.Start();

                Thread.Sleep(2000);

                Assert.AreEqual(0, discoveryA.DiscoveredNodes.Count,
                    "密钥不一致时不应加入任何节点：" + Describe(discoveryA));
                Assert.AreEqual(0, discoveryB.DiscoveredNodes.Count,
                    "密钥不一致时不应加入任何节点：" + Describe(discoveryB));
            }
        }

        /// <summary>
        /// 两端启动无信令发现后应互相可见，各自发起连接后通道类型为 Local，并能完成一次业务帧收发
        /// </summary>
        [TestMethod]
        [Timeout(180000)]
        public async Task LocalDiscovery_发现后_建立Local通道()
        {
            var port = ReserveUdpPort();
            var optionsA = new LocalDiscoveryOptions { Port = port, IntervalMilliseconds = 200 };
            var optionsB = new LocalDiscoveryOptions { Port = port, IntervalMilliseconds = 200 };

            // 同机测试要收集回环候选，两端各用独立实例，避免共享配置被并发改写
            var iceA = new IceSessionOptions { Ice = new IceOptions { AllowLoopbackCandidates = true } };
            var iceB = new IceSessionOptions { Ice = new IceOptions { AllowLoopbackCandidates = true } };

            using (var clientA = new P2PClient("local-a", optionsA, iceA))
            using (var clientB = new P2PClient("local-b", optionsB, iceB))
            {
                var seenA = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var seenB = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                clientA.OnPeerDiscovered += node =>
                {
                    if (node != null && node.NodeId == "local-b") seenA.TrySetResult(true);
                };
                clientB.OnPeerDiscovered += node =>
                {
                    if (node != null && node.NodeId == "local-a") seenB.TrySetResult(true);
                };

                var localInfoA = await clientA.StartLocalAsync().ConfigureAwait(false);
                var localInfoB = await clientB.StartLocalAsync().ConfigureAwait(false);
                Assert.IsNotNull(localInfoA, "A 侧启动发现后应返回本端节点信息");
                Assert.IsNotNull(localInfoB, "B 侧启动发现后应返回本端节点信息");
                Assert.IsTrue(clientA.IsRegistered && clientB.IsRegistered, "启动发现后两端都应处于已注册状态");

                var bothSeen = Task.WhenAll(seenA.Task, seenB.Task);
                var discoveryWinner = await Task.WhenAny(bothSeen, Task.Delay(DiscoverTimeout)).ConfigureAwait(false);
                if (!ReferenceEquals(discoveryWinner, bothSeen))
                {
                    Assert.Fail("10 秒内未互相发现：" + Describe(clientA) + "；" + Describe(clientB));
                }
                await bothSeen.ConfigureAwait(false);

                var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                clientB.OnDataReceived += (peerId, data) =>
                {
                    // ReadOnlySpan 只在回调期间有效，跨线程比较前先复制
                    if (peerId == "local-a") received.TrySetResult(data.ToArray());
                };

                var disconnected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                clientB.OnPeerDisconnected += (peerId, reason) =>
                {
                    if (peerId == "local-a") disconnected.TrySetResult(true);
                };

                // 无信令模式没有请求/应答，两端必须同时发起，否则互相等待候选会超时
                var connectA = clientA.ConnectToPeerAsync("local-b");
                var connectB = clientB.ConnectToPeerAsync("local-a");
                var bothConnected = Task.WhenAll(connectA, connectB);
                var connectWinner = await Task.WhenAny(bothConnected, Task.Delay(TimeSpan.FromSeconds(60))).ConfigureAwait(false);
                if (!ReferenceEquals(connectWinner, bothConnected))
                {
                    Assert.Fail("60 秒内未建立连接：" + Describe(clientA) + "；" + Describe(clientB));
                }
                await bothConnected.ConfigureAwait(false);

                Assert.AreEqual(ChannelType.Local, connectA.Result.Channel, "A 侧通道应为 Local：" + Describe(clientA));
                Assert.AreEqual("local-b", connectA.Result.PeerId, "A 侧对端标识应为 local-b");
                Assert.AreEqual(ChannelType.Local, connectB.Result.Channel, "B 侧通道应为 Local：" + Describe(clientB));
                Assert.AreEqual("local-a", connectB.Result.PeerId, "B 侧对端标识应为 local-a");

                var payload = Encoding.UTF8.GetBytes("local-channel-payload");
                clientA.Send("local-b", payload);

                var payloadTask = received.Task;
                var payloadWinner = await Task.WhenAny(payloadTask, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
                if (!ReferenceEquals(payloadWinner, payloadTask))
                {
                    Assert.Fail("10 秒内未收到业务帧：" + Describe(clientA) + "；" + Describe(clientB));
                }
                CollectionAssert.AreEqual(payload, await payloadTask.ConfigureAwait(false), "B 侧收到的载荷应与 A 侧发送的一致");

                await clientA.DisconnectPeerAsync("local-b").ConfigureAwait(false);
                await clientB.DisconnectPeerAsync("local-a").ConfigureAwait(false);
                Assert.IsTrue(disconnected.Task.IsCompleted, "B 侧关闭会话后应触发 OnPeerDisconnected");
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
        /// 拼出实例的端点与已发现节点，供失败断言定位问题
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

        /// <summary>
        /// 拼出客户端标识、内部发现器的端点与已发现节点数，供失败断言定位问题
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
    }
}