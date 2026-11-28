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
*文件名： PeerHalfCloseSendTests
*版本号： net10.0
*唯一标识：e9bd2eae-f84a-4b04-ac2f-581a4049590a
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/28 06:39:07
*描述：对端半关写方向或本端发送方向已关闭时的服务端发送用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/28 12:58:24
*修改人： yswenli
*版本号： net10.0
*描述：发送窗口内收集全部上报并注明本用例不覆盖同步分级分支
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
    /// 半关用例：对端发完请求即 FIN 后本端仍应发出响应；发送方向已关闭时按预期收尾处理，不上报错误。
    /// </summary>
    [TestClass]
    public class PeerHalfCloseSendTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 客户端发完请求就半关写方向，服务端读到 EOF 后仍应把响应完整发出且不报异常。
        /// <para>
        /// Windows 上对端 FIN 不会把 <see cref="Socket.Connected"/> 置为 false，本用例只锁定
        /// 「半关后仍能完整发出响应」的行为，不作为本次改动的判别依据。
        /// </para>
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task SaeaServerSocket_PeerHalfClosesWriteDirection_StillSendsResponse()
        {
            var port = GetFreePort();

            var payload = Encoding.ASCII.GetBytes("half-close-response-" + Guid.NewGuid().ToString("N"));

            // null 表示发送成功；非空表示发送路径抛出的异常
            var sendResult = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

            IServerSocket server = null;

            try
            {
                var option = new SocketOptionBuilder()
                    .SetSocket(SAEASocketType.Tcp)
                    .UseStream()
                    .UseConnectionOpened(info => { })
                    .UseInputHandler(async (userToken, reader, flow, token) =>
                    {
                        // 读到 EOF 表示对端已发出 FIN：本端写方向仍可用，此时才发送响应
                        while (true)
                        {
                            var result = await reader.ReadAsync(token).ConfigureAwait(false);

                            reader.AdvanceTo(result.Buffer.End);

                            if (result.IsCompleted)
                                break;
                        }

                        try
                        {
                            await server.SendAsync(userToken.ID, new ReadOnlyMemory<byte>(payload), null).ConfigureAwait(false);

                            sendResult.TrySetResult(null);
                        }
                        catch (Exception ex)
                        {
                            sendResult.TrySetResult(ex);
                        }
                    })
                    .SetIP("127.0.0.1")
                    .SetPort(port)
                    .SetActionTimeOut(10 * 1000)
                    .SetFreeTime(60 * 1000)
                    .Build();

                server = SocketFactory.CreateServerSocket(option);

                server.Start();

                using (var client = new TcpClient())
                {
                    await client.ConnectAsync(IPAddress.Loopback, port).WaitAsync(WaitTimeout).ConfigureAwait(false);

                    using (var stream = client.GetStream())
                    {
                        var request = Encoding.ASCII.GetBytes("half-close-request");

                        await stream.WriteAsync(request, 0, request.Length).WaitAsync(WaitTimeout).ConfigureAwait(false);

                        await stream.FlushAsync().WaitAsync(WaitTimeout).ConfigureAwait(false);

                        // 请求写完后立刻半关：服务端随后看到的 0 字节读结果会把它当成断开
                        client.Client.Shutdown(SocketShutdown.Send);

                        var received = await ReadToEofAsync(stream).ConfigureAwait(false);

                        CollectionAssert.AreEqual(payload, received, "半关后服务端仍应把响应完整发出。");

                        var error = await sendResult.Task.WaitAsync(WaitTimeout).ConfigureAwait(false);

                        Assert.IsNull(error, "半关后的发送不应失败：" + (error == null ? string.Empty : error.ToString()));
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

        /// <summary>
        /// 本端发送方向已关闭（Connected 为 false）时，发送应以异常结束，但属于预期收尾，不得触发 OnError。
        /// <para>
        /// 本用例锁定的是「发送方向关闭时异步完成路径不上报」的行为，不覆盖
        /// <c>IsExpectedSendTeardown</c> 的同步分级分支（后者由 SendTeardownClassificationTests 覆盖）。
        /// </para>
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task SaeaServerSocket_SendDirectionClosed_FaultsWithoutErrorReport()
        {
            var port = GetFreePort();

            var payload = Encoding.ASCII.GetBytes("closed-direction-" + Guid.NewGuid().ToString("N"));

            var opened = new TaskCompletionSource<IUserToken>(TaskCreationOptions.RunContinuationsAsynchronously);

            // 发送窗口内收集全部上报，不做文案过滤
            var sendErrors = new ConcurrentBag<string>();

            // 只在发送调用前后限窗，避免连接收口阶段的上报干扰结论
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
                        // 本用例只验证发送路径，接收方向保持挂起直到连接被回收
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
                        sendErrors.Add((id ?? "") + " | " + ex.GetType().Name + " | " + ex.Message);
                };

                server.Start();

                using (var client = new TcpClient())
                {
                    await client.ConnectAsync(IPAddress.Loopback, port).WaitAsync(WaitTimeout).ConfigureAwait(false);

                    var userToken = await opened.Task.WaitAsync(WaitTimeout).ConfigureAwait(false);

                    // 关闭本端发送方向：Connected 立刻变 false，发送只可能在底层失败，
                    // 正好覆盖「不再按 Connected 预判拒发」的分支
                    userToken.Socket.Shutdown(SocketShutdown.Send);

                    Assert.IsFalse(userToken.Socket.Connected, "前置条件：发送方向关闭后 Connected 应为 false。");

                    Interlocked.Exchange(ref collecting, 1);

                    Exception sendError = null;

                    try
                    {
                        await server.SendAsync(userToken.ID, new ReadOnlyMemory<byte>(payload), null).WaitAsync(WaitTimeout).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        sendError = ex;
                    }

                    Interlocked.Exchange(ref collecting, 0);

                    Assert.IsNotNull(sendError, "发送方向关闭后发送应以异常结束。");

                    Assert.AreEqual(0, sendErrors.Count, "连接收尾类发送失败不应触发 OnError 上报，实际：" + string.Join(" || ", sendErrors));
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
                        throw new TimeoutException("等待服务端响应超时。");

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
