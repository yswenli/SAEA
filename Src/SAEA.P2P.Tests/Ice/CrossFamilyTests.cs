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
*文件名： CrossFamilyTests
*版本号： net10.0
*唯一标识：72125a1f-af89-42cc-afe3-7dbb0113e003
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 21:40:00
*描述：跨地址族配对开关用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 21:40:00
*修改人： yswenli
*版本号： net10.0
*描述：新增跨地址族配对开关用例
*
*****************************************************************************/
using System.Net;
using System.Net.Sockets;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Ice;

namespace SAEA.P2P.Tests.Ice
{
    /// <summary>
    /// 跨地址族配对开关用例：默认关闭时跨族候选静默跳过并计数，开启后 IPv4 与 IPv6 可以配对。
    /// </summary>
    [TestCategory(P2PTestSuite.Ipv6)]
    [TestClass]
    public class CrossFamilyTests
    {
        /// <summary>
        /// 默认关闭跨族配对时，IPv4 本地候选与 IPv6 远端候选不配对，并累计跳过计数
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void CheckList_跨族默认关闭_不配对()
        {
            // 不传第二个参数，验证默认关闭
            var list = new CheckList(true);
            var local = new Candidate(CandidateType.Host, IceTransportType.Udp, 1, IPAddress.Parse("127.0.0.1"), 5000, TcpCandidateType.None, null, 0);
            var remote = new Candidate(CandidateType.Host, IceTransportType.Udp, 1, IPAddress.IPv6Loopback, 5001, TcpCandidateType.None, null, 0);

            list.AddLocalCandidate(local);
            list.AddRemoteCandidate(remote);

            Assert.AreEqual(0, list.PairCount, string.Format("实际 PairCount={0}", list.PairCount));
            Assert.AreEqual(1, list.CrossFamilyPairsSkipped, string.Format("实际 CrossFamilyPairsSkipped={0}", list.CrossFamilyPairsSkipped));
        }

        /// <summary>
        /// 开启跨族配对后，IPv4 本地候选与 IPv6 远端候选成功配对且不累计跳过计数
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void CheckList_开启跨族_v4与v6配对成功()
        {
            var list = new CheckList(true, true);
            var local = new Candidate(CandidateType.Host, IceTransportType.Udp, 1, IPAddress.Parse("127.0.0.1"), 5000, TcpCandidateType.None, null, 0);
            var remote = new Candidate(CandidateType.Host, IceTransportType.Udp, 1, IPAddress.IPv6Loopback, 5001, TcpCandidateType.None, null, 0);

            list.AddLocalCandidate(local);
            list.AddRemoteCandidate(remote);

            Assert.AreEqual(1, list.PairCount, string.Format("实际 PairCount={0}", list.PairCount));
            Assert.AreEqual(0, list.CrossFamilyPairsSkipped, string.Format("实际 CrossFamilyPairsSkipped={0}", list.CrossFamilyPairsSkipped));
            Assert.AreEqual(true, list.AllowCrossFamilyPairs, string.Format("实际 AllowCrossFamilyPairs={0}", list.AllowCrossFamilyPairs));

            var pair = list.Pairs[0];
            Assert.AreEqual(AddressFamily.InterNetwork, pair.Local.Address.AddressFamily, string.Format("实际 Local={0}", pair.Local.Address));
            Assert.AreEqual(AddressFamily.InterNetworkV6, pair.Remote.Address.AddressFamily, string.Format("实际 Remote={0}", pair.Remote.Address));
        }
    }
}