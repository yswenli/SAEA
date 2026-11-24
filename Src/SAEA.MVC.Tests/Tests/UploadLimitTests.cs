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
 *文件名： UploadLimitTests
 *版本号： net10.0
 *唯一标识：03254c48-5e34-41b9-8739-fc6a4c1b71c5
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/24 14:31:55
 *描述：MVC 上传上限用例：SAEAMvcApplicationConfig.MaxRequestBodySize 的默认值与真实 multipart 请求的拦截行为
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/24 18:12:35
 *修改人： yswenli
 *版本号： 
 *描述：net10.0
 *
 *****************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.Hosting;
using SAEA.MVC.Tests.Tests.Support;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// MVC 上传上限用例：用真实宿主与原始 TCP multipart 请求验证配置上限是否落到 HTTP 分帧层。
    /// </summary>
    [TestClass]
    public class UploadLimitTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

        private const string ProbePath = "/reuse/probe";

        private const int BodyLimit = 1024;

        /// <summary>
        /// 超过配置上限的 multipart 请求应返回 413。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Upload_MultipartOverConfiguredLimit_Returns413()
        {
            var port = TestAppFactory.GetFreePort();

            var rootName = TestAppFactory.CreateRootName();

            var app = CreateApp(rootName, port, BodyLimit);

            try
            {
                app.Start();

                using (var connection = await RawConnection.ConnectAsync(port).ConfigureAwait(false))
                {
                    var boundary = "limit" + Guid.NewGuid().ToString("N");

                    var body = BuildMultipartBody(boundary, 1800);

                    Assert.IsTrue(body.Length > BodyLimit, "用例前置条件：超限请求体必须大于配置上限。实际 " + body.Length + " 字节。");

                    var response = await connection.SendAsync("POST", ProbePath, body, "multipart/form-data; boundary=" + boundary).ConfigureAwait(false);

                    Assert.AreEqual(413, response.Status, "超过请求体上限应返回 413。实际响应：" + response.Header);
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 上限之内的 multipart 请求应正常交给 action，回显里能看到一个上传文件。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Upload_MultipartUnderConfiguredLimit_Returns200()
        {
            var port = TestAppFactory.GetFreePort();

            var rootName = TestAppFactory.CreateRootName();

            var app = CreateApp(rootName, port, BodyLimit);

            try
            {
                app.Start();

                using (var connection = await RawConnection.ConnectAsync(port).ConfigureAwait(false))
                {
                    var boundary = "limit" + Guid.NewGuid().ToString("N");

                    var body = BuildMultipartBody(boundary, 300);

                    Assert.IsTrue(body.Length < BodyLimit, "用例前置条件：限内请求体必须小于配置上限。实际 " + body.Length + " 字节。");

                    var response = await connection.SendAsync("POST", ProbePath, body, "multipart/form-data; boundary=" + boundary).ConfigureAwait(false);

                    Assert.AreEqual(200, response.Status, "限内 multipart 请求应返回 200。实际响应：" + response.Header);
                    StringAssert.Contains(response.BodyText, "fileCount=1", "限内 multipart 请求应解析出一个上传文件。实际回显：" + response.BodyText);
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// chunked 请求体应解码后交给 action，回显里的 bodyLen 是解码后的长度。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Upload_ChunkedBody_ReachesActionWithDecodedLength()
        {
            var port = TestAppFactory.GetFreePort();

            var rootName = TestAppFactory.CreateRootName();

            var app = CreateApp(rootName, port, BodyLimit);

            try
            {
                app.Start();

                using (var connection = await RawConnection.ConnectAsync(port).ConfigureAwait(false))
                {
                    // 不带 Content-Length，按 chunked 分两段发送，解码后共 7 字节
                    var request = new StringBuilder();

                    request.Append("POST ").Append(ProbePath).Append(" HTTP/1.1\r\n");
                    request.Append("Host: 127.0.0.1:").Append(port).Append("\r\n");
                    request.Append("Connection: close\r\n");
                    request.Append("Content-Type: application/octet-stream\r\n");
                    request.Append("Transfer-Encoding: chunked\r\n");
                    request.Append("\r\n");
                    request.Append("3\r\nabc\r\n");
                    request.Append("4\r\ndefg\r\n");
                    request.Append("0\r\n\r\n");

                    var bytes = Encoding.ASCII.GetBytes(request.ToString());

                    var response = await connection.SendRawAsync(bytes).ConfigureAwait(false);

                    Assert.AreEqual(200, response.Status, "chunked 请求应正常交给 action。实际响应：" + response.Header + "\n正文：" + response.BodyText);
                    StringAssert.Contains(response.BodyText, "bodyLen=7", "回显的请求体长度应是解码后的长度。实际回显：" + response.BodyText);
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 带 Expect: 100-continue 的定长请求应先收到 100 Continue，再发请求体并拿到最终 200。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Upload_ExpectContinue_Sends100ThenFinalResponse()
        {
            var port = TestAppFactory.GetFreePort();

            var rootName = TestAppFactory.CreateRootName();

            var app = CreateApp(rootName, port, BodyLimit);

            try
            {
                app.Start();

                using (var connection = await RawConnection.ConnectAsync(port).ConfigureAwait(false))
                {
                    var head = new StringBuilder();

                    head.Append("POST ").Append(ProbePath).Append(" HTTP/1.1\r\n");
                    head.Append("Host: 127.0.0.1:").Append(port).Append("\r\n");
                    head.Append("Connection: close\r\n");
                    head.Append("Content-Type: application/octet-stream\r\n");
                    head.Append("Content-Length: 7\r\n");
                    head.Append("Expect: 100-continue\r\n");
                    head.Append("\r\n");

                    await connection.SendHeadAsync(head.ToString()).ConfigureAwait(false);

                    var interim = await connection.ReadResponseHeadAsync().ConfigureAwait(false);

                    Assert.AreEqual(100, interim.Status, "请求体未发出前应先收到 100 Continue。实际响应：" + interim.Header);

                    await connection.SendBodyAsync(Encoding.ASCII.GetBytes("abcdefg")).ConfigureAwait(false);

                    var response = await connection.ReadResponseAsync().ConfigureAwait(false);

                    Assert.AreEqual(200, response.Status, "最终响应应为 200。实际响应：" + response.Header + "\n正文：" + response.BodyText);
                    StringAssert.Contains(response.BodyText, "bodyLen=7", "回显的请求体长度应是 7。实际回显：" + response.BodyText);
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 带 Expect: 100-continue 的 chunked 请求同样先收到 100 Continue，终止块到齐后拿到最终 200。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Upload_ExpectContinueWithChunked_Sends100ThenFinalResponse()
        {
            var port = TestAppFactory.GetFreePort();

            var rootName = TestAppFactory.CreateRootName();

            var app = CreateApp(rootName, port, BodyLimit);

            try
            {
                app.Start();

                using (var connection = await RawConnection.ConnectAsync(port).ConfigureAwait(false))
                {
                    var head = new StringBuilder();

                    head.Append("POST ").Append(ProbePath).Append(" HTTP/1.1\r\n");
                    head.Append("Host: 127.0.0.1:").Append(port).Append("\r\n");
                    head.Append("Connection: close\r\n");
                    head.Append("Content-Type: application/octet-stream\r\n");
                    head.Append("Transfer-Encoding: chunked\r\n");
                    head.Append("Expect: 100-continue\r\n");
                    head.Append("\r\n");

                    await connection.SendHeadAsync(head.ToString()).ConfigureAwait(false);

                    var interim = await connection.ReadResponseHeadAsync().ConfigureAwait(false);

                    Assert.AreEqual(100, interim.Status, "chunked 请求体未发出前应先收到 100 Continue。实际响应：" + interim.Header);

                    await connection.SendBodyAsync(Encoding.ASCII.GetBytes("3\r\nabc\r\n4\r\ndefg\r\n0\r\n\r\n")).ConfigureAwait(false);

                    var response = await connection.ReadResponseAsync().ConfigureAwait(false);

                    Assert.AreEqual(200, response.Status, "chunked 请求的最终响应应为 200。实际响应：" + response.Header + "\n正文：" + response.BodyText);
                    StringAssert.Contains(response.BodyText, "bodyLen=7", "回显的请求体长度应是解码后的 7。实际回显：" + response.BodyText);
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// MVC 配置类的请求体上限默认值应与 HTTP 层一致，为 20MB。
        /// </summary>
        [TestMethod]
        public void SAEAMvcApplicationConfig_MaxRequestBodySize_DefaultIs20MB()
        {
            Assert.AreEqual(20 * 1024 * 1024, new SAEAMvcApplicationConfig().MaxRequestBodySize, "默认请求体上限应为 20MB。");
        }

        /// <summary>
        /// 413 后的请求体排空超时默认应为 2 秒；改这个默认值会改变客户端收到 413 的时序。
        /// </summary>
        [TestMethod]
        public void SAEAMvcApplicationConfig_RequestDrainTimeout_DefaultIs2000()
        {
            Assert.AreEqual(2000, new SAEAMvcApplicationConfig().RequestDrainTimeout, "默认排空超时应为 2000 毫秒。");
        }

        /// <summary>
        /// 只发超限报头、不发正文时也必须先收到 413：服务端要先写出响应再半关等待剩余请求体。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Upload_OverLimitHeadOnly_StillReceives413()
        {
            var port = TestAppFactory.GetFreePort();

            var rootName = TestAppFactory.CreateRootName();

            var app = CreateApp(rootName, port, BodyLimit);

            try
            {
                app.Start();

                using (var connection = await RawConnection.ConnectAsync(port).ConfigureAwait(false))
                {
                    var head = new StringBuilder();
                    head.Append("POST ").Append(ProbePath).Append(" HTTP/1.1\r\n");
                    head.Append("Host: 127.0.0.1:").Append(port).Append("\r\n");
                    head.Append("Connection: close\r\n");
                    head.Append("Content-Type: multipart/form-data; boundary=limit\r\n");
                    head.Append("Content-Length: ").Append(BodyLimit * 4).Append("\r\n");
                    head.Append("\r\n");

                    // 报头声明的正文一个字节都不发：旧实现在关闭 socket 时会带着未读数据触发 RST，
                    // 客户端可能连 413 都读不到，因此这里必须能读到完整响应。
                    await connection.SendHeadAsync(head.ToString()).ConfigureAwait(false);

                    var response = await connection.ReadResponseAsync().ConfigureAwait(false);

                    Assert.AreEqual(413, response.Status, "只发超限报头也应先收到 413。实际响应：" + response.Header);
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 1MB 超限正文必须在排空超时前读完并有序关闭：排空若照旧写入 Pipe，会被背压卡到超时并带未读字节关闭。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Upload_OverLimitLargeBody_DrainsFastWithoutReset()
        {
            const int limit = 8 * 1024;
            const int drainTimeout = 8000;
            const int extraBytes = 1024 * 1024;
            const int firstBatch = 1024;
            const int writeChunk = 64 * 1024;

            var port = TestAppFactory.GetFreePort();

            var rootName = TestAppFactory.CreateRootName();

            var app = CreateApp(rootName, port, limit, drainTimeout);

            try
            {
                app.Start();

                using (var connection = await RawConnection.ConnectAsync(port).ConfigureAwait(false))
                {
                    var contentLength = limit + extraBytes;

                    var head = new StringBuilder();
                    head.Append("POST ").Append(ProbePath).Append(" HTTP/1.1\r\n");
                    head.Append("Host: 127.0.0.1:").Append(port).Append("\r\n");
                    head.Append("Connection: close\r\n");
                    head.Append("Content-Type: application/octet-stream\r\n");
                    head.Append("Content-Length: ").Append(contentLength).Append("\r\n");
                    head.Append("\r\n");

                    var headBytes = Encoding.ASCII.GetBytes(head.ToString());

                    var firstBatchBytes = new byte[headBytes.Length + firstBatch];

                    Buffer.BlockCopy(headBytes, 0, firstBatchBytes, 0, headBytes.Length);

                    var stopwatch = Stopwatch.StartNew();

                    // 报头与第一段正文一次写入：分帧器能在同一缓冲里算出已到达字节，排空目标只剩其余部分
                    await connection.SendBytesAsync(firstBatchBytes).ConfigureAwait(false);

                    var response = await connection.ReadResponseAsync().ConfigureAwait(false);

                    Assert.AreEqual(413, response.Status, "超限请求应先收到完整 413。实际响应：" + response.Header);

                    // 剩余 1MB 分多次写回：排空若落 Pipe，超过背压阈值后接收会停摆，这里会拖到排空超时甚至写入失败
                    var chunk = new byte[writeChunk];

                    var remaining = contentLength - firstBatch;

                    while (remaining > 0)
                    {
                        var take = remaining < writeChunk ? remaining : writeChunk;

                        await connection.SendBodyAsync(chunk, take).ConfigureAwait(false);

                        remaining -= take;
                    }

                    // 排空读完剩余正文后才关闭连接，读方向看到的必须是 EOF 而不是 RST
                    var extra = await connection.ReadToEndAsync().ConfigureAwait(false);

                    stopwatch.Stop();

                    Assert.AreEqual(0, extra, "413 响应读完后不应再收到字节。");
                    Assert.IsTrue(
                        stopwatch.ElapsedMilliseconds < 4000,
                        "1MB 排空应在超时前完成，实际耗时 " + stopwatch.ElapsedMilliseconds + "ms（排空超时 " + drainTimeout + "ms）。");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 客户端在请求体未发完时就半关发送方向时，排空必须立即收口，不能等满排空超时。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Upload_OverLimitClientFinBeforeDrain_ClosesQuickly()
        {
            const int limit = 8 * 1024;
            const int drainTimeout = 8000;

            var port = TestAppFactory.GetFreePort();

            var rootName = TestAppFactory.CreateRootName();

            var app = CreateApp(rootName, port, limit, drainTimeout);

            try
            {
                app.Start();

                using (var connection = await RawConnection.ConnectAsync(port).ConfigureAwait(false))
                {
                    var contentLength = limit * 4;

                    var head = new StringBuilder();
                    head.Append("POST ").Append(ProbePath).Append(" HTTP/1.1\r\n");
                    head.Append("Host: 127.0.0.1:").Append(port).Append("\r\n");
                    head.Append("Connection: close\r\n");
                    head.Append("Content-Type: application/octet-stream\r\n");
                    head.Append("Content-Length: ").Append(contentLength).Append("\r\n");
                    head.Append("\r\n");

                    var headBytes = Encoding.ASCII.GetBytes(head.ToString());

                    var firstBatchBytes = new byte[headBytes.Length + 1024];

                    Buffer.BlockCopy(headBytes, 0, firstBatchBytes, 0, headBytes.Length);

                    var stopwatch = Stopwatch.StartNew();

                    await connection.SendBytesAsync(firstBatchBytes).ConfigureAwait(false);

                    // 只发一小段就半关：服务端排空时不会再收到剩余正文，必须靠入站结束标记立即收口。
                    connection.ShutdownSend();

                    var response = await connection.ReadResponseAsync().ConfigureAwait(false);

                    Assert.AreEqual(413, response.Status, "超限请求应先收到完整 413。实际响应：" + response.Header);

                    var extra = await connection.ReadToEndAsync().ConfigureAwait(false);

                    stopwatch.Stop();

                    Assert.AreEqual(0, extra, "413 响应读完后不应再收到字节。");
                    Assert.IsTrue(
                        stopwatch.ElapsedMilliseconds < 3000,
                        "对端已半关时排空应立即收口，实际耗时 " + stopwatch.ElapsedMilliseconds + "ms（排空超时 " + drainTimeout + "ms）。");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 非正的请求体上限属于配置校验失败，Start 必须原样抛出 ArgumentOutOfRangeException，不能被包装成端口占用。
        /// </summary>
        [TestMethod]
        public void Upload_NonPositiveLimit_StartThrowsArgumentOutOfRange()
        {
            var port = TestAppFactory.GetFreePort();

            var rootName = TestAppFactory.CreateRootName();

            var app = CreateApp(rootName, port, 0);

            try
            {
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => app.Start());
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 用配置对象直接构造 MVC 应用：TestAppFactory.CreateApp 的回调拿不到 WebConfig，只能走这条链路。
        /// </summary>
        /// <param name="rootName">根目录名</param>
        /// <param name="port">监听端口</param>
        /// <param name="maxRequestBodySize">请求体上限</param>
        /// <param name="requestDrainTimeout">413 后排空超时毫秒数</param>
        /// <returns>尚未启动的应用实例</returns>
        private static SAEAMvcApplication CreateApp(string rootName, int port, int maxRequestBodySize, int requestDrainTimeout = 2000)
        {
            return new SAEAMvcApplication(new SAEAMvcApplicationConfig
            {
                Root = rootName,
                Port = port,
                IsStaticsCached = false,
                IsZiped = false,
                MaxRequestBodySize = maxRequestBodySize,
                RequestDrainTimeout = requestDrainTimeout
            });
        }

        /// <summary>
        /// 拼一个单文件 multipart 正文，文件名与分隔串固定，正文长度由参数控制。
        /// </summary>
        /// <param name="boundary">不带前导 -- 的分隔串</param>
        /// <param name="contentBytes">文件内容字节数</param>
        /// <returns>完整正文字节</returns>
        private static byte[] BuildMultipartBody(string boundary, int contentBytes)
        {
            var head = new StringBuilder();

            head.Append("--").Append(boundary).Append("\r\n");
            head.Append("Content-Disposition: form-data; name=\"file\"; filename=\"probe.txt\"\r\n");
            head.Append("Content-Type: text/plain\r\n");
            head.Append("\r\n");

            var headBytes = Encoding.ASCII.GetBytes(head.ToString());

            var tailBytes = Encoding.ASCII.GetBytes("\r\n--" + boundary + "--\r\n");

            var body = new byte[headBytes.Length + contentBytes + tailBytes.Length];

            Buffer.BlockCopy(headBytes, 0, body, 0, headBytes.Length);

            for (var i = 0; i < contentBytes; i++)
                body[headBytes.Length + i] = (byte)'a';

            Buffer.BlockCopy(tailBytes, 0, body, headBytes.Length + contentBytes, tailBytes.Length);

            return body;
        }

        /// <summary>
        /// 一条原始连接：发请求后读回完整响应，正文按 Content-Length 读满。
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
            /// 发一条带正文的请求并读回响应。超限时服务端可能先关闭连接，正文写入失败不算用例失败。
            /// </summary>
            /// <param name="method">请求方法</param>
            /// <param name="path">请求目标</param>
            /// <param name="body">请求正文</param>
            /// <param name="contentType">正文类型</param>
            /// <returns>解析后的响应</returns>
            public async Task<RawResponse> SendAsync(string method, string path, byte[] body, string contentType)
            {
                var head = new StringBuilder();

                head.Append(method).Append(' ').Append(path).Append(" HTTP/1.1\r\n");
                head.Append("Host: 127.0.0.1:").Append(_port).Append("\r\n");
                head.Append("Connection: close\r\n");
                head.Append("Content-Type: ").Append(contentType).Append("\r\n");
                head.Append("Content-Length: ").Append(body.Length).Append("\r\n");
                head.Append("\r\n");

                var headBytes = Encoding.ASCII.GetBytes(head.ToString());

                try
                {
                    await _stream.WriteAsync(headBytes, 0, headBytes.Length).ConfigureAwait(false);

                    await _stream.WriteAsync(body, 0, body.Length).ConfigureAwait(false);

                    await _stream.FlushAsync().ConfigureAwait(false);
                }
                catch (IOException)
                {
                    // 超限请求在报头后就被判定，服务端可能先发 413 并关闭连接，正文写不进去属预期
                }
                catch (SocketException)
                {
                }

                return await ReadResponseAsync().ConfigureAwait(false);
            }

            /// <summary>
            /// 原样发送一段完整请求字节并读回响应，用于构造不带 Content-Length 的 chunked 请求。
            /// </summary>
            /// <param name="requestBytes">完整请求字节，含请求行、报头与正文</param>
            /// <returns>解析后的响应</returns>
            public async Task<RawResponse> SendRawAsync(byte[] requestBytes)
            {
                try
                {
                    await _stream.WriteAsync(requestBytes, 0, requestBytes.Length).ConfigureAwait(false);

                    await _stream.FlushAsync().ConfigureAwait(false);
                }
                catch (IOException)
                {
                    // 服务端提前关闭连接时写入可能失败，响应仍按已收到的字节解析
                }
                catch (SocketException)
                {
                }

                return await ReadResponseAsync().ConfigureAwait(false);
            }

            /// <summary>
            /// 只发报头，不写请求体；用于验证 Expect: 100-continue 的中间响应。
            /// </summary>
            /// <param name="head">完整报头文本，含结束空行</param>
            /// <returns>写入并刷新的任务</returns>
            public async Task SendHeadAsync(string head)
            {
                var bytes = Encoding.ASCII.GetBytes(head);

                await _stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);

                await _stream.FlushAsync().ConfigureAwait(false);
            }

            /// <summary>
            /// 单独写一段请求体（定长正文或原始 chunk 字节）。
            /// </summary>
            /// <param name="body">请求体字节</param>
            /// <returns>写入并刷新的任务</returns>
            public Task SendBodyAsync(byte[] body)
            {
                return SendBodyAsync(body, body.Length);
            }

            /// <summary>
            /// 写出请求体的前 count 个字节；用于最后一块不足整块时避免额外拷贝。
            /// </summary>
            /// <param name="body">请求体字节，只取前 count 个</param>
            /// <param name="count">本次写出的字节数</param>
            /// <returns>写入并刷新的任务</returns>
            public async Task SendBodyAsync(byte[] body, int count)
            {
                await _stream.WriteAsync(body, 0, count).ConfigureAwait(false);

                await _stream.FlushAsync().ConfigureAwait(false);
            }

            /// <summary>
            /// 原样写一段字节且不读响应；用于先发报头与前一段正文再等 413。
            /// </summary>
            /// <param name="bytes">待写入的字节</param>
            /// <returns>写入并刷新的任务</returns>
            public async Task SendBytesAsync(byte[] bytes)
            {
                await _stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);

                await _stream.FlushAsync().ConfigureAwait(false);
            }

            /// <summary>
            /// 半关发送方向，模拟客户端在请求体未发完时发出 FIN。
            /// </summary>
            public void ShutdownSend()
            {
                _client.Client.Shutdown(SocketShutdown.Send);
            }

            /// <summary>
            /// 一直读到对端关闭（EOF），返回关闭前多收到的字节数；用于确认是正常关闭而非 RST。
            /// </summary>
            /// <returns>EOF 之前额外读到的字节数</returns>
            public async Task<int> ReadToEndAsync()
            {
                var buffer = new byte[4096];

                var total = 0;

                while (true)
                {
                    var read = await _stream.ReadAsync(buffer, 0, buffer.Length).WaitAsync(WaitTimeout).ConfigureAwait(false);

                    if (read == 0)
                        return total;

                    total += read;
                }
            }

            /// <summary>
            /// 逐字节读到报头结束标记就返回；100 Continue 没有正文，读到这里即可。
            /// </summary>
            /// <returns>只含报头与状态码的响应</returns>
            public async Task<RawResponse> ReadResponseHeadAsync()
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

                var response = new RawResponse { Header = Encoding.ASCII.GetString(head.ToArray()) };

                response.ParseHead();

                return response;
            }

            /// <summary>
            /// 逐字节读到报头结束标记，再按 Content-Length 读满正文。
            /// </summary>
            /// <returns>解析后的响应</returns>
            public async Task<RawResponse> ReadResponseAsync()
            {
                var response = await ReadResponseHeadAsync().ConfigureAwait(false);

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

            /// <summary>完整报头文本，含结束空行。</summary>
            public string Header { get; set; }

            /// <summary>响应状态码。</summary>
            public int Status { get; private set; }

            /// <summary>响应正文字节；无正文时为空数组。</summary>
            public byte[] Body { get; set; } = Array.Empty<byte>();

            /// <summary>响应正文文本。</summary>
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
            /// 解析报头文本：状态行取三位状态码，其余行按首个冒号拆键值。
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