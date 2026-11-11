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
*文件名： ConnectivityCheckTests
*版本号： net10.0
*唯一标识：05833961-6971-49eb-9898-9f461045a218
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 16:30:00
*描述：ICE 连通性检查属性构造与中继权限先行用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 16:30:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 ConnectivityCheckTests
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
    /// 连通性检查的请求属性构造、优先级取值与中继权限先行用例。
    /// </summary>
    [TestClass]
    public class ConnectivityCheckTests
    {
        /// <summary>
        /// 检查请求携带 USERNAME、PRIORITY、角色与可选的 USE-CANDIDATE，PRIORITY 按 peer-reflexive 类型计算
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void ConnectivityCheck_请求_PRIORITY按prflx类型计算()
        {
            // PRIORITY 必须按 peer-reflexive 类型算，用本地候选真实类型会让对端的候选对排序失真
            using (var check = new ConnectivityCheck("localufrag", "localpwd"))
            {
                var priority = check.ComputePeerReflexivePriority(1);

                Assert.AreEqual(IcePriority.ComputeForUdp(CandidateType.PeerReflexive, 1), priority);

                var attributes = check.BuildRequestAttributes("remoteufrag", IceRole.Controlling, 0x0102030405060708UL, priority, true);

                // 顺序固定为 Username、Priority、角色、USE-CANDIDATE
                Assert.AreEqual(4, attributes.Count);
                Assert.AreEqual((ushort)StunAttributeType.Username, attributes[0].Type);
                Assert.AreEqual((ushort)StunAttributeType.Priority, attributes[1].Type);
                Assert.AreEqual((ushort)StunAttributeType.IceControlling, attributes[2].Type);
                Assert.AreEqual((ushort)StunAttributeType.UseCandidate, attributes[3].Type);

                var username = Find(attributes, StunAttributeType.Username);
                Assert.IsNotNull(username);
                Assert.AreEqual("remoteufrag:localufrag", Encoding.UTF8.GetString(username.Value.ToArray()));

                // 110 << 24 + 65535 << 8 + (256 - 1) = 0x6EFFFFFF，按大端写入
                var priorityValue = Find(attributes, StunAttributeType.Priority);
                Assert.IsNotNull(priorityValue);
                CollectionAssert.AreEqual(new byte[] { 0x6E, 0xFF, 0xFF, 0xFF }, priorityValue.Value.ToArray());

                var controlling = Find(attributes, StunAttributeType.IceControlling);
                Assert.IsNotNull(controlling);
                CollectionAssert.AreEqual(
                    new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 },
                    controlling.Value.ToArray());

                var useCandidate = Find(attributes, StunAttributeType.UseCandidate);
                Assert.IsNotNull(useCandidate);
                Assert.AreEqual(0, useCandidate.Value.Length);

                // 未提名时只带受控角色属性，且不得再出现 USE-CANDIDATE
                var controlled = check.BuildRequestAttributes("remoteufrag", IceRole.Controlled, 0x0102030405060708UL, priority, false);

                Assert.AreEqual(3, controlled.Count);
                Assert.IsNull(Find(controlled, StunAttributeType.UseCandidate));
                Assert.IsNull(Find(controlled, StunAttributeType.IceControlling));
                Assert.IsNotNull(Find(controlled, StunAttributeType.IceControlled));
            }
        }

        /// <summary>
        /// 从中继候选发起检查时，建权限必须先于首个检查包，否则 TURN 会丢弃报文
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public async Task ConnectivityCheck_relay发起_先建立权限()
        {
            // 顺序由权限侧与发送侧共用同一个自增计数器判定，权限序号必须小于首个发送序号
            using (var check = new ConnectivityCheck("localufrag", "localpwd"))
            {
                var permission = new FakePermission();
                var transport = new FakeUdpTransport();
                var remoteAddresses = new[] { IPAddress.Parse("127.0.0.3") };

                using (var cts = new CancellationTokenSource())
                {
                    cts.CancelAfter(300);

                    var result = await check.CheckAsync(
                        transport,
                        new IPEndPoint(IPAddress.Parse("127.0.0.3"), 6000),
                        "remoteufrag",
                        "remotepwd",
                        IceRole.Controlling,
                        1UL,
                        check.ComputePeerReflexivePriority(1),
                        false,
                        permission,
                        remoteAddresses,
                        cts.Token);

                    Assert.AreEqual(1, permission.CallCount);
                    Assert.AreEqual(1, permission.Addresses.Count);
                    Assert.AreEqual(remoteAddresses[0], permission.Addresses[0]);

                    Assert.IsTrue(transport.SendCount >= 1);
                    Assert.IsTrue(permission.Sequence < transport.FirstSendSequence);

                    // 无人应答，取消后按失败上报
                    Assert.IsFalse(result.Succeeded);
                    Assert.IsFalse(result.RoleConflict);
                    Assert.IsNull(result.MappedAddress);
                    Assert.IsNotNull(result.Binding);
                }
            }
        }

        /// <summary>
        /// 按类型取第一个匹配的属性；不存在时返回 null
        /// </summary>
        /// <param name="attributes">属性列表</param>
        /// <param name="type">属性类型</param>
        /// <returns>匹配到的属性</returns>
        private static StunAttribute Find(IReadOnlyList<StunAttribute> attributes, StunAttributeType type)
        {
            for (var i = 0; i < attributes.Count; i++)
            {
                if (attributes[i].Type == (ushort)type)
                {
                    return attributes[i];
                }
            }

            return null;
        }

        /// <summary>
        /// 权限侧与发送侧共用的序号计数器，用于判定两者先后
        /// </summary>
        private static class Shared
        {
            /// <summary>
            /// 最近一次分配到的序号
            /// </summary>
            public static int Counter;
        }

        /// <summary>
        /// 记录调用次数、地址列表与调用序号的中继权限替身
        /// </summary>
        private sealed class FakePermission : ITurnRelayPermission
        {
            /// <summary>
            /// 被调用次数
            /// </summary>
            public int CallCount { get; private set; }

            /// <summary>
            /// 最近一次传入的地址列表
            /// </summary>
            public IReadOnlyList<IPAddress> Addresses { get; private set; }

            /// <summary>
            /// 调用时分配到的序号
            /// </summary>
            public int Sequence { get; private set; }

            /// <summary>
            /// 记录调用信息并立刻完成，序号与发送侧共用同一计数器
            /// </summary>
            /// <param name="remoteAddresses">对端地址列表</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>已完成的任务</returns>
            public Task CreatePermissionsAsync(IReadOnlyList<IPAddress> remoteAddresses, CancellationToken cancellationToken)
            {
                CallCount++;
                Addresses = remoteAddresses;
                Sequence = Interlocked.Increment(ref Shared.Counter);
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// 只记录发送次数与首个发送序号的 UDP 传输替身，从不收包
        /// </summary>
        private sealed class FakeUdpTransport : IUdpTransport
        {
#pragma warning disable 67
            /// <summary>
            /// 接收事件；StunClient 会订阅并在释放时退订，本替身从不触发
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
            /// 首个数据报分配到的序号
            /// </summary>
            public int FirstSendSequence { get; private set; }

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
            /// 记录发送序号并立刻完成，序号与权限侧共用同一计数器
            /// </summary>
            /// <param name="remoteEndPoint">目标端点</param>
            /// <param name="data">待发送数据</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>已完成的任务</returns>
            public Task SendToAsync(IPEndPoint remoteEndPoint, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
            {
                var sequence = Interlocked.Increment(ref Shared.Counter);
                if (SendCount == 0)
                {
                    FirstSendSequence = sequence;
                }
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
