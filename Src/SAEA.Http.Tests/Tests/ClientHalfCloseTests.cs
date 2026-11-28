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
 *文件名： ClientHalfCloseTests
 *版本号： net10.0
 *唯一标识：3d20aaac-62e8-4f06-950a-c8752732e29c
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/28 07:07:26
 *描述：客户端发完请求后只半关写方向仍应收到完整响应的端到端用例
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/28 07:07:26
 *修改人： yswenli
 *版本号： net10.0
 *描述：新增，覆盖大文件静态响应在客户端半关写方向后的完整交付
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
    /// 客户端发完请求后立刻 shutdown(SHUT_WR) 半关写方向：服务端读到 EOF 后仍要把完整响应写完。
    /// </summary>
    [TestClass]
    public class ClientHalfCloseTests
    {
        private const int BodySize = 4 * 1024 * 1024 + 16;

        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(20);

        /// <summary>
        /// 4MB 以上静态文件走流式结果：连续三轮新建连接、写请求后立刻半关，均应收到完整正文。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpHost_ClientHalfClosesWriteDirection_StillReceivesFullResponse()
        {
            var rootName = "saea-http-half-close-" + Guid.NewGuid().ToString("N");

            var rootPath = Path.Combine(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory), rootName);

            var fileName = "large-" + Guid.NewGuid().ToString("N") + ".bin";

            var expected = CreatePayload();

            Directory.CreateDirectory(rootPath);

            File.WriteAllBytes(Path.Combine(rootPath, fileName), expected);

            var port = GetFreePort();

            var host = new WebHost(root: rootName, port: port, isStaticsCached: false, isZiped: false);

            try
            {
                host.Start();

                for (var round = 0; round < 3; round++)
                {
                    using (var client = await ConnectAsync(port).ConfigureAwait(false))
                    using (var stream = client.GetStream())
                    {
                        // 每轮都设总截止时间，避免服务端异常时用例永久挂住。
                        var deadline = DateTime.UtcNow + WaitTimeout;

                        client.ReceiveTimeout = (int)WaitTimeout.TotalMilliseconds;

                        var request = BuildRequest("/" + fileName, port);

                        var requestBytes = Encoding.ASCII.GetBytes(request);

                        await stream.WriteAsync(requestBytes, 0, requestBytes.Length)
                            .WaitAsync(Remaining(deadline)).ConfigureAwait(false);

                        await stream.FlushAsync().WaitAsync(Remaining(deadline)).ConfigureAwait(false);

                        // 请求写完后立刻半关写方向：服务端会读到 EOF，但响应仍应完整返回。
                        client.Client.Shutdown(SocketShutdown.Send);

                        var response = await ReadResponseAsync(stream, deadline).ConfigureAwait(false);

                        StringAssert.StartsWith(
                            response.Header,
                            "HTTP/1.1 200",
                            "第 " + (round + 1) + " 轮应返回 200 状态行。");

                        Assert.AreEqual(
                            expected.Length,
                            response.ContentLength,
                            "第 " + (round + 1) + " 轮 Content-Length 应等于文件长度。");

                        AssertBodyEquals(expected, response.Body);
                    }
                }
            }
            finally
            {
                StopQuietly(host);
                DeleteDirectory(rootPath);
            }
        }

        /// <summary>生成确定性的伪随机正文，包含 0 与 0xFF 等边界字节，便于逐字节比对。</summary>
        private static byte[] CreatePayload()
        {
            var data = new byte[BodySize];

            var state = 0x9E3779B9u;

            for (var i = 0; i < data.Length; i++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                data[i] = (byte)state;
            }

            return data;
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
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

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

        /// <summary>拼一条最小 GET 请求：声明短连接，正文由服务端写完。</summary>
        private static string BuildRequest(string path, int port)
        {
            var builder = new StringBuilder();

            builder.Append("GET ").Append(path).Append(" HTTP/1.1\r\n");
            builder.Append("Host: 127.0.0.1:").Append(port).Append("\r\n");
            builder.Append("Connection: close\r\n");
            builder.Append("\r\n");

            return builder.ToString();
        }

        /// <summary>
        /// 逐字节读到报头结束标记，再按 Content-Length 读满报文体；每次读都受总截止时间约束。
        /// </summary>
        private static async Task<RawResponse> ReadResponseAsync(NetworkStream stream, DateTime deadline)
        {
            var head = new List<byte>(512);

            var one = new byte[1];

            while (true)
            {
                var read = await stream.ReadAsync(one, 0, 1)
                    .WaitAsync(Remaining(deadline)).ConfigureAwait(false);

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

            var contentLength = ParseContentLength(header);

            if (contentLength <= 0)
                return new RawResponse { Header = header, ContentLength = contentLength, Body = Array.Empty<byte>() };

            var body = new byte[contentLength];

            var offset = 0;

            while (offset < contentLength)
            {
                var read = await stream.ReadAsync(body, offset, contentLength - offset)
                    .WaitAsync(Remaining(deadline)).ConfigureAwait(false);

                if (read == 0)
                    throw new IOException("连接在响应报文体读完前被关闭。");

                offset += read;
            }

            return new RawResponse { Header = header, ContentLength = contentLength, Body = body };
        }

        /// <summary>从报头里取 Content-Length；缺失时返回 -1。</summary>
        private static int ParseContentLength(string header)
        {
            var lines = header.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var index = line.IndexOf(':');

                if (index <= 0)
                    continue;

                if (line.Substring(0, index).Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                    return int.Parse(line.Substring(index + 1).Trim());
            }

            return -1;
        }

        /// <summary>逐字节比对正文；不直接把 4MB 数组交给集合断言，避免失败信息爆量。</summary>
        private static void AssertBodyEquals(byte[] expected, byte[] actual)
        {
            Assert.AreEqual(expected.Length, actual.Length, "正文长度应与文件长度一致。");

            for (var i = 0; i < expected.Length; i++)
            {
                if (expected[i] != actual[i])
                    Assert.Fail("正文第 " + i + " 字节与文件不一致：期望 " + expected[i] + "，实际 " + actual[i] + "。");
            }
        }

        /// <summary>把总截止时间换算成单次读写的等待上限。</summary>
        private static TimeSpan Remaining(DateTime deadline)
        {
            var remaining = deadline - DateTime.UtcNow;

            if (remaining <= TimeSpan.Zero)
                throw new TimeoutException("请求超出总截止时间。");

            return remaining;
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

        /// <summary>删除临时站点根目录。</summary>
        private static void DeleteDirectory(string directory)
        {
            try
            {
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

        /// <summary>解析后的原始响应：报头文本、Content-Length 与读满的报文体。</summary>
        private sealed class RawResponse
        {
            /// <summary>完整报头文本，含结束空行。</summary>
            public string Header { get; set; }

            /// <summary>报头声明的正文长度。</summary>
            public int ContentLength { get; set; }

            /// <summary>报文体字节。</summary>
            public byte[] Body { get; set; }
        }
    }
}