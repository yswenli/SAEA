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
 *文件名： HttpResponseWritePathTests
 *版本号： net10.0
 *唯一标识：c0f5f7c2-9f4e-4d0a-a1f3-6b7d2e8a5c61
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/18 14:41:09
 *描述：响应写出路径用例：真实 TCP 连接上验证响应完整、顺序正确且关闭前排空
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/19 20:26:00
 *修改人： yswenli
 *版本号： net10.0
 *描述：补未声明 IReusableHttpContext 的上下文按请求新建的用例
 *
 *****************************************************************************/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.Http.Base;
using SAEA.Http.Model;
using SAEA.Sockets.Pipeline;

namespace SAEA.Http.Tests
{
    /// <summary>
    /// 响应写出路径用例：起一个真实的 SAEA.Http 宿主，用原始 TCP 请求验证响应完整性、顺序与关闭时机。
    /// </summary>
    [TestClass]
    public class HttpResponseWritePathTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(20);

        /// <summary>
        /// 多条连接并发各发多个保活请求时，每个响应都完整且与请求一一对应，不串响应也不截断。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpResponseWrite_ManyKeepAliveConnections_ResponsesStayPaired()
        {
            const int connectionCount = 8;

            const int requestsPerConnection = 10;

            var rootName = NewRootName();

            var port = GetFreePort();

            var host = CreateHost(port, rootName, context =>
            {
                context.Response.Write("ok:" + context.Request.Url);
                context.Response.SetHeader(ResponseHeaderType.Connection, "keep-alive");
                context.Response.Send();
            });

            try
            {
                host.Start();

                var workers = new Task[connectionCount];

                for (var i = 0; i < connectionCount; i++)
                {
                    var connectionIndex = i;

                    workers[i] = Task.Run(async () =>
                    {
                        using (var client = await ConnectAsync(port).ConfigureAwait(false))
                        using (var stream = client.GetStream())
                        {
                            for (var j = 0; j < requestsPerConnection; j++)
                            {
                                var path = "/pair-" + connectionIndex + "-" + j;

                                await SendAsync(stream, BuildRequest(path, port, null)).ConfigureAwait(false);

                                var response = await ReadResponseAsync(stream).ConfigureAwait(false);

                                StringAssert.StartsWith(response.Header, "HTTP/1.1 200", "每个保活请求都应返回 200。");
                                Assert.AreEqual(
                                    "ok:" + path,
                                    Encoding.UTF8.GetString(response.Body),
                                    "响应体必须与请求一一对应，不能串响应。");
                            }
                        }
                    });
                }

                await Task.WhenAll(workers).WaitAsync(WaitTimeout).ConfigureAwait(false);
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>
        /// 未声明 IReusableHttpContext 的上下文仍按请求新建：同一条保活连接上的两次请求必须落到不同实例。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpContextReuse_NonOptInContext_CreatesNewInstancePerRequest()
        {
            var rootName = NewRootName();

            var port = GetFreePort();

            var host = CreateHost(port, rootName, context =>
            {
                // 把实例标识写进正文：同一连接上两条正文不同即说明不是同一个上下文
                context.Response.Write(RuntimeHelpers.GetHashCode(context).ToString(CultureInfo.InvariantCulture));
                context.Response.SetHeader(ResponseHeaderType.Connection, "keep-alive");
                context.Response.Send();
            });

            try
            {
                host.Start();

                using (var client = await ConnectAsync(port).ConfigureAwait(false))
                using (var stream = client.GetStream())
                {
                    await SendAsync(stream, BuildRequest("/non-optin-1", port, null)).ConfigureAwait(false);

                    var first = await ReadResponseAsync(stream).ConfigureAwait(false);

                    await SendAsync(stream, BuildRequest("/non-optin-2", port, null)).ConfigureAwait(false);

                    var second = await ReadResponseAsync(stream).ConfigureAwait(false);

                    StringAssert.StartsWith(first.Header, "HTTP/1.1 200", "第一条请求应返回 200。");
                    StringAssert.StartsWith(second.Header, "HTTP/1.1 200", "第二条请求应返回 200。");
                    Assert.AreNotEqual(
                        Encoding.UTF8.GetString(first.Body),
                        Encoding.UTF8.GetString(second.Body),
                        "未实现 IReusableHttpContext 的上下文必须每条请求新建，不能被复用。");
                }
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>
        /// 响应体明显大于单次 socket 读缓冲时，客户端仍应收到完整长度与完整内容。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpResponseWrite_LargeBody_ClientReceivesFullBytes()
        {
            const int bodySize = 256 * 1024;

            var payload = BuildPattern(bodySize);

            var rootName = NewRootName();

            var port = GetFreePort();

            var host = CreateHost(port, rootName, context =>
            {
                context.Response.BinaryWrite(payload);
                context.Response.SetHeader(ResponseHeaderType.Connection, "keep-alive");
                context.Response.Send();
            });

            try
            {
                host.Start();

                using (var client = await ConnectAsync(port).ConfigureAwait(false))
                using (var stream = client.GetStream())
                {
                    // 连续三次：第一次走提交即返回，后两次覆盖已有在途写时的阻塞回退。
                    for (var i = 0; i < 3; i++)
                    {
                        await SendAsync(stream, BuildRequest("/large", port, null)).ConfigureAwait(false);

                        var response = await ReadResponseAsync(stream).ConfigureAwait(false);

                        StringAssert.StartsWith(response.Header, "HTTP/1.1 200", "大响应体请求应返回 200。");
                        Assert.AreEqual(bodySize, response.Body.Length, "响应体长度必须与内容长度一致，不能被截断。");
                        Assert.IsTrue(
                            payload.AsSpan().SequenceEqual(response.Body),
                            "响应体内容必须与写入内容逐字节一致。");
                    }
                }
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>
        /// 请求声明 Connection: close 时，响应体必须完整收到后连接才关闭。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpResponseWrite_RequestClose_ResponseFullyWrittenBeforeClose()
        {
            const int bodySize = 128 * 1024;

            var payload = BuildPattern(bodySize);

            var rootName = NewRootName();

            var port = GetFreePort();

            var host = CreateHost(port, rootName, context =>
            {
                context.Response.BinaryWrite(payload);
                context.Response.End();
            });

            try
            {
                host.Start();

                using (var client = await ConnectAsync(port).ConfigureAwait(false))
                using (var stream = client.GetStream())
                {
                    await SendAsync(stream, BuildRequest("/closing", port, "close")).ConfigureAwait(false);

                    var response = await ReadResponseAsync(stream).ConfigureAwait(false);

                    StringAssert.StartsWith(response.Header, "HTTP/1.1 200", "关闭连接的请求也应返回完整响应。");
                    Assert.IsTrue(HasHeader(response.Header, "Connection", "close"), "请求声明 close 时应回 close。");
                    Assert.AreEqual(bodySize, response.Body.Length, "关闭前响应体必须完整写出，不能被截断。");
                    Assert.IsTrue(
                        payload.AsSpan().SequenceEqual(response.Body),
                        "关闭前写出的响应体内容必须完整。");

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
        /// SendHeader + 两次 SendData + SendEnd 的多段写应保持字节顺序与总长度。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpResponseWrite_SendHeaderTwoSendDataSendEnd_BytesArriveInOrder()
        {
            var first = Encoding.ASCII.GetBytes("part-one-");

            var second = Encoding.ASCII.GetBytes("part-two");

            var expected = new byte[first.Length + second.Length];

            Buffer.BlockCopy(first, 0, expected, 0, first.Length);
            Buffer.BlockCopy(second, 0, expected, first.Length, second.Length);

            var rootName = NewRootName();

            var port = GetFreePort();

            var host = CreateHost(port, rootName, context =>
            {
                context.Response.SendHeader(expected.Length);
                context.Response.SendData(first);
                context.Response.SendData(second);
                context.Response.SendEnd();
            });

            try
            {
                host.Start();

                using (var client = await ConnectAsync(port).ConfigureAwait(false))
                using (var stream = client.GetStream())
                {
                    await SendAsync(stream, BuildRequest("/segments", port, null)).ConfigureAwait(false);

                    var response = await ReadResponseAsync(stream).ConfigureAwait(false);

                    StringAssert.StartsWith(response.Header, "HTTP/1.1 200", "多段写应返回 200 状态行。");
                    Assert.AreEqual(expected.Length, response.Body.Length, "多段写的总长度必须与声明一致。");
                    Assert.IsTrue(
                        expected.AsSpan().SequenceEqual(response.Body),
                        "多段写的字节顺序必须是先报头、再第一段、最后第二段。");

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
        /// 多条连接同时走多段写加关闭时，所有响应都应完整收齐，阻塞回退不能把线程池拖死。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpResponseWrite_ConcurrentMultiSegmentClose_AllResponsesComplete()
        {
            const int connectionCount = 16;

            var payload = BuildPattern(64 * 1024);

            var halfway = payload.Length / 2;

            var first = new byte[halfway];

            var second = new byte[payload.Length - halfway];

            Buffer.BlockCopy(payload, 0, first, 0, first.Length);
            Buffer.BlockCopy(payload, halfway, second, 0, second.Length);

            var rootName = NewRootName();

            var port = GetFreePort();

            var host = CreateHost(port, rootName, context =>
            {
                context.Response.SendHeader(payload.Length);
                context.Response.SendData(first);
                context.Response.SendData(second);
                context.Response.SendEnd();
            });

            try
            {
                host.Start();

                var workers = new Task[connectionCount];

                for (var i = 0; i < connectionCount; i++)
                {
                    workers[i] = Task.Run(async () =>
                    {
                        using (var client = await ConnectAsync(port).ConfigureAwait(false))
                        using (var stream = client.GetStream())
                        {
                            await SendAsync(stream, BuildRequest("/concurrent-segments", port, null)).ConfigureAwait(false);

                            var response = await ReadResponseAsync(stream).ConfigureAwait(false);

                            Assert.AreEqual(payload.Length, response.Body.Length, "并发多段写的响应体必须完整收齐。");
                            Assert.IsTrue(
                                payload.AsSpan().SequenceEqual(response.Body),
                                "并发多段写的响应体内容必须完整且顺序正确。");
                        }
                    });
                }

                await Task.WhenAll(workers).WaitAsync(WaitTimeout).ConfigureAwait(false);
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>
        /// WebHost.SendAsync 应在真实 TCP 上写出完整响应，写完连接仍可继续处理下一个请求。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task WebHost_SendAsync_WritesCompleteResponseOverTcp()
        {
            const int bodySize = 96 * 1024;

            var payload = BuildPattern(bodySize);

            var rootName = NewRootName();

            var port = GetFreePort();

            var host = CreateAsyncHost(port, rootName, async (context, channel) =>
            {
                var data = BuildRawResponse(payload, "keep-alive");

                // 直接验证 WebHost 的异步写出入口，不经过 HttpResponse 的包装
                await ((WebHost)context.Host).SendAsync(channel, data).ConfigureAwait(false);
            });

            try
            {
                host.Start();

                using (var client = await ConnectAsync(port).ConfigureAwait(false))
                using (var stream = client.GetStream())
                {
                    // 连续两次：写出完成后连接仍保持，第二条请求仍能拿到完整响应
                    for (var i = 0; i < 2; i++)
                    {
                        await SendAsync(stream, BuildRequest("/send-async", port, null)).ConfigureAwait(false);

                        var response = await ReadResponseAsync(stream).ConfigureAwait(false);

                        StringAssert.StartsWith(response.Header, "HTTP/1.1 200", "SendAsync 应写出完整状态行。");
                        Assert.AreEqual(bodySize, response.Body.Length, "SendAsync 写出的正文长度必须完整。");
                        Assert.IsTrue(
                            payload.AsSpan().SequenceEqual(response.Body),
                            "SendAsync 写出的正文内容必须逐字节一致。");
                    }
                }
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>
        /// WebHost.EndAsync 应先写完整响应再关闭连接，客户端读到完整正文后才收到 FIN。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task WebHost_EndAsync_WritesResponseThenClosesConnection()
        {
            const int bodySize = 64 * 1024;

            var payload = BuildPattern(bodySize);

            var rootName = NewRootName();

            var port = GetFreePort();

            var host = CreateAsyncHost(port, rootName, async (context, channel) =>
            {
                var data = BuildRawResponse(payload, "close");

                await ((WebHost)context.Host).EndAsync(channel, data).ConfigureAwait(false);
            });

            try
            {
                host.Start();

                using (var client = await ConnectAsync(port).ConfigureAwait(false))
                using (var stream = client.GetStream())
                {
                    await SendAsync(stream, BuildRequest("/end-async", port, null)).ConfigureAwait(false);

                    var response = await ReadResponseAsync(stream).ConfigureAwait(false);

                    StringAssert.StartsWith(response.Header, "HTTP/1.1 200", "EndAsync 应写出完整状态行。");
                    Assert.IsTrue(HasHeader(response.Header, "Connection", "close"), "EndAsync 的响应应声明 Connection: close。");
                    Assert.AreEqual(bodySize, response.Body.Length, "关闭前正文必须完整写出。");
                    Assert.IsTrue(
                        payload.AsSpan().SequenceEqual(response.Body),
                        "关闭前写出的正文内容必须完整。");

                    // 正文读满后再读到 0 字节，说明关闭发生在写完成之后
                    await AssertClosedAsync(stream).ConfigureAwait(false);
                }
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>建一个异步脚本化宿主：回调直接拿到上下文与通道，可调用 WebHost 的异步写出入口。</summary>
        private static WebHost CreateAsyncHost(int port, string rootName, Func<AsyncScriptedHttpContext, IChannel, Task> handler)
        {
            var host = new WebHost(typeof(AsyncScriptedHttpContext), root: rootName, port: port, isStaticsCached: false, isZiped: false);

            host.RouteParam = handler;

            return host;
        }

        /// <summary>拼出「状态行 + Content-Length + Connection + 空行 + 正文」的完整响应字节。</summary>
        private static byte[] BuildRawResponse(byte[] body, string connection)
        {
            var header = "HTTP/1.1 200 OK\r\nContent-Length: " + body.Length + "\r\nConnection: " + connection + "\r\n\r\n";

            var headerBytes = Encoding.ASCII.GetBytes(header);

            var data = new byte[headerBytes.Length + body.Length];

            Buffer.BlockCopy(headerBytes, 0, data, 0, headerBytes.Length);
            Buffer.BlockCopy(body, 0, data, headerBytes.Length, body.Length);

            return data;
        }

        /// <summary>建一个带脚本化响应回调的宿主。</summary>
        private static WebHost CreateHost(int port, string rootName, Action<IHttpContext> handler)
        {
            var host = new WebHost(typeof(ScriptedHttpContext), root: rootName, port: port, isStaticsCached: false, isZiped: false);

            host.RouteParam = handler;

            return host;
        }

        /// <summary>生成可逐字节比对的内容：小写字母循环，便于定位错位。</summary>
        private static byte[] BuildPattern(int length)
        {
            var data = new byte[length];

            for (var i = 0; i < length; i++)
                data[i] = (byte)('a' + i % 26);

            return data;
        }

        /// <summary>生成只含安全字符的站点根目录名，避免 HttpUtility 归一化时报错。</summary>
        private static string NewRootName()
        {
            return "saea-http-deferred-" + Guid.NewGuid().ToString("N");
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

        /// <summary>拼一条最小 GET 请求。</summary>
        private static string BuildRequest(string path, int port, string connection)
        {
            var builder = new StringBuilder();

            builder.Append("GET ").Append(path).Append(" HTTP/1.1\r\n");
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

        /// <summary>
        /// 脚本化上下文：不经过默认路由，直接执行用例注入的响应回调。
        /// </summary>
        public sealed class ScriptedHttpContext : HttpContextBase
        {
            /// <summary>以所属宿主创建上下文。</summary>
            /// <param name="webHost">所属 Web 主机</param>
            public ScriptedHttpContext(IWebHost webHost)
                : base(webHost)
            {
            }

            /// <summary>初始化请求/响应后执行用例注入的响应回调。</summary>
            /// <param name="channel">连接通道</param>
            /// <param name="httpMessage">已解析的 Http 消息</param>
            public override void HttpHandle(IChannel channel, HttpMessage httpMessage)
            {
                base.HttpHandle(channel, httpMessage);

                var host = _webHost as WebHost;

                var handler = host == null ? null : host.RouteParam as Action<IHttpContext>;

                if (handler != null)
                    handler(this);
            }

            /// <summary>脚本化上下文不产出结果对象，响应由回调直接写出。</summary>
            /// <returns>固定返回 null</returns>
            public override IHttpResult GetActionResult()
            {
                return null;
            }
        }

        /// <summary>
        /// 异步脚本化上下文：把上下文与通道一起交给用例回调，回调可直接调用 WebHost 的异步写出入口。
        /// </summary>
        public sealed class AsyncScriptedHttpContext : HttpContextBase
        {
            /// <summary>以所属宿主创建上下文。</summary>
            /// <param name="webHost">所属 Web 主机</param>
            public AsyncScriptedHttpContext(IWebHost webHost)
                : base(webHost)
            {
            }

            /// <summary>所属 Web 主机，供回调直接调用异步写出入口。</summary>
            public IWebHost Host
            {
                get { return _webHost; }
            }

            /// <summary>初始化请求/响应后执行用例注入的异步回调。</summary>
            /// <param name="channel">连接通道</param>
            /// <param name="httpMessage">已解析的 Http 消息</param>
            /// <returns>回调完成后的任务</returns>
            public override async Task HttpHandleAsync(IChannel channel, HttpMessage httpMessage)
            {
                InitContext(channel, httpMessage);

                var host = _webHost as WebHost;

                var handler = host == null ? null : host.RouteParam as Func<AsyncScriptedHttpContext, IChannel, Task>;

                if (handler != null)
                    await handler(this, channel).ConfigureAwait(false);
            }

            /// <summary>脚本化上下文不产出结果对象，响应由回调直接写出。</summary>
            /// <returns>固定返回 null</returns>
            public override IHttpResult GetActionResult()
            {
                return null;
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