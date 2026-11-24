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
*文件名： HttpRequestDecoderTests
*版本号： net10.0
*唯一标识：3dd2117b-4365-4fd7-b3d9-86061fc0f513
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/9 13:12:01
*描述：HTTP 入站解码器的行为用例
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
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Http.Base;
using SAEA.Http.Tests.Tests.Support;
using SAEA.Sockets.Pipeline;

namespace SAEA.Http.Tests
{
    /// <summary>HTTP 入站解码器：整帧解析成 HttpMessage，分段写入仍只产出一条消息。</summary>
    [TestClass]
    public class HttpRequestDecoderTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 整帧应解析出请求行、查询串、报头与请求体。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpRequestDecoder_RequestLineAndHeaders_ParsedIntoHttpMessage()
        {
            var options = PipelineTestKit.CreateOptions(new HttpRequestSplitter());
            var stream = new TestRawByteStream("http-decoder");
            var capturing = new CapturingInboundHandler();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new HttpRequestDecoder());
                pipeline.AddLast(capturing);

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel("http-decoder"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    var body = Encoding.ASCII.GetBytes("hello world");
                    var header = Encoding.ASCII.GetBytes(
                        "POST /submit?name=alice&x=1 HTTP/1.1\r\n"
                        + "Host: example.com\r\n"
                        + "Content-Type: text/plain\r\n"
                        + "Content-Length: 11\r\n"
                        + "\r\n");

                    await PipelineTestKit.WriteAsync(stream, Concat(header, body));

                    Assert.IsTrue(await capturing.WaitCountAsync(1, WaitTimeout), "五秒内应产出一条消息。");

                    var message = (HttpMessage)await capturing.FirstAsync(WaitTimeout);

                    Assert.AreEqual("POST", message.Method, "应解析出请求方法。");
                    Assert.AreEqual("/submit", message.Url, "应解析出不含查询串的路径。");
                    Assert.AreEqual("alice", message.Query["name"], "应解析出查询串参数。");
                    Assert.AreEqual("1", message.Query["x"], "应解析出全部查询串参数。");
                    Assert.AreEqual("example.com", message.Headers["Host"], "应解析出报头。");
                    CollectionAssert.AreEqual(body, ReadBody(message.Body), "请求体应完整复制到消息中。");
                }
                finally
                {
                    await PipelineTestKit.StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 报头与请求体分两次写入时，分帧器负责累加、解码器只解整帧，最终只产出一条完整消息。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpRequestDecoder_SplitAcrossReads_ProducesSingleMessage()
        {
            var options = PipelineTestKit.CreateOptions(new HttpRequestSplitter());
            var stream = new TestRawByteStream("http-decoder-split");
            var capturing = new CapturingInboundHandler();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new HttpRequestDecoder());
                pipeline.AddLast(capturing);

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel("http-decoder-split"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    var body = Encoding.ASCII.GetBytes("hello world");
                    var header = Encoding.ASCII.GetBytes(
                        "POST /split HTTP/1.1\r\n"
                        + "Host: a\r\n"
                        + "Content-Length: 11\r\n"
                        + "\r\n");

                    // 只发报头：帧不完整，分帧器必须把残帧留在管道里，解码器不应被调用。
                    await PipelineTestKit.WriteAsync(stream, header);
                    await Task.Delay(50);
                    Assert.AreEqual(0, capturing.Count, "请求体未到齐时不得产出消息。");

                    await PipelineTestKit.WriteAsync(stream, body);

                    Assert.IsTrue(await capturing.WaitCountAsync(1, WaitTimeout), "请求体到齐后应产出一条消息。");

                    // 再等一小段时间，确认没有把同一次请求拆成两条。
                    await Task.Delay(100);
                    Assert.AreEqual(1, capturing.Count, "一次请求只能产出一条消息。");

                    var message = (HttpMessage)await capturing.FirstAsync(WaitTimeout);

                    Assert.AreEqual("/split", message.Url, "应解析出请求路径。");
                    Assert.AreEqual(11, message.ContentLength, "应解析出 Content-Length。");
                    CollectionAssert.AreEqual(body, ReadBody(message.Body), "跨段请求体应完整复制到消息中。");
                }
                finally
                {
                    await PipelineTestKit.StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// JSON 请求体应在解码阶段就填好 <see cref="HttpMessage.Json"/>，业务不必再读原始字节。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpRequestDecoder_JsonBody_FillsJson()
        {
            var body = "{\"name\":\"alice\"}";

            var message = await DecodeAsync(
                "http-decoder-json",
                "POST /json HTTP/1.1\r\n"
                + "Host: a\r\n"
                + "Content-Type: application/json; charset=UTF-8\r\n"
                + "Content-Length: " + Encoding.UTF8.GetByteCount(body) + "\r\n"
                + "\r\n",
                body);

            Assert.AreEqual(body, message.Json, "JSON 请求体应解析到 Json 字段。");
            Assert.AreEqual(body, Encoding.UTF8.GetString(ReadBody(message.Body)), "原始请求体仍应保留。");
        }

        /// <summary>
        /// 表单请求体应在解码阶段填好 <see cref="HttpMessage.Forms"/>。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpRequestDecoder_FormUrlEncodedBody_FillsForms()
        {
            // 旧解析器按行切分键值，这里沿用它的行式表单格式。
            var body = "name=alice\r\nage=18";

            var message = await DecodeAsync(
                "http-decoder-form",
                "POST /form HTTP/1.1\r\n"
                + "Host: a\r\n"
                + "Content-Type: application/x-www-form-urlencoded\r\n"
                + "Content-Length: " + Encoding.UTF8.GetByteCount(body) + "\r\n"
                + "\r\n",
                body);

            Assert.IsNotNull(message.Forms, "表单请求体应产出 Forms。");
            Assert.AreEqual("alice", message.Forms["name"], "应解析出表单字段 name。");
            Assert.AreEqual("18", message.Forms["age"], "应解析出表单字段 age。");
        }

        /// <summary>
        /// 同一连接连续两条请求共用一条解码链：第二条应重新解析，上一条的查询串、Cookie 与报头不得残留。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpRequestDecoder_SequentialRequests_ReusesMessageWithoutLeftoverState()
        {
            var options = PipelineTestKit.CreateOptions(new HttpRequestSplitter());
            var stream = new TestRawByteStream("http-decoder-sequential");
            var capturing = new CapturingInboundHandler();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new HttpRequestDecoder());
                pipeline.AddLast(capturing);

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel("http-decoder-sequential"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    await PipelineTestKit.WriteAsync(stream, Encoding.ASCII.GetBytes(
                        "GET /first?x=1 HTTP/1.1\r\n"
                        + "Host: a\r\n"
                        + "Cookie: sid=one\r\n"
                        + "\r\n"));

                    Assert.IsTrue(await capturing.WaitCountAsync(1, WaitTimeout), "第一条请求应产出一条消息。");

                    // 复用同一个实例，第二条解码会改写它，第一条的断言必须在写入第二条之前完成
                    var first = (HttpMessage)await capturing.FirstAsync(WaitTimeout);

                    Assert.AreEqual("/first", first.Url, "第一条请求应解析出路径。");
                    Assert.AreEqual("1", first.Query["x"], "第一条请求应解析出查询串。");
                    Assert.AreEqual("one", first.Cookies["sid"].Value, "第一条请求应解析出 Cookie。");

                    await PipelineTestKit.WriteAsync(stream, Encoding.ASCII.GetBytes(
                        "GET /second?y=2 HTTP/1.1\r\n"
                        + "Host: a\r\n"
                        + "\r\n"));

                    Assert.IsTrue(await capturing.WaitCountAsync(2, WaitTimeout), "第二条请求应再产出一条消息。");

                    HttpMessage second = null;

                    foreach (var item in capturing.Messages)
                        second = (HttpMessage)item;

                    Assert.AreSame(first, second, "同一连接应复用一份消息实例。");
                    Assert.AreEqual("/second", second.Url, "第二条请求应重新解析出路径。");
                    Assert.AreEqual("2", second.Query["y"], "第二条请求应解析出本次的查询串。");
                    Assert.IsFalse(second.Query.ContainsKey("x"), "上一条请求的查询串不得残留。");
                    Assert.IsFalse(second.Headers.ContainsKey("Cookie"), "上一条请求的报头不得残留。");
                    Assert.AreEqual(0, second.Cookies.Count, "上一条请求的 Cookie 不得残留。");
                }
                finally
                {
                    await PipelineTestKit.StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// chunked 请求体应解码成普通负载，ContentLength 改写为解码后的长度，扩展与 trailer 被忽略。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpRequestDecoder_ChunkedBody_DecodesToPlainBody()
        {
            var message = await DecodeAsync(
                "http-decoder-chunked",
                "POST /chunked HTTP/1.1\r\n"
                + "Host: a\r\n"
                + "Transfer-Encoding: chunked\r\n"
                + "\r\n",
                "5;name=value\r\nhello\r\n6\r\n world\r\n0\r\nX-Trace: t\r\n\r\n");

            Assert.AreEqual(11, message.ContentLength, "ContentLength 应改写为解码后的长度。");
            CollectionAssert.AreEqual(
                Encoding.ASCII.GetBytes("hello world"),
                ReadBody(message.Body),
                "请求体应是解码后的负载，chunk 扩展与 trailer 不计入。");
        }

        /// <summary>
        /// chunked 解码长度超过落盘阈值时写入临时文件，释放请求体流后临时文件应被删除。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpRequestDecoder_ChunkedBodyOverThreshold_SpoolsToTempFile()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "saea-http-test-" + Guid.NewGuid().ToString("N"));

            try
            {
                var message = await DecodeAsync(
                    "http-decoder-chunked-file",
                    "POST /chunked HTTP/1.1\r\n"
                    + "Host: a\r\n"
                    + "Transfer-Encoding: chunked\r\n"
                    + "\r\n",
                    "5\r\nhello\r\n6\r\n world\r\n0\r\n\r\n",
                    largeBodyThreshold: 4,
                    tempDirectory: tempDirectory);

                var body = message.Body as HttpRequestBodyStream;

                Assert.IsNotNull(body, "请求体应是库内的请求体流实现。");
                Assert.IsTrue(body.IsFileBacked, "解码长度超阈值时应写入临时文件。");
                Assert.IsTrue(File.Exists(body.TempFilePath), "落盘后临时文件应存在。");
                Assert.AreEqual(11, message.ContentLength, "ContentLength 应改写为解码后的长度。");
                CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("hello world"), ReadBody(body), "从文件读回的应是解码后的负载。");

                var path = body.TempFilePath;

                body.Dispose();

                Assert.IsFalse(File.Exists(path), "释放请求体流后应删除临时文件。");
            }
            finally
            {
                if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
            }
        }

        /// <summary>
        /// 解码总长在第二个 chunk 越过落盘阈值时，已解码前缀与后续块都要完整写进同一个临时文件。
        /// </summary>
        [TestMethod]
        public void ChunkedBodyDecoder_CrossThresholdAtSecondChunk_SpoolsWholeBody()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "saea-http-test-" + Guid.NewGuid().ToString("N"));

