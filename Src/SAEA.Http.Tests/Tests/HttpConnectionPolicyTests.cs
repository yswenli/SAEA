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
 *文件名： HttpConnectionPolicyTests
 *版本号： net10.0
 *唯一标识：a022bd94-0d2d-4a43-a23d-16b1ba8d006a
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/18 13:26:01
 *描述：真实 TCP 连接上的 Connection 策略用例：响应头与实际连接行为必须一致
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/18 13:26:01
 *修改人： yswenli
 *版本号： net10.0
 *描述：新增，覆盖 HTTP/1.1 默认保活、请求声明 close 即关闭、HTTP/1.0 默认关闭与显式保活
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
    /// 连接策略用例：起一个真实的 SAEA.Http 宿主，用原始 TCP 请求验证响应头与连接副作用一致。
    /// </summary>
    [TestClass]
    public class HttpConnectionPolicyTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// HTTP/1.1 不带 Connection 头时应回 keep-alive，且同一连接能连续完成两个请求。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpConnectionPolicy_Http11WithoutConnection_KeepsConnectionAlive()
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

                    await SendAsync(stream, BuildRequest("HTTP/1.1", path, port, null)).ConfigureAwait(false);

                    var first = await ReadResponseAsync(stream).ConfigureAwait(false);

                    StringAssert.StartsWith(first.Header, "HTTP/1.1 404", "首个请求应返回 404 状态行。");
                    Assert.IsTrue(HasHeader(first.Header, "Connection", "keep-alive"), "HTTP/1.1 未声明 close 时应回 keep-alive。");

                    // 同一连接上的第二个请求必须成功，说明服务端确实没有关闭连接。
                    await SendAsync(stream, BuildRequest("HTTP/1.1", path, port, null)).ConfigureAwait(false);

                    var second = await ReadResponseAsync(stream).ConfigureAwait(false);

                    StringAssert.StartsWith(second.Header, "HTTP/1.1 404", "保活连接上的第二个请求应正常返回。");
                    Assert.IsTrue(HasHeader(second.Header, "Connection", "keep-alive"), "第二个响应同样应回 keep-alive。");
                }
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>
        /// 请求声明 Connection: close 时应回 close，并在响应写完后真正关闭连接。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpConnectionPolicy_RequestClose_RespondsCloseAndDisconnects()
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

                    await SendAsync(stream, BuildRequest("HTTP/1.1", path, port, "close")).ConfigureAwait(false);

                    var response = await ReadResponseAsync(stream).ConfigureAwait(false);

                    StringAssert.StartsWith(response.Header, "HTTP/1.1 404", "应返回 404 状态行。");
                    Assert.IsTrue(HasHeader(response.Header, "Connection", "close"), "请求声明 close 时应回 close。");

                    await AssertClosedAsync(stream).ConfigureAwait(false);
                }
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>
        /// HTTP/1.0 不带 keep-alive 时应回 close 并关闭连接：1.0 默认是短连接。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpConnectionPolicy_Http10WithoutKeepAlive_RespondsClose()
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

                    await SendAsync(stream, BuildRequest("HTTP/1.0", path, port, null)).ConfigureAwait(false);

                    var response = await ReadResponseAsync(stream).ConfigureAwait(false);

                    StringAssert.StartsWith(response.Header, "HTTP/1.0 404", "HTTP/1.0 请求的响应协议应与请求一致。");
                    Assert.IsTrue(HasHeader(response.Header, "Connection", "close"), "HTTP/1.0 未声明 keep-alive 时应回 close。");

                    await AssertClosedAsync(stream).ConfigureAwait(false);
                }
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>
        /// HTTP/1.0 显式声明 keep-alive 时应回 keep-alive，且同一连接可继续处理下一个请求。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpConnectionPolicy_Http10WithKeepAlive_KeepsConnectionAlive()
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

                    await SendAsync(stream, BuildRequest("HTTP/1.0", path, port, "keep-alive")).ConfigureAwait(false);

                    var first = await ReadResponseAsync(stream).ConfigureAwait(false);

                    StringAssert.StartsWith(first.Header, "HTTP/1.0 404", "应返回 404 状态行。");
                    Assert.IsTrue(HasHeader(first.Header, "Connection", "keep-alive"), "HTTP/1.0 显式 keep-alive 时应回 keep-alive。");

                    await SendAsync(stream, BuildRequest("HTTP/1.0", path, port, "keep-alive")).ConfigureAwait(false);

                    var second = await ReadResponseAsync(stream).ConfigureAwait(false);

                    StringAssert.StartsWith(second.Header, "HTTP/1.0 404", "保活连接上的第二个请求应正常返回。");
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
            return "saea-http-policy-" + Guid.NewGuid().ToString("N");
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

        /// <summary>按协议与连接选项拼一条最小 GET 请求；协议为 1.0 时不发 Host。</summary>
        private static string BuildRequest(string protocol, string path, int port, string connection)
        {
            var builder = new StringBuilder();

            builder.Append("GET ").Append(path).Append(' ').Append(protocol).Append("\r\n");

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

        /// <summary>判断报头中是否含指定名字与值的字段，名字与值都不区分大小写。</summary>
        private static bool HasHeader(string header, string name, string value)
        {
            var lines = header.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var index = line.IndexOf(':');

                if (index <= 0)
                    continue;

                if (!line.Substring(0, index).Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                    continue;

                return line.Substring(index + 1).Trim().Equals(value, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        /// <summary>确认服务端已关闭连接：读到 0 字节即对端发出 FIN。</summary>
        private static async Task AssertClosedAsync(NetworkStream stream)
        {
            var buffer = new byte[1];

            var read = await stream.ReadAsync(buffer, 0, 1).WaitAsync(WaitTimeout).ConfigureAwait(false);

            Assert.AreEqual(0, read, "响应后服务端应关闭连接。");
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

        /// <summary>解析后的原始响应：报头文本与按 Content-Length 读满的报文体。</summary>
        private sealed class RawResponse
        {
            /// <summary>完整报头文本，含结束空行。</summary>
            public string Header { get; set; }

            /// <summary>报文体字节。</summary>
            public byte[] Body { get; set; }
        }
    }
}