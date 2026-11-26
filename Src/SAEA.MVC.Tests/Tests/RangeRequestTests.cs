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
*文件名： RangeRequestTests
*版本号： net10.0
*唯一标识：99a0b861-6e70-4153-8bcf-47b6be0ddce3
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/26 10:15:00
*描述：静态文件与控制器结果单区间 Range 的端到端用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/26 10:15:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖小文件 206/416/HEAD、原始 socket 回退与 4MB 以上大文件
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
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
    /// 单区间 Range 的端到端用例：静态小文件、静态大文件与控制器结果都按同一语义返回 206 或 416。
    /// </summary>
    [TestClass]
    public class RangeRequestTests
    {
        // 站点内文件名必须逐用例唯一：MapPath 的结果缓存是进程级静态字典，
        // 复用同名文件会让后续用例命中已删除的旧根目录而误报 404
        private const string SmallFileContent = "0123456789";

        private const int BigFileLength = 4 * 1024 * 1024 + 16;

        /// <summary>
        /// 静态小文件请求首段时应回 206、带 Content-Range 与对应切片。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task StaticFile_RangeFirstBytes_Returns206WithSlice()
        {
            var smallFileName = NewFileName("range", ".txt");

            await WithAppAsync(async (port, rootDirectory) =>
            {
                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await SendAsync(client, BaseUrl(port) + "/" + smallFileName, "bytes=0-3").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.PartialContent, response.StatusCode, "Range 命中时应返回 206");
                    Assert.AreEqual("bytes 0-3/10", Header(response, "Content-Range"), "206 应写完整区间");
                    Assert.AreEqual("bytes", Header(response, "Accept-Ranges"), "206 应声明支持字节区间");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("0123", body, "206 正文应只含请求区间");
                }
            }, new Dictionary<string, string> { { smallFileName, SmallFileContent } }).ConfigureAwait(false);
        }

        /// <summary>
        /// 静态小文件的后缀区间应取正文末尾字节。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task StaticFile_RangeSuffix_Returns206Tail()
        {
            var smallFileName = NewFileName("range", ".txt");

            await WithAppAsync(async (port, rootDirectory) =>
            {
                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await SendAsync(client, BaseUrl(port) + "/" + smallFileName, "bytes=-3").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.PartialContent, response.StatusCode, "后缀区间应返回 206");
                    Assert.AreEqual("bytes 7-9/10", Header(response, "Content-Range"), "后缀区间应换算成绝对区间");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("789", body, "后缀区间正文应取末尾字节");
                }
            }, new Dictionary<string, string> { { smallFileName, SmallFileContent } }).ConfigureAwait(false);
        }

        /// <summary>
        /// 静态小文件请求超出范围的区间时应回 416，正文为空且带不可满足区间。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task StaticFile_RangeUnsatisfiable_Returns416()
        {
            var smallFileName = NewFileName("range", ".txt");

            await WithAppAsync(async (port, rootDirectory) =>
            {
                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await SendAsync(client, BaseUrl(port) + "/" + smallFileName, "bytes=100-").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.RequestedRangeNotSatisfiable, response.StatusCode, "区间超出正文应返回 416");
                    Assert.AreEqual("bytes */10", Header(response, "Content-Range"), "416 应写完整长度");
                    Assert.AreEqual("bytes", Header(response, "Accept-Ranges"), "416 仍应声明支持字节区间");
                    Assert.AreEqual(0L, response.Content.Headers.ContentLength, "416 正文长度应为 0");

                    var body = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                    Assert.AreEqual(0, body.Length, "416 不应带正文");
                }
            }, new Dictionary<string, string> { { smallFileName, SmallFileContent } }).ConfigureAwait(false);
        }

        /// <summary>
        /// 没有 Range 的普通静态文件请求仍回 200 全量，但声明支持区间。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task StaticFile_NoRange_Returns200FullBodyWithAcceptRanges()
        {
            var smallFileName = NewFileName("range", ".txt");

            await WithAppAsync(async (port, rootDirectory) =>
            {
                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/" + smallFileName).ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "没有 Range 时应返回 200");
                    Assert.AreEqual("bytes", Header(response, "Accept-Ranges"), "普通文件响应也应声明支持区间");
                    Assert.AreEqual("<缺失>", Header(response, "Content-Range"), "200 全量响应不应写 Content-Range");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual(SmallFileContent, body, "200 响应应返回完整正文");
                }
            }, new Dictionary<string, string> { { smallFileName, SmallFileContent } }).ConfigureAwait(false);
        }

        /// <summary>
        /// HEAD 带 Range 时只回报头：状态 206、Content-Length 等于区间长度、正文为空。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task StaticFile_HeadWithRange_Returns206WithoutBody()
        {
            var smallFileName = NewFileName("range", ".txt");

            await WithAppAsync(async (port, rootDirectory) =>
            {
                using (var client = TestAppFactory.CreateHttpClient())
                {
                    var url = BaseUrl(port) + "/" + smallFileName;

                    using (var response = await TestAppFactory.SendWithReadyRetryAsync(client, () =>
                    {
                        var request = new HttpRequestMessage(HttpMethod.Head, url);

                        request.Headers.TryAddWithoutValidation("Range", "bytes=0-3");

                        return request;
                    }).ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.PartialContent, response.StatusCode, "HEAD 带 Range 也应返回 206");
                        Assert.AreEqual(4L, response.Content.Headers.ContentLength, "HEAD 的 Content-Length 应等于区间长度");
                        Assert.AreEqual("bytes 0-3/10", Header(response, "Content-Range"), "HEAD 206 也应写区间头");

                        var body = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                        Assert.AreEqual(0, body.Length, "HEAD 不应带正文");
                    }
                }
            }, new Dictionary<string, string> { { smallFileName, SmallFileContent } }).ConfigureAwait(false);
        }

        /// <summary>
        /// 多区间用原始 socket 验证：按 RFC 7233 忽略 Range，回 200 全量。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task StaticFile_MultipleRanges_RawSocketReturns200FullBody()
        {
            var smallFileName = NewFileName("range", ".txt");

            await WithAppAsync(async (port, rootDirectory) =>
            {
                var response = await SendRawAsync(port, "/" + smallFileName, "Range: bytes=0-1,3-4\r\n").ConfigureAwait(false);

                Assert.AreEqual(200, response.StatusCode, "多区间应按忽略 Range 处理，返回 200");
                Assert.AreEqual(SmallFileContent, response.Body, "忽略 Range 时应返回完整正文");
                Assert.AreEqual("<缺失>", response.Header("Content-Range"), "忽略 Range 时不应写 Content-Range");
            }, new Dictionary<string, string> { { smallFileName, SmallFileContent } }).ConfigureAwait(false);
        }

        /// <summary>
        /// 非法区间语法用原始 socket 验证：同样忽略 Range，回 200 全量。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task StaticFile_InvalidRange_RawSocketReturns200FullBody()
        {
            var smallFileName = NewFileName("range", ".txt");

            await WithAppAsync(async (port, rootDirectory) =>
            {
                var response = await SendRawAsync(port, "/" + smallFileName, "Range: bytes=abc\r\n").ConfigureAwait(false);

                Assert.AreEqual(200, response.StatusCode, "非法区间语法应忽略 Range，返回 200");
                Assert.AreEqual(SmallFileContent, response.Body, "非法区间时应返回完整正文");
                Assert.AreEqual("<缺失>", response.Header("Content-Range"), "非法区间时不应写 Content-Range");
            }, new Dictionary<string, string> { { smallFileName, SmallFileContent } }).ConfigureAwait(false);
        }

        /// <summary>
        /// 4MB 以上的静态文件走流式结果，首段区间同样应回 206 并只发区间字节。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task BigDataFile_RangeFirstBytes_Returns206WithSlice()
        {
            var bigFileName = NewFileName("range-large", ".bin");

            await WithAppAsync(async (port, rootDirectory) =>
            {
                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await SendAsync(client, BaseUrl(port) + "/" + bigFileName, "bytes=0-9").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.PartialContent, response.StatusCode, "大文件区间应返回 206");
                    Assert.AreEqual("bytes 0-9/" + BigFileLength, Header(response, "Content-Range"), "大文件 206 应写完整长度");

                    var body = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                    Assert.AreEqual(10, body.Length, "大文件 206 正文长度应等于区间长度");

                    for (var i = 0; i < body.Length; i++)
                    {
                        Assert.AreEqual(ExpectedBigFileByte(i), body[i], "大文件 206 第 " + i + " 字节应与源文件一致");
                    }
                }
            }, new Dictionary<string, string>(), root => CreateBigFile(root, bigFileName)).ConfigureAwait(false);
        }

        /// <summary>
        /// 4MB 以上的静态文件后缀区间应只发末尾字节。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task BigDataFile_RangeSuffix_Returns206Tail()
        {
            var bigFileName = NewFileName("range-large", ".bin");

            await WithAppAsync(async (port, rootDirectory) =>
            {
                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await SendAsync(client, BaseUrl(port) + "/" + bigFileName, "bytes=-5").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.PartialContent, response.StatusCode, "大文件后缀区间应返回 206");
                    Assert.AreEqual(
                        "bytes " + (BigFileLength - 5) + "-" + (BigFileLength - 1) + "/" + BigFileLength,
                        Header(response, "Content-Range"),
                        "大文件后缀区间应换算成绝对区间");

                    var body = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                    Assert.AreEqual(5, body.Length, "大文件后缀区间正文长度应为 5");

                    for (var i = 0; i < body.Length; i++)
                    {
                        Assert.AreEqual(ExpectedBigFileByte(BigFileLength - 5 + i), body[i], "大文件后缀第 " + i + " 字节应与源文件一致");
                    }
                }
            }, new Dictionary<string, string>(), root => CreateBigFile(root, bigFileName)).ConfigureAwait(false);
        }

        /// <summary>
        /// 控制器返回 FileResult 时也应按 Range 切片。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task ControllerFile_RangeFirstBytes_Returns206WithSlice()
        {
            await WithAppAsync(async (port, rootDirectory) =>
            {
                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await SendAsync(client, BaseUrl(port) + "/rangeprobe/filepayload", "bytes=0-9").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.PartialContent, response.StatusCode, "控制器 FileResult 命中 Range 时应返回 206");
                    Assert.AreEqual("bytes 0-9/64", Header(response, "Content-Range"), "控制器文件区间应写完整长度");

                    var body = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                    Assert.AreEqual(10, body.Length, "控制器文件 206 正文长度应等于区间长度");

                    for (var i = 0; i < body.Length; i++)
                    {
                        Assert.AreEqual((byte)i, body[i], "控制器文件 206 第 " + i + " 字节应与源数据一致");
                    }
                }
            }, new Dictionary<string, string>()).ConfigureAwait(false);
        }

        /// <summary>
        /// 控制器返回 BigDataResult 时也应按 Range 切片。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task ControllerBigData_RangeFirstBytes_Returns206WithSlice()
        {
            await WithAppAsync(async (port, rootDirectory) =>
            {
                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await SendAsync(client, BaseUrl(port) + "/bigdataprobe/payload", "bytes=0-9").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.PartialContent, response.StatusCode, "控制器 BigDataResult 命中 Range 时应返回 206");
                    Assert.AreEqual("bytes 0-9/" + (64 * 1024), Header(response, "Content-Range"), "大数据区间应写完整长度");

                    var body = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                    Assert.AreEqual(10, body.Length, "大数据 206 正文长度应等于区间长度");

                    for (var i = 0; i < body.Length; i++)
                    {
                        Assert.AreEqual((byte)(i % 251), body[i], "大数据 206 第 " + i + " 字节应与源数据一致");
                    }
                }
            }, new Dictionary<string, string>()).ConfigureAwait(false);
        }

        /// <summary>
        /// 生成进程内唯一的站点内文件名，避免命中 MapPath 的进程级结果缓存。
        /// </summary>
        /// <param name="prefix">文件名前缀</param>
        /// <param name="extension">扩展名，含点</param>
        /// <returns>站点内文件名</returns>
        private static string NewFileName(string prefix, string extension)
        {
            return prefix + "-" + Guid.NewGuid().ToString("N") + extension;
        }

        /// <summary>
        /// 建站点、铺文件、启动应用并在结束后统一停止与清理。
        /// </summary>
        /// <param name="body">用例正文</param>
        /// <param name="textFiles">需要写入的文件名与文本内容</param>
        /// <param name="createExtraFiles">额外的文件生成回调，可为 null</param>
        /// <returns>该用例的异步执行任务。</returns>
        private static async Task WithAppAsync(Func<int, string, Task> body, Dictionary<string, string> textFiles, Action<string> createExtraFiles = null)
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var rootDirectory = TestAppFactory.GetRootDirectory(rootName);

            try
            {
                if (textFiles != null)
                {
                    foreach (var pair in textFiles)
                    {
                        File.WriteAllText(Path.Combine(rootDirectory, pair.Key), pair.Value, new UTF8Encoding(false));
                    }
                }

                if (createExtraFiles != null)
                    createExtraFiles(rootDirectory);

                var app = TestAppFactory.CreateApp(rootName, port);

                try
                {
                    app.Start();

                    await body(port, rootDirectory).ConfigureAwait(false);
                }
                finally
                {
                    TestAppFactory.StopQuietly(app);
                }
            }
            finally
            {
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 生成 4MB 以上的大文件，第 i 个字节的值为 i % 251。
        /// </summary>
        /// <param name="rootDirectory">站点根目录</param>
        /// <param name="fileName">文件名</param>
        private static void CreateBigFile(string rootDirectory, string fileName)
        {
            var buffer = new byte[65536];

            using (var stream = new FileStream(Path.Combine(rootDirectory, fileName), FileMode.Create, FileAccess.Write))
            {
                var offset = 0;

                while (offset < BigFileLength)
                {
                    var count = Math.Min(buffer.Length, BigFileLength - offset);

                    for (var i = 0; i < count; i++)
                    {
                        buffer[i] = ExpectedBigFileByte(offset + i);
                    }

                    stream.Write(buffer, 0, count);

                    offset += count;
                }
            }
        }

        /// <summary>
        /// 大文件第 i 个字节的期望值，与生成规则一致。
        /// </summary>
        /// <param name="index">字节下标</param>
        /// <returns>期望字节</returns>
        private static byte ExpectedBigFileByte(long index)
        {
            return (byte)(index % 251);
        }

        /// <summary>
        /// 带 Range 头发 GET 请求。
        /// </summary>
        /// <param name="client">请求客户端</param>
        /// <param name="url">完整地址</param>
        /// <param name="range">Range 头值</param>
        /// <returns>响应</returns>
        private static Task<HttpResponseMessage> SendAsync(HttpClient client, string url, string range)
        {
            return TestAppFactory.SendWithReadyRetryAsync(client, () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);

                request.Headers.TryAddWithoutValidation("Range", range);

                return request;
            });
        }

        /// <summary>
        /// 取响应头值，响应头与正文头都查一遍。
        /// </summary>
        /// <param name="response">HTTP 响应</param>
        /// <param name="name">标头名</param>
        /// <returns>标头值，缺失时为「&lt;缺失&gt;」</returns>
        private static string Header(HttpResponseMessage response, string name)
        {
            if (response.Headers.TryGetValues(name, out var values))
                return string.Join(",", values);

            if (response.Content.Headers.TryGetValues(name, out values))
                return string.Join(",", values);

            return "<缺失>";
        }

        /// <summary>
        /// 用原始 socket 发 GET 并带上额外请求头，读完整响应。
        /// </summary>
        /// <param name="port">监听端口</param>
        /// <param name="path">请求路径</param>
        /// <param name="extraHeaders">额外请求头文本，每行以 CRLF 结尾</param>
        /// <returns>解析后的响应</returns>
        private static async Task<RawResponse> SendRawAsync(int port, string path, string extraHeaders)
        {
            using (var client = new TcpClient())
            {
                var deadline = DateTime.UtcNow.AddSeconds(10);

                while (true)
                {
                    try
                    {
                        await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
                        break;
                    }
                    catch (SocketException) when (DateTime.UtcNow < deadline)
                    {
                        await Task.Delay(100).ConfigureAwait(false);
                    }
                }

                using (var stream = client.GetStream())
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    var request = "GET " + path + " HTTP/1.1\r\nHost: " + TestAppFactory.LoopbackHost + ":" + port + "\r\n" + extraHeaders + "Connection: close\r\n\r\n";
                    var requestBytes = Encoding.ASCII.GetBytes(request);

                    await stream.WriteAsync(requestBytes, 0, requestBytes.Length, timeout.Token).ConfigureAwait(false);

                    using (var memory = new MemoryStream())
                    {
                        var buffer = new byte[8192];
                        int read;

                        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(false)) > 0)
                        {
                            memory.Write(buffer, 0, read);
                        }

                        return RawResponse.Parse(Encoding.UTF8.GetString(memory.ToArray()));
                    }
                }
            }
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

        /// <summary>
        /// 原始 HTTP 响应：状态码、响应头与正文。
        /// </summary>
        private sealed class RawResponse
        {
            /// <summary>
            /// 响应状态码。
            /// </summary>
            public int StatusCode { get; private set; }

            /// <summary>
            /// 响应头，键名不区分大小写。
            /// </summary>
            public Dictionary<string, string> Headers { get; private set; }

            /// <summary>
            /// 响应正文。
            /// </summary>
            public string Body { get; private set; }

            /// <summary>
            /// 解析原始响应文本。
            /// </summary>
            /// <param name="text">完整响应文本</param>
            /// <returns>解析后的响应</returns>
            public static RawResponse Parse(string text)
            {
                var headerEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                var headerText = headerEnd >= 0 ? text.Substring(0, headerEnd) : text;
                var lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);

                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                for (var i = 1; i < lines.Length; i++)
                {
                    var separator = lines[i].IndexOf(':');

                    if (separator <= 0) continue;

                    headers[lines[i].Substring(0, separator).Trim()] = lines[i].Substring(separator + 1).Trim();
                }

                var parts = lines[0].Split(' ');

                if (parts.Length < 2)
                {
                    throw new FormatException("响应状态行不符合 HTTP 格式：" + lines[0]);
                }

                return new RawResponse
                {
                    StatusCode = int.Parse(parts[1]),
                    Headers = headers,
                    Body = headerEnd >= 0 ? text.Substring(headerEnd + 4) : string.Empty
                };
            }

            /// <summary>
            /// 取响应头值。
            /// </summary>
            /// <param name="name">标头名</param>
            /// <returns>标头值，缺失时为「&lt;缺失&gt;」</returns>
            public string Header(string name)
            {
                return Headers.TryGetValue(name, out var value) ? value : "<缺失>";
            }
        }
    }
}