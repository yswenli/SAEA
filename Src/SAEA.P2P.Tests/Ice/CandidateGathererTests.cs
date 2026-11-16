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
*命名空间：SAEA.P2P.Tests.Ice
*文件名： CandidateGathererTests
*版本号： net10.0
*唯一标识：dd0f54e1-f3ec-420e-8146-47742aa0e98f
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 13:30:00
*描述：ICE 候选收集用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 13:30:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 CandidateGathererTests
*
*****************************************************************************/
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Ice;

namespace SAEA.P2P.Tests.Ice
{
    /// <summary>
    /// ICE 候选收集用例。
    /// </summary>
    [TestCategory(P2PTestSuite.Unit)]
    [TestClass]
    public class CandidateGathererTests
    {
        /// <summary>
        /// 打开回环开关并绑定 127.0.0.1 时，产出地址为该回环地址、端口已分配的 UDP host 候选
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public async Task CandidateGatherer_开启AllowLoopbackCandidates_产生回环host候选()
        {
            // 回环候选默认被过滤，只有显式开开关才能观察到 host 候选
            var loopback = IPAddress.Parse("127.0.0.1");
            var options = new IceOptions
            {
                AllowLoopbackCandidates = true,
                GatheringTimeout = TimeSpan.FromMilliseconds(300)
            };
            options.BindAddresses.Add(loopback);

            using var gatherer = new CandidateGatherer(options);
            await gatherer.GatherAsync(CancellationToken.None);

            var candidate = gatherer.Candidates.FirstOrDefault(c => c.Type == CandidateType.Host && loopback.Equals(c.Address) && c.Port > 0);

            Assert.IsNotNull(candidate);
            Assert.AreEqual(IceTransportType.Udp, candidate.Transport);
            Assert.AreEqual(1, candidate.Component);
        }

        /// <summary>
        /// 绑定 127.0.0.3 时 host 候选地址等于绑定地址而不是 127.0.0.1，且只绑定一个传输
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public async Task CandidateGatherer_绑定127_0_0_3_候选地址等于绑定地址()
        {
            // 端口为 0 时由系统分配，因此地址必须取自 socket 的实际绑定端点而非配置值
            var bindAddress = IPAddress.Parse("127.0.0.3");
            var options = new IceOptions
            {
                AllowLoopbackCandidates = true,
                GatheringTimeout = TimeSpan.FromMilliseconds(300)
            };
            options.BindAddresses.Add(bindAddress);

            using var gatherer = new CandidateGatherer(options);
            await gatherer.GatherAsync(CancellationToken.None);

            var hosts = gatherer.Candidates.Where(c => c.Type == CandidateType.Host).ToList();

            Assert.AreEqual(1, hosts.Count);
            Assert.AreEqual(bindAddress, hosts[0].Address);
            Assert.AreNotEqual(IPAddress.Parse("127.0.0.1"), hosts[0].Address);
            Assert.IsTrue(hosts[0].Port > 0);

            Assert.AreEqual(1, gatherer.Transports.Count);
            Assert.IsNotNull(gatherer.Transports[0].LocalEndPoint);
            Assert.AreEqual(bindAddress, gatherer.Transports[0].LocalEndPoint.Address);
        }

        /// <summary>
        /// 默认配置按网卡枚举时不收集回环地址，也不残留 v4-mapped 形式的地址
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public async Task CandidateGatherer_默认收集_不含loopback与v4映射()
        {
            // 本机可能没有可用非回环网卡，因此只校验过滤结果，不断言候选数量
            var options = new IceOptions
            {
                GatheringTimeout = TimeSpan.FromMilliseconds(500)
            };

            using var gatherer = new CandidateGatherer(options);
            await gatherer.GatherAsync(CancellationToken.None);

            for (int i = 0; i < gatherer.Candidates.Count; i++)
            {
                var candidate = gatherer.Candidates[i];
                Assert.IsFalse(IPAddress.IsLoopback(candidate.Address), "不应出现回环候选：" + candidate.Address);
                Assert.IsFalse(candidate.Address.IsIPv4MappedToIPv6, "不应出现 v4-mapped 候选：" + candidate.Address);
            }
        }

        /// <summary>
        /// STUN 服务器不可达时按预算结束收集，保留 host 候选、不产出 srflx 候选并只触发一次结束事件
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public async Task CandidateGatherer_收集超时_返回已有候选并标记结束()
        {
            // 端口 1 上没有 STUN 服务，srflx 必然失败，用于验证预算到期按正常结束处理
            var options = new IceOptions
            {
                AllowLoopbackCandidates = true,
                GatheringTimeout = TimeSpan.FromMilliseconds(300)
            };
            options.BindAddresses.Add(IPAddress.Parse("127.0.0.1"));
            options.StunServers.Add(new IPEndPoint(IPAddress.Loopback, 1));

            using var gatherer = new CandidateGatherer(options);

            int endCount = 0;
            gatherer.EndOfCandidates += () => endCount++;

            var stopwatch = Stopwatch.StartNew();
            await gatherer.GatherAsync(CancellationToken.None);
            stopwatch.Stop();

            Assert.AreEqual(1, endCount);
            Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(5), "收集耗时超出预期：" + stopwatch.Elapsed);
            Assert.IsTrue(gatherer.Candidates.Any(c => c.Type == CandidateType.Host), "应保留已收集的 host 候选");
            Assert.IsFalse(gatherer.Candidates.Any(c => c.Type == CandidateType.ServerReflexive), "不应产出 srflx 候选");
        }

        /// <summary>
        /// 两个会话并发收集时各自占用独立端口，候选端口与传输端口都不重叠
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public async Task CandidateGatherer_两个会话_端口集不重叠()
        {
            // 同一回环地址上并发绑定，端口由系统分配，验证两个会话不会拿到同一个端口
            using var first = new CandidateGatherer(CreateLoopbackOptions());
            using var second = new CandidateGatherer(CreateLoopbackOptions());

            await Task.WhenAll(
                first.GatherAsync(CancellationToken.None),
                second.GatherAsync(CancellationToken.None));

            var firstPorts = first.Candidates.Where(c => c.Type == CandidateType.Host).Select(c => c.Port).ToList();
            var secondPorts = second.Candidates.Where(c => c.Type == CandidateType.Host).Select(c => c.Port).ToList();

            Assert.IsTrue(firstPorts.Count > 0, "第一个会话应有 host 候选");
            Assert.IsTrue(secondPorts.Count > 0, "第二个会话应有 host 候选");
            Assert.IsFalse(firstPorts.Intersect(secondPorts).Any(), "两个会话的 host 候选端口不应重叠");

            var firstTransportPorts = first.Transports.Select(t => t.LocalEndPoint.Port).ToList();
            var secondTransportPorts = second.Transports.Select(t => t.LocalEndPoint.Port).ToList();

            Assert.IsTrue(firstTransportPorts.Count > 0);
            Assert.IsTrue(secondTransportPorts.Count > 0);
            Assert.IsFalse(firstTransportPorts.Intersect(secondTransportPorts).Any(), "两个会话的传输端口不应重叠");
        }

        /// <summary>
        /// 创建只绑定回环地址的收集配置
        /// </summary>
        /// <returns>打开回环开关、预算 300 毫秒的配置</returns>
        private static IceOptions CreateLoopbackOptions()
        {
            var options = new IceOptions
            {
                AllowLoopbackCandidates = true,
                GatheringTimeout = TimeSpan.FromMilliseconds(300)
            };
            options.BindAddresses.Add(IPAddress.Parse("127.0.0.1"));
            return options;
        }
    }
}