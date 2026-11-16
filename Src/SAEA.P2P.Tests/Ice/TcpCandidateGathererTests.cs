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
*文件名： TcpCandidateGathererTests
*版本号： net10.0
*唯一标识：9804211a-2277-47a4-bae7-a66e8cdbf0b8
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 12:00:19
*描述：TCP 候选收集与候选格式化用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 12:00:19
*修改人： yswenli
*版本号： net10.0
*描述：新增 TcpCandidateGathererTests
*
*****************************************************************************/
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Ice;

namespace SAEA.P2P.Tests.Ice
{
    /// <summary>
    /// TCP 候选收集与候选格式化用例。
    /// </summary>
    [TestCategory(P2PTestSuite.Unit)]
    [TestClass]
    public class TcpCandidateGathererTests
    {
        /// <summary>
        /// 开启同时打开开关时，Windows 上降级为只收集被动候选
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task TcpCandidateGatherer_Windows_不收集so()
        {
            // 同时打开依赖非 Windows 的 TCP 栈行为，其他平台没有可复现的判定条件
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Inconclusive("同时打开只在 Windows 上被禁用");
            }

            var options = new IceOptions
            {
                EnableTcpSimultaneousOpen = true,
                AllowLoopbackCandidates = true
            };
            options.BindAddresses.Add(IPAddress.Parse("127.0.0.21"));

            using var gatherer = new TcpCandidateGatherer(options);
            await gatherer.GatherAsync(CancellationToken.None);

            Assert.IsFalse(TcpCandidateGatherer.IsSimultaneousOpenSupported, "Windows 上应报告不支持同时打开");

            var candidates = gatherer.Candidates;
            Assert.IsFalse(
                candidates.Any(c => c.TcpType == TcpCandidateType.SimultaneousOpen),
                "Windows 上不应产出同时打开候选");
            Assert.IsTrue(
                candidates.Count(c => c.TcpType == TcpCandidateType.Passive) >= 1,
                "降级后仍应产出被动候选");
        }

        /// <summary>
        /// 每个绑定地址各产生一个被动候选，候选端口与监听端口一一对应且互不重复
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task TcpCandidateGatherer_每接口_产生passive候选()
        {
            var first = IPAddress.Parse("127.0.0.21");
            var second = IPAddress.Parse("127.0.0.22");

            // 端口为 0 时由系统分配，因此地址与端口都必须取自监听的实际绑定端点
            var options = new IceOptions
            {
                AllowLoopbackCandidates = true,
                Component = 1
            };
            options.BindAddresses.Add(first);
            options.BindAddresses.Add(second);

            using var gatherer = new TcpCandidateGatherer(options);
            await gatherer.GatherAsync(CancellationToken.None);

            var candidates = gatherer.Candidates;
            Assert.AreEqual(2, candidates.Count, "每个绑定地址应各产生一个候选");

            for (int i = 0; i < candidates.Count; i++)
            {
                Assert.AreEqual(CandidateType.Host, candidates[i].Type, "候选类型应为 host");
                Assert.AreEqual(IceTransportType.Tcp, candidates[i].Transport, "候选传输应为 TCP");
                Assert.AreEqual(TcpCandidateType.Passive, candidates[i].TcpType, "候选方向应为被动");
                Assert.IsTrue(candidates[i].Port > 0, "候选端口应已分配：" + candidates[i].Port);
                Assert.AreEqual(
                    IcePriority.ComputeForTcp(CandidateType.Host, 1, TcpCandidateType.Passive),
                    candidates[i].Priority,
                    "被动候选项优先级应按方向偏好计算");
            }

            CollectionAssert.AreEquivalent(
                new[] { first, second },
                candidates.Select(c => c.Address).ToArray(),
                "候选地址应与绑定地址一一对应");

            Assert.AreNotEqual(candidates[0].Port, candidates[1].Port, "两个候选端口不应相同");

            var listenerPorts = gatherer.Listeners.Select(l => l.LocalEndPoint.Port).ToList();
            for (int i = 0; i < candidates.Count; i++)
            {
                Assert.IsTrue(
                    listenerPorts.Contains(candidates[i].Port),
                    "候选端口应能在监听端点中找到：" + candidates[i].Port);
            }
        }

        /// <summary>
        /// 候选格式化：主动方向端口固定写 9，被动写真实端口，中继候选带关联地址
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public void TcpCandidate_序列化_active端口写9()
        {
            var address = IPAddress.Parse("127.0.0.21");

            var active = new Candidate(CandidateType.Host, IceTransportType.Tcp, 1, address, 65432, TcpCandidateType.Active, null, 0);
            var activeFields = CandidateCodec.Format(active).Split(' ');

            // 字段顺序：a=candidate:foundation component transport priority address port typ type ...
            Assert.AreEqual("tcp", activeFields[2], "主动候选的传输字段应写 tcp");
            Assert.AreEqual("9", activeFields[5], "主动候选的端口字段应写 9 而不是真实端口");
            Assert.IsTrue(
                CandidateCodec.Format(active).EndsWith("tcptype active"),
                "主动候选应带 tcptype active：" + CandidateCodec.Format(active));

            var passive = new Candidate(CandidateType.Host, IceTransportType.Tcp, 1, address, 65432, TcpCandidateType.Passive, null, 0);
            var passiveFields = CandidateCodec.Format(passive).Split(' ');

            Assert.AreEqual("65432", passiveFields[5], "被动候选的端口字段应写真实端口");
            Assert.IsTrue(
                CandidateCodec.Format(passive).EndsWith("tcptype passive"),
                "被动候选应带 tcptype passive：" + CandidateCodec.Format(passive));

            var relayAddress = IPAddress.Parse("127.0.0.22");
            var relay = new Candidate(CandidateType.Relay, IceTransportType.Udp, 1, relayAddress, 50000, TcpCandidateType.None, relayAddress, 60000);
            var relayText = CandidateCodec.Format(relay);

            Assert.IsTrue(
                relayText.Contains(" raddr 127.0.0.22 rport 60000"),
                "中继候选应带关联地址与关联端口：" + relayText);
        }
    }
}