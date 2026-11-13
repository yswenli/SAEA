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
*文件名： TlsAbruptCloseTests
*版本号： net10.0
*唯一标识：71d58e5b-1044-43ae-9c9d-ba1c4948c592
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 10:55:00
*描述：TLS 客户端不发 close_notify 直接断开时的服务端收尾回归用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 10:55:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，验证客户端直接关闭套接字不会在服务端收尾路径产生 NullReferenceException
*
*****************************************************************************/
using System;
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

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// TLS 客户端收尾用例：客户端握手、请求后不发 close_notify 直接关闭套接字，
    /// 服务端不应在收尾路径抛出 NullReferenceException 或 ObjectDisposedException。
    /// </summary>
    [TestClass]
    public class TlsAbruptCloseTests
    {
        /// <summary>并发客户端数。</summary>
        private const int WorkerCount = 16;

        /// <summary>每个客户端发起的断开次数。</summary>
        private const int IterationsPerWorker = 30;

        /// <summary>服务端日志里不允许出现的异常标记。</summary>
        private static readonly string[] ErrorMarkers = { "NullReferenceException", "ObjectDisposedException" };

        /// <summary>
        /// 反复让客户端跳过 close_notify 直接断开，服务端日志不应出现空引用或已释放对象异常，
        /// 且随后仍能正常处理 HTTPS 请求。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Tls_ClientAbruptClose_DoesNotRaiseNullReference()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var bundle = TestAppFactory.CreateSelfSignedCertificate();
            var app = TestAppFactory.CreateApp(rootName, port, instance => instance.UseSsl(options =>
            {
                options.Protocols = SslProtocols.Tls12;

                options.ServerCertificate = bundle.Certificate;
            }));

            var capture = new CaptureWriter();
            var originalOut = Console.Out;
            Console.SetOut(capture);

            try
            {
                app.Start();

                // 先确认站点可用，避免把启动失败误判成收尾缺陷。
                using (var client = TestAppFactory.CreateHttpClient(true))
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/basic/index").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "预热请求应返回 200");

                    await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }

                var completed = 0;
                var workers = new Task[WorkerCount];
                for (var w = 0; w < WorkerCount; w++)
                {
                    workers[w] = Task.Run(async () =>
                    {
                        for (var i = 0; i < IterationsPerWorker; i++)
                        {
                            try
                            {
                                if (await AbruptCloseOnceAsync(port).ConfigureAwait(false))
                                    Interlocked.Increment(ref completed);
                            }
                            catch (Exception)
                            {
                                // 客户端断开期间的异常不影响服务端结论，服务可用性由收尾后的 GET 证明。
                            }

                            await Task.Delay(10).ConfigureAwait(false);
                        }
                    });
                }

                await Task.WhenAll(workers).ConfigureAwait(false);

                Assert.IsTrue(completed > 0, "至少有客户端完成握手并发出请求，否则本用例无法覆盖收尾路径");

                var text = await WaitForMarkersAsync(capture, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                var excerpt = BuildExcerpt(text);

                Assert.IsNull(excerpt, "客户端跳过 close_notify 直接断开时服务端不应记录空引用或已释放对象异常：" + excerpt);

                using (var client = TestAppFactory.CreateHttpClient(true))
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/basic/index").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "断开风暴之后服务应仍能处理 HTTPS 请求");
                }
            }
            finally
            {
                Console.SetOut(originalOut);

                TestAppFactory.StopQuietly(app);
                bundle.Dispose();
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 建立 TLS 连接、发出一个请求并只读一个字节，随后不发 close_notify 直接关闭套接字，
        /// 对端只能看到 FIN，与客户端「直接 Close」的现场一致。
        /// </summary>
        /// <param name="port">服务端口</param>
        /// <returns>握手与请求是否完成</returns>
        private static async Task<bool> AbruptCloseOnceAsync(int port)
        {
            var tcp = new TcpClient();
            var completed = false;

            try
            {
                await tcp.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);

                var ssl = new SslStream(tcp.GetStream(), false, (sender, certificate, chain, errors) => true);

                try
                {
                    await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                    {
                        TargetHost = "127.0.0.1",
                        EnabledSslProtocols = SslProtocols.Tls12
                    }).ConfigureAwait(false);

                    var request = Encoding.ASCII.GetBytes(
                        "GET /basic/index HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n");

                    await ssl.WriteAsync(request, 0, request.Length).ConfigureAwait(false);
                    await ssl.FlushAsync().ConfigureAwait(false);

                    completed = true;

                    // 只读一个字节：此时服务端通常还在写响应，收尾与接收回调交错。
                    // 异步读没有 ReadTimeout，用取消令牌兜底，避免服务端异常时卡住用例。
                    using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                    {
                        var buffer = new byte[1];

                        try
                        {
                            await ssl.ReadAsync(buffer.AsMemory(0, 1), cancellation.Token).ConfigureAwait(false);
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
                finally
                {
                    // 不调用 close_notify：直接丢弃 TLS 状态并关闭套接字。
                    try { ssl.Dispose(); } catch { }
                    try { tcp.Client.Close(); } catch { }
                }
            }
            finally
            {
                try { tcp.Dispose(); } catch { }
            }

            return completed;
        }

        /// <summary>
        /// 轮询捕获文本，直到出现异常标记或超时。
        /// </summary>
        /// <param name="capture">控制台捕获器</param>
        /// <param name="timeout">最长等待时间；日志由后台线程异步刷出</param>
        /// <returns>等待结束时的捕获文本</returns>
        private static async Task<string> WaitForMarkersAsync(CaptureWriter capture, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            var text = capture.Snapshot();

            while (DateTime.UtcNow < deadline && !ContainsMarker(text))
            {
                await Task.Delay(100).ConfigureAwait(false);
                text = capture.Snapshot();
            }

            return text;
        }

        /// <summary>
        /// 判断捕获文本里是否出现异常标记。
        /// </summary>
        /// <param name="text">捕获文本</param>
        /// <returns>出现任一标记返回 true</returns>
        private static bool ContainsMarker(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            foreach (var marker in ErrorMarkers)
            {
                if (text.IndexOf(marker, StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 截取异常标记附近的文本，便于失败时直接看到异常类型与栈。
        /// </summary>
        /// <param name="text">捕获文本</param>
        /// <returns>未命中返回 null；命中返回包含标记的片段</returns>
        private static string BuildExcerpt(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            foreach (var marker in ErrorMarkers)
            {
                var index = text.IndexOf(marker, StringComparison.Ordinal);

                if (index >= 0)
                {
                    var start = Math.Max(0, index - 400);
                    var end = Math.Min(text.Length, index + 1600);

                    return text.Substring(start, end - start);
                }
            }

            return null;
        }

        /// <summary>
        /// 拼出本用例服务的回环基地址。
        /// </summary>
        /// <param name="port">监听端口</param>
        /// <returns>不含路径的基地址</returns>
        private static string BaseUrl(int port)
        {
            return "https://" + TestAppFactory.LoopbackHost + ":" + port;
        }

        /// <summary>
        /// 线程安全的控制台捕获器：日志后台线程与用例线程会同时写入。
        /// </summary>
        private sealed class CaptureWriter : TextWriter
        {
            private readonly object _sync = new object();

            private readonly StringBuilder _buffer = new StringBuilder();

            /// <summary>捕获内容的编码，固定 UTF-8。</summary>
            public override Encoding Encoding => Encoding.UTF8;

            /// <summary>写入单个字符。</summary>
            /// <param name="value">待写入字符</param>
            public override void Write(char value)
            {
                lock (_sync)
                {
                    _buffer.Append(value);
                }
            }

            /// <summary>写入字符串。</summary>
            /// <param name="value">待写入文本</param>
            public override void Write(string value)
            {
                lock (_sync)
                {
                    _buffer.Append(value);
                }
            }

            /// <summary>写入一行。</summary>
            /// <param name="value">待写入文本</param>
            public override void WriteLine(string value)
            {
                lock (_sync)
                {
                    _buffer.Append(value).Append(Environment.NewLine);
                }
            }

            /// <summary>
            /// 取当前捕获内容的一致性快照。
            /// </summary>
            /// <returns>已捕获的全部文本</returns>
            public string Snapshot()
            {
                lock (_sync)
                {
                    return _buffer.ToString();
                }
            }
        }
    }
}