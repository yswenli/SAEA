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
*文件名： HttpRangeHostTests
*版本号： net10.0
*唯一标识：4f0d0c5b-c00b-4a4f-a5d2-6f6a1e0f1c71
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/26 10:40:00
*描述：真实 SAEA.Http 宿主的单区间 Range 端到端用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/26 10:40:00
*修改人： yswenli
*版本号： net10.0
*描述：补充 gzip 开启、大文件句柄释放、不可定位流与非 200 状态下的 Range 用例
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.Http.Model;

namespace SAEA.Http.Tests
{
    /// <summary>
    /// 单区间 Range 的真实宿主用例：静态小文件走内存正文，超过 4MB 的静态文件走流式结果。
    /// </summary>
    [TestClass]
    public class HttpRangeHostTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

        private const string SmallFileContent = "0123456789";

        private const int BigFileLength = 4 * 1024 * 1024 + 16;

        /// <summary>
        /// 内存静态文件命中区间时应回 206，正文只含区间字节。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpRange_SmallFileFirstRange_Returns206WithSlice()
        {
            var rootName = NewRootName();

            var path = NewFilePath(".txt");

            var host = NewHost(rootName, out var port);

            try
            {
                WriteStaticFile(rootName, path, Encoding.ASCII.GetBytes(SmallFileContent));

                host.Start();

                var response = await RequestAsync(port, "GET", path, "Range: bytes=2-5\r\n").ConfigureAwait(false);

                Assert.AreEqual(206, response.StatusCode, "命中区间应返回 206。");
                Assert.AreEqual("bytes 2-5/10", response.Header("Content-Range"), "206 应写闭区间与完整长度。");
                Assert.AreEqual("bytes", response.Header("Accept-Ranges"), "206 应声明支持字节区间。");
                Assert.AreEqual("2345", response.BodyText, "206 正文应只含区间字节。");
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>
        /// 后缀区间应取内存正文末尾字节。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpRange_SmallFileSuffix_Returns206Tail()
        {
            var rootName = NewRootName();

            var path = NewFilePath(".txt");

            var host = NewHost(rootName, out var port);

            try
            {
                WriteStaticFile(rootName, path, Encoding.ASCII.GetBytes(SmallFileContent));

                host.Start();

                var response = await RequestAsync(port, "GET", path, "Range: bytes=-3\r\n").ConfigureAwait(false);

                Assert.AreEqual(206, response.StatusCode, "后缀区间应返回 206。");
                Assert.AreEqual("bytes 7-9/10", response.Header("Content-Range"), "后缀区间应换算成绝对区间。");
                Assert.AreEqual("789", response.BodyText, "后缀区间正文应取末尾字节。");
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>
        /// 区间超出内存正文长度时应回 416，正文为空。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpRange_SmallFileUnsatisfiable_Returns416()
        {
            var rootName = NewRootName();

            var path = NewFilePath(".txt");

            var host = NewHost(rootName, out var port);

            try
            {
                WriteStaticFile(rootName, path, Encoding.ASCII.GetBytes(SmallFileContent));

                host.Start();

                var response = await RequestAsync(port, "GET", path, "Range: bytes=100-\r\n").ConfigureAwait(false);

                Assert.AreEqual(416, response.StatusCode, "区间超出正文应返回 416。");
                Assert.AreEqual("bytes */10", response.Header("Content-Range"), "416 应写完整长度。");
                Assert.AreEqual("bytes", response.Header("Accept-Ranges"), "416 仍应声明支持字节区间。");
                Assert.AreEqual("0", response.Header("Content-Length"), "416 的正文长度应为 0。");
                Assert.AreEqual(0, response.Body.Length, "416 不应带正文。");
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>
        /// 没有 Range 的静态文件请求仍回 200 全量，但声明支持区间。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpRange_SmallFileNoRange_Returns200FullBodyWithAcceptRanges()
        {
            var rootName = NewRootName();

            var path = NewFilePath(".txt");

            var host = NewHost(rootName, out var port);

            try
            {
                WriteStaticFile(rootName, path, Encoding.ASCII.GetBytes(SmallFileContent));

                host.Start();

                var response = await RequestAsync(port, "GET", path, null).ConfigureAwait(false);

                Assert.AreEqual(200, response.StatusCode, "没有 Range 时应返回 200。");
                Assert.AreEqual("bytes", response.Header("Accept-Ranges"), "普通文件响应也应声明支持区间。");
                Assert.AreEqual("<缺失>", response.Header("Content-Range"), "200 全量响应不应写 Content-Range。");
                Assert.AreEqual(SmallFileContent, response.BodyText, "200 响应应返回完整正文。");
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>
        /// 多区间按 RFC 7233 忽略 Range，回 200 全量。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpRange_MultipleRanges_Returns200FullBody()
        {
            var rootName = NewRootName();

            var path = NewFilePath(".txt");

            var host = NewHost(rootName, out var port);

            try
            {
                WriteStaticFile(rootName, path, Encoding.ASCII.GetBytes(SmallFileContent));

                host.Start();

                var response = await RequestAsync(port, "GET", path, "Range: bytes=0-1,3-4\r\n").ConfigureAwait(false);

                Assert.AreEqual(200, response.StatusCode, "多区间应忽略 Range，返回 200。");
                Assert.AreEqual("<缺失>", response.Header("Content-Range"), "忽略 Range 时不应写 Content-Range。");
                Assert.AreEqual(SmallFileContent, response.BodyText, "忽略 Range 时应返回完整正文。");
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>
        /// HEAD 带 Range 时只回报头，Content-Length 等于区间长度。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpRange_HeadWithRange_Returns206WithoutBody()
        {
            var rootName = NewRootName();

            var path = NewFilePath(".txt");

            var host = NewHost(rootName, out var port);

            try
            {
                WriteStaticFile(rootName, path, Encoding.ASCII.GetBytes(SmallFileContent));

                host.Start();

                var response = await RequestAsync(port, "HEAD", path, "Range: bytes=2-5\r\n").ConfigureAwait(false);

                Assert.AreEqual(206, response.StatusCode, "HEAD 带 Range 也应返回 206。");
                Assert.AreEqual("4", response.Header("Content-Length"), "HEAD 的 Content-Length 应等于区间长度。");
                Assert.AreEqual("bytes 2-5/10", response.Header("Content-Range"), "HEAD 206 也应写区间头。");
                Assert.AreEqual(0, response.Body.Length, "HEAD 不应带正文。");
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>
        /// 超过 4MB 的静态文件走流式结果，首段区间应回 206 且只发区间字节。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpRange_BigFileFirstRange_Returns206WithSlice()
        {
            var rootName = NewRootName();

            var path = NewFilePath(".bin");

            var host = NewHost(rootName, out var port);

            try
            {
                WriteBigFile(rootName, path);

                host.Start();

                var response = await RequestAsync(port, "GET", path, "Range: bytes=0-9\r\n").ConfigureAwait(false);

                Assert.AreEqual(206, response.StatusCode, "大文件区间应返回 206。");
                Assert.AreEqual("bytes 0-9/" + BigFileLength, response.Header("Content-Range"), "大文件 206 应写完整长度。");
                Assert.AreEqual("bytes", response.Header("Accept-Ranges"), "大文件 206 应声明支持区间。");
                Assert.AreEqual(10, response.Body.Length, "大文件 206 正文长度应等于区间长度。");

                for (var i = 0; i < response.Body.Length; i++)
                {
                    Assert.AreEqual((byte)(i % 251), response.Body[i], "大文件 206 第 " + i + " 字节应与源文件一致。");
                }
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>
        /// 超过 4MB 的静态文件区间不可满足时应回 416，正文为空。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpRange_BigFileUnsatisfiable_Returns416()
        {
            var rootName = NewRootName();

            var path = NewFilePath(".bin");

            var host = NewHost(rootName, out var port);

            try
            {
                WriteBigFile(rootName, path);

                host.Start();

                var response = await RequestAsync(port, "GET", path, "Range: bytes=" + BigFileLength + "-\r\n").ConfigureAwait(false);

                Assert.AreEqual(416, response.StatusCode, "大文件区间超出正文应返回 416。");
                Assert.AreEqual("bytes */" + BigFileLength, response.Header("Content-Range"), "416 应写完整长度。");
                Assert.AreEqual("0", response.Header("Content-Length"), "416 的正文长度应为 0。");
                Assert.AreEqual(0, response.Body.Length, "416 不应带正文。");
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>
        /// 启用 gzip 时小文件不按 Range 切片，回 200 且正文是真实压缩的完整内容。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpRange_GzipEnabledSmallFileWithRange_Returns200RealGzip()
        {
            var rootName = NewRootName();

            var path = NewFilePath(".txt");

            var host = NewHost(rootName, out var port, true);

            try
            {
                WriteStaticFile(rootName, path, Encoding.ASCII.GetBytes(SmallFileContent));

                host.Start();

                var response = await RequestAsync(port, "GET", path, "Range: bytes=2-5\r\n").ConfigureAwait(false);

                Assert.AreEqual(200, response.StatusCode, "启用 gzip 时不按 Range 切片，应返回 200。");
                Assert.AreEqual("gzip", response.Header("Content-Encoding"), "小文件 200 应真实压缩并声明 gzip。");
                Assert.AreEqual("<缺失>", response.Header("Content-Range"), "gzip 响应不应写 Content-Range。");
                Assert.AreEqual("<缺失>", response.Header("Accept-Ranges"), "gzip 响应不应声明支持区间。");

                var body = Gunzip(response.Body);

                Assert.AreEqual(SmallFileContent, Encoding.ASCII.GetString(body), "解压后正文应为完整文件内容。");
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>
        /// 启用 gzip 时大文件走流式结果，仍回 200 全量且不声明压缩。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpRange_GzipEnabledBigFileNoRange_Returns200WithoutContentEncoding()
        {
            var rootName = NewRootName();

            var path = NewFilePath(".bin");

            var host = NewHost(rootName, out var port, true);

            try
            {
                WriteBigFile(rootName, path);

                host.Start();

                var response = await RequestAsync(port, "GET", path, null).ConfigureAwait(false);

                Assert.AreEqual(200, response.StatusCode, "没有 Range 的大文件应返回 200。");
                Assert.AreEqual("<缺失>", response.Header("Content-Encoding"), "流式大文件不压缩，不应声明 gzip。");
                Assert.IsTrue(BytesEqual(BuildBigFilePayload(), response.Body), "大文件 200 正文应与源文件逐字节一致。");
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>
        /// 启用 gzip 时大文件仍能按 Range 回 206，且不声明压缩。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpRange_GzipEnabledBigFileWithRange_Returns206WithoutContentEncoding()
        {
            var rootName = NewRootName();

            var path = NewFilePath(".bin");

            var host = NewHost(rootName, out var port, true);

            try
            {
                WriteBigFile(rootName, path);

                host.Start();

                var response = await RequestAsync(port, "GET", path, "Range: bytes=0-9\r\n").ConfigureAwait(false);

                Assert.AreEqual(206, response.StatusCode, "启用 gzip 时大文件仍应支持 Range。");
                Assert.AreEqual("<缺失>", response.Header("Content-Encoding"), "流式大文件不压缩，不应声明 gzip。");
                Assert.AreEqual("bytes 0-9/" + BigFileLength, response.Header("Content-Range"), "206 应写完整长度。");
                Assert.AreEqual(10, response.Body.Length, "206 正文长度应等于区间长度。");

                for (var i = 0; i < response.Body.Length; i++)
                {
                    Assert.AreEqual((byte)(i % 251), response.Body[i], "206 第 " + i + " 字节应与源文件一致。");
                }
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>
        /// 启用 gzip 时大文件区间不可满足仍应回 416 空正文。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpRange_GzipEnabledBigFileUnsatisfiable_Returns416()
        {
            var rootName = NewRootName();

            var path = NewFilePath(".bin");

            var host = NewHost(rootName, out var port, true);

            try
            {
                WriteBigFile(rootName, path);

                host.Start();

                var response = await RequestAsync(port, "GET", path, "Range: bytes=" + BigFileLength + "-\r\n").ConfigureAwait(false);

                Assert.AreEqual(416, response.StatusCode, "启用 gzip 时区间不可满足仍应返回 416。");
                Assert.AreEqual("bytes */" + BigFileLength, response.Header("Content-Range"), "416 应写完整长度。");
                Assert.AreEqual("0", response.Header("Content-Length"), "416 的正文长度应为 0。");
                Assert.AreEqual(0, response.Body.Length, "416 不应带正文。");
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>
        /// 大文件 HEAD 带 Range 时只回报头，Content-Length 等于区间长度。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpRange_BigFileHeadWithRange_Returns206WithoutBody()
        {
            var rootName = NewRootName();

            var path = NewFilePath(".bin");

            var host = NewHost(rootName, out var port);

            try
            {
                WriteBigFile(rootName, path);

                host.Start();

                var response = await RequestAsync(port, "HEAD", path, "Range: bytes=0-9\r\n").ConfigureAwait(false);

                Assert.AreEqual(206, response.StatusCode, "大文件 HEAD 带 Range 应返回 206。");
                Assert.AreEqual("10", response.Header("Content-Length"), "HEAD 的 Content-Length 应等于区间长度。");
                Assert.AreEqual("bytes 0-9/" + BigFileLength, response.Header("Content-Range"), "HEAD 206 也应写区间头。");
                Assert.AreEqual(0, response.Body.Length, "HEAD 不应带正文。");
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>
        /// 大文件在 200、206、416 三种响应写完后都应释放文件句柄。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpRange_BigFileHandlesReleased_After200And206And416()
        {
            var scenarios = new[]
            {
                new BigFileScenario { RangeHeader = null, Status = 200 },
                new BigFileScenario { RangeHeader = "Range: bytes=0-9\r\n", Status = 206 },
                new BigFileScenario { RangeHeader = "Range: bytes=" + BigFileLength + "-\r\n", Status = 416 }
            };

            foreach (var scenario in scenarios)
            {
                var rootName = NewRootName();

                var path = NewFilePath(".bin");

                var host = NewHost(rootName, out var port);

                try
                {
                    WriteBigFile(rootName, path);

                    host.Start();

                    var response = await RequestAsync(port, "GET", path, scenario.RangeHeader).ConfigureAwait(false);

                    Assert.AreEqual(scenario.Status, response.StatusCode, "前置响应状态与场景不一致。");

                    var filePath = Path.Combine(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory), rootName, path.TrimStart('/'));

                    Assert.IsTrue(await DeleteFileWithRetryAsync(filePath).ConfigureAwait(false), "状态 " + scenario.Status + " 响应写完后文件句柄未释放。");
                }
                finally
                {
                    StopQuietly(host);
                    DeleteRoot(rootName);
                }
            }
        }

        /// <summary>
        /// 不可定位流无法按区间定位时回 200 全量，不写长度与区间支持，并以短连接结束正文。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpRange_NonSeekableBigDataWithRange_Returns200FullBodyWithClose()
        {
            var rootName = NewRootName();

            var host = NewHost(rootName, out var port, false, typeof(ProbeBigDataContext));

            try
            {
                host.Start();

                var response = await RequestAsync(port, "GET", "/probe-non-seekable", "Range: bytes=0-9\r\n").ConfigureAwait(false);

                Assert.AreEqual(200, response.StatusCode, "不可定位流无法按区间定位，应回 200。");
                Assert.AreEqual("<缺失>", response.Header("Accept-Ranges"), "不可定位流不应声明支持区间。");
                Assert.AreEqual("<缺失>", response.Header("Content-Length"), "不可定位流不应写 Content-Length。");
                Assert.AreEqual("close", response.Header("Connection"), "不可定位流应使用短连接通知正文结束。");
                Assert.IsTrue(BytesEqual(BuildPayload(4096), response.Body), "不可定位流应发送完整正文。");
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>
        /// 非 200 的大数据结果带 Range 时不声明区间支持，回完整正文。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpRange_NotFoundBigDataWithRange_Returns404FullBodyWithoutRangeHeaders()
        {
            var rootName = NewRootName();

            var host = NewHost(rootName, out var port, false, typeof(ProbeBigDataContext));

            try
            {
                host.Start();

                var response = await RequestAsync(port, "GET", "/probe-not-found", "Range: bytes=0-9\r\n").ConfigureAwait(false);

                Assert.AreEqual(404, response.StatusCode, "非 200 结果应保持传入状态码。");
                Assert.AreEqual("<缺失>", response.Header("Accept-Ranges"), "非 200 不应声明支持区间。");
                Assert.AreEqual("<缺失>", response.Header("Content-Range"), "非 200 不应写 Content-Range。");
                Assert.IsTrue(BytesEqual(BuildPayload(256), response.Body), "非 200 应发送完整正文。");
            }
            finally
            {
                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>
        /// leaveOpen 为 false 时结果应在发送结束后关闭数据流。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpRange_BigDataLeaveOpenFalse_DisposesStream()
        {
            var rootName = NewRootName();

            var host = NewHost(rootName, out var port, false, typeof(ProbeBigDataContext));

            var stream = new TrackedMemoryStream(BuildPayload(128));

            ProbeBigDataContext.PendingStream = stream;

            try
            {
                host.Start();

                var response = await RequestAsync(port, "GET", "/probe-owned", null).ConfigureAwait(false);

                Assert.AreEqual(200, response.StatusCode, "默认 leaveOpen 场景应正常返回 200。");
                Assert.IsTrue(await WaitUntilAsync(() => stream.IsDisposed, 1000).ConfigureAwait(false), "leaveOpen 为 false 时结果应关闭数据流。");
            }
            finally
            {
                ProbeBigDataContext.PendingStream = null;

                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>
        /// leaveOpen 为 true 时结果不应关闭调用方传入的数据流。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpRange_BigDataLeaveOpenTrue_KeepsStreamOpen()
        {
            var rootName = NewRootName();

            var host = NewHost(rootName, out var port, false, typeof(ProbeBigDataContext));

            var stream = new TrackedMemoryStream(BuildPayload(128));

            ProbeBigDataContext.PendingStream = stream;

            try
            {
                host.Start();

                var response = await RequestAsync(port, "GET", "/probe-leave-open", null).ConfigureAwait(false);

                Assert.AreEqual(200, response.StatusCode, "leaveOpen 为 true 时应正常返回 200。");
                Assert.IsTrue(BytesEqual(BuildPayload(128), response.Body), "leaveOpen 场景正文应完整。");

                await Task.Delay(100).ConfigureAwait(false);

                Assert.IsFalse(stream.IsDisposed, "leaveOpen 为 true 时结果不应关闭数据流。");
            }
            finally
            {
                stream.Dispose();

                ProbeBigDataContext.PendingStream = null;

                StopQuietly(host);
                DeleteRoot(rootName);
            }
        }

        /// <summary>生成只含安全字符的站点根目录名，避免 HttpUtility 归一化时报错。</summary>
        private static string NewRootName()
        {
            return "saea-http-range-" + Guid.NewGuid().ToString("N");
        }

        /// <summary>生成站点内唯一的 URL 路径，避免命中 MapPath 的进程级结果缓存。</summary>
        /// <param name="extension">扩展名，含点</param>
        /// <returns>以斜杠开头的 URL 路径</returns>
        private static string NewFilePath(string extension)
        {
            return "/range-" + Guid.NewGuid().ToString("N") + extension;
        }

        /// <summary>创建指向临时站点根目录的宿主，并取一个空闲的回环端口。</summary>
        /// <param name="rootName">站点根目录名</param>
        /// <param name="port">取到的监听端口</param>
        /// <param name="isZiped">是否启用 gzip 压缩</param>
        /// <param name="contextType">自定义上下文类型，null 表示使用内置上下文</param>
        /// <returns>尚未启动的宿主</returns>
        private static WebHost NewHost(string rootName, out int port, bool isZiped = false, Type contextType = null)
        {
            port = GetFreePort();

            return new WebHost(httpContentType: contextType, root: rootName, port: port, isStaticsCached: false, isZiped: isZiped);
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

        /// <summary>发一条带 Connection: close 的请求并读到连接关闭，再切分报头与正文。</summary>
        /// <param name="port">监听端口</param>
        /// <param name="method">HTTP 方法</param>
        /// <param name="path">请求路径</param>
        /// <param name="extraHeaders">额外请求头文本，每行以 CRLF 结尾，可为 null</param>
        /// <returns>解析后的响应</returns>
        private static async Task<RawResponse> RequestAsync(int port, string method, string path, string extraHeaders)
        {
            using (var client = await ConnectAsync(port).ConfigureAwait(false))
            using (var stream = client.GetStream())
            {
                var request = new StringBuilder();

                request.Append(method).Append(' ').Append(path).Append(" HTTP/1.1\r\n");
                request.Append("Host: 127.0.0.1:").Append(port).Append("\r\n");

                if (!string.IsNullOrEmpty(extraHeaders))
                    request.Append(extraHeaders);

                request.Append("Connection: close\r\n\r\n");

                var bytes = Encoding.ASCII.GetBytes(request.ToString());

                await stream.WriteAsync(bytes, 0, bytes.Length).WaitAsync(WaitTimeout).ConfigureAwait(false);
                await stream.FlushAsync().WaitAsync(WaitTimeout).ConfigureAwait(false);

                using (var memory = new MemoryStream())
                {
                    var chunk = new byte[8192];

                    while (true)
                    {
                        var read = await stream.ReadAsync(chunk, 0, chunk.Length).WaitAsync(WaitTimeout).ConfigureAwait(false);

                        if (read == 0)
                            break;

                        memory.Write(chunk, 0, read);
                    }

                    return RawResponse.Parse(memory.ToArray());
                }
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

        /// <summary>把静态文件写到站点根目录，目录位于运行基目录的上一级，与 HttpUtility 的解析规则一致。</summary>
        private static void WriteStaticFile(string rootName, string urlPath, byte[] content)
        {
            var directory = Path.Combine(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory), rootName);

            Directory.CreateDirectory(directory);

            File.WriteAllBytes(Path.Combine(directory, urlPath.TrimStart('/')), content);
        }

        /// <summary>写入 4MB 以上的大文件，第 i 个字节的值为 i % 251。</summary>
        private static void WriteBigFile(string rootName, string urlPath)
        {
            var directory = Path.Combine(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory), rootName);

            Directory.CreateDirectory(directory);

            var buffer = new byte[65536];

            using (var stream = new FileStream(Path.Combine(directory, urlPath.TrimStart('/')), FileMode.Create, FileAccess.Write))
            {
                var offset = 0;

                while (offset < BigFileLength)
                {
                    var count = Math.Min(buffer.Length, BigFileLength - offset);

                    for (var i = 0; i < count; i++)
                    {
                        buffer[i] = (byte)((offset + i) % 251);
                    }

                    stream.Write(buffer, 0, count);

                    offset += count;
                }
            }
        }

        /// <summary>按与写入规则相同的模式生成大文件全量正文，供整段比对。</summary>
        private static byte[] BuildBigFilePayload()
        {
            return BuildPayload(BigFileLength);
        }

        /// <summary>生成第 i 个字节值为 i % 251 的正文。</summary>
        /// <param name="length">正文长度</param>
        /// <returns>按规则填充的字节数组</returns>
        private static byte[] BuildPayload(int length)
        {
            var payload = new byte[length];

            for (var i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)(i % 251);
            }

            return payload;
        }

        /// <summary>逐字节比较两段正文。</summary>
        /// <param name="expected">期望正文</param>
        /// <param name="actual">实际正文</param>
        /// <returns>完全一致返回 true</returns>
        private static bool BytesEqual(byte[] expected, byte[] actual)
        {
            return actual != null && expected.AsSpan().SequenceEqual(actual);
        }

        /// <summary>解压 gzip 正文。</summary>
        /// <param name="data">压缩后的字节</param>
        /// <returns>解压后的字节</returns>
        private static byte[] Gunzip(byte[] data)
        {
            using (var input = new MemoryStream(data))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                gzip.CopyTo(output);

                return output.ToArray();
            }
        }

        /// <summary>重试删除文件，最多 10 次、每次间隔 50 毫秒，用于验证响应写完后句柄已释放。</summary>
        /// <param name="filePath">待删除的文件路径</param>
        /// <returns>删除成功返回 true</returns>
        private static async Task<bool> DeleteFileWithRetryAsync(string filePath)
        {
            for (var attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    File.Delete(filePath);

                    return true;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            return false;
        }

        /// <summary>轮询等待条件成立，超时返回 false。</summary>
        /// <param name="predicate">判定委托</param>
        /// <param name="timeoutMilliseconds">最长等待毫秒数</param>
        /// <returns>条件成立返回 true</returns>
        private static async Task<bool> WaitUntilAsync(Func<bool> predicate, int timeoutMilliseconds)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);

            while (true)
            {
                if (predicate())
                    return true;

                if (DateTime.UtcNow >= deadline)
                    return false;

                await Task.Delay(20).ConfigureAwait(false);
            }
        }

        /// <summary>停止宿主并吞掉异常：收尾阶段失败不应覆盖用例结论。</summary>
        private static void StopQuietly(WebHost host)
        {
            try
            {
                host.Stop();
            }
            catch (Exception)
            {
            }
        }

        /// <summary>删除临时站点根目录，删不掉不影响用例结论。</summary>
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
        /// 大文件句柄释放场景：可选的 Range 头与预期状态码。
        /// </summary>
        private sealed class BigFileScenario
        {
            /// <summary>
            /// 请求携带的 Range 头文本，null 表示不带。
            /// </summary>
            public string RangeHeader { get; set; }

            /// <summary>
            /// 预期的响应状态码。
            /// </summary>
            public int Status { get; set; }
        }

        /// <summary>
        /// 返回大数据结果的自定义上下文，用于覆盖静态文件结果之外的出口。
        /// </summary>
        private sealed class ProbeBigDataContext : HttpContext
        {
            /// <summary>
            /// 供 leaveOpen 用例注入的数据流。
            /// </summary>
            internal static TrackedMemoryStream PendingStream;

            /// <summary>
            /// 以宿主创建上下文。
            /// </summary>
            /// <param name="webHost">Web 宿主</param>
            public ProbeBigDataContext(IWebHost webHost) : base(webHost)
            {
            }

            /// <summary>
            /// 按请求路径返回对应的大数据结果。
            /// </summary>
            /// <returns>业务处理结果</returns>
            public override IHttpResult GetActionResult()
            {
                switch (Request.Url)
                {
                    case "/probe-non-seekable":
                        return new HttpBigDataResult(new NonSeekableReadStream(BuildPayload(4096)));

                    case "/probe-not-found":
                        return new HttpBigDataResult(new MemoryStream(BuildPayload(256)), "application/octet-stream", HttpStatusCode.NotFound);

                    case "/probe-owned":
                        return new HttpBigDataResult(PendingStream, "application/octet-stream");

                    case "/probe-leave-open":
                        return new HttpBigDataResult(PendingStream, "application/octet-stream", HttpStatusCode.OK, true);
                }

                return base.GetActionResult();
            }
        }

        /// <summary>
        /// 不可定位只读流：Length、Position 与 Seek 均不支持，用于验证短连接回退。
        /// </summary>
        private sealed class NonSeekableReadStream : Stream
        {
            private readonly MemoryStream _inner;

            /// <summary>
            /// 包装一段内存正文。
            /// </summary>
            /// <param name="payload">正文</param>
            public NonSeekableReadStream(byte[] payload)
            {
                _inner = new MemoryStream(payload, false);
            }

            /// <summary>
            /// 是否可读。
            /// </summary>
            public override bool CanRead { get { return true; } }

            /// <summary>
            /// 是否可定位，固定为 false。
            /// </summary>
            public override bool CanSeek { get { return false; } }

            /// <summary>
            /// 是否可写，固定为 false。
            /// </summary>
            public override bool CanWrite { get { return false; } }

            /// <summary>
            /// 长度不可用。
            /// </summary>
            public override long Length { get { throw new NotSupportedException(); } }

            /// <summary>
            /// 位置不可用。
            /// </summary>
            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

            /// <summary>
            /// 不缓冲，无需刷新。
            /// </summary>
            public override void Flush()
            {
            }

            /// <summary>
            /// 读取正文。
            /// </summary>
            /// <param name="buffer">目标缓冲</param>
            /// <param name="offset">起始下标</param>
            /// <param name="count">读取字节数</param>
            /// <returns>实际读取的字节数</returns>
            public override int Read(byte[] buffer, int offset, int count)
            {
                return _inner.Read(buffer, offset, count);
            }

            /// <summary>
            /// 不支持定位。
            /// </summary>
            /// <param name="offset">偏移量</param>
            /// <param name="origin">基准位置</param>
            /// <returns>不会返回</returns>
            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            /// <summary>
            /// 不支持修改长度。
            /// </summary>
            /// <param name="value">新长度</param>
            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            /// <summary>
            /// 不支持写入。
            /// </summary>
            /// <param name="buffer">源缓冲</param>
            /// <param name="offset">起始下标</param>
            /// <param name="count">写入字节数</param>
            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            /// <summary>
            /// 释放内部流。
            /// </summary>
            /// <param name="disposing">是否为显式释放</param>
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    _inner.Dispose();

                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// 记录是否被释放的内存流，用于验证 leaveOpen 的语义。
        /// </summary>
        private sealed class TrackedMemoryStream : MemoryStream
        {
            /// <summary>
            /// 以一段正文创建可定位的只读内存流。
            /// </summary>
            /// <param name="payload">正文</param>
            public TrackedMemoryStream(byte[] payload) : base(payload, false)
            {
            }

            /// <summary>
            /// 是否已经被释放。
            /// </summary>
            public bool IsDisposed { get; private set; }

            /// <summary>
            /// 记录释放状态后交给基类释放。
            /// </summary>
            /// <param name="disposing">是否为显式释放</param>
            protected override void Dispose(bool disposing)
            {
                IsDisposed = true;

                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// 原始 HTTP 响应：状态码、响应头与正文字节。
        /// </summary>
        private sealed class RawResponse
        {
            private static readonly byte[] Terminator = { (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' };

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
            public byte[] Body { get; private set; }

            /// <summary>
            /// 按 ASCII 解码的响应正文。
            /// </summary>
            public string BodyText
            {
                get { return Encoding.ASCII.GetString(Body); }
            }

            /// <summary>
            /// 解析原始响应字节。
            /// </summary>
            /// <param name="bytes">完整响应字节</param>
            /// <returns>解析后的响应</returns>
            public static RawResponse Parse(byte[] bytes)
            {
                var index = IndexOfBytes(bytes, Terminator);

                if (index < 0)
                    throw new IOException("响应缺少报头结束标记。");

                var headerText = Encoding.ASCII.GetString(bytes, 0, index);

                var lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                for (var i = 1; i < lines.Length; i++)
                {
                    var separator = lines[i].IndexOf(':');

                    if (separator <= 0) continue;

                    headers[lines[i].Substring(0, separator).Trim()] = lines[i].Substring(separator + 1).Trim();
                }

                var parts = lines[0].Split(' ');

                if (parts.Length < 2)
                    throw new FormatException("响应状态行不符合 HTTP 格式：" + lines[0]);

                var bodyStart = index + Terminator.Length;

                var body = new byte[bytes.Length - bodyStart];

                Array.Copy(bytes, bodyStart, body, 0, body.Length);

                return new RawResponse
                {
                    StatusCode = int.Parse(parts[1]),
                    Headers = headers,
                    Body = body
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
        }
    }
}