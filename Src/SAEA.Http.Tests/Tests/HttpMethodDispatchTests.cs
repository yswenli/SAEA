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
 *命名空间：SAEA.Http.Tests
 *文件名： HttpMethodDispatchTests
 *版本号： net10.0
 *唯一标识：cfcf8c93-4d26-4a28-aec5-0d02e5065085
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/19 18:14:08
 *描述：真实 TCP 请求上的方法分发用例：HEAD 只回报头、PATCH 走路由、未知方法回 501
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/19 18:14:08
 *修改人： yswenli
 *版本号： net10.0
 *描述：新增，覆盖 HEAD 只回报头、PATCH 正常路由与未知方法回 501
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

namespace SAEA.Http.Tests
{
    /// <summary>
    /// 方法分发用例：起一个真实的 SAEA.Http 宿主，用原始 TCP 请求验证 HEAD、PATCH 与未知方法的应答。
    /// </summary>
    [TestClass]
    public class HttpMethodDispatchTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

        private const string StaticFileBody = "head-probe-body";

        /// <summary>
        /// HEAD 与 GET 走同一条静态文件路由：GET 带回正文，HEAD 只回报头且 Content-Length 与 GET 一致。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpMethodDispatch_HeadOnStaticFile_ReturnsHeadersOnly()
        {
            var rootName = NewRootName();

            var port = GetFreePort();

            var path = "/head-probe-" + Guid.NewGuid().ToString("N") + ".txt";

            var host = new WebHost(root: rootName, port: port, isStaticsCached: false, isZiped: false);

            try
            {
                WriteStaticFile(rootName, path, StaticFileBody);

                host.Start();

                var bodyLength = Encoding.ASCII.GetByteCount(StaticFileBody);

                using (var client = await ConnectAsync(port).ConfigureAwait(false))
                using (var stream = client.GetStream())
                {
                    await SendAsync(stream, BuildRequest("GET", "HTTP/1.1", path, port, "close")).ConfigureAwait(false);

                    var getResponse = await ReadResponseAsync(stream).ConfigureAwait(false);

                    StringAssert.StartsWith(getResponse.Header, "HTTP/1.1 200", "GET 静态文件应返回 200 状态行。");
                    Assert.AreEqual(bodyLength.ToString(), GetHeaderValue(getResponse.Header, "Content-Length"), "GET 的 Content-Length 应等于文件字节数。");
                    Assert.AreEqual(bodyLength, getResponse.Body.Length, "GET 应带回完整文件正文。");
                    Assert.AreEqual(StaticFileBody, Encoding.ASCII.GetString(getResponse.Body), "GET 正文应与文件内容一致。");
                }

                using (var client = await ConnectAsync(port).ConfigureAwait(false))
                using (var stream = client.GetStream())
                {
                    await SendAsync(stream, BuildRequest("HEAD", "HTTP/1.1", path, port, "close")).ConfigureAwait(false);

                    // HEAD 不能按 Content-Length 读正文：服务端只发报头就关连接，读到 FIN 为止
                    var headResponse = await ReadHeadersToCloseAsync(stream).ConfigureAwait(false);

                    StringAssert.StartsWith(headResponse.Header, "HTTP/1.1 200", "HEAD 静态文件应返回 200 状态行。");
                    Assert.AreEqual(bodyLength.ToString(), GetHeaderValue(headResponse.Header, "Content-Length"), "HEAD 的 Content-Length 仍应等于等价 GET 的正文长度。");
                    Assert.AreEqual(0, headResponse.Body.Length, "HEAD 响应在报头之后不应再发送正文。");
                }
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>
        /// PATCH 应进入与 GET 相同的路由分支：访问不存在的路径时两者都回 404，而不是落进未知方法的兜底。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpMethodDispatch_PatchIsRouted_Returns404LikeGet()
        {
            var rootName = NewRootName();

            var port = GetFreePort();

            var host = new WebHost(root: rootName, port: port, isStaticsCached: false, isZiped: false);

            try
            {
                host.Start();

                var path = "/missing-" + Guid.NewGuid().ToString("N");

                int getStatus;

                using (var client = await ConnectAsync(port).ConfigureAwait(false))
                using (var stream = client.GetStream())
                {
                    await SendAsync(stream, BuildRequest("GET", "HTTP/1.1", path, port, "close")).ConfigureAwait(false);

                    var response = await ReadResponseAsync(stream).ConfigureAwait(false);

                    getStatus = GetStatusCode(response.Header);

                    Assert.AreEqual(404, getStatus, "GET 不存在的路径应返回 404。");
                }

                using (var client = await ConnectAsync(port).ConfigureAwait(false))
                using (var stream = client.GetStream())
                {
                    await SendAsync(stream, BuildRequest("PATCH", "HTTP/1.1", path, port, "close")).ConfigureAwait(false);

                    var response = await ReadResponseAsync(stream).ConfigureAwait(false);

                    var patchStatus = GetStatusCode(response.Header);

                    Assert.AreEqual(getStatus, patchStatus, "PATCH 应与 GET 走同一条路由，得到相同状态码。");
                    Assert.AreEqual(404, patchStatus, "PATCH 不应被当成未知方法处理。");
                }
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>
        /// 未知方法应回 501（方法未实现），不能再用 505（HTTP 版本不支持）作答。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpMethodDispatch_UnknownMethod_Returns501()
        {
            var rootName = NewRootName();

            var port = GetFreePort();

            var host = new WebHost(root: rootName, port: port, isStaticsCached: false, isZiped: false);

            try
            {
                host.Start();

                using (var client = await ConnectAsync(port).ConfigureAwait(false))
                using (var stream = client.GetStream())
                {
                    var path = "/missing-" + Guid.NewGuid().ToString("N");

                    await SendAsync(stream, BuildRequest("TRACE", "HTTP/1.1", path, port, "close")).ConfigureAwait(false);

                    var response = await ReadResponseAsync(stream).ConfigureAwait(false);

                    Assert.AreEqual(501, GetStatusCode(response.Header), "未知方法应返回 501，而不是 505。");
                    StringAssert.Contains(Encoding.UTF8.GetString(response.Body), "不支持的请求方式", "501 的正文应说明请求方式不受支持。");
                }
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>生成只含安全字符的站点根目录名，避免 HttpUtility 归一化时报错。</summary>
        private static string NewRootName()
        {
            return "saea-http-method-" + Guid.NewGuid().ToString("N");
        }

        /// <summary>取一个当前空闲的回环端口，先占后放，避免与其它用例撞端口。</summary>
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

        /// <summary>连接宿主：服务可能尚未就绪，按 50 毫秒间隔重试，最长 10 秒。</summary>
        private static async Task<TcpClient> ConnectAsync(int port)
        {
            var deadline = DateTime.UtcNow + WaitTimeout;

            while (true)
            {
                var client = new TcpClient();

                try
                {
                    await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);

                    return client;
                }
                catch (SocketException) when (DateTime.UtcNow < deadline)
                {
                    client.Dispose();

                    await Task.Delay(50).ConfigureAwait(false);
                }
            }
        }

        /// <summary>按方法、协议与连接选项拼一条最小请求；协议为 1.0 时不发 Host。</summary>
        private static string BuildRequest(string method, string protocol, string path, int port, string connection)
        {
            var builder = new StringBuilder();

            builder.Append(method).Append(' ').Append(path).Append(' ').Append(protocol).Append("\r\n");

            if (!string.Equals(protocol, "HTTP/1.0", StringComparison.OrdinalIgnoreCase))
                builder.Append("Host: 127.0.0.1:").Append(port).Append("\r\n");

            if (!string.IsNullOrEmpty(connection))
                builder.Append("Connection: ").Append(connection).Append("\r\n");

            builder.Append("\r\n");

            return builder.ToString();
        }

        /// <summary>把请求字节写到连接并冲刷。</summary>
        private static async Task SendAsync(NetworkStream stream, string request)
        {
            var bytes = Encoding.ASCII.GetBytes(request);

            await stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);

            await stream.FlushAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// 逐字节读到报头结束标记，再按 Content-Length 读满报文体，避免把残留字节留给下一个响应。
        /// </summary>
        private static async Task<RawResponse> ReadResponseAsync(NetworkStream stream)
        {
            var head = new List<byte>(512);

            var one = new byte[1];

            while (true)
            {
                var read = await stream.ReadAsync(one, 0, 1).WaitAsync(WaitTimeout).ConfigureAwait(false);

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

            var header = Encoding.ASCII.GetString(head.ToArray());

            var body = await ReadBodyAsync(stream, header).ConfigureAwait(false);

            return new RawResponse { Header = header, Body = body };
        }

        /// <summary>按报头里的 Content-Length 读满报文体；没有正文时返回空数组。</summary>
        private static async Task<byte[]> ReadBodyAsync(NetworkStream stream, string header)
        {
            var length = 0;

            var lines = header.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var index = line.IndexOf(':');

                if (index <= 0)
                    continue;

                if (line.Substring(0, index).Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    length = int.Parse(line.Substring(index + 1).Trim());
                    break;
                }
            }

            if (length <= 0)
                return Array.Empty<byte>();

            var body = new byte[length];

            var offset = 0;

            while (offset < length)
            {
                var read = await stream.ReadAsync(body, offset, length - offset).WaitAsync(WaitTimeout).ConfigureAwait(false);

                if (read == 0)
                    throw new IOException("连接在响应报文体读完前被关闭。");

                offset += read;
            }

            return body;
        }

        /// <summary>
        /// 读到连接关闭为止再切分报头与报尾：HEAD 的 Content-Length 只描述等价 GET 的正文长度，
        /// 服务端不会真的发送正文，按长度读会一直等到超时。
        /// </summary>
        private static async Task<RawResponse> ReadHeadersToCloseAsync(NetworkStream stream)
        {
            var received = new List<byte>(1024);

            var chunk = new byte[1024];

            while (true)
            {
                var read = await stream.ReadAsync(chunk, 0, chunk.Length).WaitAsync(WaitTimeout).ConfigureAwait(false);

                if (read == 0)
                    break;

                for (var i = 0; i < read; i++)
                    received.Add(chunk[i]);
            }

            var bytes = received.ToArray();

            var terminator = Encoding.ASCII.GetBytes("\r\n\r\n");

            var index = IndexOfBytes(bytes, terminator);

            if (index < 0)
                throw new IOException("连接关闭前未读到完整的响应报头。");

            var headerLength = index + terminator.Length;

            var header = Encoding.ASCII.GetString(bytes, 0, headerLength);

            var body = new byte[bytes.Length - headerLength];

            Array.Copy(bytes, headerLength, body, 0, body.Length);

            return new RawResponse { Header = header, Body = body };
        }

        /// <summary>在字节序列中查找首个匹配的子序列，未找到返回 -1。</summary>
        private static int IndexOfBytes(byte[] buffer, byte[] pattern)
        {
            var limit = buffer.Length - pattern.Length;

            for (var i = 0; i <= limit; i++)
            {
                var matched = true;

                for (var j = 0; j < pattern.Length; j++)
                {
                    if (buffer[i + j] != pattern[j])
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                    return i;
            }

            return -1;
        }

        /// <summary>取报头中指定字段的值，名字不区分大小写，缺失时返回 null。</summary>
        private static string GetHeaderValue(string header, string name)
        {
            var lines = header.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var index = line.IndexOf(':');

                if (index <= 0)
                    continue;

                if (line.Substring(0, index).Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                    return line.Substring(index + 1).Trim();
            }

            return null;
        }

        /// <summary>从状态行解析三位状态码，形如 "HTTP/1.1 404 Not Found"。</summary>
        private static int GetStatusCode(string header)
        {
            var lines = header.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            Assert.IsTrue(lines.Length > 0, "响应缺少状态行。");

            var parts = lines[0].Split(' ');

            Assert.IsTrue(parts.Length >= 2, "状态行格式不正确：" + lines[0]);

            return int.Parse(parts[1]);
        }

        /// <summary>把静态文件写到站点根目录，目录位于运行基目录的上一级，与 HttpUtility 的解析规则一致。</summary>
        private static void WriteStaticFile(string rootName, string urlPath, string content)
        {
            var directory = Path.Combine(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory), rootName);

            Directory.CreateDirectory(directory);

            var fileName = urlPath.TrimStart('/');

            File.WriteAllText(Path.Combine(directory, fileName), content, Encoding.ASCII);
        }

        /// <summary>停止宿主并吞掉异常：收尾失败不应覆盖用例结论。</summary>
        private static void StopQuietly(WebHost host)
        {
            if (host == null)
                return;

            try
            {
                host.Stop();
            }
            catch (Exception)
            {
            }
        }

        /// <summary>删除临时站点根目录；目录位于运行基目录的上一级，与 HttpUtility 的解析规则一致。</summary>
        private static void DeleteRoot(string rootName)
        {
            try
            {
                var directory = Path.Combine(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory), rootName);

                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        /// <summary>解析后的原始响应：报头文本与报头之后的字节。</summary>
        private sealed class RawResponse
        {
            /// <summary>完整报头文本，含结束空行。</summary>
            public string Header { get; set; }

            /// <summary>报头之后的字节；HEAD 场景应为空。</summary>
            public byte[] Body { get; set; }
        }
    }
}