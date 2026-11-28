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
*文件名： SendTeardownClassificationTests
*版本号： net10.0
*唯一标识：399f0cca-c1e6-4b6f-bee9-4e8c8ca6e718
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/28 12:58:24
*描述：令牌套接字已释放时的同步发送分级与上报用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/28 12:58:24
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖同步 ObjectDisposedException 落入 IsExpectedSendTeardown 分级分支
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
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

namespace SAEA.Sockets.Tests
{
    /// <summary>
    /// 同步发送命中收尾分级分支的用例：令牌套接字已释放时，发送以异常结束但不按错误级别上报。
    /// </summary>
    [TestClass]
    public class SendTeardownClassificationTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 令牌套接字已释放（仍可解引用）时，同步发送应以 ObjectDisposedException 结束，
        /// 证明命中的是 IsExpectedSendTeardown 的分级分支而非 Socket == null 早退分支；且发送窗口内不上报错误。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task SaeaServerSocket_SendOnDisposedSocket_FaultsWithoutErrorReport()
        {
            var port = GetFreePort();

            var payload = Encoding.ASCII.GetBytes("disposed-socket-send-" + Guid.NewGuid().ToString("N"));

            var opened = new TaskCompletionSource<IUserToken>(TaskCreationOptions.RunContinuationsAsynchronously);

            // 记录发送窗口内的全部上报，用于确认收尾类失败没有按错误级别外泄
            var errors = new ConcurrentBag<string>();

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
                    errors.Add((id ?? "") + " | " + ex.GetType().Name + " | " + ex.Message);
                };

                server.Start();

                using (var client = new TcpClient())
                {
                    await client.ConnectAsync(IPAddress.Loopback, port).WaitAsync(WaitTimeout).ConfigureAwait(false);

                    var userToken = await opened.Task.WaitAsync(WaitTimeout).ConfigureAwait(false);

                    // 只释放套接字，不调用 Disconnect：令牌的 Socket 引用在发送瞬间仍可解引用，
                    // 从而绕过 Socket == null 早退分支，落到同步发送抛异常的分级分支
                    userToken.Socket.Dispose();

                    Exception sendError = null;

                    try
                    {
                        await server.SendAsync(userToken.ID, new ReadOnlyMemory<byte>(payload), null).WaitAsync(WaitTimeout).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        sendError = ex;
                    }

                    Assert.IsNotNull(sendError, "套接字释放后发送应以异常结束。");
                    Assert.IsInstanceOfType(sendError, typeof(ObjectDisposedException), "应命中同步抛出 ObjectDisposedException 的分级分支：" + sendError);
                    Assert.AreEqual(0, errors.Count, "收尾类发送失败不应触发 OnError 上报，实际：" + string.Join(" || ", errors));
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
    }
}
