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
*命名空间：SAEA.P2P.Tests.TurnServer
*文件名： TurnAllocationTests
*版本号： net10.0
*唯一标识：e8ccf70b-7442-4387-a428-4f350e0422a1
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 01:10:00
*描述：TURN 分配、权限、通道与端口分配器计时验收
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 01:10:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 TURN 分配与计时用例
*
*****************************************************************************/
using System;
using System.Net;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.TurnServer;

using TurnServerHost = SAEA.P2P.TurnServer.TurnServer;

namespace SAEA.P2P.Tests.TurnServer
{
    /// <summary>
    /// TURN 分配模型：权限按 IP 放行、三层计时互不影响、通道过期后的隔离期、分配表与端口分配器边界。
    /// </summary>
    [TestCategory(P2PTestSuite.Unit)]
    [TestClass]
    public class TurnAllocationTests
    {
        /// <summary>
        /// 用例统一从该时刻起算
        /// </summary>
        private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 11, 12, 0, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// 权限只按 IP 标识，同 IP 的其它端口必须放行
        /// </summary>
        [TestMethod]
        public void TurnAllocation_权限按IP放行_端口无关()
        {
            using (var allocation = CreateAllocation())
            {
                allocation.AddPermission(new IPEndPoint(IPAddress.Parse("10.0.0.7"), 40000), Start);

                Assert.IsTrue(allocation.IsPermitted(new IPEndPoint(IPAddress.Parse("10.0.0.7"), 50000), Start), "同 IP 的其它端口应放行");
                Assert.IsFalse(allocation.IsPermitted(new IPEndPoint(IPAddress.Parse("10.0.0.8"), 40000), Start), "不同 IP 不得放行");
            }
        }

        /// <summary>
        /// 权限固定 300 秒，刷新分配不会顺带延长权限
        /// </summary>
        [TestMethod]
        public void TurnAllocation_权限固定300秒_分配刷新不影响()
        {
            using (var allocation = CreateAllocation())
            {
                var peer = new IPEndPoint(IPAddress.Parse("10.0.0.7"), 40000);
                allocation.AddPermission(peer, Start);

                allocation.Refresh(3600, Start.AddSeconds(299));

                Assert.IsFalse(allocation.IsPermitted(peer, Start.AddSeconds(301)), "权限应在 300 秒后失效，与分配生命周期无关");
                Assert.IsFalse(allocation.IsExpired(Start.AddSeconds(301)), "分配本身仍应在有效期内");
            }
        }

        /// <summary>
        /// 通道过期后同一通道号在 300 秒隔离期内不得改绑到其它对端
        /// </summary>
        [TestMethod]
        public void TurnAllocation_通道过期后_300秒内禁止改绑()
        {
            const ushort channelNumber = 0x4001;
            var peerA = new IPEndPoint(IPAddress.Parse("10.0.0.7"), 40000);
            var peerB = new IPEndPoint(IPAddress.Parse("10.0.0.9"), 40001);

            using (var allocation = CreateAllocation())
            {
                Assert.IsTrue(allocation.BindChannel(channelNumber, peerA, Start), "首次绑定应成功");

                // 通道自然过期，隔离期从过期时刻起算 300 秒
                Assert.IsFalse(allocation.BindChannel(channelNumber, peerB, Start.AddSeconds(601)), "隔离期内改绑应失败");
                Assert.IsTrue(allocation.BindChannel(channelNumber, peerB, Start.AddSeconds(901)), "隔离期结束后应允许改绑");
            }
        }

        /// <summary>
        /// 通道绑定同时创建对应权限，不要求先发 CreatePermission
        /// </summary>
        [TestMethod]
        public void TurnAllocation_绑定通道_同时创建权限()
        {
            const ushort channelNumber = 0x4002;
            var peer = new IPEndPoint(IPAddress.Parse("10.0.0.7"), 40000);

            using (var allocation = CreateAllocation())
            {
                Assert.IsFalse(allocation.IsPermitted(peer, Start), "绑定前不应有权限");
                Assert.IsTrue(allocation.BindChannel(channelNumber, peer, Start), "绑定应成功");
                Assert.IsTrue(allocation.IsPermitted(new IPEndPoint(peer.Address, 40099), Start), "绑定应同时创建按 IP 生效的权限");
            }
        }

        /// <summary>
        /// 同一隔离键重复加入分配表应返回 false，避免覆盖已有分配
        /// </summary>
        [TestMethod]
        public void TurnAllocationTable_同键重复添加_返回false()
        {
            var table = new TurnAllocationTable();
            using (var first = CreateAllocation())
            using (var second = CreateAllocation())
            {
                Assert.IsTrue(table.TryAdd(first), "首次加入应成功");
                Assert.IsFalse(table.TryAdd(second), "同一隔离键重复加入应失败");
                Assert.AreEqual(1, table.Count, "分配表数量不应变化");
            }
        }

        /// <summary>
        /// 端口区间耗尽后分配应失败，归还后可再次分出
        /// </summary>
        [TestMethod]
        public void RelayPortAllocator_端口耗尽_返回false()
        {
            var allocator = new RelayPortAllocator(50000, 50001, new Random(7));

            int first;
            int second;
            int third;
            Assert.IsTrue(allocator.TryAllocate(out first), "第一个端口应分出");
            Assert.IsTrue(allocator.TryAllocate(out second), "第二个端口应分出");
            Assert.IsFalse(allocator.TryAllocate(out third), "区间耗尽后应失败");
            Assert.AreNotEqual(first, second, "两次分出不得相同");

            allocator.Release(first);
            Assert.IsTrue(allocator.TryAllocate(out third), "归还后应能再分出");
            Assert.AreEqual(first, third, "归还的端口应被重新使用");
        }

        /// <summary>
        /// 创建一个 UDP 分配
        /// </summary>
        /// <returns>分配</returns>
        private static TurnAllocation CreateAllocation()
        {
            return new TurnAllocation(
                new IPEndPoint(IPAddress.Parse("192.0.2.10"), 51000),
                new IPEndPoint(IPAddress.Parse("192.0.2.1"), 3478),
                TurnServerHost.UdpTransport,
                "learner",
                new IPEndPoint(IPAddress.Parse("192.0.2.1"), 50000),
                600,
                null,
                Start);
        }
    }
}