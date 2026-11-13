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
*命名空间：SAEA.P2P.Tests.Core
*文件名： P2PClientApiTests
*版本号： net10.0
*唯一标识：e942fdf2-1750-4556-91a6-4722c4bd3a67
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 11:19:45
*描述：客户端信令与 ICE 编排用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 11:19:45
*修改人： yswenli
*版本号： net10.0
*描述：客户端信令与 ICE 编排用例
*
*****************************************************************************/
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Builder;
using SAEA.P2P.Channel;
using SAEA.P2P.Common;
using SAEA.P2P.Core;
using SAEA.P2P.Ice;
using SAEA.P2P.Signaling;

namespace SAEA.P2P.Tests.Core
{
    /// <summary>
    /// P2P 客户端：未注册时的连接与发送校验，以及两端经信令交换参数后在同一回环地址上选出 UDP 直连通道、
    /// 断开时由对端收到会话关闭通知的端到端用例。
    /// 需要服务端的用例都在本机回环上起真实信令服务端，不依赖任何外部服务。
    /// </summary>
    [TestClass]
    public class P2PClientApiTests
    {
        /// <summary>
        /// 发起方节点标识
        /// </summary>
        private const string PeerA = "client-a";

        /// <summary>
        /// 应答方节点标识
        /// </summary>
        private const string PeerB = "client-b";

        /// <summary>
        /// 等待事件通知的默认预算
        /// </summary>
        private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 未注册即发起连接：应立即以 NotConnected 失败，而不是等到会话总预算耗尽
        /// </summary>
        [TestMethod]
        [Timeout(15000)]
        public async Task ConnectToPeerAsync_未注册_抛NotConnected()
        {
            // 端口只预留不启动服务端，构造出的客户端始终处于未注册状态
            using (var client = new P2PClient(CreateSignaling(PeerA, ReserveTcpPort()), CreateIceOptions()))
            {
                try
                {
                    await client.ConnectToPeerAsync(PeerB).ConfigureAwait(false);
                    Assert.Fail("未注册时应拒绝发起连接");
                }
                catch (P2PException ex)
                {
                    Assert.AreEqual(ErrorCode.NotConnected, ex.ErrorCode, "未注册发起连接的错误码应为 EI03");
                }
            }
        }

        /// <summary>
        /// 向未知对端发送：应直接以 NotConnected 失败，不能静默丢弃
        /// </summary>
        [TestMethod]
        [Timeout(15000)]
        public void Send_未知对端_抛NotConnected()
        {
            // 没有任何已建立的会话，发送必须在本地入口处被拦下
            using (var client = new P2PClient(CreateSignaling(PeerA, ReserveTcpPort()), CreateIceOptions()))
            {
                try
                {
                    client.Send("peer-x", new byte[] { 0xC0, 0x01 });
                    Assert.Fail("向未知对端发送应抛异常");
                }
                catch (P2PException ex)
                {
                    Assert.AreEqual(ErrorCode.NotConnected, ex.ErrorCode, "向未知对端发送的错误码应为 EI03");
                }
            }
        }

        /// <summary>
        /// 双方直连：两端注册到同一信令服务端后，发起方经信令交换 ICE 参数并选出 UDP 直连，
        /// 两端都应收到 OnPeerConnected；发起方断开后应答方应收到带原因的 OnPeerDisconnected
        /// </summary>
        [TestMethod]
        [Timeout(120000)]
        public async Task ConnectToPeerAsync_双方直连_两端建立UdpDirect并通知断开()
        {
            // 服务端与客户端都绑回环，避免依赖本机实际网卡与外网环境
            var port = ReserveTcpPort();
            P2PServer server = null;
            P2PClient clientA = null;
            P2PClient clientB = null;

            try
            {
                server = new P2PServer(CreateServerOptions(port));
                server.Start();

                clientA = new P2PClient(CreateSignaling(PeerA, port), CreateIceOptions());
                clientB = new P2PClient(CreateSignaling(PeerB, port), CreateIceOptions());

                // 两端都可能在发起方任务返回前就触发事件，用异步续延避免回调重入选路线程
                var aConnected = new TaskCompletionSource<P2PConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
                var bConnected = new TaskCompletionSource<P2PConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
                clientA.OnPeerConnected += connection => aConnected.TrySetResult(connection);
                clientB.OnPeerConnected += connection => bConnected.TrySetResult(connection);

                await clientA.RegisterAsync().ConfigureAwait(false);
                await clientB.RegisterAsync().ConfigureAwait(false);
                Assert.IsTrue(clientA.IsRegistered, "client-a 注册成功后 IsRegistered 应为 true");
                Assert.IsTrue(clientB.IsRegistered, "client-b 注册成功后 IsRegistered 应为 true");

                var connectTask = clientA.ConnectToPeerAsync(PeerB);
                var completed = await Task.WhenAny(connectTask, Task.Delay(30000)).ConfigureAwait(false);
                if (completed != connectTask)
                {
                    Assert.Fail("等待客户端连接超时（30s 内未完成与 " + PeerB + " 的选路）");
                }

                var connection = await connectTask.ConfigureAwait(false);
                var bConnection = await WaitAsync(bConnected.Task, EventTimeout).ConfigureAwait(false);
                // 发起方的事件可能早于 ConnectToPeerAsync 返回，单独等一次事件以确认通知确实到达
                await WaitAsync(aConnected.Task, EventTimeout).ConfigureAwait(false);

                Assert.AreEqual(PeerB, connection.PeerId, "发起方连接描述的对端标识不一致");
                Assert.AreEqual(ChannelType.UdpDirect, connection.Channel, "发起方应选出 UDP 直连通道");
                Assert.IsNotNull(connection.LocalEndPoint, "发起方连接的本地端点不应为空");
                Assert.IsNotNull(connection.RemoteEndPoint, "发起方连接的远端端点不应为空");
                Assert.IsTrue(IPAddress.IsLoopback(connection.LocalEndPoint.Address), "发起方本地端点应落在回环地址上");
                Assert.IsTrue(IPAddress.IsLoopback(connection.RemoteEndPoint.Address), "发起方远端端点应落在回环地址上");

                Assert.AreEqual(PeerA, bConnection.PeerId, "应答方连接描述的对端标识不一致");
                Assert.AreEqual(ChannelType.UdpDirect, bConnection.Channel, "应答方应选出 UDP 直连通道");
                Assert.IsNotNull(bConnection.LocalEndPoint, "应答方连接的本地端点不应为空");
                Assert.IsNotNull(bConnection.RemoteEndPoint, "应答方连接的远端端点不应为空");
                Assert.IsTrue(IPAddress.IsLoopback(bConnection.LocalEndPoint.Address), "应答方本地端点应落在回环地址上");
                Assert.IsTrue(IPAddress.IsLoopback(bConnection.RemoteEndPoint.Address), "应答方远端端点应落在回环地址上");

                // 必须在断开前挂上通知，否则可能错过会话关闭报文
                var bDisconnected = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                clientB.OnPeerDisconnected += (peerId, reason) => bDisconnected.TrySetResult(peerId + "|" + reason);

                await clientA.DisconnectPeerAsync(PeerB).ConfigureAwait(false);
                var disconnected = await WaitAsync(bDisconnected.Task, EventTimeout).ConfigureAwait(false);
                var separator = disconnected.IndexOf('|');
                Assert.AreEqual(PeerA, disconnected.Substring(0, separator), "应答方收到的断开通知应对应发起方");
                Assert.IsFalse(string.IsNullOrEmpty(disconnected.Substring(separator + 1)), "应答方收到的断开原因不应为空");
            }
            finally
            {
                if (clientA != null)
                {
                    clientA.Dispose();
                }

                if (clientB != null)
                {
                    clientB.Dispose();
                }

                if (server != null)
                {
                    server.Stop();
                }
            }
        }

