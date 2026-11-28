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
*命名空间：SAEA.Sockets.Tests
*文件名： ClientSendTeardownTests
*版本号： net10.0
*唯一标识：630db30b-fc16-4906-a3f0-6f8d44d4c11d
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/28 14:08:39
*描述：客户端写方向已关闭时的发送收尾用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/28 14:08:39
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖客户端同步/异步发送收尾分级、Task 失败可观察与断开不重复上报
*
 *****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.Sockets;
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Model;

namespace SAEA.Sockets.Tests
{
    /// <summary>
    /// 客户端发送收尾用例：本端写方向已关闭但连接标志仍为 true 时，发送失败按收尾分级处理，
    /// 断开只通知一次，带 owner 的发送 Task 仍可观察失败。
    /// </summary>
    [TestClass]
    public class ClientSendTeardownTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 记录释放次数的所有权对象，用于确认 owner 恰好释放一次。
        /// </summary>
        private sealed class DisposeCounter : IDisposable
        {
            private int _count;

            /// <summary>
            /// 当前释放次数。
            /// </summary>
            public int Count => Volatile.Read(ref _count);

            /// <summary>
            /// 累加释放次数。
            /// </summary>
            public void Dispose()
            {
                Interlocked.Increment(ref _count);
            }
        }

        /// <summary>
        /// void 异步发送命中收尾类失败时，窗口内不触发 OnError，OnDisconnected 恰好一次。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task SaeaClientSocket_SendAsyncOnShutdownWrite_DisconnectsOnceWithoutError()
        {
            using (var harness = await ClientHarness.ConnectAsync(WaitTimeout).ConfigureAwait(false))
            {
                var errors = new ConcurrentBag<string>();

                var disconnected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                // 只统计发送调用窗口内的上报，避免连接收口后的迟到回调干扰结论
                var collecting = 0;

                var windowDisconnected = 0;

                harness.Client.OnError += (id, ex) =>
                {
                    if (Volatile.Read(ref collecting) == 1)
                        errors.Add((id ?? "") + " | " + ex.GetType().Name + " | " + ex.Message);
                };

                harness.Client.OnDisconnected += (id, ex) =>
                {
                    if (Volatile.Read(ref collecting) == 1)
                        Interlocked.Increment(ref windowDisconnected);
                    disconnected.TrySetResult(true);
                };

                CloseWriteDirection(harness.Client);

                Interlocked.Exchange(ref collecting, 1);

                harness.Client.SendAsync(new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes("client-void-" + Guid.NewGuid().ToString("N"))));

                Interlocked.Exchange(ref collecting, 0);

                await disconnected.Task.WaitAsync(WaitTimeout).ConfigureAwait(false);

                Assert.AreEqual(1, windowDisconnected, "发送失败收尾应在调用窗口内通知一次断开。");
                Assert.AreEqual(0, errors.Count, "收尾类发送失败不应触发 OnError，实际：" + string.Join(" || ", errors));
            }
        }

        /// <summary>
        /// 同步发送命中收尾类失败时，窗口内不触发 OnError，OnDisconnected 恰好一次。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task SaeaClientSocket_SendOnShutdownWrite_DisconnectsOnceWithoutError()
        {
            using (var harness = await ClientHarness.ConnectAsync(WaitTimeout).ConfigureAwait(false))
            {
                var errors = new ConcurrentBag<string>();

                var disconnected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                var collecting = 0;

                var windowDisconnected = 0;

                harness.Client.OnError += (id, ex) =>
                {
                    if (Volatile.Read(ref collecting) == 1)
                        errors.Add((id ?? "") + " | " + ex.GetType().Name + " | " + ex.Message);
                };

                harness.Client.OnDisconnected += (id, ex) =>
                {
                    if (Volatile.Read(ref collecting) == 1)
                        Interlocked.Increment(ref windowDisconnected);
                    disconnected.TrySetResult(true);
                };

                CloseWriteDirection(harness.Client);

                Interlocked.Exchange(ref collecting, 1);

                harness.Client.Send(new ReadOnlySpan<byte>(Encoding.ASCII.GetBytes("client-sync-" + Guid.NewGuid().ToString("N"))));

                Interlocked.Exchange(ref collecting, 0);

                await disconnected.Task.WaitAsync(WaitTimeout).ConfigureAwait(false);

                Assert.AreEqual(1, windowDisconnected, "发送失败收尾应在调用窗口内通知一次断开。");
                Assert.AreEqual(0, errors.Count, "收尾类发送失败不应触发 OnError，实际：" + string.Join(" || ", errors));
            }
        }

        /// <summary>
        /// 带 owner 的发送在写方向关闭时返回的 Task 仍以异常结束，owner 恰好释放一次且不上报 OnError。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task SaeaClientSocket_SendAsyncWithOwnerOnShutdownWrite_FaultsAndDisposesOwnerOnce()
        {
            using (var harness = await ClientHarness.ConnectAsync(WaitTimeout).ConfigureAwait(false))
            {
                var errors = new ConcurrentBag<string>();

                var collecting = 0;

                harness.Client.OnError += (id, ex) =>
                {
                    if (Volatile.Read(ref collecting) == 1)
                        errors.Add((id ?? "") + " | " + ex.GetType().Name + " | " + ex.Message);
                };

                CloseWriteDirection(harness.Client);

                var owner = new DisposeCounter();

                Exception sendError = null;

                Interlocked.Exchange(ref collecting, 1);

                try
                {
                    await harness.Client.SendAsync(new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes("client-owner-" + Guid.NewGuid().ToString("N"))), owner)
                        .WaitAsync(WaitTimeout)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    sendError = ex;
                }
                finally
                {
                    Interlocked.Exchange(ref collecting, 0);
                }

                Assert.IsNotNull(sendError, "写方向关闭后带 owner 的发送应让 Task 以异常结束。");
                Assert.IsFalse(sendError is KernelException, "连接已断开时不应走前置判定抛 KernelException，实际：" + sendError);
                Assert.AreEqual(1, owner.Count, "owner 应恰好释放一次。");
                Assert.AreEqual(0, errors.Count, "收尾类发送失败不应触发 OnError，实际：" + string.Join(" || ", errors));
            }
        }

        /// <summary>
        /// 断开后再次 void 异步发送不抛未处理异常，窗口内不再上报错误、也不重复累加断开通知。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task SaeaClientSocket_SendAsyncAfterDisconnect_DoesNotRepeatNotifications()
        {
            using (var harness = await ClientHarness.ConnectAsync(WaitTimeout).ConfigureAwait(false))
            {
                var errors = new ConcurrentBag<string>();

                var disconnectedCount = 0;

                var collecting = 0;

                harness.Client.OnError += (id, ex) =>
                {
                    if (Volatile.Read(ref collecting) == 1)
                        errors.Add((id ?? "") + " | " + ex.GetType().Name + " | " + ex.Message);
                };

                harness.Client.OnDisconnected += (id, ex) => Interlocked.Increment(ref disconnectedCount);

                CloseWriteDirection(harness.Client);

                // 第一次发送负责完成断开收口
                harness.Client.SendAsync(new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes("client-first-" + Guid.NewGuid().ToString("N"))));

                var afterFirst = Volatile.Read(ref disconnectedCount);

                Assert.AreEqual(1, afterFirst, "首次发送失败应完成一次断开通知。");

                Interlocked.Exchange(ref collecting, 1);

                harness.Client.SendAsync(new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes("client-second-" + Guid.NewGuid().ToString("N"))));

                Interlocked.Exchange(ref collecting, 0);

                Assert.AreEqual(afterFirst, Volatile.Read(ref disconnectedCount), "已断开后再次发送不应重复累加断开通知。");
                Assert.AreEqual(0, errors.Count, "已断开后再次发送不应触发 OnError，实际：" + string.Join(" || ", errors));
            }
        }

        /// <summary>
        /// 关闭本端写方向并校验前置条件：连接标志仍为 true，底层套接字已不可写。
        /// </summary>
        /// <param name="client">已连接的客户端</param>
        private static void CloseWriteDirection(SaeaClientSocket client)
        {
            client.Socket.Shutdown(SocketShutdown.Send);

            Assert.IsTrue(client.Connected, "前置条件：关闭写方向后客户端连接标志应仍为 true。");
            Assert.IsFalse(client.Socket.Connected, "前置条件：关闭写方向后底层套接字应不可写。");
        }

        /// <summary>
        /// 回环假服务端：只接受连接、不读不写，用于验证客户端本端发送失败。
        /// </summary>
        private sealed class ClientHarness : IDisposable
        {
            private ClientHarness(TcpListener listener, TcpClient peer, SaeaClientSocket client)
            {
                Listener = listener;
                Peer = peer;
                Client = client;
            }

            /// <summary>
            /// 监听套接字。
            /// </summary>
            public TcpListener Listener { get; }

            /// <summary>
            /// 服务端侧连接，只持有不放数据。
            /// </summary>
            public TcpClient Peer { get; }

            /// <summary>
            /// 被测客户端。
            /// </summary>
            public SaeaClientSocket Client { get; }

            /// <summary>
            /// 起假服务端并让客户端连接。
            /// </summary>
            /// <param name="timeout">接受连接的超时时间</param>
            /// <returns>已连接并可直接释放的测试夹具</returns>
            public static async Task<ClientHarness> ConnectAsync(TimeSpan timeout)
            {
                var port = GetFreePort();

                var listener = new TcpListener(IPAddress.Loopback, port);

                listener.Start();

                TcpClient peer = null;

                SaeaClientSocket client = null;

                try
                {
                    var option = new SocketOptionBuilder()
                        .SetSocket(SAEASocketType.Tcp)
                        .SetIP("127.0.0.1")
                        .SetPort(port)
                        .SetActionTimeOut(10 * 1000)
                        .SetFreeTime(60 * 1000)
                        .SetConnectTimeout(10 * 1000)
                        .Build();

                    client = new SaeaClientSocket(option);

                    client.Connect();

                    peer = await listener.AcceptTcpClientAsync().WaitAsync(timeout).ConfigureAwait(false);

                    return new ClientHarness(listener, peer, client);
                }
                catch
                {
                    try { client?.Dispose(); } catch { }
                    try { peer?.Dispose(); } catch { }
                    try { listener.Stop(); } catch { }
                    throw;
                }
            }

            /// <summary>
            /// 释放客户端、服务端连接与监听套接字。
            /// </summary>
            public void Dispose()
            {
                try { Client?.Dispose(); } catch { }
                try { Peer?.Dispose(); } catch { }
                try { Listener.Stop(); } catch { }
            }
        }

        /// <summary>取一个当前空闲的回环端口，先占后放，避免与其它用例撞端口。</summary>
        /// <returns>空闲端口号</returns>
        private static int GetFreePort()
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
    }
}