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
*文件名： NominatorTests
*版本号： net10.0
*唯一标识：b457745e-8eaf-4be9-8144-f0fd8f533850
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 17:00:00
*描述：ICE 提名请求、peer-reflexive 候选创建与选中对发布用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 17:00:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 NominatorTests
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Ice;
using SAEA.P2P.Stun;
using SAEA.P2P.Transport;

namespace SAEA.P2P.Tests.Ice
{
    /// <summary>
    /// 提名请求的事务 ID 与属性、peer-reflexive 候选两条创建路径、未选出候选对时的发送约束与被动侧行为。
    /// </summary>
    [TestCategory(P2PTestSuite.Unit)]
    [TestClass]
    public class NominatorTests
    {
        /// <summary>
        /// 每次提名都必须是全新事务 ID 的请求，并携带 USE-CANDIDATE、完整性、prflx 优先级与短凭据用户名
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void Nominator_提名_使用新transactionId()
        {
            // 复用上一次检查请求的事务 ID 会被对端当成重传，拿不到独立的提名应答
            using (var nominator = new Nominator(IceRole.Controlling, 1000UL, "local", "localpwd", "remote", "remotepwd", new CheckList(true)))
            {
                var local = new Candidate(CandidateType.Host, IceTransportType.Udp, 1, IPAddress.Parse("127.0.0.2"), 5000, 2130706431u);
                var remote = new Candidate(CandidateType.Host, IceTransportType.Udp, 1, IPAddress.Parse("127.0.0.3"), 6000, 2130706431u);
                var pair = new CandidatePair(local, remote, true);
                pair.MarkInProgress();
                pair.MarkSucceeded();

                var first = nominator.CreateNominationMessage(pair);
                var second = nominator.CreateNominationMessage(pair);

                CollectionAssert.AreNotEqual(first.TransactionId.ToArray(), second.TransactionId.ToArray());

                StunAttribute useCandidate;
                Assert.IsTrue(first.TryGet(StunAttributeType.UseCandidate, out useCandidate));
                Assert.IsTrue(second.TryGet(StunAttributeType.UseCandidate, out useCandidate));

                StunAttribute integrity;
                Assert.IsTrue(first.TryGet(StunAttributeType.MessageIntegrity, out integrity));
                Assert.IsTrue(second.TryGet(StunAttributeType.MessageIntegrity, out integrity));

                // PRIORITY 必须按 peer-reflexive 类型算，不是本地候选真实类型
                StunAttribute priority;
                Assert.IsTrue(first.TryGet(StunAttributeType.Priority, out priority));
                CollectionAssert.AreEqual(
                    new byte[] { 0x6E, 0xFF, 0xFF, 0xFF },
                    priority.Value.ToArray());
                Assert.AreEqual(IcePriority.ComputeForUdp(CandidateType.PeerReflexive, 1), DecodeUInt32(priority.Value.ToArray()));

                StunAttribute username;
                Assert.IsTrue(first.TryGet(StunAttributeType.Username, out username));
                Assert.AreEqual("remote:local", Encoding.UTF8.GetString(username.Value.ToArray()));
            }
        }

        /// <summary>
        /// 收到检查请求时，来源端点不在已知远端候选里才创建 peer-reflexive 远端候选，重复端点不再新建
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void Nominator_prflx_从请求创建()
        {
            var source = new IPEndPoint(IPAddress.Parse("127.0.0.3"), 6000);
            var known = new List<Candidate>();

            var candidate = Nominator.CreatePeerReflexiveFromRequest(source, 1862270975u, 1, IceTransportType.Udp, known);

            Assert.IsNotNull(candidate);
            Assert.AreEqual(CandidateType.PeerReflexive, candidate.Type);
            Assert.AreEqual(source.Address, candidate.Address);
            Assert.AreEqual(source.Port, candidate.Port);
            Assert.AreEqual(1862270975u, candidate.Priority);
            Assert.AreEqual(IceTransportType.Udp, candidate.Transport);

            // 同一端点已在已知候选里时不得重复创建，否则候选对会翻倍
            known.Add(candidate);
            Assert.IsNull(Nominator.CreatePeerReflexiveFromRequest(source, 1862270975u, 1, IceTransportType.Udp, known));
        }

