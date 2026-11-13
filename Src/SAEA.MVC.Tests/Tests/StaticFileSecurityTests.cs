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
*文件名： StaticFileSecurityTests
*版本号： net10.0
*唯一标识：f8fbfc0e-796e-4834-9600-10a5b7416f47
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 12:04:00
*描述：静态文件路径穿越回归用例，用原始 socket 发送未归一化的请求行
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 13:14:00
*修改人： yswenli
*版本号： net10.0
*描述：穿越用例只保留字面 .. 与反斜杠两种真实形式，%2e%2e 另立断言说明服务端不解码
*
*****************************************************************************/
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.Tests.Tests.Support;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// 静态文件路径穿越回归用例：用原始 socket 发送请求行，避免客户端提前归一化路径。
    /// </summary>
    [TestClass]
    public class StaticFileSecurityTests
    {
        /// <summary>
        /// 父目录越界文件在字面 .. 与反斜杠两种真实穿越形式下都必须 404，且正文不含文件内容；
        /// %2e%2e 因服务端不解码，只按字面文件名处理，同样读不到站外文件。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task StaticFile_ParentTraversalAndEncodedSegment_Returns404WithoutSecretContent()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var rootDirectory = TestAppFactory.GetRootDirectory(rootName);
            var parentDirectory = Directory.GetParent(rootDirectory).FullName;

            var secretName = "secret-" + Guid.NewGuid().ToString("N") + ".txt";
            var secretContent = "secret-content-" + Guid.NewGuid().ToString("N");
            var secretPath = Path.Combine(parentDirectory, secretName);

            File.WriteAllText(secretPath, secretContent, new UTF8Encoding(false));

            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                var paths = new[]
                {
                    "/../" + secretName,
                    "/..\\" + secretName
                };

                foreach (var path in paths)
                {
                    var response = await SendRawAsync(port, path).ConfigureAwait(false);

                    Assert.AreEqual(404, response.StatusCode, "GET " + path + " 应返回 404，实际响应行：" + response.StatusLine);
                    Assert.IsFalse(
                        response.Body.Contains(secretContent, StringComparison.Ordinal),
                        "GET " + path + " 的正文不得包含站点根目录之外的文件内容");
                }

                // 服务端全程不解码 URL，%2e%2e 只是一个普通的文件名字段，响应内容与本轮修复的归一化逻辑无关；
                // 这里只校验读不到父目录文件，不把它当成编码形态的穿越回归。
                var encodedPath = "/%2e%2e/" + secretName;
                var encodedResponse = await SendRawAsync(port, encodedPath).ConfigureAwait(false);

                Assert.AreEqual(404, encodedResponse.StatusCode, "GET " + encodedPath + " 应返回 404，实际响应行：" + encodedResponse.StatusLine);
                Assert.IsFalse(
                    encodedResponse.Body.Contains(secretContent, StringComparison.Ordinal),
                    "GET " + encodedPath + " 的正文不得包含站点根目录之外的文件内容");
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);

                try
                {
                    File.Delete(secretPath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        /// <summary>
        /// 单点路径、普通文件与默认首页仍返回 200，路径归一化不破坏正常静态文件访问。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task StaticFile_NormalAndDotPaths_Return200()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var rootDirectory = TestAppFactory.GetRootDirectory(rootName);

            var fileName = "ok-" + Guid.NewGuid().ToString("N") + ".txt";
            var fileContent = "ok-content-" + Guid.NewGuid().ToString("N");
            var indexContent = "<html>index-" + Guid.NewGuid().ToString("N") + "</html>";

            File.WriteAllText(Path.Combine(rootDirectory, fileName), fileContent, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(rootDirectory, "index.html"), indexContent, new UTF8Encoding(false));

            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                var dotResponse = await SendRawAsync(port, "/./" + fileName).ConfigureAwait(false);

                Assert.AreEqual(200, dotResponse.StatusCode, "GET /./" + fileName + " 应返回 200，实际响应行：" + dotResponse.StatusLine);
                Assert.AreEqual(fileContent, dotResponse.Body, "单点路径归一化后应返回文件原内容");

                var fileResponse = await SendRawAsync(port, "/" + fileName).ConfigureAwait(false);

                Assert.AreEqual(200, fileResponse.StatusCode, "GET /" + fileName + " 应返回 200，实际响应行：" + fileResponse.StatusLine);
                Assert.AreEqual(fileContent, fileResponse.Body, "普通静态文件应返回原内容");

                var indexResponse = await SendRawAsync(port, "/").ConfigureAwait(false);

                Assert.AreEqual(200, indexResponse.StatusCode, "GET / 应返回 200，实际响应行：" + indexResponse.StatusLine);
                Assert.AreEqual(indexContent, indexResponse.Body, "根路径应返回默认首页 index.html 的内容");
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 用原始 socket 发送请求行并读完整响应，连接拒绝时在 10 秒内重试。
        /// </summary>
        /// <param name="port">监听端口</param>
        /// <param name="rawPath">未归一化的请求目标</param>
        /// <returns>解析后的响应</returns>
        private static async Task<RawHttpResponse> SendRawAsync(int port, string rawPath)
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
                    var request = "GET " + rawPath + " HTTP/1.1\r\nHost: " + TestAppFactory.LoopbackHost + ":" + port + "\r\nConnection: close\r\n\r\n";
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

                        return RawHttpResponse.Parse(Encoding.UTF8.GetString(memory.ToArray()));
                    }
                }
            }
        }

        /// <summary>
        /// 原始 HTTP 响应：状态行、状态码与正文。
        /// </summary>
        private sealed class RawHttpResponse
        {
            /// <summary>
            /// 响应状态行。
            /// </summary>
            public string StatusLine { get; private set; }

            /// <summary>
            /// 响应状态码。
            /// </summary>
            public int StatusCode { get; private set; }

            /// <summary>
            /// 响应正文。
            /// </summary>
            public string Body { get; private set; }

            /// <summary>
            /// 解析原始响应文本。
            /// </summary>
            /// <param name="text">完整响应文本</param>
            /// <returns>解析后的响应</returns>
            public static RawHttpResponse Parse(string text)
            {
                var headerEnd = text.IndexOf("\r\n", StringComparison.Ordinal);

                var statusLine = headerEnd >= 0 ? text.Substring(0, headerEnd) : text;

                var parts = statusLine.Split(' ');

                if (parts.Length < 2)
                {
                    throw new FormatException("响应状态行不符合 HTTP 格式：" + statusLine);
                }

                var bodyStart = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);

                return new RawHttpResponse
                {
                    StatusLine = statusLine,
                    StatusCode = int.Parse(parts[1]),
                    Body = bodyStart >= 0 ? text.Substring(bodyStart + 4) : string.Empty
                };
            }
        }
    }
}