        /// <summary>
        /// 构造只监听回环的信令服务端配置，日志压到错误级并关闭中继，避免用例输出与无关模块干扰
        /// </summary>
        /// <param name="port">预占的监听端口</param>
        /// <returns>服务端配置</returns>
        private static P2PServerOptions CreateServerOptions(int port)
        {
            var options = new P2PServerOptions
            {
                BindIP = "127.0.0.1",
                Port = port,
                SharedSecret = null,
                RejectDuplicatePeer = false
            };
            options.Relay.Enabled = false;
            options.Logging.Level = 4;
            return options;
        }

        /// <summary>
        /// 构造指向本机信令服务端的客户端配置，心跳间隔放宽避免用例期间出现无关心跳
        /// </summary>
        /// <param name="peerId">本端节点标识</param>
        /// <param name="port">信令服务端端口</param>
        /// <returns>信令配置</returns>
        private static SignalingOptions CreateSignaling(string peerId, int port)
        {
            return new SignalingOptions
            {
                ServerAddress = "127.0.0.1",
                ServerPort = port,
                PeerId = peerId,
                NodeName = peerId,
                ConnectTimeoutMs = 5000,
                RegisterTimeoutMs = 5000,
                HeartbeatIntervalMs = 30000
            };
        }

        /// <summary>
        /// 构造只收集回环 host 候选的 ICE 配置，直连预算收窄以缩短失败路径的等待时间
        /// </summary>
        /// <returns>ICE 会话配置</returns>
        private static IceSessionOptions CreateIceOptions()
        {
            var ice = new IceOptions();
            ice.AllowLoopbackCandidates = true;
            ice.BindAddresses.Add(IPAddress.Loopback);
            ice.GatheringTimeout = TimeSpan.FromSeconds(2);

            var options = new IceSessionOptions();
            options.Ice = ice;
            options.GatheringBudget = TimeSpan.FromSeconds(1);
            options.UdpDirectBudget = TimeSpan.FromSeconds(5);
            options.TcpDirectBudget = TimeSpan.FromSeconds(2);
            options.TurnStageBudget = TimeSpan.FromSeconds(1);
            options.SessionBudget = TimeSpan.FromSeconds(30);
            return options;
        }

        /// <summary>
        /// 预占一个尚未使用的 TCP 端口：先监听取到系统分配端口，再立即停止交给被测服务端
        /// </summary>
        /// <returns>端口号</returns>
        private static int ReserveTcpPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        /// <summary>
        /// 在预算内等待任务完成，超时抛 TimeoutException 而不是让用例一直挂到 [Timeout] 上限
        /// </summary>
        /// <typeparam name="T">任务结果类型</typeparam>
        /// <param name="task">被等待的任务</param>
        /// <param name="timeout">等待预算</param>
        /// <returns>任务结果</returns>
        private static async Task<T> WaitAsync<T>(Task<T> task, TimeSpan timeout)
        {
            var completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
            if (completed != task)
            {
                throw new TimeoutException("等待任务超时：" + (long)timeout.TotalMilliseconds + " ms");
            }
            return await task.ConfigureAwait(false);
        }

        /// <summary>
        /// 在预算内等待无结果的任务完成，超时抛 TimeoutException
        /// </summary>
        /// <param name="task">被等待的任务</param>
        /// <param name="timeout">等待预算</param>
        private static async Task WaitAsync(Task task, TimeSpan timeout)
        {
            var completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
            if (completed != task)
            {
                throw new TimeoutException("等待任务超时：" + (long)timeout.TotalMilliseconds + " ms");
            }
            await task.ConfigureAwait(false);
        }
    }
}