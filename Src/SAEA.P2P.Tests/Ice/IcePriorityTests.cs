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
*文件名： IcePriorityTests
*版本号： net10.0
*唯一标识：0abd9f05-b38f-4b81-8908-5234da8e856b
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 12:00:00
*描述：ICE 候选与候选对优先级计算用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 12:00:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 IcePriorityTests
*
*****************************************************************************/
using System.Net;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Ice;

namespace SAEA.P2P.Tests.Ice
{
    /// <summary>
    /// ICE 候选与候选对优先级计算用例。
    /// </summary>
    [TestCategory(P2PTestSuite.Unit)]
    [TestClass]
    public class IcePriorityTests
    {
        /// <summary>
        /// UDP host 候选优先级等于类型优先级、本地偏好与分量补数按位拼装的结果
        /// </summary>
        [TestMethod]
        public void IcePriority_host候选_等于公式值()
        {
            // 显式算式与魔数各算一遍，防止实现与常量一起漂移
            const uint expected = (126u << 24) + (65535u << 8) + 255u;

            uint actual = IcePriority.ComputeForUdp(CandidateType.Host, 1);

            Assert.AreEqual(expected, actual);
            Assert.AreEqual(2130706431u, actual);
        }

        /// <summary>
        /// relay 的类型优先级为 0，候选优先级排在最后，且类型优先级保持 host 最高
        /// </summary>
        [TestMethod]
        public void IcePriority_relay为0_排序在最后()
        {
            // 其余三项类型优先级都大于 0，因此 relay 候选必然排在它们之后
            uint relay = IcePriority.ComputeForUdp(CandidateType.Relay, 1);

            Assert.IsTrue(relay < IcePriority.ComputeForUdp(CandidateType.Host, 1));
            Assert.IsTrue(relay < IcePriority.ComputeForUdp(CandidateType.ServerReflexive, 1));
            Assert.IsTrue(relay < IcePriority.ComputeForUdp(CandidateType.PeerReflexive, 1));

            Assert.AreEqual(0, IcePriority.GetTypePreference(CandidateType.Relay));

            Assert.IsTrue(IcePriority.GetTypePreference(CandidateType.Host) > IcePriority.GetTypePreference(CandidateType.PeerReflexive));
            Assert.IsTrue(IcePriority.GetTypePreference(CandidateType.PeerReflexive) > IcePriority.GetTypePreference(CandidateType.ServerReflexive));
            Assert.IsTrue(IcePriority.GetTypePreference(CandidateType.ServerReflexive) > IcePriority.GetTypePreference(CandidateType.Relay));
        }

        /// <summary>
        /// 同一对候选在控制方与受控方两种角色下，候选对优先级按 G/D 公式取不同的 64 位值
        /// </summary>
        [TestMethod]
        public void IceCandidatePair_控制方与受控方_优先级方向不同()
        {
            // 控制方候选落 G 位、受控方候选落 D 位；两种情况只有末尾 tie-break 位随之翻转
            const uint hostPriority = (126u << 24) + (65535u << 8) + 255u;
            const uint relayPriority = (0u << 24) + (65535u << 8) + 255u;

            var hostCandidate = new Candidate(CandidateType.Host, IceTransportType.Udp, 1, IPAddress.Parse("127.0.0.2"), 5000, hostPriority);
            var relayCandidate = new Candidate(CandidateType.Relay, IceTransportType.Udp, 1, IPAddress.Parse("10.0.0.2"), 6000, relayPriority);

            var controllingPair = new CandidatePair(hostCandidate, relayCandidate, true);
            var controlledPair = new CandidatePair(hostCandidate, relayCandidate, false);

            // G 为 host、D 为 relay 时 MIN 是 relay、MAX 是 host，且 G 更大要加 1；受控方方向相反则不加
            ulong expectedControlling = ((ulong)relayPriority << 32) | ((ulong)hostPriority << 1) | 1UL;
            ulong expectedControlled = ((ulong)relayPriority << 32) | ((ulong)hostPriority << 1);

            Assert.AreEqual(expectedControlling, controllingPair.Priority);
            Assert.AreEqual(expectedControlled, controlledPair.Priority);
            Assert.AreNotEqual(controllingPair.Priority, controlledPair.Priority);

            // 再比一对交换了本地/远端位置的组合：高位由较小优先级主导，因此两种情况只差末尾 tie-break 位
            var controllingSwapped = new CandidatePair(relayCandidate, hostCandidate, true);
            ulong expectedControllingSwapped = ((ulong)relayPriority << 32) | ((ulong)hostPriority << 1);

            Assert.AreEqual(expectedControllingSwapped, controllingSwapped.Priority);
            Assert.IsTrue(controllingPair.Priority > controllingSwapped.Priority);

            var controlledSwapped = new CandidatePair(relayCandidate, hostCandidate, false);
            ulong expectedControlledSwapped = ((ulong)relayPriority << 32) | ((ulong)hostPriority << 1) | 1UL;

            Assert.AreEqual(expectedControlledSwapped, controlledSwapped.Priority);
            Assert.IsTrue(controlledSwapped.Priority > controlledPair.Priority);
        }

        /// <summary>
        /// foundation 只由类型优先级、base 地址与传输协议构成，同地址不同端口相同、地址或类型不同则不同
        /// </summary>
        [TestMethod]
        public void IceFoundation_同接口不同端口_相同()
        {
            // foundation 不含端口，同类型同传输同地址时不同端口必须归入同一个 foundation
            var host5000 = new Candidate(CandidateType.Host, IceTransportType.Udp, 1, IPAddress.Parse("127.0.0.2"), 5000, 0u);
            var host6000 = new Candidate(CandidateType.Host, IceTransportType.Udp, 1, IPAddress.Parse("127.0.0.2"), 6000, 0u);

            Assert.AreEqual(host5000.Foundation, host6000.Foundation);

            var otherAddress = new Candidate(CandidateType.Host, IceTransportType.Udp, 1, IPAddress.Parse("127.0.0.3"), 5000, 0u);
            var otherType = new Candidate(CandidateType.ServerReflexive, IceTransportType.Udp, 1, IPAddress.Parse("127.0.0.2"), 5000, 0u);

            Assert.AreNotEqual(host5000.Foundation, otherAddress.Foundation);
            Assert.AreNotEqual(host5000.Foundation, otherType.Foundation);
        }
    }
}