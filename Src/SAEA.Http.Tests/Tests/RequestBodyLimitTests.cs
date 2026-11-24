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
 *文件名： RequestBodyLimitTests
 *版本号： net10.0
 *唯一标识：3a7d836a-6d80-4004-afbe-a94207d8bfba
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/24 14:31:55
 *描述：请求体上限用例：默认值、报头后早判 413、边界值放行与非法上限的启动期校验
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/24 14:41:40
 *修改人： yswenli
 *版本号： net10.0
 *描述：新增，覆盖 WebConfig.MaxRequestBodySize 的默认值、413 早判行为与上界启动期校验
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

using SAEA.Http.Model;

namespace SAEA.Http.Tests
{
    /// <summary>
    /// 请求体上限用例：起真实宿主，用原始 TCP 请求验证上限判定发生在请求体到齐之前。
    /// </summary>
    [TestClass]
    public class RequestBodyLimitTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// WebConfig 的请求体上限默认值应为 20MB。
        /// </summary>
        [TestMethod]
        public void WebConfig_MaxRequestBodySize_DefaultIs20MB()
        {
            Assert.AreEqual(20 * 1024 * 1024, new WebConfig().MaxRequestBodySize, "默认请求体上限应为 20MB。");
        }

        /// <summary>
        /// Content-Length 超过上限时只发报头也应立刻收到 413：服务端不能等请求体到齐。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task WebConfig_MaxRequestBodySize_BodyOverLimit_Returns413BeforeBody()
        {
            var rootName = NewRootName();

            var port = GetFreePort();

            var host = new WebHost(root: rootName, port: port, isStaticsCached: false, isZiped: false);

            try
            {
                host.WebConfig.MaxRequestBodySize = 1024;

                host.Start();

                using (var client = await ConnectAsync(port).ConfigureAwait(false))
                using (var stream = client.GetStream())
                {
                    var builder = new StringBuilder();

                    builder.Append("POST /missing-").Append(Guid.NewGuid().ToString("N")).Append(" HTTP/1.1\r\n");
                    builder.Append("Host: 127.0.0.1:").Append(port).Append("\r\n");
                    builder.Append("Content-Length: 2048\r\n");
                    builder.Append("Connection: close\r\n");
                    builder.Append("\r\n");

                    await SendAsync(stream, builder.ToString()).ConfigureAwait(false);

                    // 报头已声明 2048 字节但不发请求体：能读到 413 就证明判定没有等请求体。
                    var response = await ReadResponseAsync(stream).ConfigureAwait(false);

                    StringAssert.StartsWith(response.Header, "HTTP/1.1 413", "超过请求体上限应在报头后立刻返回 413。");
                }
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>
        /// 请求体正好等于上限时不应被拒绝，应继续走正常路由并返回 404。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task WebConfig_MaxRequestBodySize_BodyAtLimit_NotRejected()
        {
            var rootName = NewRootName();

            var port = GetFreePort();

            var host = new WebHost(root: rootName, port: port, isStaticsCached: false, isZiped: false);

            try
            {
                host.WebConfig.MaxRequestBodySize = 1024;

                host.Start();

                using (var client = await ConnectAsync(port).ConfigureAwait(false))
                using (var stream = client.GetStream())
                {
                    var body = new byte[1024];

                    for (var i = 0; i < body.Length; i++)
                        body[i] = (byte)'a';

                    var builder = new StringBuilder();

                    builder.Append("POST /missing-").Append(Guid.NewGuid().ToString("N")).Append(" HTTP/1.1\r\n");
                    builder.Append("Host: 127.0.0.1:").Append(port).Append("\r\n");
                    builder.Append("Content-Length: ").Append(body.Length).Append("\r\n");
                    builder.Append("Connection: close\r\n");
                    builder.Append("\r\n");

                    await SendAsync(stream, builder.ToString()).ConfigureAwait(false);

                    await stream.WriteAsync(body, 0, body.Length).ConfigureAwait(false);

                    await stream.FlushAsync().ConfigureAwait(false);

                    var response = await ReadResponseAsync(stream).ConfigureAwait(false);

                    StringAssert.StartsWith(response.Header, "HTTP/1.1 404", "等于上限的请求体不应被拒绝，应走正常路由返回 404。");
                }
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>
        /// 上限配置为 0 时 Start 应直接抛 ArgumentOutOfRangeException，且监听端口未被占用。
        /// </summary>
        [TestMethod]
        public void WebConfig_MaxRequestBodySize_NonPositive_StartThrows()
        {
            var rootName = NewRootName();

            var port = GetFreePort();

            var host = new WebHost(root: rootName, port: port, isStaticsCached: false, isZiped: false);

            try
            {
                host.WebConfig.MaxRequestBodySize = 0;

                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => host.Start());

                // 校验早于建链：如果端口已被监听，这里会抛 SocketException。
                var listener = new TcpListener(IPAddress.Loopback, port);

                listener.Start();

                listener.Stop();
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>
        /// 上限大到让整帧缓冲推导溢出 int 时，Start 也应直接抛 ArgumentOutOfRangeException，且监听端口未被占用。
        /// </summary>
        [TestMethod]
        public void WebConfig_MaxRequestBodySize_TooLarge_StartThrows()
        {
            var rootName = NewRootName();

            var port = GetFreePort();

            var host = new WebHost(root: rootName, port: port, isStaticsCached: false, isZiped: false);

            try
            {
                host.WebConfig.MaxRequestBodySize = int.MaxValue;

                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => host.Start());

                // 上界校验同样早于建链：如果端口已被监听，这里会抛 SocketException。
                var listener = new TcpListener(IPAddress.Loopback, port);

                listener.Start();

                listener.Stop();
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>生成只含安全字符的站点根目录名，避免 HttpUtility 归一化时报错。</summary>
        /// <returns>临时根目录名</returns>
        private static string NewRootName()
        {
            return "saea-http-body-limit-" + Guid.NewGuid().ToString("N");
        }

        /// <summary>取一个当前空闲的回环端口，先占后放，避免与其它用例撞端口。</summary>
        /// <returns>可监听的端口号</returns>
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
        /// <param name="port">监听端口</param>
        /// <returns>已连接的客户端</returns>
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

        /// <summary>把请求文本写成 ASCII 字节并冲刷。</summary>
        /// <param name="stream">连接流</param>
        /// <param name="request">请求文本</param>
        /// <returns>写入完成的任务</returns>
        private static async Task SendAsync(NetworkStream stream, string request)
        {
            var bytes = Encoding.ASCII.GetBytes(request);

            await stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);

            await stream.FlushAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// 逐字节读到报头结束标记，再按 Content-Length 读满报文体，避免把残留字节留给下一个响应。
        /// </summary>
        /// <param name="stream">连接流</param>
        /// <returns>解析后的原始响应</returns>
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
        /// <param name="stream">连接流</param>
        /// <param name="header">响应报头文本</param>
        /// <returns>报文体字节</returns>
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

        /// <summary>停止宿主并吞掉异常：收尾失败不应覆盖用例结论。</summary>
        /// <param name="host">待停止的宿主，可为 null</param>
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
        /// <param name="rootName">根目录名</param>
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