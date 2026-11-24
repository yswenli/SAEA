/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.MVC.Tests
*文件名： TlsTests
*版本号： net10.0
*唯一标识：5629858e-7656-413f-ad0e-005ffc68f370
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 18:07:54
*描述：HTTPS 与 WSS 端到端用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/24 18:12:35
*修改人： yswenli
*版本号： 
*描述：net10.0
*
*****************************************************************************/
using SAEA.MVC.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.Tests.Tests.Support;
using SAEA.Sockets.Ssl;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// TLS 入口的端到端用例：同一端口承载 HTTPS 请求与 WSS 升级，证书与协议在启动前配置。
    /// </summary>
    [TestClass]
    public class TlsTests
    {
        /// <summary>等待服务端响应或关闭的上限。</summary>
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);
        /// <summary>
        /// HTTPS GET 返回 200 与动作正文。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Https_Get_Returns200AndBody()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var bundle = TestAppFactory.CreateSelfSignedCertificate();
            var app = CreateHttpsApp(rootName, port, bundle);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient(true))
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/basic/index").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "HTTPS GET /basic/index 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("basic-index-ok", body, "HTTPS 响应正文应为 Index 动作的返回值");
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
        /// HTTPS 表单 POST 的参数被绑定到动作形参并回显。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Https_PostForm_BindsParameter()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var bundle = TestAppFactory.CreateSelfSignedCertificate();
            var app = CreateHttpsApp(rootName, port, bundle);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient(true))
                using (var response = await TestAppFactory.PostFormAsync(client, BaseUrl(port) + "/basic/echo", "name=saea-tls").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "HTTPS POST /basic/echo 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("basic-echo:saea-tls", body, "HTTPS 通道上的表单字段应绑定到动作形参");
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
        /// WSS 升级后客户端发送文本，服务端把同一文本回写。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Wss_ClientEcho_Works()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var bundle = TestAppFactory.CreateSelfSignedCertificate();
            var app = CreateHttpsApp(rootName, port, bundle);

            try
            {
                app.Start();

                using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                {
                    var socket = await WebSocketProbe.ConnectAsync(BaseUrl(port, "wss") + "/echo/open", true, cancellation.Token)
                        .ConfigureAwait(false);

                    try
                    {
                        var reply = await WebSocketProbe.SendAndReceiveTextAsync(socket, "wss-hello", cancellation.Token)
                            .ConfigureAwait(false);

                        Assert.AreEqual("wss-hello", reply, "WSS 通道上的文本消息应原样回显");
                    }
                    finally
                    {
                        await WebSocketProbe.CloseAsync(socket, cancellation.Token).ConfigureAwait(false);

                        socket.Dispose();
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
        /// TLS 下 1MB 超限正文也必须在排空超时前读完并有序收尾：排空若照旧写入 Pipe，会被背压卡到超时并带未读字节关闭。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Https_OverLimitLargeBody_DrainsFastWithoutReset()
        {
            const int limit = 8 * 1024;
            const int drainTimeout = 8000;
            const int extraBytes = 1024 * 1024;
            const int firstBatch = 1024;
            const int writeChunk = 16 * 1024;

            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var bundle = TestAppFactory.CreateSelfSignedCertificate();

            var app = new SAEAMvcApplication(new SAEAMvcApplicationConfig
            {
                Root = rootName,
                Port = port,
                IsStaticsCached = false,
                IsZiped = false,
                MaxRequestBodySize = limit,
                RequestDrainTimeout = drainTimeout
            });

            app.UseSsl(options =>
            {
                options.Protocols = SslProtocols.Tls12;

                options.ServerCertificate = bundle.Certificate;
            });

            try
            {
                app.Start();

                using (var client = new TcpClient())
                {
                    await client.ConnectAsync(IPAddress.Loopback, port).WaitAsync(WaitTimeout).ConfigureAwait(false);

                    using (var ssl = new SslStream(client.GetStream(), false, (sender, certificate, chain, errors) => true))
                    {
                        var authOptions = new SslClientAuthenticationOptions { TargetHost = "127.0.0.1" };

                        await ssl.AuthenticateAsClientAsync(authOptions).WaitAsync(WaitTimeout).ConfigureAwait(false);

                        var contentLength = limit + extraBytes;

                        var head = new StringBuilder();
                        head.Append("POST /reuse/probe HTTP/1.1\r\n");
                        head.Append("Host: 127.0.0.1:").Append(port).Append("\r\n");
                        head.Append("Connection: close\r\n");
                        head.Append("Content-Type: application/octet-stream\r\n");
                        head.Append("Content-Length: ").Append(contentLength).Append("\r\n");
                        head.Append("\r\n");

                        var headBytes = Encoding.ASCII.GetBytes(head.ToString());

                        var firstBatchBytes = new byte[headBytes.Length + firstBatch];

                        Buffer.BlockCopy(headBytes, 0, firstBatchBytes, 0, headBytes.Length);

                        var stopwatch = Stopwatch.StartNew();

                        // 报头与第一段正文同一条 TLS 记录写出：分帧器能在解密后的同一批字节里算出已到达量
                        await ssl.WriteAsync(firstBatchBytes, 0, firstBatchBytes.Length).WaitAsync(WaitTimeout).ConfigureAwait(false);

                        var status = await ReadStatusAndBodyAsync(ssl).ConfigureAwait(false);

                        Assert.AreEqual(413, status, "超限请求应先收到完整 413。");

                        // 剩余正文分块写回：排空若落 Pipe，超过背压阈值后接收会停摆，这里的写入会失败
                        var chunk = new byte[writeChunk];

                        var remaining = contentLength - firstBatch;

                        while (remaining > 0)
                        {
                            var take = remaining < writeChunk ? remaining : writeChunk;

                            await ssl.WriteAsync(chunk, 0, take).WaitAsync(WaitTimeout).ConfigureAwait(false);

                            remaining -= take;
                        }

                        // 服务端按解密后的明文字节计数，读完全部正文后才关闭；这里应看到有序收尾而不是连接重置
                        await DrainToCloseAsync(ssl).ConfigureAwait(false);

                        stopwatch.Stop();

                        Assert.IsTrue(
                            stopwatch.ElapsedMilliseconds < 4000,
                            "TLS 下 1MB 排空应在超时前完成，实际耗时 " + stopwatch.ElapsedMilliseconds + "ms（排空超时 " + drainTimeout + "ms）。");
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
        /// 逐字节读到报头结束标记，再按 Content-Length 读满 413 正文，返回状态码。
        /// </summary>
        /// <param name="ssl">已握手的 TLS 客户端流</param>
        /// <returns>响应状态码</returns>
        private static async Task<int> ReadStatusAndBodyAsync(SslStream ssl)
        {
            var head = new List<byte>(512);

            var one = new byte[1];

            while (true)
            {
                var read = await ssl.ReadAsync(one, 0, 1).WaitAsync(WaitTimeout).ConfigureAwait(false);

                if (read == 0)
                    Assert.Fail("读取 413 报头时连接提前关闭。");

                head.Add(one[0]);

                var count = head.Count;

                if (count >= 4
                    && head[count - 4] == (byte)'\r'
                    && head[count - 3] == (byte)'\n'
                    && head[count - 2] == (byte)'\r'
                    && head[count - 1] == (byte)'\n')
                    break;
            }

            var text = Encoding.ASCII.GetString(head.ToArray());

            Assert.IsTrue(text.StartsWith("HTTP/1.1 ", StringComparison.Ordinal), "响应应以状态行开始。实际：" + text);

            var status = int.Parse(text.Substring(9, 3));

            var length = 0;

            foreach (var line in text.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    length = int.Parse(line.Substring("Content-Length:".Length).Trim());
            }

            var body = new byte[length];

            var offset = 0;

            while (offset < length)
            {
                var read = await ssl.ReadAsync(body, offset, length - offset).WaitAsync(WaitTimeout).ConfigureAwait(false);

                if (read == 0)
                    Assert.Fail("读取 413 正文时连接提前关闭，已读 " + offset + "/" + length + " 字节。");

                offset += read;
            }

            return status;
        }

        /// <summary>
        /// 读到对端关闭为止；服务端半关后未发 close_notify 就关闭 TCP，SslStream 会以 IOException 收尾，
        /// 只有连接重置才算排空失败。
        /// </summary>
        /// <param name="ssl">已握手的 TLS 客户端流</param>
        /// <returns>读到关闭的任务</returns>
        private static async Task DrainToCloseAsync(SslStream ssl)
        {
            var buffer = new byte[4096];

            try
            {
                while (true)
                {
                    var read = await ssl.ReadAsync(buffer, 0, buffer.Length).WaitAsync(WaitTimeout).ConfigureAwait(false);

                    if (read == 0)
                        return;
                }
            }
            catch (IOException ex) when (!IsConnectionReset(ex))
            {
                // 缺少 close_notify 的收尾属于有序关闭：SslStream 把 TCP 关闭包装成 IOException，
                // 服务端不会另行发送 alert；只有链路上出现 ConnectionReset 才说明排空失败
            }
        }

        /// <summary>
        /// 判断异常链上是否出现连接重置：SslStream 会把 SocketException 逐层包装，必须递归解包。
        /// </summary>
        /// <param name="ex">待检查的异常</param>
        /// <returns>链上存在 ConnectionReset 时返回 true</returns>
        private static bool IsConnectionReset(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                var socketException = current as SocketException;

                if (socketException != null && socketException.SocketErrorCode == SocketError.ConnectionReset)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 按固定 TLS 1.2 与自签证书创建应用。
        /// </summary>
        /// <param name="rootName">站点根目录名</param>
        /// <param name="port">监听端口</param>
        /// <param name="bundle">自签证书，生命周期由调用方管理</param>
        /// <returns>尚未启动的应用实例</returns>
        private static SAEAMvcApplication CreateHttpsApp(string rootName, int port, SaeaCertificateBundle bundle)
        {
            return TestAppFactory.CreateApp(rootName, port, app => app.UseSsl(options =>
            {
                options.Protocols = SslProtocols.Tls12;

                options.ServerCertificate = bundle.Certificate;
            }));
        }

        /// <summary>
        /// 拼出本用例服务的回环基地址。
        /// </summary>
        /// <param name="port">监听端口</param>
        /// <param name="scheme">http 或 https、ws 或 wss</param>
        /// <returns>不含路径的基地址</returns>
        private static string BaseUrl(int port, string scheme = "https")
        {
            return scheme + "://" + TestAppFactory.LoopbackHost + ":" + port;
        }
    }
}