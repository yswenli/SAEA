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
*文件名： RouteLadderTests
*版本号： net10.0
*唯一标识：fed33b20-4c66-4414-89da-22520499f994
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 14:24:38
*描述：选路阶梯逐级执行与预算用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 14:24:38
*修改人： yswenli
*版本号： net10.0
*描述：新增 RouteLadderTests
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Common;
using SAEA.P2P.Ice;

namespace SAEA.P2P.Tests.Ice
{
    /// <summary>
    /// 选路阶梯逐级执行与预算用例。
    /// </summary>
    [TestClass]
    public class RouteLadderTests
    {
        /// <summary>
        /// 首级打通即停，不再进入后续阶段
        /// </summary>
        [TestMethod]
        [Timeout(10000)]
        public async Task RouteLadder_UDP直连成功_不进入TCP()
        {
            var options = CreateOptions(
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(10));
            var ladder = new RouteLadder(options);
            var stages = new List<IceStage>();

            IceStage result = await ladder.RunAsync(
                (stage, budget, token) =>
                {
                    stages.Add(stage);
                    return Task.FromResult<string>(null);
                },
                null,
                CancellationToken.None);

            Assert.AreEqual(IceStage.UdpDirect, result, "首级打通应返回 UDP 直连");
            CollectionAssert.AreEqual(
                new[] { IceStage.UdpDirect },
                stages,
                "成功即停，不应继续尝试后续阶段");
        }

        /// <summary>
        /// UDP 全失败后才在进入 TCP 阶段前补收集，并在 TCP 打通后停止
        /// </summary>
        [TestMethod]
        [Timeout(10000)]
        public async Task RouteLadder_UDP全失败_补收集TCP与relay()
        {
            var options = CreateOptions(
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(10));
            var ladder = new RouteLadder(options);
            var events = new List<string>();

            IceStage result = await ladder.RunAsync(
                (stage, budget, token) =>
                {
                    events.Add("attempt:" + stage);
                    if (stage == IceStage.UdpDirect)
                    {
                        return Task.FromResult("udp 未打通");
                    }
                    if (stage == IceStage.TcpDirect)
                    {
                        return Task.FromResult<string>(null);
                    }
                    return Task.FromResult("不应进入");
                },
                (stage, token) =>
                {
                    events.Add("start:" + stage);
                    return Task.CompletedTask;
                },
                CancellationToken.None);

            Assert.AreEqual(IceStage.TcpDirect, result, "UDP 失败后应在 TCP 直连打通");
            // 每级尝试前都会回调 stageStarting，因此第一级也有 start 事件；关键是 TCP 的补收集发生在 UDP 失败之后
            CollectionAssert.AreEqual(
                new[]
                {
                    "start:UdpDirect",
                    "attempt:UdpDirect",
                    "start:TcpDirect",
                    "attempt:TcpDirect"
                },
                events,
                "应为「UDP 尝试失败 → 进入 TCP 前补收集 → TCP 尝试成功」");
        }

        /// <summary>
        /// 三个 TURN 阶段按 UDP、TCP、TLS 顺序回退，最后一级才打通
        /// </summary>
        [TestMethod]
        [Timeout(10000)]
        public async Task RouteLadder_TURN三级_按UDP到TLS顺序()
        {
            var options = CreateOptions(
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(10));
            var ladder = new RouteLadder(options);
            var stages = new List<IceStage>();

            IceStage result = await ladder.RunAsync(
                (stage, budget, token) =>
                {
                    stages.Add(stage);
                    if (stage == IceStage.TurnTls)
                    {
                        return Task.FromResult<string>(null);
                    }
                    return Task.FromResult("未打通");
                },
                null,
                CancellationToken.None);

            Assert.AreEqual(IceStage.TurnTls, result, "应回退到 TURN over TLS");
            CollectionAssert.AreEqual(
                new[]
                {
                    IceStage.UdpDirect,
                    IceStage.TcpDirect,
                    IceStage.TurnUdp,
                    IceStage.TurnTcp,
                    IceStage.TurnTls
                },
                stages,
                "应按 UDP 直连 → TCP 直连 → TURN over UDP/TCP/TLS 顺序逐级尝试");
        }

        /// <summary>
        /// 阶段实现不响应取消令牌时，总预算兜底仍能结束并抛出带阶段信息的超时错误
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task RouteLadder_总预算超时_抛出带阶段信息的错误()
        {
            var options = CreateOptions(
                TimeSpan.FromMilliseconds(30),
                TimeSpan.FromMilliseconds(30),
                TimeSpan.FromMilliseconds(30),
                TimeSpan.FromMilliseconds(200));
            var ladder = new RouteLadder(options);

            var ex = await Assert.ThrowsExceptionAsync<P2PException>(() => ladder.RunAsync(
                (stage, budget, token) => new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously).Task,
                null,
                CancellationToken.None));

            Assert.AreEqual(ErrorCode.IceTimeout, ex.ErrorCode, "总预算耗尽应抛 IceTimeout");
            StringAssert.Contains(ex.Message, "UDP 直连", "错误消息要指出停在哪一级");
        }

        /// <summary>
        /// 构造预算可控的会话配置，收集预算置零以便只验证阶梯本身
        /// </summary>
        /// <param name="udp">UDP 直连阶段预算</param>
        /// <param name="tcp">TCP 直连阶段预算</param>
        /// <param name="turn">单个 TURN 阶段预算</param>
        /// <param name="session">会话总预算</param>
        /// <returns>满足校验的会话配置</returns>
        private static IceSessionOptions CreateOptions(TimeSpan udp, TimeSpan tcp, TimeSpan turn, TimeSpan session)
        {
            var options = new IceSessionOptions();
            options.GatheringBudget = TimeSpan.Zero;
            options.UdpDirectBudget = udp;
            options.TcpDirectBudget = tcp;
            options.TurnStageBudget = turn;
            options.SessionBudget = session;
            return options;
        }
    }
}