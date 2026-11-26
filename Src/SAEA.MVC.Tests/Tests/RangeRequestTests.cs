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
*修改时间：2026/11/26 17:00:00
*修改人： yswenli
*版本号： net10.0
*描述：补充 gzip 开启、416、HEAD、非 200、不可定位流、leaveOpen 与大文件句柄释放用例
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

using SAEA.MVC.Hosting;
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
        /// 4MB 以上的静态文件区间不可满足时应回 416、正文为空。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task BigDataFile_RangeUnsatisfiable_Returns416()
        {
            var bigFileName = NewFileName("range-large", ".bin");

            await WithAppAsync(async (port, rootDirectory) =>
            {
                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await SendAsync(client, BaseUrl(port) + "/" + bigFileName, "bytes=" + BigFileLength + "-").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.RequestedRangeNotSatisfiable, response.StatusCode, "大文件区间超出正文应返回 416");
                    Assert.AreEqual("bytes */" + BigFileLength, Header(response, "Content-Range"), "416 应写完整长度");
                    Assert.AreEqual("bytes", Header(response, "Accept-Ranges"), "416 仍应声明支持字节区间");
                    Assert.AreEqual(0L, response.Content.Headers.ContentLength, "416 正文长度应为 0");

                    var body = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                    Assert.AreEqual(0, body.Length, "416 不应带正文");
                }
            }, new Dictionary<string, string>(), root => CreateBigFile(root, bigFileName)).ConfigureAwait(false);
        }

        /// <summary>
        /// 启用 gzip 时大文件走流式结果，仍回 200 全量且不声明压缩。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task BigDataFile_GzipEnabledNoRange_Returns200WithoutContentEncoding()
        {
            var bigFileName = NewFileName("range-large", ".bin");

            await WithAppAsync(async (port, rootDirectory) =>
            {
                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/" + bigFileName).ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "启用 gzip 时大文件仍应返回 200");
                    Assert.AreEqual("<缺失>", Header(response, "Content-Encoding"), "流式大文件不压缩，不应声明 gzip");

                    var body = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                    Assert.IsTrue(BytesEqual(BuildBigFilePayload(), body), "大文件 200 正文应与源文件逐字节一致");
                }
            }, new Dictionary<string, string>(), root => CreateBigFile(root, bigFileName), true).ConfigureAwait(false);
        }

        /// <summary>
        /// 启用 gzip 时大文件仍能按 Range 回 206，且不声明压缩。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task BigDataFile_GzipEnabledRange_Returns206WithoutContentEncoding()
        {
            var bigFileName = NewFileName("range-large", ".bin");

            await WithAppAsync(async (port, rootDirectory) =>
            {
                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await SendAsync(client, BaseUrl(port) + "/" + bigFileName, "bytes=0-9").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.PartialContent, response.StatusCode, "启用 gzip 时大文件仍应支持 Range");
                    Assert.AreEqual("<缺失>", Header(response, "Content-Encoding"), "流式大文件不压缩，不应声明 gzip");
                    Assert.AreEqual("bytes 0-9/" + BigFileLength, Header(response, "Content-Range"), "206 应写完整长度");

                    var body = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                    Assert.AreEqual(10, body.Length, "206 正文长度应等于区间长度");

                    for (var i = 0; i < body.Length; i++)
                    {
                        Assert.AreEqual(ExpectedBigFileByte(i), body[i], "206 第 " + i + " 字节应与源文件一致");
                    }
                }
            }, new Dictionary<string, string>(), root => CreateBigFile(root, bigFileName), true).ConfigureAwait(false);
        }

        /// <summary>
        /// 4MB 以上的静态文件 HEAD 带 Range 时只回报头，Content-Length 等于区间长度。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task BigDataFile_HeadWithRange_Returns206WithoutBody()
        {
            var bigFileName = NewFileName("range-large", ".bin");

            await WithAppAsync(async (port, rootDirectory) =>
            {
                using (var client = TestAppFactory.CreateHttpClient())
                {
                    var url = BaseUrl(port) + "/" + bigFileName;

                    using (var response = await TestAppFactory.SendWithReadyRetryAsync(client, () =>
                    {
                        var request = new HttpRequestMessage(HttpMethod.Head, url);

                        request.Headers.TryAddWithoutValidation("Range", "bytes=0-9");

                        return request;
                    }).ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.PartialContent, response.StatusCode, "大文件 HEAD 带 Range 应返回 206");
                        Assert.AreEqual(10L, response.Content.Headers.ContentLength, "HEAD 的 Content-Length 应等于区间长度");
                        Assert.AreEqual("bytes 0-9/" + BigFileLength, Header(response, "Content-Range"), "HEAD 206 也应写区间头");

                        var body = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                        Assert.AreEqual(0, body.Length, "HEAD 不应带正文");
                    }
                }
            }, new Dictionary<string, string>(), root => CreateBigFile(root, bigFileName)).ConfigureAwait(false);
        }

        /// <summary>
        /// 非 200 的大数据结果带 Range 时不声明区间支持，回完整正文。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task BigData_NotFoundWithRange_Returns404FullBodyWithoutRangeHeaders()
        {
            await WithAppAsync(async (port, rootDirectory) =>
            {
                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await SendAsync(client, BaseUrl(port) + "/rangeprobe/missingbigdata", "bytes=0-9").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, "非 200 结果应保持传入状态码");
                    Assert.AreEqual("<缺失>", Header(response, "Accept-Ranges"), "非 200 不应声明支持区间");
                    Assert.AreEqual("<缺失>", Header(response, "Content-Range"), "非 200 不应写 Content-Range");

                    var body = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                    Assert.IsTrue(BytesEqual(BuildPayload(256), body), "非 200 应发送完整正文");
                }
            }, new Dictionary<string, string>()).ConfigureAwait(false);
        }

        /// <summary>
        /// 不可定位流无法按区间定位时回 200 全量，不写长度与区间支持，并以短连接结束正文。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task BigData_NonSeekableWithRange_Returns200FullBodyWithClose()
        {
            await WithAppAsync(async (port, rootDirectory) =>
            {
                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await SendHeadersReadWithReadyRetryAsync(client, BaseUrl(port) + "/rangeprobe/nonseekable", "bytes=0-9").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "不可定位流无法按区间定位，应回 200");
                    Assert.AreEqual("<缺失>", Header(response, "Accept-Ranges"), "不可定位流不应声明支持区间");
                    Assert.AreEqual("<缺失>", Header(response, "Content-Range"), "不可定位流不应写 Content-Range");
                    // 只读报头时不能提前缓冲正文，否则 HttpClient 会把已读长度回填成 ContentLength，服务端有没有写就看不出来
                    Assert.IsNull(response.Content.Headers.ContentLength, "不可定位流不应写 Content-Length");
                    Assert.AreEqual("close", Header(response, "Connection"), "不可定位流应使用短连接");

                    var body = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                    Assert.IsTrue(BytesEqual(BuildPayload(4096), body), "不可定位流应发送完整正文");
                }
            }, new Dictionary<string, string>()).ConfigureAwait(false);
        }

        /// <summary>
        /// 默认 leaveOpen 时结果应在发送结束后关闭数据流。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task BigData_LeaveOpenFalse_DisposesStream()
        {
            var id = Guid.NewGuid().ToString("N");

            await WithAppAsync(async (port, rootDirectory) =>
            {
                using (var client = TestAppFactory.CreateHttpClient())
                {
                    using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/rangeprobe/owned?id=" + id).ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "默认 leaveOpen 场景应正常返回 200");

                        await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    }

                    var state = await WaitForStreamStateAsync(client, BaseUrl(port) + "/rangeprobe/streamstate?id=" + id, "disposed", 1000).ConfigureAwait(false);

                    Assert.AreEqual("disposed", state, "leaveOpen 为 false 时结果应关闭数据流");
                }
            }, new Dictionary<string, string>()).ConfigureAwait(false);
        }

        /// <summary>
        /// leaveOpen 为 true 时结果不应关闭调用方传入的数据流。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task BigData_LeaveOpenTrue_KeepsStreamOpen()
        {
            var id = Guid.NewGuid().ToString("N");

            await WithAppAsync(async (port, rootDirectory) =>
            {
                using (var client = TestAppFactory.CreateHttpClient())
                {
                    using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/rangeprobe/leaveopen?id=" + id).ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "leaveOpen 为 true 时应正常返回 200");

                        var body = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                        Assert.IsTrue(BytesEqual(BuildPayload(64), body), "leaveOpen 场景正文应完整");
                    }

                    await Task.Delay(100).ConfigureAwait(false);

                    var state = await ReadTextAsync(client, BaseUrl(port) + "/rangeprobe/streamstate?id=" + id).ConfigureAwait(false);

                    Assert.AreEqual("open", state, "leaveOpen 为 true 时结果不应关闭数据流");
                }
            }, new Dictionary<string, string>()).ConfigureAwait(false);
        }

        /// <summary>
        /// 4MB 以上的静态文件在 200、206、416 三种响应写完后都应释放文件句柄。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task BigDataFile_HandlesReleased_After200And206And416()
        {
            var scenarios = new[]
            {
                new BigFileRangeScenario { RangeHeader = null, Status = 200 },
                new BigFileRangeScenario { RangeHeader = "bytes=0-9", Status = 206 },
                new BigFileRangeScenario { RangeHeader = "bytes=" + BigFileLength + "-", Status = 416 }
            };

            foreach (var scenario in scenarios)
            {
                var bigFileName = NewFileName("range-large", ".bin");

                await WithAppAsync(async (port, rootDirectory) =>
                {
                    using (var client = TestAppFactory.CreateHttpClient())
                    {
                        HttpResponseMessage response;

                        if (string.IsNullOrEmpty(scenario.RangeHeader))
                        {
                            response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/" + bigFileName).ConfigureAwait(false);
                        }
                        else
                        {
                            response = await SendAsync(client, BaseUrl(port) + "/" + bigFileName, scenario.RangeHeader).ConfigureAwait(false);
                        }

                        using (response)
                        {
                            Assert.AreEqual(scenario.Status, (int)response.StatusCode, "前置响应状态与场景不一致");

                            await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                        }
                    }

                    Assert.IsTrue(await DeleteFileWithRetryAsync(Path.Combine(rootDirectory, bigFileName)).ConfigureAwait(false), "状态 " + scenario.Status + " 响应写完后文件句柄未释放");
                }, new Dictionary<string, string>(), root => CreateBigFile(root, bigFileName)).ConfigureAwait(false);
            }
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
        /// <param name="isZiped">是否启用 gzip 压缩</param>
        /// <returns>该用例的异步执行任务。</returns>
        private static async Task WithAppAsync(Func<int, string, Task> body, Dictionary<string, string> textFiles, Action<string> createExtraFiles = null, bool isZiped = false)
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

                var app = isZiped
                    ? new SAEAMvcApplication(rootName, port, false, true)
                    : TestAppFactory.CreateApp(rootName, port);

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
        /// 按与写入规则相同的模式生成大文件全量正文，供整段比对。
        /// </summary>
        /// <returns>按规则填充的字节数组</returns>
        private static byte[] BuildBigFilePayload()
        {
            return BuildPayload(BigFileLength);
        }

        /// <summary>
        /// 生成第 i 个字节值为 i % 251 的正文。
        /// </summary>
        /// <param name="length">正文长度</param>
        /// <returns>按规则填充的字节数组</returns>
        private static byte[] BuildPayload(int length)
        {
            var payload = new byte[length];

            for (var i = 0; i < payload.Length; i++)
            {
                payload[i] = ExpectedBigFileByte(i);
            }

            return payload;
        }

        /// <summary>
        /// 逐字节比较两段正文。
        /// </summary>
        /// <param name="expected">期望正文</param>
        /// <param name="actual">实际正文</param>
        /// <returns>完全一致返回 true</returns>
        private static bool BytesEqual(byte[] expected, byte[] actual)
        {
            return actual != null && expected.AsSpan().SequenceEqual(actual);
        }

        /// <summary>
        /// 重试删除文件，最多 10 次、每次间隔 50 毫秒，用于验证响应写完后句柄已释放。
        /// </summary>
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

        /// <summary>
        /// 发 GET 并读取文本正文。
        /// </summary>
        /// <param name="client">请求客户端</param>
        /// <param name="url">完整地址</param>
        /// <returns>响应正文</returns>
        private static async Task<string> ReadTextAsync(HttpClient client, string url)
        {
            using (var response = await TestAppFactory.GetAsync(client, url).ConfigureAwait(false))
            {
                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 轮询查询数据流状态，直到等于期望值或超时。
        /// </summary>
        /// <param name="client">请求客户端</param>
        /// <param name="url">状态查询地址</param>
        /// <param name="expected">期望状态</param>
        /// <param name="timeoutMilliseconds">最长等待毫秒数</param>
        /// <returns>最后一次查询到的状态</returns>
        private static async Task<string> WaitForStreamStateAsync(HttpClient client, string url, string expected, int timeoutMilliseconds)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);

            string state;

            while (true)
            {
                state = await ReadTextAsync(client, url).ConfigureAwait(false);

                if (string.Equals(state, expected, StringComparison.Ordinal) || DateTime.UtcNow >= deadline)
                    return state;

                await Task.Delay(50).ConfigureAwait(false);
            }
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
        /// 带 Range 头发 GET 请求并只读取报头，正文留给调用方按需读取。
        /// </summary>
        /// <param name="client">请求客户端</param>
        /// <param name="url">完整地址</param>
        /// <param name="range">Range 头值</param>
        /// <returns>正文尚未读取的响应</returns>
        private static async Task<HttpResponseMessage> SendHeadersReadWithReadyRetryAsync(HttpClient client, string url, string range)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);

            while (true)
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, url);

                    request.Headers.TryAddWithoutValidation("Range", range);

                    // 必须用 ResponseHeadersRead：默认会把正文缓冲完，未知长度会被回填成 Content-Length
                    return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                }
                catch (HttpRequestException) when (DateTime.UtcNow < deadline)
                {
                    await Task.Delay(100).ConfigureAwait(false);
                }
            }
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
        /// 大文件句柄释放场景：可选的 Range 头与预期状态码。
        /// </summary>
        private sealed class BigFileRangeScenario
        {
            /// <summary>
            /// 请求携带的 Range 头值，null 表示不带。
            /// </summary>
            public string RangeHeader { get; set; }

            /// <summary>
            /// 预期的响应状态码。
            /// </summary>
            public int Status { get; set; }
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