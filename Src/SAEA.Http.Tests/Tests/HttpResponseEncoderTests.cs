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
 *文件名： HttpResponseEncoderTests
 *版本号： net10.0
 *唯一标识：19fff319-f94b-4cf4-884d-9a3109b29a5f
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/9 15:12:00
 *描述：HTTP 出站编码器的行为用例
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/9 15:12:00
 *修改人： yswenli
 *版本号： net10.0
 *描述：HTTP 出站编码器的行为用例
 *
 *****************************************************************************/
using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Http.Tests.Tests.Support;
using SAEA.Sockets.Pipeline;

namespace SAEA.Http.Tests
{
    /// <summary>HTTP 出站编码器：普通响应补齐长度，101 升级响应不写报文体。</summary>
    [TestClass]
    public class HttpResponseEncoderTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 普通响应应编码成含状态行、Content-Length 与报文体的完整原始帧。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpResponseEncoder_ContentLength_WritesCompleteRawFrame()
        {
            var options = PipelineTestKit.CreateOptions(new HttpRequestSplitter());
            var stream = new TestRawByteStream("http-encoder-body");

            var response = CreateResponse();
            response.Protocal = "HTTP/1.1";
            response.Status = HttpStatusCode.OK;
            response.ContentType = "text/plain; charset=utf-8";
            response.Body = Encoding.ASCII.GetBytes("hello");

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new HttpResponseEncoder());
                pipeline.AddLast(new OutboundSerializingHandler());

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel("http-encoder-body"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    await pipeline.WriteAsync(response).WaitAsync(WaitTimeout);

                    var text = SentText(stream);
                    StringAssert.Contains(text, "HTTP/1.1 200  OK", "状态行应写在帧首。");
                    StringAssert.Contains(text, "Content-Length: 5", "普通响应应补齐报文体长度。");
                    StringAssert.Contains(text, "hello", "报文体应完整写出。");
                }
                finally
                {
                    await PipelineTestKit.StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 101 升级响应只写报头与空行，不得被覆写成 Connection: close 或追加 Content-Length。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpResponseEncoder_Upgrade101_WritesSwitchingProtocols()
        {
            var options = PipelineTestKit.CreateOptions(new HttpRequestSplitter());
            var stream = new TestRawByteStream("http-encoder-upgrade");

            var response = CreateResponse();
            response.Protocal = "HTTP/1.1";
            response.Status = HttpStatusCode.SwitchingProtocols;
            response.Headers["Upgrade"] = "websocket";
            response.Headers["Connection"] = "Upgrade";

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new HttpResponseEncoder());
                pipeline.AddLast(new OutboundSerializingHandler());

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel("http-encoder-upgrade"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    await pipeline.WriteAsync(response).WaitAsync(WaitTimeout);

                    var text = SentText(stream);
                    StringAssert.Contains(text, "HTTP/1.1 101  SwitchingProtocols", "应写出 101 状态行。");
                    StringAssert.Contains(text, "Upgrade: websocket", "应保留升级握手报头。");
                    Assert.IsFalse(text.Contains("Content-Length"), "101 响应不得追加 Content-Length。");
                    Assert.IsFalse(text.Contains("Connection: close"), "101 响应不得把连接覆写为关闭。");
                }
                finally
                {
                    await PipelineTestKit.StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 编码器只接受 HttpResponse 与 RawFrame，其他对象应直接失败而不是静默丢弃。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpResponseEncoder_UnsupportedMessage_Throws()
        {
            var options = PipelineTestKit.CreateOptions(new HttpRequestSplitter());
            var stream = new TestRawByteStream("http-encoder-invalid");

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new HttpResponseEncoder());
                pipeline.AddLast(new OutboundSerializingHandler());

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel("http-encoder-invalid"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                        () => pipeline.WriteAsync(new object()).WaitAsync(WaitTimeout));
                }
                finally
                {
                    await PipelineTestKit.StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 响应实体的构造函数是 internal，测试通过反射创建实例，不扩大产品公开面。
        /// </summary>
        private static HttpResponse CreateResponse()
        {
            return (HttpResponse)Activator.CreateInstance(typeof(HttpResponse), true);
        }

        private static string SentText(TestRawByteStream stream)
        {
            var builder = new StringBuilder();
            foreach (var frame in stream.Sent)
                builder.Append(Encoding.ASCII.GetString(frame));

            return builder.ToString();
        }
    }
}