            var chunk20 = "abcdefghijklmnopqrst";
            var chunk40 = "0123456789" + "0123456789" + "0123456789" + "0123456789";

            var raw = Encoding.ASCII.GetBytes(
                "5\r\nhello\r\n"
                + "14\r\n" + chunk20 + "\r\n"
                + "28\r\n" + chunk40 + "\r\n"
                + "0\r\n\r\n");

            try
            {
                using (var body = ChunkedBodyDecoder.Decode(
                    new ReadOnlyMemory<byte>(raw),
                    16,
                    tempDirectory,
                    "decoder-cross-threshold",
                    null,
                    out var decodedLength))
                {
                    var stream = (HttpRequestBodyStream)body;

                    Assert.AreEqual(65, decodedLength, "解码长度应是三个 chunk 数据长度之和。");
                    Assert.IsTrue(stream.IsFileBacked, "5+20 已越过阈值 16，应转为临时文件。");
                    CollectionAssert.AreEqual(
                        Encoding.ASCII.GetBytes("hello" + chunk20 + chunk40),
                        ReadBody(stream),
                        "已解码前缀与后续块必须按原顺序完整写入临时文件。");
                }
            }
            finally
            {
                if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
            }
        }

        /// <summary>
        /// 解码总长恰好等于落盘阈值时仍应留在内存，边界不能多算一个字节。
        /// </summary>
        [TestMethod]
        public void ChunkedBodyDecoder_BodyExactlyAtThreshold_StaysInMemory()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "saea-http-test-" + Guid.NewGuid().ToString("N"));

            var raw = Encoding.ASCII.GetBytes("6\r\nabcdef\r\na\r\n0123456789\r\n0\r\n\r\n");

            try
            {
                using (var body = ChunkedBodyDecoder.Decode(
                    new ReadOnlyMemory<byte>(raw),
                    16,
                    tempDirectory,
                    "decoder-exact-threshold",
                    null,
                    out var decodedLength))
                {
                    var stream = (HttpRequestBodyStream)body;

                    Assert.AreEqual(16, decodedLength, "6+10 应正好等于阈值 16。");
                    Assert.IsFalse(stream.IsFileBacked, "恰好等于阈值不应落盘。");
                    Assert.IsTrue(stream.TryGetMemory(out _), "留在内存时应能直接给出内存切片。");
                    CollectionAssert.AreEqual(
                        Encoding.ASCII.GetBytes("abcdef0123456789"),
                        ReadBody(stream),
                        "内存解码结果应是按序拼接的负载。");
                }

                Assert.IsFalse(
                    Directory.Exists(tempDirectory) && Directory.GetFiles(tempDirectory).Length > 0,
                    "未越过阈值时不应产生临时文件。");
            }
            finally
            {
                if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
            }
        }

        /// <summary>
        /// 临时文件已创建后才发现畸形 chunk 时，必须按 400 抛出并删除半成品文件。
        /// </summary>
        [TestMethod]
        public void ChunkedBodyDecoder_MalformedChunkAfterSpoolStart_ThrowsBadRequestAndDeletesTempFile()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "saea-http-test-" + Guid.NewGuid().ToString("N"));

            // 第一个 chunk 已越过阈值 4 并触发落盘，第二个 chunk-size 行才是畸形的
            var raw = Encoding.ASCII.GetBytes("5\r\nhello\r\nzz\r\n");

            try
            {
                FrameProtocolException error = null;

                try
                {
                    ChunkedBodyDecoder.Decode(
                        new ReadOnlyMemory<byte>(raw),
                        4,
                        tempDirectory,
                        "decoder-malformed-spool",
                        null,
                        out _);
                }
                catch (FrameProtocolException ex)
                {
                    error = ex;
                }

                Assert.IsNotNull(error, "畸形 chunk-size 应抛出协议异常。");
                Assert.AreEqual(HttpProtocolErrorCodes.BadRequest, error.Message, "畸形 chunk-size 应按 400 处理。");
                Assert.IsFalse(
                    Directory.Exists(tempDirectory) && Directory.GetFiles(tempDirectory).Length > 0,
                    "异常路径必须删除已创建的临时文件。");
            }
            finally
            {
                if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
            }
        }

        /// <summary>
        /// 输入没有数组背衬时解码器先复制再解码，成功后必须且只能归还一次原始帧引用。
        /// </summary>
        [TestMethod]
        public void ChunkedBodyDecoder_NonArrayBackedBody_ReleasesFrameOwnerOnce()
        {
            var raw = Encoding.ASCII.GetBytes("5\r\nhello\r\n0\r\n\r\n");
            var manager = new ArrayBackedMemoryManager(raw);
            var owner = new CountingOwner();

            using (var body = ChunkedBodyDecoder.Decode(
                manager.Memory,
                0,
                null,
                "decoder-non-array",
                owner,
                out var decodedLength))
            {
                Assert.AreEqual(5, decodedLength, "非数组背衬输入也应按 chunk 语法解出负载长度。");
                CollectionAssert.AreEqual(
                    Encoding.ASCII.GetBytes("hello"),
                    ReadBody(body),
                    "解码结果应与 chunk 负载一致。");
            }

            Assert.AreEqual(1, owner.DisposeCount, "成功解码后原始帧引用必须归还一次，否则池化缓冲泄漏。");

            ((IDisposable)manager).Dispose();
        }

        /// <summary>
        /// 非数组背衬输入解码失败时，解码器不能自行释放原始帧引用：调用方 catch 会统一 Release，
        /// 这里再释放一次就会让同一个引用被归还两次。
        /// </summary>
        [TestMethod]
        public void ChunkedBodyDecoder_NonArrayBackedMalformed_LeavesFrameOwnerToCaller()
        {
            var raw = Encoding.ASCII.GetBytes("5\r\nhello\r\nzz\r\n");
            var manager = new ArrayBackedMemoryManager(raw);
            var owner = new CountingOwner();

            Assert.ThrowsExactly<FrameProtocolException>(() => ChunkedBodyDecoder.Decode(
                manager.Memory,
                0,
                null,
                "decoder-non-array-malformed",
                owner,
                out _));

            Assert.AreEqual(0, owner.DisposeCount, "失败路径必须把引用留给调用方统一释放，避免重复归还。");

            ((IDisposable)manager).Dispose();
        }

        /// <summary>
        /// 非数组背衬的内存提供者：Memory 的 TryGetArray 返回 false，用于覆盖解码器的复制分支。
        /// </summary>
        private sealed class ArrayBackedMemoryManager : MemoryManager<byte>
        {
            private readonly byte[] _buffer;

            public ArrayBackedMemoryManager(byte[] buffer)
            {
                _buffer = buffer;
            }

            public override Span<byte> GetSpan()
            {
                return _buffer;
            }

            public override MemoryHandle Pin(int elementIndex = 0)
            {
                throw new NotSupportedException();
            }

            public override void Unpin()
            {
            }

            protected override void Dispose(bool disposing)
            {
            }
        }

        /// <summary>
        /// 记录释放次数的帧所有者替身，用于验证解码器是否重复归还原始帧引用。
        /// </summary>
        private sealed class CountingOwner : IDisposable
        {
            private int _disposeCount;

            public int DisposeCount
            {
                get { return Volatile.Read(ref _disposeCount); }
            }

            public void Dispose()
            {
                Interlocked.Increment(ref _disposeCount);
            }
        }

        private static async Task<HttpMessage> DecodeAsync(
            string channelId,
            string header,
            string body,
            int largeBodyThreshold = 0,
            string tempDirectory = null)
        {
            var options = PipelineTestKit.CreateOptions(new HttpRequestSplitter());
            var stream = new TestRawByteStream(channelId);
            var capturing = new CapturingInboundHandler();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new HttpRequestDecoder(largeBodyThreshold, tempDirectory));
                pipeline.AddLast(capturing);

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel(channelId), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    await PipelineTestKit.WriteAsync(
                        stream,
                        Concat(Encoding.UTF8.GetBytes(header), Encoding.UTF8.GetBytes(body)));

                    Assert.IsTrue(await capturing.WaitCountAsync(1, WaitTimeout), "五秒内应产出一条消息。");

                    return (HttpMessage)await capturing.FirstAsync(WaitTimeout);
                }
                finally
                {
                    await PipelineTestKit.StopAsync(executor);
                }
            }
        }

        private static byte[] Concat(byte[] first, byte[] second)
        {
            var result = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, result, 0, first.Length);
            Buffer.BlockCopy(second, 0, result, first.Length, second.Length);
            return result;
        }

        private static byte[] ReadBody(Stream body)
        {
            Assert.IsNotNull(body, "请求体流不应为 null。");

            body.Position = 0;

            using (var buffer = new MemoryStream())
            {
                body.CopyTo(buffer);

                return buffer.ToArray();
            }
        }
    }
}