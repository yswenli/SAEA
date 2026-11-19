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
*修改时间：2026/11/19 23:31:33
*修改人： yswenli
*版本号： net10.0
*描述：补 JSON 与表单请求体的解析用例，覆盖解码阶段产出 Forms / Json；新增同连接连续两条请求复用消息实例且不残留上一条状态的用例
*
*****************************************************************************/
using System;
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
                    CollectionAssert.AreEqual(body, message.Body, "请求体应完整复制到消息中。");
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
                    CollectionAssert.AreEqual(body, message.Body, "跨段请求体应完整复制到消息中。");
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
            Assert.AreEqual(body, Encoding.UTF8.GetString(message.Body), "原始请求体仍应保留。");
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

        private static async Task<HttpMessage> DecodeAsync(string channelId, string header, string body)
        {
            var options = PipelineTestKit.CreateOptions(new HttpRequestSplitter());
            var stream = new TestRawByteStream(channelId);
            var capturing = new CapturingInboundHandler();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new HttpRequestDecoder());
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
    }
}