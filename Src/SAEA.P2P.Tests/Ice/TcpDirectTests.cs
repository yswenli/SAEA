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
*文件名： TcpDirectTests
*版本号： net10.0
*唯一标识：80a45cae-b1e8-4394-b018-5a1d8db50804
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 13:33:14
*描述：TCP 直连通道与连接检查用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 13:33:14
*修改人： yswenli
*版本号： net10.0
*描述：新增 TcpDirectTests
*
*****************************************************************************/
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Ice;
using SAEA.P2P.Transport;

namespace SAEA.P2P.Tests.Ice
{
    /// <summary>
    /// TCP 直连通道与连接检查用例。
    /// </summary>
    [TestClass]
    public class TcpDirectTests
    {
        /// <summary>
        /// 主动连接被动监听端口：双方检查都通过，业务帧双向可达
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task TcpDirect_active连接passive_检查通过()
        {
            using var listener = new SaeaTcpTransport(new IPEndPoint(IPAddress.Parse("127.0.0.32"), 0));
            using var passive = new TcpDirectChannel("passiveUfrag", "passivePassword");
            using var active = new TcpDirectChannel("activeUfrag", "activePassword");

            var connectTask = active.ConnectAsync(
                new IPEndPoint(IPAddress.Parse("127.0.0.31"), 0),
                listener.LocalEndPoint,
                "passiveUfrag",
                "passivePassword",
                IceRole.Controlling,
                12345UL,
                IcePriority.ComputeForTcp(CandidateType.Host, 1, TcpCandidateType.Active),
                true,
                CancellationToken.None);

            var accepted = await listener.AcceptAsync(CancellationToken.None);
            var attachTask = passive.AttachAsync(accepted, CancellationToken.None);

            var connectResult = await connectTask;
            var attachResult = await attachTask;

            Assert.IsTrue(
                connectResult.Succeeded,
                "主动侧检查应通过：" + connectResult.Failure + " " + connectResult.ErrorReason);
            Assert.IsTrue(
                attachResult.Succeeded,
                "被动侧检查应通过：" + attachResult.Failure + " " + attachResult.ErrorReason);
            Assert.IsTrue(active.IsConnected, "主动侧应处在数据面可用状态");
            Assert.IsTrue(passive.IsConnected, "被动侧应处在数据面可用状态");
            Assert.AreEqual(listener.LocalEndPoint, connectResult.MappedAddress, "被动侧回的是接受连接的本地端点");

            var passiveReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            passive.FrameReceived += frame => passiveReceived.TrySetResult(frame.ToArray());

            active.Send(new byte[] { 1, 2, 3 });

            var passiveCompleted = await Task.WhenAny(passiveReceived.Task, Task.Delay(5000));
            Assert.AreSame(passiveReceived.Task, passiveCompleted, "被动侧应在 5 秒内收到业务帧");
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, passiveReceived.Task.Result, "被动侧收到的载荷应原样");

            var activeReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            active.FrameReceived += frame => activeReceived.TrySetResult(frame.ToArray());

            passive.Send(new byte[] { 9, 8 });

            var activeCompleted = await Task.WhenAny(activeReceived.Task, Task.Delay(5000));
            Assert.AreSame(activeReceived.Task, activeCompleted, "主动侧应在 5 秒内收到业务帧");
            CollectionAssert.AreEqual(new byte[] { 9, 8 }, activeReceived.Task.Result, "主动侧收到的载荷应原样");
        }

        /// <summary>
        /// 首个响应帧不是 STUN 时立即关闭连接，失败结果带回远端端点供调用方把该 pair 判为 Failed
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task TcpDirect_首响应非STUN_关闭并把pair判失败()
        {
            using var listener = new SaeaTcpTransport(new IPEndPoint(IPAddress.Parse("127.0.0.33"), 0));
            using var active = new TcpDirectChannel("activeUfrag", "activePassword");
            active.CheckTimeoutMilliseconds = 3000;

            var connectTask = active.ConnectAsync(
                new IPEndPoint(IPAddress.Parse("127.0.0.31"), 0),
                listener.LocalEndPoint,
                "passiveUfrag",
                "passivePassword",
                IceRole.Controlling,
                12345UL,
                IcePriority.ComputeForTcp(CandidateType.Host, 1, TcpCandidateType.Active),
                true,
                CancellationToken.None);

            var accepted = await listener.AcceptAsync(CancellationToken.None);
            try
            {
                // 首字节落在业务帧前缀上，主动侧必须立刻判失败，而不是等到检查超时
                var junk = Rfc4571Framer.Encode(new byte[] { 0xC0, 1, 2, 3 });
                accepted.Stream.Write(junk, 0, junk.Length);
                accepted.Stream.Flush();

                var result = await connectTask;

                Assert.IsFalse(result.Succeeded, "非 STUN 首帧不能让检查通过");
                Assert.AreEqual(TcpDirectFailure.NonStunFrame, result.Failure, "失败原因应为首帧不是 STUN 消息");
                Assert.IsFalse(active.IsConnected, "检查失败后不应处在数据面可用状态");
                Assert.AreEqual(
                    listener.LocalEndPoint,
                    result.RemoteEndPoint,
                    "失败结果要带回远端端点，调用方据此把该远端候选的 pair 判 Failed");
            }
            finally
            {
                // 断言之后再断开：先断会让主动侧读到 EOF 而报传输错误，覆盖掉本次要验证的原因
                accepted.Dispose();
            }
        }
    }
}