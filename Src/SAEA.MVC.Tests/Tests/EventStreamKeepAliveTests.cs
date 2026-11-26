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
*命名空间：SAEA.MVC.Tests
*文件名： EventStreamKeepAliveTests
*版本号： net10.0
*唯一标识：107a7b36-8778-4e54-9e17-bc6941493970
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/26 17:59:02
*描述：事件流在空闲超时窗口外仍保持心跳与连接的用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/26 20:40:52
*修改人： yswenli
*版本号： net10.0
*描述：新增明文与 TLS 两个 7 秒保活用例，统一心跳条数阈值，读取逻辑复用同一 Stream 实现
*
*****************************************************************************/
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.Hosting;
using SAEA.MVC.Tests.Tests.Support;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// 事件流保活用例：心跳写入会经 SessionManager.Active 续期会话，用例验证 7 秒内心跳连续且连接不被服务端空闲清理。
    /// </summary>
    [TestClass]
    public class EventStreamKeepAliveTests
    {
        /// <summary>等待服务端响应或关闭的上限。</summary>
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 服务端空闲超时 3 秒，事件流连接持续约 7 秒仍应收到多条心跳且不被断开。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task EventStream_LongIdleBeyondFreeTime_KeepsHeartbeat()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = new SAEAMvcApplication(rootName, port: port, timeout: 3);

            try
            {
                app.Start();

                // 先用普通入口确认服务已就绪，避免把连接未就绪误判成心跳未启动
                using (var client = TestAppFactory.CreateHttpClient())
                using (var ready = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/bigdataprobe/plain").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, ready.StatusCode, "GET /bigdataprobe/plain 应返回 200，用于确认服务已就绪");
                }

                var result = await ObserveHeartbeatAsync(port, "/events/probe", TimeSpan.FromSeconds(7)).ConfigureAwait(false);

                Assert.IsFalse(result.ClosedByServer, "空闲超过服务端超时后连接不应被断开");
                Assert.IsTrue(result.HeartbeatLines >= 5, "7 秒内应持续收到心跳，实际 " + result.HeartbeatLines + " 条");
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// TLS 通道下服务端空闲超时 3 秒，事件流连接持续约 7 秒仍应收到连续心跳且不被断开。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task EventStream_TlsLongIdleBeyondFreeTime_KeepsHeartbeat()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var bundle = TestAppFactory.CreateSelfSignedCertificate();
            var app = new SAEAMvcApplication(rootName, port: port, timeout: 3);

            app.UseSsl(options =>
            {
                options.Protocols = SslProtocols.Tls12;

                options.ServerCertificate = bundle.Certificate;
            });

            try
            {
                app.Start();

                using (var tcp = new TcpClient())
                {
                    await tcp.ConnectAsync(IPAddress.Loopback, port).WaitAsync(WaitTimeout).ConfigureAwait(false);

                    using (var ssl = new SslStream(tcp.GetStream(), false, (sender, certificate, chain, errors) => true))
                    {
                        var authOptions = new SslClientAuthenticationOptions { TargetHost = TestAppFactory.LoopbackHost };

                        await ssl.AuthenticateAsClientAsync(authOptions).WaitAsync(WaitTimeout).ConfigureAwait(false);

                        var result = await ObserveHeartbeatAsync(ssl, "/events/probe", TimeSpan.FromSeconds(7)).ConfigureAwait(false);

                        Assert.IsFalse(result.ClosedByServer, "TLS 空闲超过服务端超时后连接不应被断开");
                        Assert.IsTrue(result.HeartbeatLines >= 5, "TLS 通道 7 秒内应持续收到心跳，实际 " + result.HeartbeatLines + " 条，ClosedByServer=" + result.ClosedByServer);
                    }
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                bundle.Dispose();
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 用原始 socket 打开事件流，读到报头后继续观察指定时长，统计心跳行数并记录是否被服务端断开。
        /// </summary>
        /// <param name="port">服务监听端口</param>
        /// <param name="path">请求路径，以 / 开头</param>
        /// <param name="observe">报头之后继续观察的时长</param>
        /// <returns>心跳行数与关闭方式</returns>
        private static async Task<ObserveResult> ObserveHeartbeatAsync(int port, string path, TimeSpan observe)
        {
            using (var tcp = new TcpClient())
            {
                await tcp.ConnectAsync(IPAddress.Loopback, port).WaitAsync(WaitTimeout).ConfigureAwait(false);

                using (var stream = tcp.GetStream())
                {
                    return await ObserveHeartbeatAsync(stream, path, observe).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// 在给定的连接流上打开事件流，读到报头后继续观察指定时长，统计心跳行数并记录是否被服务端断开。
        /// </summary>
        /// <param name="stream">已建立的连接流，明文与 TLS 共用</param>
        /// <param name="path">请求路径，以 / 开头</param>
        /// <param name="observe">报头之后继续观察的时长</param>
        /// <returns>心跳行数与关闭方式</returns>
        private static async Task<ObserveResult> ObserveHeartbeatAsync(Stream stream, string path, TimeSpan observe)
        {
            var request = Encoding.ASCII.GetBytes("GET " + path + " HTTP/1.1\r\nHost: " + TestAppFactory.LoopbackHost + "\r\nConnection: keep-alive\r\n\r\n");

            await stream.WriteAsync(request, 0, request.Length).WaitAsync(WaitTimeout).ConfigureAwait(false);

            var builder = new StringBuilder();

            var buffer = new byte[4096];

            var closed = false;

            // 先读到报头结束，5 秒上限是硬保护，避免服务端迟迟不回报头时用例挂死
            using (var headerTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                while (builder.ToString().IndexOf("\r\n\r\n", StringComparison.Ordinal) < 0)
                {
                    int read;

                    try
                    {
                        read = await stream.ReadAsync(buffer, 0, buffer.Length, headerTimeout.Token).ConfigureAwait(false);
                    }
                    catch (IOException)
                    {
                        // TLS 下服务端未发 close_notify 就断开时 SslStream 抛 IOException，按已关闭处理
                        closed = true;

                        break;
                    }

                    if (read <= 0)
                    {
                        closed = true;

                        break;
                    }

                    builder.Append(Encoding.ASCII.GetString(buffer, 0, read));
                }
            }

            if (!closed)
            {
                // 报头之后继续观察心跳；读超时是预期结束方式，按正常观察结束处理
                using (var observeTimeout = new CancellationTokenSource(observe))
                {
                    while (true)
                    {
                        int read;

                        try
                        {
                            read = await stream.ReadAsync(buffer, 0, buffer.Length, observeTimeout.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (IOException)
                        {
                            // TLS 下服务端主动断开（未发 close_notify）时按已关闭处理
                            closed = true;

                            break;
                        }

                        if (read <= 0)
                        {
                            closed = true;

                            break;
                        }

                        builder.Append(Encoding.ASCII.GetString(buffer, 0, read));
                    }
                }
            }

            var text = builder.ToString();

            var headerEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);

            var body = headerEnd >= 0 ? text.Substring(headerEnd + 4) : text;

            return new ObserveResult
            {
                HeartbeatLines = CountHeartbeatLines(body),
                ClosedByServer = closed
            };
        }

        /// <summary>
        /// 统计以「: 」开头的心跳行数，心跳与事件正文都用空行分隔。
        /// </summary>
        /// <param name="body">报头之后的响应正文</param>
        /// <returns>心跳行数</returns>
        private static int CountHeartbeatLines(string body)
        {
            var count = 0;

            foreach (var line in body.Split('\n'))
            {
                if (line.StartsWith(": ", StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        /// <summary>
        /// 事件流观察结果。
        /// </summary>
        private class ObserveResult
        {
            /// <summary>
            /// 收到的心跳行数。
            /// </summary>
            public int HeartbeatLines { get; set; }

            /// <summary>
            /// 服务端是否在观察结束前主动关闭了连接。
            /// </summary>
            public bool ClosedByServer { get; set; }
        }

        /// <summary>
        /// 拼出本用例服务的回环基地址。
        /// </summary>
        /// <param name="port">监听端口</param>
        /// <returns>不含路径的基地址</returns>
        private static string BaseUrl(int port)
        {
            return "http://" + TestAppFactory.LoopbackHost + ":" + port;
        }
    }
}