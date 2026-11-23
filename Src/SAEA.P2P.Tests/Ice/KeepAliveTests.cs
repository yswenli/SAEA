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
*文件名： KeepAliveTests
*版本号： net10.0
*唯一标识：7c1e9a52-6b4d-4f83-a2c5-9e0d3b7a1f64
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 18:00:00
*描述：保活指示与 consent freshness 用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 18:00:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 KeepAliveTests
*
*****************************************************************************/
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Ice;
using SAEA.P2P.Stun;
using SAEA.P2P.Transport;

namespace SAEA.P2P.Tests.Ice
{
    /// <summary>
    /// 保活间隔下限、consent 失效判定、抖动夹取与保活指示不带认证的用例。
    /// </summary>
    [TestCategory(P2PTestSuite.Unit)]
    [TestClass]
    public class KeepAliveTests
    {
        /// <summary>
        /// Tr 低于 15 秒必须被拒绝，等于 15 秒可用
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void KeepAlive_Tr小于15秒_被拒绝()
        {
            // 14.999 秒会与检查阶段抢节奏，构造时就要拦下
            var transport = new FakeUdpTransport();
            var remote = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 6000);

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new KeepAlive(TimeSpan.FromMilliseconds(14999), transport, remote));

            // 边界值 15 秒正好合格，且间隔原样保存
            using (var keepAlive = new KeepAlive(TimeSpan.FromSeconds(15), transport, remote))
            {
                Assert.AreEqual(TimeSpan.FromSeconds(15), keepAlive.Tr);
            }
        }

        /// <summary>
        /// 超过 30 秒没有有效响应即判 consent 失效，且失效事件只触发一次
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void ConsentFreshness_30秒无响应_pair被丢弃()
        {
            // 用可变时钟推进时间，不真等 30 秒
            var t0 = new DateTime(2026, 11, 12, 18, 0, 0, DateTimeKind.Utc);
            var now = t0;
            Func<DateTime> clock = () => now;

            using (var check = new ConnectivityCheck("local", "localpwd"))
            using (var consent = new ConsentFreshness(
                check,
                new FakeUdpTransport(),
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 6000),
                "remote",
                "remotepwd",
                IceRole.Controlling,
                1UL,
                1862270975u,
                () => 1.0,
                clock))
            {
                var expiredCount = 0;
                consent.OnConsentExpired += () => expiredCount++;

                // 29 秒仍在窗口内，不失效也不触发事件
                now = t0.AddSeconds(29);
                Assert.IsFalse(consent.CheckExpiry());
                Assert.IsFalse(consent.IsExpired);
                Assert.AreEqual(0, expiredCount);

                // 31 秒越过 30 秒窗口，判定失效并触发一次事件
                now = t0.AddSeconds(31);
                Assert.IsTrue(consent.CheckExpiry());
                Assert.IsTrue(consent.IsExpired);
                Assert.AreEqual(1, expiredCount);

                // 已失效后重复判定仍返回 true，但事件不得重复触发
                Assert.IsTrue(consent.CheckExpiry());
                Assert.AreEqual(1, expiredCount);
            }
        }

        /// <summary>
        /// 探测间隔按 0.8 到 1.2 的倍率夹取，默认工厂的取值也落在 4 秒到 6 秒
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void ConsentFreshness_抖动在0_8到1_2之间()
        {
            var remote = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 6000);
            var now = new DateTime(2026, 11, 12, 18, 0, 0, DateTimeKind.Utc);
            Func<DateTime> clock = () => now;

            using (var check = new ConnectivityCheck("local", "localpwd"))
            {
                // 工厂给出 0 被夹到下限 0.8，得到 4 秒
                using (var low = CreateConsent(check, remote, () => 0.0, clock))
                {
                    Assert.AreEqual(TimeSpan.FromSeconds(4), low.ComputeInterval());
                }

                // 1.0 保持基准间隔
                using (var middle = CreateConsent(check, remote, () => 1.0, clock))
                {
                    Assert.AreEqual(TimeSpan.FromSeconds(5), middle.ComputeInterval());
                }

                // 工厂给出 2 被夹到上限 1.2，得到 6 秒
                using (var high = CreateConsent(check, remote, () => 2.0, clock))
                {
                    Assert.AreEqual(TimeSpan.FromSeconds(6), high.ComputeInterval());
                }

                // 默认工厂的随机值也必须始终落在夹取后的区间内
                using (var randomized = CreateConsent(check, remote, null, clock))
                {
                    var min = TimeSpan.FromSeconds(4);
                    var max = TimeSpan.FromSeconds(6);

                    for (var i = 0; i < 200; i++)
                    {
                        var interval = randomized.ComputeInterval();
                        Assert.IsTrue(interval >= min && interval <= max, "第 " + i + " 次抖动超出区间：" + interval);
                    }
                }
            }
        }

        /// <summary>
        /// 保活指示是 Binding Indication，不带 MESSAGE-INTEGRITY，并原样发往构造时的远端
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public async Task KeepAlive_Indication_不带认证()
        {
            var transport = new FakeUdpTransport();
            var remote = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 6000);

            using (var keepAlive = new KeepAlive(TimeSpan.FromSeconds(15), transport, remote))
            {
                var bytes = keepAlive.BuildIndication();

                Assert.IsTrue(StunMessage.TryParse(bytes, out var message, out _));
                Assert.AreEqual(StunClass.Indication, message.Class);
                Assert.AreEqual(StunMessageType.Binding, message.Method);

                // 保活指示不认证，不得出现 MESSAGE-INTEGRITY，否则对端会按校验失败丢弃
                Assert.IsFalse(message.TryGet(StunAttributeType.MessageIntegrity, out _));

                await keepAlive.SendOnceAsync(CancellationToken.None);

                Assert.AreEqual(1, transport.SendCount);
                Assert.AreEqual(remote, transport.LastRemoteEndPoint);
            }
        }

        /// <summary>
        /// 构造带指定抖动工厂的 consent 定时器，时钟统一用测试传入的可变时钟
        /// </summary>
        /// <param name="check">连通性检查</param>
        /// <param name="remote">对端端点</param>
        /// <param name="jitterFactory">抖动工厂，null 表示用内部默认工厂</param>
        /// <param name="utcNow">时钟</param>
        /// <returns>consent 定时器</returns>
        private static ConsentFreshness CreateConsent(ConnectivityCheck check, IPEndPoint remote, Func<double> jitterFactory, Func<DateTime> utcNow)
        {
            return new ConsentFreshness(
                check,
                new FakeUdpTransport(),
                remote,
                "remote",
                "remotepwd",
                IceRole.Controlling,
                1UL,
                1862270975u,
                jitterFactory,
                utcNow);
        }

        /// <summary>
        /// 只记录发送次数与目标端点的 UDP 传输替身，从不收包
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
            /// 最近一次发送的目标端点
            /// </summary>
            public IPEndPoint LastRemoteEndPoint { get; private set; }

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
            /// 记录发送次数与目标端点后立刻完成
            /// </summary>
            /// <param name="remoteEndPoint">目标端点</param>
            /// <param name="data">待发送数据</param>
            /// <param name="cancellationToken">取消令牌</param>
            /// <returns>已完成的任务</returns>
            public Task SendToAsync(IPEndPoint remoteEndPoint, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
            {
                SendCount++;
                LastRemoteEndPoint = remoteEndPoint;
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