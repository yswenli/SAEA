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
*文件名： EventStreamHeadTests
*版本号： net10.0
*唯一标识：89e1d568-139d-49b5-b9f9-f70d24560dcf
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 16:54:00
*描述：事件流在 HEAD 下的收尾用例，覆盖不挂起与不留心跳线程
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 16:54:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，用原始 socket 验证 HEAD 命中事件流时只回报头、不声明长度并在 5 秒内断开
*
*****************************************************************************/
using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.Tests.Tests.Support;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// 事件流 HEAD 用例：事件流给不出与 GET 等价的正文长度，只能回报头后立即收尾。
    /// </summary>
    [TestClass]
    public class EventStreamHeadTests
    {
        /// <summary>
        /// HEAD 命中事件流入口时只回报头、不声明正文长度，服务端断开连接后同客户端仍能正常请求。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task EventStream_HeadRequest_CompletesWithoutBodyAndKeepsServerUsable()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                {
                    // 先用普通入口确认服务已就绪，避免把连接未就绪误判成事件流挂死
                    using (var ready = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/bigdataprobe/plain").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, ready.StatusCode, "GET /bigdataprobe/plain 应返回 200，用于确认服务已就绪");
                    }

                    // 用原始 socket 读，避免 HttpClient 按「HEAD 无正文」替服务端补一个 Content-Length
                    var raw = await ReadRawHeadAsync(port, "/events/probe").ConfigureAwait(false);

                    StringAssert.StartsWith(raw.Headers, "HTTP/1.1 200", "HEAD /events/probe 应返回 200");
                    StringAssert.Contains(raw.Headers, "text/event-stream", "HEAD 应保留事件流的 Content-Type 报头");
                    Assert.IsFalse(raw.Headers.IndexOf("Content-Length", StringComparison.OrdinalIgnoreCase) >= 0, "无限流给不出与 GET 等价的长度，HEAD 不应声明 Content-Length");
                    Assert.IsTrue(raw.ClosedByServer, "HEAD 命中事件流后服务端必须断开连接，否则客户端会一直等正文");

                    // HEAD 之后同一客户端还能正常请求：心跳线程与连接都没有被留在服务端
                    using (var after = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/bigdataprobe/plain").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, after.StatusCode, "HEAD 之后 GET /bigdataprobe/plain 仍应返回 200");

                        var body = await after.Content.ReadAsStringAsync().ConfigureAwait(false);

                        Assert.AreEqual("bigdata-plain", body, "HEAD 之后普通响应的正文不应受影响");
                    }
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 用原始 socket 发一个 HEAD 请求，读到连接关闭为止，返回报头文本与关闭方式。
        /// </summary>
        /// <param name="port">服务监听端口</param>
        /// <param name="path">请求路径，以 / 开头</param>
        /// <returns>报头文本与是否由服务端主动关闭连接</returns>
        private static async Task<RawHead> ReadRawHeadAsync(int port, string path)
        {
            using (var tcp = new TcpClient())
            {
                await tcp.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);

                using (var stream = tcp.GetStream())
                {
                    var request = Encoding.ASCII.GetBytes("HEAD " + path + " HTTP/1.1\r\nHost: " + TestAppFactory.LoopbackHost + "\r\nConnection: close\r\n\r\n");

                    await stream.WriteAsync(request, 0, request.Length).ConfigureAwait(false);

                    var builder = new StringBuilder();

                    var buffer = new byte[4096];

                    var closed = false;

                    // 5 秒上限是硬保护：修复前事件流会在 HEAD 下保持长连接，读不到结束符只能等到超时。
                    // 不能用 NetworkStream.ReadTimeout，它只对同步读生效，这里走的是异步读
                    using (var readTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                    {
                        while (true)
                        {
                            int read;

                            try
                            {
                                read = await stream.ReadAsync(buffer, 0, buffer.Length, readTimeout.Token).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException)
                            {
                                // 读超时说明服务端没有收尾，按未正常关闭处理
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

                    var text = builder.ToString();

                    // 只取报头：正文为空时整段就是报头，截到空行为止可兼容偶发多读的情况
                    var end = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);

                    return new RawHead
                    {
                        Headers = end >= 0 ? text.Substring(0, end) : text,
                        ClosedByServer = closed
                    };
                }
            }
        }

        /// <summary>
        /// 原始 HEAD 响应的读取结果。
        /// </summary>
        private class RawHead
        {
            /// <summary>
            /// 报头文本，不含结尾空行。
            /// </summary>
            public string Headers { get; set; }

            /// <summary>
            /// 服务端是否在超时前主动关闭了连接。
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