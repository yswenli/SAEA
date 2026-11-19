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
*文件名： HttpContextReuseTests
*版本号： net10.0
*唯一标识：c0d78b05-c905-43ca-b9f8-6d85799fd87b
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 20:15:09
*描述：同一条连接上复用 HTTP 上下文的端到端用例：状态、正文、集合与响应副作用不得跨请求残留
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 20:15:09
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖 POST→GET、多段表单、响应副作用、304、输出缓存、404、异常与 HEAD→GET
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.Tests.Tests.Support;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// 上下文复用用例：在一条真实的保活连接上连续发请求，验证上一次请求的请求态、响应态与集合状态都不残留。
    /// </summary>
    [TestClass]
    public class HttpContextReuseTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

        private const string ProbePath = "/reuse/probe";

        private const string ProbeHeaderName = "X-Reuse-Probe";

        /// <summary>
        /// POST 的 JSON 正文、自定义请求头与 query 不能带到同一条连接的下一条 GET：正文、JSON 与表单集合必须复位。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Reuse_PostThenGet_DoesNotLeakRequestState()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var connection = await RawConnection.ConnectAsync(port).ConfigureAwait(false))
                {
                    var headers = new Dictionary<string, string> { { ProbeHeaderName, "token-post" } };

                    var first = await connection.SendAsync(
                        "POST",
                        ProbePath + "?from=post",
                        "{\"marker\":\"post-body\"}",
                        // 解析按内容类型匹配「application/json; charset=UTF-8」，不带 charset 不会被解析成 Json
                        "application/json; charset=UTF-8",
                        headers).ConfigureAwait(false);

                    Assert.AreEqual(200, first.Status, "POST 探针应返回 200。");

                    var firstEcho = ParseEcho(first.BodyText);

                    Assert.AreEqual("POST", firstEcho["method"], "第一次请求的方法应为 POST。");
                    Assert.AreEqual("token-post", firstEcho["probeHeader"], "第一次请求应回显自定义请求头。");
                    Assert.IsTrue(int.Parse(firstEcho["bodyLen"]) > 0, "JSON 正文应按字节出现在 Request.Body 上。echo=" + first.BodyText);
                    Assert.IsTrue(int.Parse(firstEcho["jsonLen"]) > 0, "JSON 正文应被解析到 Request.Json。echo=" + first.BodyText);
                    Assert.IsTrue((firstEcho["paramKeys"] ?? string.Empty).Contains("from"), "POST 应把 query 并进参数集合。echo=" + first.BodyText);
                    Assert.IsTrue((firstEcho["paramKeys"] ?? string.Empty).Contains("marker"), "JSON 正文的键应并进参数集合。echo=" + first.BodyText);

                    var second = await connection.SendAsync("GET", ProbePath).ConfigureAwait(false);

                    Assert.AreEqual(200, second.Status, "同一连接上的 GET 应返回 200。");

                    var secondEcho = ParseEcho(second.BodyText);

                    Assert.AreEqual("token-post", firstEcho["probeHeader"], "第一次请求的自定义头应被读到。");
                    Assert.AreEqual("GET", secondEcho["method"], "第二次请求的方法应为 GET。");
                    Assert.AreEqual("-", secondEcho["probeHeader"], "上一次请求的自定义头不能残留到下一次请求。");
                    Assert.AreEqual("0", secondEcho["jsonLen"], "上一次请求的 JSON 不能残留。");
                    Assert.AreEqual("-1", secondEcho["bodyLen"], "上一次请求的正文不能残留。");
                    Assert.AreEqual("-", secondEcho["formKeys"], "上一次请求的表单不能残留。");
                    Assert.AreEqual("-", secondEcho["paramKeys"], "上一次请求的参数不能残留。");
                    Assert.AreEqual("0", secondEcho["cookieCount"], "上一次请求的 Cookie 不能残留。");
                    Assert.AreEqual(firstEcho["hash"], secondEcho["hash"], "同一连接上的两条请求应复用同一个上下文实例。");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 多段表单上传产生的文件列表不能残留到下一条请求，否则下一条请求会带着上一次的上传内容。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Reuse_MultipartThenGet_DoesNotLeakPostFiles()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var connection = await RawConnection.ConnectAsync(port).ConfigureAwait(false))
                {
                    var boundary = "reuse" + Guid.NewGuid().ToString("N");

                    var first = await connection.SendAsync(
                        "POST",
                        ProbePath,
                        BuildMultipartBody(boundary),
                        "multipart/form-data; boundary=" + boundary).ConfigureAwait(false);

                    Assert.AreEqual(200, first.Status, "多段表单 POST 应返回 200。");

                    var firstEcho = ParseEcho(first.BodyText);

                    Assert.AreEqual("1", firstEcho["fileCount"], "多段表单应解析出一个上传文件。");

                    var second = await connection.SendAsync("GET", ProbePath).ConfigureAwait(false);

                    var secondEcho = ParseEcho(second.BodyText);

                    Assert.AreEqual(200, second.Status, "同一连接上的 GET 应返回 200。");
                    Assert.AreEqual("-1", secondEcho["fileCount"], "上一次上传的文件列表不能残留到下一次请求。");
                    Assert.AreEqual("-1", secondEcho["bodyLen"], "上一次上传的正文不能残留到下一次请求。");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 上一次响应写入的状态码、响应头与响应 Cookie 不能残留：下一次响应必须是 200、无自定义头、无自定义 Cookie。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Reuse_ResponseSideEffects_DoNotLeakToNextRequest()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var connection = await RawConnection.ConnectAsync(port).ConfigureAwait(false))
                {
                    var marked = await connection.SendAsync("GET", "/reuse/mark").ConfigureAwait(false);

                    Assert.AreEqual(202, marked.Status, "标记入口应把状态码改成 202。");
                    Assert.AreEqual("leak", marked.GetHeader("X-Reuse-Mark"), "标记入口应写出自定义响应头。");
                    // 一次响应可有多条 Set-Cookie，会话 Cookie 与自定义 Cookie 各占一行，必须全部看
                    StringAssert.Contains(string.Join(";", marked.GetHeaderValues("Set-Cookie")), "reuse-probe", "标记入口应写出自定义 Cookie。");

                    var plain = await connection.SendAsync("GET", ProbePath).ConfigureAwait(false);

                    var echo = ParseEcho(plain.BodyText);

                    Assert.AreEqual(200, plain.Status, "下一次响应必须是 200，状态码不能沿用上一次的 202。");
                    Assert.IsNull(plain.GetHeader("X-Reuse-Mark"), "上一次的自定义响应头不能残留。");
                    Assert.IsFalse(string.Join(";", plain.GetHeaderValues("Set-Cookie")).Contains("reuse-probe"), "上一次的自定义 Cookie 不能残留。");
                    Assert.AreEqual("200", echo["status"], "上一次请求里读到的响应状态应是新建实例的默认值 200。");
                    Assert.AreEqual("0", echo["cookieCount"], "客户端没回传 Cookie 时请求 Cookie 集合应为空。");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 304 分支会改状态码并写缓存头且不发正文：下一条请求必须回到 200、有正文且不再带缓存头。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Reuse_NotModifiedThenOk_ResetsStatusAndCacheHeaders()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var connection = await RawConnection.ConnectAsync(port).ConfigureAwait(false))
                {
                    var priming = await connection.SendAsync("GET", ProbePath).ConfigureAwait(false);

                    var sessionId = ExtractCookieValue(string.Join(";", priming.GetHeaderValues("Set-Cookie")), "SAEA.HttpServer.SessionID");

                    Assert.IsFalse(string.IsNullOrEmpty(sessionId), "首次响应应下发会话 Cookie。");

                    var notModified = await connection.SendAsync(
                        "GET",
                        "/reuse/not-modified",
                        null,
                        null,
                        new Dictionary<string, string> { { "Cookie", "SAEA.HttpServer.SessionID=" + sessionId } }).ConfigureAwait(false);

                    Assert.AreEqual(304, notModified.Status, "把缓存判定置为已缓存时应返回 304。");
                    Assert.AreEqual(0, notModified.Body.Length, "304 不应携带正文。");

                    // 不带会话 Cookie：下一条请求落到新会话，避免继续命中 304 判定
                    var plain = await connection.SendAsync("GET", ProbePath).ConfigureAwait(false);

                    var echo = ParseEcho(plain.BodyText);

                    Assert.AreEqual(200, plain.Status, "304 之后的下一条请求必须是 200。");
                    Assert.IsTrue(plain.Body.Length > 0, "200 响应必须带回正文。");
                    Assert.IsNull(plain.GetHeader("Expires"), "304 写出的 Expires 不能残留。");
                    Assert.AreEqual("no-cache", plain.GetHeader("Cache-Control"), "无缓存的普通响应应写 no-cache，不能沿用 Max-Age。");
                    Assert.AreEqual("200", echo["status"], "重新绑定前的响应状态应是新建实例的默认值 200。");
                    Assert.AreEqual("-1", echo["bodyLen"], "304 没有正文，下一条请求同样不应有正文。");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 输出缓存命中后上下文仍可复用：第二次命中缓存不执行 action，下一条普通请求正文干净。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Reuse_OutputCacheHitThenPlain_KeepsCacheAndCleanState()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            // query 让缓存键唯一，避免进程内缓存被其它用例或重复运行污染
            var url = "/reuse/cached?tag=" + Guid.NewGuid().ToString("N");

            try
            {
                app.Start();

                using (var connection = await RawConnection.ConnectAsync(port).ConfigureAwait(false))
                {
                    var first = await connection.SendAsync("GET", url).ConfigureAwait(false);

                    var second = await connection.SendAsync("GET", url).ConfigureAwait(false);

                    Assert.AreEqual(200, first.Status, "首次请求应返回 200。");
                    Assert.AreEqual(200, second.Status, "命中输出缓存时应返回 200。");
                    Assert.AreEqual(first.BodyText, second.BodyText, "第二次请求应命中缓存，正文与执行次数都不变。");
                    StringAssert.StartsWith(second.BodyText, "reuse-cached:", "缓存正文应是 action 的原始结果。");

                    var plain = await connection.SendAsync("GET", ProbePath).ConfigureAwait(false);

                    var echo = ParseEcho(plain.BodyText);

                    Assert.AreEqual(200, plain.Status, "缓存之后的下一条请求应返回 200。");
                    StringAssert.StartsWith(plain.BodyText, "url=", "缓存正文不能残留到下一次响应。");
                    Assert.AreEqual("-1", echo["bodyLen"], "缓存正文不能残留到下一次请求。");
                    Assert.AreEqual("-1", echo["fileCount"], "缓存请求的文件集合不能残留到下一次请求。");
                    Assert.AreEqual("200", echo["status"], "缓存结果不能把状态码带进下一次请求。");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 上一次请求的 404 状态码不能带到下一条正常请求。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Reuse_NotFoundThenOk_ReturnsFreshStatus()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var connection = await RawConnection.ConnectAsync(port).ConfigureAwait(false))
                {
                    var missing = await connection.SendAsync("GET", "/reuse/missing-" + Guid.NewGuid().ToString("N")).ConfigureAwait(false);

                    Assert.AreEqual(404, missing.Status, "不存在的路径应返回 404。");

                    var plain = await connection.SendAsync("GET", ProbePath).ConfigureAwait(false);

                    Assert.AreEqual(200, plain.Status, "404 之后的下一条请求必须回到 200。");
                    StringAssert.StartsWith(plain.BodyText, "url=", "下一条响应应是探针回显，而不是 404 的正文。");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 不同连接各自持有独立上下文：同一连接的两次请求实例相同，另一条连接的实例不同。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Reuse_TwoConnections_UseSeparateContexts()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var firstConnection = await RawConnection.ConnectAsync(port).ConfigureAwait(false))
                using (var secondConnection = await RawConnection.ConnectAsync(port).ConfigureAwait(false))
                {
                    var first = ParseEcho((await firstConnection.SendAsync("GET", ProbePath).ConfigureAwait(false)).BodyText);

                    var again = ParseEcho((await firstConnection.SendAsync("GET", ProbePath).ConfigureAwait(false)).BodyText);

                    var other = ParseEcho((await secondConnection.SendAsync("GET", ProbePath).ConfigureAwait(false)).BodyText);

                    Assert.AreEqual(first["hash"], again["hash"], "同一连接上的两条请求应复用同一个上下文实例。");
                    Assert.AreNotEqual(first["hash"], other["hash"], "不同连接不能共用同一个上下文实例。");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// action 抛异常转成 500 之后，同一条连接上的下一条请求仍应正常返回，不能被上一次的异常状态污染。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Reuse_ExceptionThenOk_ReturnsNormalResponse()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var connection = await RawConnection.ConnectAsync(port).ConfigureAwait(false))
                {
                    var failed = await connection.SendAsync("GET", "/asyncaction/throws").ConfigureAwait(false);

                    Assert.AreEqual(500, failed.Status, "action 抛异常应转成 500。");
                    StringAssert.Contains(failed.BodyText, "asyncaction-boom", "500 正文应带上异常信息。");

                    var plain = await connection.SendAsync("GET", ProbePath).ConfigureAwait(false);

                    var echo = ParseEcho(plain.BodyText);

                    Assert.AreEqual(200, plain.Status, "异常之后的下一条请求必须回到 200。");
                    Assert.IsFalse(plain.BodyText.Contains("asyncaction-boom"), "上一次的异常正文不能残留。");
                    Assert.AreEqual(ProbePath, echo["url"], "下一条请求应完整重新初始化请求地址。");
                    Assert.AreEqual("200", echo["status"], "异常之后响应状态应复位为 200。");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// HEAD 会置 SuppressBody 只回报头：同一条连接上的下一条 GET 必须重新带回正文。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Reuse_HeadThenGet_RestoresBody()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var connection = await RawConnection.ConnectAsync(port).ConfigureAwait(false))
                {
                    var head = await connection.SendAsync("HEAD", ProbePath).ConfigureAwait(false);

                    Assert.AreEqual(200, head.Status, "HEAD 探针应返回 200。");
                    Assert.AreEqual(0, head.Body.Length, "HEAD 响应不应携带正文。");
                    Assert.IsTrue(int.Parse(head.GetHeader("Content-Length")) > 0, "HEAD 仍应给出等价 GET 的正文长度。");

                    var get = await connection.SendAsync("GET", ProbePath).ConfigureAwait(false);

                    Assert.AreEqual(200, get.Status, "HEAD 之后的下一条 GET 应返回 200。");
                    Assert.IsTrue(get.Body.Length > 0, "下一条 GET 必须带回正文，SuppressBody 不能残留。");
                    StringAssert.StartsWith(get.BodyText, "url=", "下一条 GET 的正文应是探针回显。");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 拼一个最小的多段表单正文：一个文本类型文件段，分隔串与 Content-Type 里的 boundary 一致。
        /// </summary>
        /// <param name="boundary">不带前导 -- 的分隔串</param>
        /// <returns>完整正文文本</returns>
        private static string BuildMultipartBody(string boundary)
        {
            var builder = new StringBuilder();

            builder.Append("--").Append(boundary).Append("\r\n");
            builder.Append("Content-Disposition: form-data; name=\"file\"; filename=\"probe.txt\"\r\n");
            builder.Append("Content-Type: text/plain\r\n");
            builder.Append("\r\n");
            builder.Append("reuse-file-body\r\n");
            builder.Append("--").Append(boundary).Append("--\r\n");

            return builder.ToString();
        }

        /// <summary>
        /// 把探针回显的「键=值;键=值」解析成字典。
        /// </summary>
        /// <param name="body">探针回显正文</param>
        /// <returns>键值字典</returns>
        private static Dictionary<string, string> ParseEcho(string body)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);

            var segments = body.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var segment in segments)
            {
                var index = segment.IndexOf('=');

                if (index <= 0)
                    continue;

                result[segment.Substring(0, index)] = segment.Substring(index + 1);
            }

            return result;
        }

        /// <summary>
        /// 从 Set-Cookie 文本里取指定 Cookie 的值。
        /// </summary>
        /// <param name="setCookie">Set-Cookie 报头值，可为 null</param>
        /// <param name="name">Cookie 名</param>
        /// <returns>Cookie 值；不存在时返回 null</returns>
        private static string ExtractCookieValue(string setCookie, string name)
        {
            if (string.IsNullOrEmpty(setCookie))
                return null;

            var segments = setCookie.Split(';');

            foreach (var segment in segments)
            {
                var index = segment.IndexOf('=');

                if (index <= 0)
                    continue;

                if (segment.Substring(0, index).Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                    return segment.Substring(index + 1).Trim();
            }

            return null;
        }

        /// <summary>
        /// 一条保活连接：按顺序发请求并读完整响应，响应之间不留残留字节。
        /// </summary>
        private sealed class RawConnection : IDisposable
        {
            private readonly TcpClient _client;

            private readonly NetworkStream _stream;

            private readonly int _port;

            private RawConnection(TcpClient client, int port)
            {
                _client = client;

                _stream = client.GetStream();

                _port = port;
            }

            /// <summary>
            /// 连接宿主：服务可能尚未就绪，按 100 毫秒间隔重试，最长 10 秒。
            /// </summary>
            /// <param name="port">监听端口</param>
            /// <returns>已连接的原始连接</returns>
            public static async Task<RawConnection> ConnectAsync(int port)
            {
                var deadline = DateTime.UtcNow + WaitTimeout;

                while (true)
                {
                    var client = new TcpClient();

                    try
                    {
                        await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);

                        return new RawConnection(client, port);
                    }
                    catch (SocketException) when (DateTime.UtcNow < deadline)
                    {
                        client.Dispose();

                        await Task.Delay(100).ConfigureAwait(false);
                    }
                }
            }

            /// <summary>
            /// 发一条请求并读回对应响应。
            /// </summary>
            /// <param name="method">请求方法</param>
            /// <param name="path">请求目标</param>
            /// <param name="body">请求正文，可为 null</param>
            /// <param name="contentType">正文类型，可为 null</param>
            /// <param name="headers">附加请求头，可为 null</param>
            /// <returns>解析后的响应</returns>
            public async Task<RawResponse> SendAsync(string method, string path, string body = null, string contentType = null, IDictionary<string, string> headers = null)
            {
                var builder = new StringBuilder(256);

                builder.Append(method).Append(' ').Append(path).Append(" HTTP/1.1\r\n");
                builder.Append("Host: 127.0.0.1:").Append(_port).Append("\r\n");
                builder.Append("Connection: keep-alive\r\n");

                if (headers != null)
                {
                    foreach (var header in headers)
                        builder.Append(header.Key).Append(": ").Append(header.Value).Append("\r\n");
                }

                if (contentType != null)
                    builder.Append("Content-Type: ").Append(contentType).Append("\r\n");

                var bodyBytes = body == null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(body);

                if (bodyBytes.Length > 0)
                    builder.Append("Content-Length: ").Append(bodyBytes.Length).Append("\r\n");

                builder.Append("\r\n");

                var headBytes = Encoding.ASCII.GetBytes(builder.ToString());

                await _stream.WriteAsync(headBytes, 0, headBytes.Length).ConfigureAwait(false);

                if (bodyBytes.Length > 0)
                    await _stream.WriteAsync(bodyBytes, 0, bodyBytes.Length).ConfigureAwait(false);

                await _stream.FlushAsync().ConfigureAwait(false);

                var head = await ReadHeadAsync().ConfigureAwait(false);

                return await ReadResponseAsync(head, method).ConfigureAwait(false);
            }

            /// <summary>
            /// 逐字节读到报头结束标记。
            /// </summary>
            /// <returns>完整报头文本</returns>
            private async Task<string> ReadHeadAsync()
            {
                var head = new List<byte>(512);

                var one = new byte[1];

                while (true)
                {
                    var read = await _stream.ReadAsync(one, 0, 1).WaitAsync(WaitTimeout).ConfigureAwait(false);

                    if (read == 0)
                        throw new IOException("连接在响应报头结束前被关闭。");

                    head.Add(one[0]);

                    var count = head.Count;

                    if (count >= 4
                        && head[count - 4] == (byte)'\r'
                        && head[count - 3] == (byte)'\n'
                        && head[count - 2] == (byte)'\r'
                        && head[count - 1] == (byte)'\n')
                        break;
                }

                return Encoding.ASCII.GetString(head.ToArray());
            }

            /// <summary>
            /// 按状态码与方法决定是否读正文，并把报头解析进响应对象。
            /// </summary>
            /// <param name="head">报头文本</param>
            /// <param name="method">请求方法</param>
            /// <returns>解析后的响应</returns>
            private async Task<RawResponse> ReadResponseAsync(string head, string method)
            {
                var response = new RawResponse { Header = head };

                response.ParseHead();

                // HEAD 只回报头，304/204 按协议也没有正文：按 Content-Length 读会一直等到超时
                var noBody = string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase)
                    || response.Status == 304
                    || response.Status == 204;

                if (noBody)
                    return response;

                var length = 0;

                var contentLength = response.GetHeader("Content-Length");

                if (!string.IsNullOrEmpty(contentLength))
                    length = int.Parse(contentLength);

                if (length <= 0)
                    return response;

                var body = new byte[length];

                var offset = 0;

                while (offset < length)
                {
                    var read = await _stream.ReadAsync(body, offset, length - offset).WaitAsync(WaitTimeout).ConfigureAwait(false);

                    if (read == 0)
                        throw new IOException("连接在响应报文体读完前被关闭。");

                    offset += read;
                }

                response.Body = body;

                return response;
            }

            /// <summary>
            /// 释放连接与网络流。
            /// </summary>
            public void Dispose()
            {
                _stream.Dispose();

                _client.Dispose();
            }
        }

        /// <summary>
        /// 原始 HTTP 响应：报头文本、状态码、报头集合与正文字节。
        /// </summary>
        private sealed class RawResponse
        {
            private readonly Dictionary<string, List<string>> _headers = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// 完整报头文本，含结束空行。
            /// </summary>
            public string Header { get; set; }

            /// <summary>
            /// 响应状态码。
            /// </summary>
            public int Status { get; private set; }

            /// <summary>
            /// 响应正文字节；无正文时为空数组。
            /// </summary>
            public byte[] Body { get; set; } = Array.Empty<byte>();

            /// <summary>
            /// 响应正文文本。
            /// </summary>
            public string BodyText
            {
                get { return Encoding.UTF8.GetString(Body); }
            }

            /// <summary>
            /// 取报头首个值，名字不区分大小写，缺失时返回 null。
            /// </summary>
            /// <param name="name">报头名</param>
            /// <returns>报头值或 null</returns>
            public string GetHeader(string name)
            {
                List<string> values;

                if (_headers.TryGetValue(name, out values) && values.Count > 0)
                    return values[0];

                return null;
            }

            /// <summary>
            /// 取报头全部值，名字不区分大小写，缺失时返回空数组。
            /// </summary>
            /// <param name="name">报头名</param>
            /// <returns>同名报头值列表</returns>
            public List<string> GetHeaderValues(string name)
            {
                List<string> values;

                if (_headers.TryGetValue(name, out values))
                    return values;

                return new List<string>();
            }

            /// <summary>
            /// 解析报头文本：状态行取三位状态码，其余行按首个冒号拆键值，允许同名多值。
            /// </summary>
            public void ParseHead()
            {
                var lines = Header.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                Assert.IsTrue(lines.Length > 0, "响应缺少状态行。");

                var statusParts = lines[0].Split(' ');

                Assert.IsTrue(statusParts.Length >= 2, "状态行格式不正确：" + lines[0]);

                Status = int.Parse(statusParts[1]);

                for (var i = 1; i < lines.Length; i++)
                {
                    var index = lines[i].IndexOf(':');

                    if (index <= 0)
                        continue;

                    var name = lines[i].Substring(0, index).Trim();

                    var value = lines[i].Substring(index + 1).Trim();

                    List<string> values;

                    if (!_headers.TryGetValue(name, out values))
                    {
                        values = new List<string>(1);

                        _headers[name] = values;
                    }

                    values.Add(value);
                }
            }
        }
    }
}