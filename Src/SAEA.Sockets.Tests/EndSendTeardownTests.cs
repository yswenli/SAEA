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
*文件名： EndSendTeardownTests
*版本号： net10.0
*唯一标识：b057b1ed-c03f-4ae7-b0a4-0b8d099763ba
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/28 14:01:49
*描述：连接写方向已关闭时的 End 收尾用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/28 14:01:49
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖 End 跳过发送、窗口内零 OnError 并完成断开收口
*
 *****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.Sockets;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

namespace SAEA.Sockets.Tests
{
    /// <summary>
    /// 收尾发送用例：连接写方向不可写时 End 跳过发送、不上报错误，并完成断开收口。
    /// </summary>
    [TestClass]
    public class EndSendTeardownTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 本端写方向已关闭（Connected 为 false）时，End 应跳过发送、窗口内不触发 OnError，
        /// 并照常触发 OnDisconnected；客户端读不到任何收尾数据。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task SaeaServerSocket_EndOnClosedWriteDirection_SkipsSendWithoutErrorReport()
        {
            var port = GetFreePort();

            var payload = Encoding.ASCII.GetBytes("end-skipped-" + Guid.NewGuid().ToString("N"));

            var opened = new TaskCompletionSource<IUserToken>(TaskCreationOptions.RunContinuationsAsynchronously);

            var disconnected = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            // 只统计 End 调用窗口内的上报，避免连接收口阶段的其它上报干扰结论
            var errors = new ConcurrentBag<string>();

            var collecting = 0;

            IServerSocket server = null;

            try
            {
                var option = new SocketOptionBuilder()
                    .SetSocket(SAEASocketType.Tcp)
                    .UseStream()
                    .UseConnectionOpened(info => opened.TrySetResult(info.UserToken))
                    .UseInputHandler(async (userToken, reader, flow, token) =>
                    {
                        // 本用例只验证收尾发送，接收方向保持挂起直到连接被回收
                        try
                        {
                            await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                        }
                    })
                    .SetIP("127.0.0.1")
                    .SetPort(port)
                    .SetActionTimeOut(10 * 1000)
                    .SetFreeTime(60 * 1000)
                    .Build();

                server = SocketFactory.CreateServerSocket(option);

                server.OnError += (id, ex) =>
                {
                    if (Volatile.Read(ref collecting) == 1)
                        errors.Add((id ?? "") + " | " + ex.GetType().Name + " | " + ex.Message);
                };

                server.OnDisconnected += (id, ex) => disconnected.TrySetResult(id);

                server.Start();

                using (var client = new TcpClient())
                {
                    await client.ConnectAsync(IPAddress.Loopback, port).WaitAsync(WaitTimeout).ConfigureAwait(false);

                    var userToken = await opened.Task.WaitAsync(WaitTimeout).ConfigureAwait(false);

                    var sessionId = userToken.ID;

                    // 关闭本端写方向后 Connected 立刻为 false，End 会走「跳过发送」分支
                    userToken.Socket.Shutdown(SocketShutdown.Send);

                    Assert.IsFalse(userToken.Socket.Connected, "前置条件：写方向关闭后 Connected 应为 false。");

                    Interlocked.Exchange(ref collecting, 1);

                    server.End(sessionId, new ReadOnlyMemory<byte>(payload));

                    Interlocked.Exchange(ref collecting, 0);

                    Assert.AreEqual(sessionId, await disconnected.Task.WaitAsync(WaitTimeout).ConfigureAwait(false), "End 应完成断开收口。");

                    Assert.AreEqual(0, errors.Count, "连接已不可写时 End 不应触发 OnError，实际：" + string.Join(" || ", errors));

                    using (var stream = client.GetStream())
                    {
                        var received = await ReadToEofAsync(stream).ConfigureAwait(false);

                        Assert.AreEqual(0, received.Length, "跳过发送后客户端不应收到收尾数据。");
                    }
                }
            }
            finally
            {
                try
                {
                    if (server != null)
                        server.Stop();
                }
                catch (Exception)
                {
                }
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

        /// <summary>读到对端关闭为止，带总截止时间避免用例挂死。</summary>
        /// <param name="stream">连接字节流</param>
        /// <returns>读到的全部字节</returns>
        private static async Task<byte[]> ReadToEofAsync(NetworkStream stream)
        {
            var deadline = DateTime.UtcNow + WaitTimeout * 3;

            var buffer = new byte[1024];

            using (var received = new MemoryStream())
            {
                while (true)
                {
                    if (DateTime.UtcNow >= deadline)
                        throw new TimeoutException("等待服务端收尾超时。");

                    var read = await stream.ReadAsync(buffer, 0, buffer.Length).WaitAsync(WaitTimeout).ConfigureAwait(false);

                    if (read == 0)
                        break;

                    received.Write(buffer, 0, read);
                }

                return received.ToArray();
            }
        }
    }
}
