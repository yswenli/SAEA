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
*修改时间：2026/11/13 17:05:13
*修改人： yswenli
*版本号： net10.0
*描述：新增局域网发现器的同机互相发现与 HMAC 隔离用例
*
*****************************************************************************/
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Discovery;

namespace SAEA.P2P.Tests.Discovery
{
    /// <summary>
    /// 局域网发现器：两个实例绑定同一端口后应互相发现并携带来源端点；
    /// 共享密钥不一致时应因 HMAC 校验失败而互不可见。
    /// </summary>
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
    }
}