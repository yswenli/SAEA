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
*文件名： CheckListTests
*版本号： net10.0
*唯一标识：688e4e70-c94a-4834-993c-d2214fad0aa3
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 15:00:00
*描述：ICE 检查表配对与解冻用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 15:00:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 CheckListTests
*
*****************************************************************************/
using System.Linq;
using System.Net;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Ice;

namespace SAEA.P2P.Tests.Ice
{
    /// <summary>
    /// ICE 检查表的增量配对、解冻顺序、方向剪除与提名后忽略用例。
    /// </summary>
    [TestClass]
    public class CheckListTests
    {
        /// <summary>
        /// 新增远端候选只追加新候选对，既有候选对实例保持不变
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void CheckList_新候选到达_增量配对不重建()
        {
            // 后到的远端候选不能重建已有候选对，否则既有检查状态会被清空
            var list = new CheckList(true);
            var a = new Candidate(CandidateType.Host, IceTransportType.Udp, 1, IPAddress.Parse("127.0.0.2"), 5000, TcpCandidateType.None, null, 0);
            var r1 = new Candidate(CandidateType.Host, IceTransportType.Udp, 1, IPAddress.Parse("127.0.0.3"), 6000, TcpCandidateType.None, null, 0);
            var r2 = new Candidate(CandidateType.Host, IceTransportType.Udp, 1, IPAddress.Parse("127.0.0.3"), 6001, TcpCandidateType.None, null, 0);

            list.AddLocalCandidate(a);
            list.AddRemoteCandidate(r1);

            Assert.AreEqual(1, list.PairCount);
            var first = list.Pairs[0];

            list.AddRemoteCandidate(r2);

            Assert.AreEqual(2, list.PairCount);
            Assert.IsTrue(list.Pairs.Any(pair => ReferenceEquals(pair, first)));
        }

        /// <summary>
        /// 同 foundation 组只解冻一个候选对，失败的候选对让组内下一个解冻
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void CheckList_同foundation_解冻顺序正确()
        {
            // foundation 不含端口，同地址不同端口的候选对归入同组并按组逐个解冻
            var list = new CheckList(true);
            var a1 = new Candidate(CandidateType.Host, IceTransportType.Udp, 1, IPAddress.Parse("127.0.0.2"), 5000, TcpCandidateType.None, null, 0);
            var a2 = new Candidate(CandidateType.Host, IceTransportType.Udp, 1, IPAddress.Parse("127.0.0.2"), 5001, TcpCandidateType.None, null, 0);
            var r = new Candidate(CandidateType.Host, IceTransportType.Udp, 1, IPAddress.Parse("127.0.0.3"), 6000, TcpCandidateType.None, null, 0);

            list.AddLocalCandidate(a1);
            list.AddLocalCandidate(a2);
            list.AddRemoteCandidate(r);

            Assert.AreEqual(2, list.PairCount);
            Assert.AreEqual(a1.Foundation, list.Pairs[0].Local.Foundation);
            Assert.AreEqual(a1.Foundation, list.Pairs[1].Local.Foundation);

            var waiting = list.Pairs.Single(pair => pair.State == PairState.Waiting);
            var frozen = list.Pairs.Single(pair => pair.State == PairState.Frozen);

            list.NotifyPairFailed(waiting);

            Assert.AreEqual(PairState.Failed, waiting.State);
            Assert.AreEqual(PairState.Waiting, frozen.State);
        }

        /// <summary>
        /// 本地被动 TCP 候选不主动发起检查而被剪除，方向反过来的组合仍能配对
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void CheckList_本地passive对_被剪除()
        {
            // 被动候选在等对端连接，本端主动检查注定发不出去，因此只剪除本地被动方向
            var list = new CheckList(true);
            var localPassive = new Candidate(CandidateType.Host, IceTransportType.Tcp, 1, IPAddress.Parse("127.0.0.2"), 5000, TcpCandidateType.Passive, null, 0);
            var remoteActive = new Candidate(CandidateType.Host, IceTransportType.Tcp, 1, IPAddress.Parse("127.0.0.3"), 6000, TcpCandidateType.Active, null, 0);

            list.AddLocalCandidate(localPassive);
            list.AddRemoteCandidate(remoteActive);

            Assert.AreEqual(0, list.PairCount);

            var controlList = new CheckList(true);
            var localActive = new Candidate(CandidateType.Host, IceTransportType.Tcp, 1, IPAddress.Parse("127.0.0.2"), 5000, TcpCandidateType.Active, null, 0);
            var remotePassive = new Candidate(CandidateType.Host, IceTransportType.Tcp, 1, IPAddress.Parse("127.0.0.3"), 6000, TcpCandidateType.Passive, null, 0);

            controlList.AddLocalCandidate(localActive);
            controlList.AddRemoteCandidate(remotePassive);

            Assert.AreEqual(1, controlList.PairCount);
        }

        /// <summary>
        /// 提名完成后再到达的远端候选被忽略，检查表不再扩张
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void CheckList_提名后新候选_被忽略()
        {
            // 提名结束后引入的候选对没参与提名，只能整体忽略，避免出现无法提名的候选对
            var list = new CheckList(true);
            var a = new Candidate(CandidateType.Host, IceTransportType.Udp, 1, IPAddress.Parse("127.0.0.2"), 5000, TcpCandidateType.None, null, 0);
            var r1 = new Candidate(CandidateType.Host, IceTransportType.Udp, 1, IPAddress.Parse("127.0.0.3"), 6000, TcpCandidateType.None, null, 0);
            var r2 = new Candidate(CandidateType.Host, IceTransportType.Udp, 1, IPAddress.Parse("127.0.0.3"), 6001, TcpCandidateType.None, null, 0);

            list.AddLocalCandidate(a);
            list.AddRemoteCandidate(r1);

            Assert.AreEqual(1, list.PairCount);

            list.MarkNominationCompleted();
            list.AddRemoteCandidate(r2);

            Assert.AreEqual(1, list.PairCount);
            Assert.IsFalse(list.Pairs.Any(pair => pair.Remote.EndPoint.Equals(r2.EndPoint)));
        }
    }
}