        /// <summary>
        /// 收到响应时，映射地址不匹配已知本地候选才创建 peer-reflexive 本地候选，并用 related 字段保留该 pair 的本地候选作为 base
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void Nominator_prflx_从响应创建()
        {
            var localHost = new Candidate(CandidateType.Host, IceTransportType.Udp, 1, IPAddress.Parse("127.0.0.2"), 5000, 2130706431u);
            var remoteHost = new Candidate(CandidateType.Host, IceTransportType.Udp, 1, IPAddress.Parse("127.0.0.3"), 6000, 2130706431u);
            var pair = new CandidatePair(localHost, remoteHost, true);
            var mappedAddress = new IPEndPoint(IPAddress.Parse("127.0.0.2"), 40000);
            var known = new List<Candidate> { localHost };

            var candidate = Nominator.CreatePeerReflexiveFromResponse(pair, mappedAddress, 1862270975u, known);

            Assert.IsNotNull(candidate);
            Assert.AreEqual(IPAddress.Parse("127.0.0.2"), candidate.Address);
            Assert.AreEqual(40000, candidate.Port);
            Assert.AreEqual(IPAddress.Parse("127.0.0.2"), candidate.RelatedAddress);
            Assert.AreEqual(5000, candidate.RelatedPort);
            Assert.AreEqual(1862270975u, candidate.Priority);

            // 映射地址已在已知本地候选里时说明它就是该候选，不得重复创建
            known.Add(candidate);
            Assert.IsNull(Nominator.CreatePeerReflexiveFromResponse(pair, mappedAddress, 1862270975u, known));
        }

        /// <summary>
        /// 未选出候选对时发送业务数据必须直接抛错，避免把数据发到未提名的路径上
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void Nominator_未选出pair_Send抛错()
        {
            using (var nominator = new Nominator(IceRole.Controlling, 1000UL, "local", "localpwd", "remote", "remotepwd", new CheckList(true)))
            {
                Assert.IsNull(nominator.SelectedPair);
                Assert.ThrowsException<InvalidOperationException>(() => nominator.EnsureCanSend());
            }
        }

        /// <summary>
        /// 被动侧不得自行提名：不发送任何报文、不改状态、不抛异常，直接返回失败
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public async Task Nominator_被动侧_不提名()
        {
            using (var nominator = new Nominator(IceRole.Controlled, 2000UL, "local", "localpwd", "remote", "remotepwd", new CheckList(false)))
            {
                var local = new Candidate(CandidateType.Host, IceTransportType.Udp, 1, IPAddress.Parse("127.0.0.2"), 5000, 2130706431u);
                var remote = new Candidate(CandidateType.Host, IceTransportType.Udp, 1, IPAddress.Parse("127.0.0.3"), 6000, 2130706431u);
                var pair = new CandidatePair(local, remote, false);
                var transport = new FakeUdpTransport();

                var selected = await nominator.NominateAsync(transport, pair, null, new List<IPAddress>(), CancellationToken.None);

                Assert.IsFalse(selected);
                Assert.AreEqual(0, transport.SendCount);
                Assert.IsNull(nominator.SelectedPair);
                Assert.IsFalse(nominator.IsNominationCompleted);
            }
        }

        /// <summary>
        /// 按大端把 4 字节解成无符号整数
        /// </summary>
        /// <param name="bytes">4 字节大端数组</param>
        /// <returns>解出的数值</returns>
        private static uint DecodeUInt32(byte[] bytes)
        {
            return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
        }

        /// <summary>
        /// 只记录发送次数的 UDP 传输替身，从不收包
        /// </summary>
        private sealed class FakeUdpTransport : IUdpTransport
        {
#pragma warning disable 67
            /// <summary>
            /// 接收事件；本替身从不触发
            /// </summary>
            public event Action<IPEndPoint, ReadOnlyMemory<byte>> DatagramReceived;
#pragma warning restore 67

            /// <summary>
            /// 本地端点，取回环地址固定端口
            /// </summary>
            public IPEndPoint LocalEndPoint
            {
                get { return new IPEndPoint(IPAddress.Loopback, 5000); }
            }

            /// <summary>
            /// 固定视为可用
            /// </summary>
            public bool IsConnected
            {
                get { return true; }
            }

            /// <summary>
            /// 累计发送次数
            /// </summary>
            public int SendCount { get; private set; }

            /// <summary>
            /// 绑定空实现
            /// </summary>
            /// <param name="localEndPoint">本地端点</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>已完成的任务</returns>
            public Task BindAsync(IPEndPoint localEndPoint, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            /// <summary>
            /// 启动接收空实现
            /// </summary>
            public void StartReceive()
            {
            }

            /// <summary>
            /// 记录发送次数并立刻完成
            /// </summary>
            /// <param name="remoteEndPoint">目标端点</param>
            /// <param name="data">待发送数据</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>已完成的任务</returns>
            public Task SendToAsync(IPEndPoint remoteEndPoint, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
            {
                SendCount++;
                return Task.CompletedTask;
            }

            /// <summary>
            /// 释放空实现
            /// </summary>
            public void Dispose()
            {
            }
        }
    }
}