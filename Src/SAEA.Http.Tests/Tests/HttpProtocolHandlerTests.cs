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
 *文件名： HttpProtocolHandlerTests
 *版本号： net10.0
 *唯一标识：e75e9635-25f8-43a1-b09c-380b319bd856
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/9 15:20:00
 *描述：HTTP 协议边界处理器的行为用例
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Reflection;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Http.Base;
using SAEA.Http.Tests.Tests.Support;
using SAEA.Sockets.Pipeline;

namespace SAEA.Http.Tests
{
    /// <summary>HTTP 协议边界处理器：升级协商、写失败不切换与协议错误回写。</summary>
    [TestClass]
    public class HttpProtocolHandlerTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 业务接受升级时先写出 101，写成功后分帧器才切到升级分帧器。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpProtocolHandler_UpgradeAccepted_SwitchesAfterWriteCompletes()
        {
            var http = new HttpRequestSplitter();
            var upgraded = new FixedLengthSplitter(4);
            var barrier = new SwitchableSplitter(http, upgraded);
            http.Barrier = barrier;

            var options = PipelineTestKit.CreateOptions(barrier);
            var stream = new TestRawByteStream("http-upgrade-ok");
            var capturing = new CapturingInboundHandler();
            var target = new StubUpgradeTarget(CreateUpgradeResponse());

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new HttpResponseEncoder());
                pipeline.AddLast(new OutboundSerializingHandler());
                pipeline.AddLast(new HttpRequestDecoder());
                pipeline.AddLast(new HttpProtocolHandler(barrier, upgraded, target));
                pipeline.AddLast(capturing);

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel("http-upgrade-ok"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    await PipelineTestKit.WriteAsync(stream, UpgradeRequest());

                    Assert.IsTrue(
                        await WaitUntilAsync(() => ReferenceEquals(barrier.Current, upgraded), WaitTimeout),
                        "握手写成功后应切到升级分帧器。");

                    var text = SentText(stream);
                    StringAssert.Contains(text, "HTTP/1.1 101  SwitchingProtocols", "应先写出 101 握手响应。");
                    Assert.AreEqual(1, target.Calls, "接受升级只应协商一次。");
                }
                finally
                {
                    await PipelineTestKit.StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 握手响应写失败时不得切换分帧器，连接按升级写失败收口。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpProtocolHandler_UpgradeWriteFails_DoesNotSwitch()
        {
            var http = new HttpRequestSplitter();
            var upgraded = new FixedLengthSplitter(4);
            var barrier = new SwitchableSplitter(http, upgraded);
            http.Barrier = barrier;

            var options = PipelineTestKit.CreateOptions(barrier);
            var stream = new FailingRawByteStream("http-upgrade-fail");
            var capturing = new CapturingInboundHandler();
            var target = new StubUpgradeTarget(CreateUpgradeResponse());

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new HttpResponseEncoder());
                pipeline.AddLast(new OutboundSerializingHandler());
                pipeline.AddLast(new HttpRequestDecoder());
                pipeline.AddLast(new HttpProtocolHandler(barrier, upgraded, target));
                pipeline.AddLast(capturing);

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel("http-upgrade-fail"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    // 直接写内层流：该流只把 SendAsync 换成抛错，读取侧仍是内存管道。
                    await stream.Inner.WriteAsync(UpgradeRequest()).WaitAsync(WaitTimeout);

                    var reason = await stream.Inner.Closed.Task.WaitAsync(WaitTimeout);
                    Assert.AreEqual("http-upgrade-write-failed", reason, "写失败应按升级写失败收口。");
                    Assert.AreSame(http, barrier.Current, "写失败不得切换分帧器。");
                    Assert.IsTrue(barrier.StopAfterCurrent, "写失败后屏障应保持开启。");
                }
                finally
                {
                    await PipelineTestKit.StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 业务拒绝升级时复位屏障，请求继续按普通 HTTP 交给下游。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpProtocolHandler_UpgradeRejected_ResetsBarrier()
        {
            var http = new HttpRequestSplitter();
            var upgraded = new FixedLengthSplitter(4);
            var barrier = new SwitchableSplitter(http, upgraded);
            http.Barrier = barrier;

            var options = PipelineTestKit.CreateOptions(barrier);
            var stream = new TestRawByteStream("http-upgrade-reject");
            var capturing = new CapturingInboundHandler();
            var target = new StubUpgradeTarget(null);

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new HttpResponseEncoder());
                pipeline.AddLast(new OutboundSerializingHandler());
                pipeline.AddLast(new HttpRequestDecoder());
                pipeline.AddLast(new HttpProtocolHandler(barrier, upgraded, target));
                pipeline.AddLast(capturing);

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel("http-upgrade-reject"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    await PipelineTestKit.WriteAsync(stream, UpgradeRequest());

                    Assert.IsTrue(await capturing.WaitCountAsync(1, WaitTimeout), "拒绝升级后请求应继续下发。");
                    Assert.AreSame(http, barrier.Current, "拒绝升级应保持当前分帧器。");
                    Assert.IsFalse(barrier.StopAfterCurrent, "拒绝升级应复位屏障。");
                }
                finally
                {
                    await PipelineTestKit.StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 分帧器抛出 413 时回写 413 响应并按协议错误收口，不再把异常派发到链尾。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpProtocolHandler_FrameProtocolError413_Writes413AndCloses()
        {
            var options = PipelineTestKit.CreateOptions(new HttpRequestSplitter(maxRequestSize: 8));
            var stream = new TestRawByteStream("http-protocol-413");
            var capturing = new CapturingInboundHandler();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new HttpResponseEncoder());
                pipeline.AddLast(new OutboundSerializingHandler());
                pipeline.AddLast(new HttpRequestDecoder());
                pipeline.AddLast(new HttpProtocolHandler(null, null, null));
                pipeline.AddLast(capturing);

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel("http-protocol-413"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    await PipelineTestKit.WriteAsync(stream, Encoding.ASCII.GetBytes(
                        "POST /big HTTP/1.1\r\n"
                        + "Host: a\r\n"
                        + "Content-Length: 9\r\n"
                        + "\r\n"));

                    var reason = await stream.Closed.Task.WaitAsync(WaitTimeout);
                    Assert.AreEqual("http-protocol-error", reason, "协议错误应按协议错误收口。");

                    var text = SentText(stream);
                    StringAssert.Contains(text, "HTTP/1.1 413  RequestEntityTooLarge", "应回写 413 状态行。");
                    StringAssert.Contains(text, "413 RequestEntityTooLarge", "应回写 413 响应体。");
                    Assert.AreEqual(0, capturing.Count, "协议异常不应再派发到链尾。");
                }
                finally
                {
                    await PipelineTestKit.StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 定长请求 413 时按「Content-Length 减去已到达字节」声明排空规格，且不按请求体上限截断。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpProtocolHandler_FrameProtocolError413_SetsDrainSpecForWholeUnreadBody()
        {
            var options = PipelineTestKit.CreateOptions(new HttpRequestSplitter(maxRequestSize: 8));
            var stream = new TestRawByteStream("http-protocol-413-drain-spec");
            var channel = new TestChannel("http-protocol-413-drain-spec");
            var capturing = new CapturingInboundHandler();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new HttpResponseEncoder());
                pipeline.AddLast(new OutboundSerializingHandler());
                pipeline.AddLast(new HttpRequestDecoder());
                pipeline.AddLast(new HttpProtocolHandler(null, null, null, 8, 500));
                pipeline.AddLast(capturing);

                var executor = new ChannelPipelineExecutor(stream, channel, options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    await PipelineTestKit.WriteAsync(stream, Encoding.ASCII.GetBytes(
                        "POST /big HTTP/1.1\r\n"
                        + "Host: a\r\n"
                        + "Content-Length: 40\r\n"
                        + "\r\n"));

                    var reason = await stream.Closed.Task.WaitAsync(WaitTimeout);
                    Assert.AreEqual("http-protocol-error", reason, "协议错误应按协议错误收口。");

                    long drainBytes;
                    int drainTimeoutMs;
                    Assert.IsTrue(
                        channel.TryTakeDrainSpec(out drainBytes, out drainTimeoutMs),
                        "413 后应声明有界排空规格。");
                    Assert.AreEqual(40L, drainBytes, "定长请求应排空全部剩余 40 字节，不能只按请求体上限 8 截断。");
                    Assert.AreEqual(500, drainTimeoutMs, "排空超时应取配置值。");
                }
                finally
                {
                    await PipelineTestKit.StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// chunked 请求无法推断剩余长度时，排空字节数按请求体上限兜底。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpProtocolHandler_FrameProtocolError413_ChunkedFallsBackToBodyLimit()
        {
            var options = PipelineTestKit.CreateOptions(new HttpRequestSplitter(maxRequestSize: 8));
            var stream = new TestRawByteStream("http-protocol-413-chunked");
            var channel = new TestChannel("http-protocol-413-chunked");
            var capturing = new CapturingInboundHandler();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new HttpResponseEncoder());
                pipeline.AddLast(new OutboundSerializingHandler());
                pipeline.AddLast(new HttpRequestDecoder());
                pipeline.AddLast(new HttpProtocolHandler(null, null, null, 8, 300));
                pipeline.AddLast(capturing);

                var executor = new ChannelPipelineExecutor(stream, channel, options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    await PipelineTestKit.WriteAsync(stream, Encoding.ASCII.GetBytes(
                        "POST /big HTTP/1.1\r\n"
                        + "Host: a\r\n"
                        + "Transfer-Encoding: chunked\r\n"
                        + "\r\n"
                        + "10\r\n"
                        + "0123456789abcdef\r\n"
                        + "0\r\n"
                        + "\r\n"));

                    var reason = await stream.Closed.Task.WaitAsync(WaitTimeout);
                    Assert.AreEqual("http-protocol-error", reason, "协议错误应按协议错误收口。");

                    long drainBytes;
                    int drainTimeoutMs;
                    Assert.IsTrue(
                        channel.TryTakeDrainSpec(out drainBytes, out drainTimeoutMs),
                        "chunked 413 后应按请求体上限兜底声明排空规格。");
                    Assert.AreEqual(8L, drainBytes, "剩余长度未知时排空字节数应取请求体上限。");
                    Assert.AreEqual(300, drainTimeoutMs, "排空超时应取配置值。");
                }
                finally
                {
                    await PipelineTestKit.StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 排空超时配置为 0 时不声明排空规格，413 后直接关闭。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpProtocolHandler_FrameProtocolError413_DrainDisabled_NoDrainSpec()
        {
            var options = PipelineTestKit.CreateOptions(new HttpRequestSplitter(maxRequestSize: 8));
            var stream = new TestRawByteStream("http-protocol-413-nodrain");
            var channel = new TestChannel("http-protocol-413-nodrain");
            var capturing = new CapturingInboundHandler();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new HttpResponseEncoder());
                pipeline.AddLast(new OutboundSerializingHandler());
                pipeline.AddLast(new HttpRequestDecoder());
                pipeline.AddLast(new HttpProtocolHandler(null, null, null, 8, 0));
                pipeline.AddLast(capturing);

                var executor = new ChannelPipelineExecutor(stream, channel, options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    await PipelineTestKit.WriteAsync(stream, Encoding.ASCII.GetBytes(
                        "POST /big HTTP/1.1\r\n"
                        + "Host: a\r\n"
                        + "Content-Length: 40\r\n"
                        + "\r\n"));

                    var reason = await stream.Closed.Task.WaitAsync(WaitTimeout);
                    Assert.AreEqual("http-protocol-error", reason, "协议错误应按协议错误收口。");

                    long drainBytes;
                    int drainTimeoutMs;
                    Assert.IsFalse(
                        channel.TryTakeDrainSpec(out drainBytes, out drainTimeoutMs),
                        "排空超时为 0 时不应声明排空规格。");
                }
                finally
                {
                    await PipelineTestKit.StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 收口末端识别到排空规格且字节流支持半关排空时，走排空关闭而不是普通关闭。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpProtocolHandler_FrameProtocolError413_DrainsBeforeClose()
        {
            var options = PipelineTestKit.CreateOptions(new HttpRequestSplitter(maxRequestSize: 8));
            var stream = new RecordingDrainedByteStream("http-protocol-413-drained");
            var capturing = new CapturingInboundHandler();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new HttpResponseEncoder());
                pipeline.AddLast(new OutboundSerializingHandler());
                pipeline.AddLast(new HttpRequestDecoder());
                pipeline.AddLast(new HttpProtocolHandler(null, null, null, 8, 500));
                pipeline.AddLast(capturing);

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel("http-protocol-413-drained"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    await stream.Inner.WriteAsync(Encoding.ASCII.GetBytes(
                        "POST /big HTTP/1.1\r\n"
                        + "Host: a\r\n"
                        + "Content-Length: 40\r\n"
                        + "\r\n")).WaitAsync(WaitTimeout);

                    var reason = await stream.Inner.Closed.Task.WaitAsync(WaitTimeout);
                    Assert.AreEqual("http-protocol-error", reason, "排空完成后仍应按协议错误收口。");
                    Assert.AreEqual(1, stream.DrainCount, "收口末端应只调用一次排空关闭。");
                    Assert.AreEqual("http-protocol-error", stream.DrainReason, "排空关闭应带上收口原因。");
                    Assert.AreEqual(40L, stream.DrainBytes, "排空字节数应覆盖全部剩余请求体。");
                    Assert.AreEqual(500, stream.DrainTimeoutMs, "排空超时应与声明规格一致。");
                }
                finally
                {
                    await PipelineTestKit.StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 分帧器抛出 431 时回写 431 响应并按协议错误收口。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpProtocolHandler_FrameProtocolError431_Writes431AndCloses()
        {
            var options = PipelineTestKit.CreateOptions(
                new HttpRequestSplitter(maxHeaderSize: 32));
            var stream = new TestRawByteStream("http-protocol-431");
            var capturing = new CapturingInboundHandler();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new HttpResponseEncoder());
                pipeline.AddLast(new OutboundSerializingHandler());
                pipeline.AddLast(new HttpRequestDecoder());
                pipeline.AddLast(new HttpProtocolHandler(null, null, null));
                pipeline.AddLast(capturing);

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel("http-protocol-431"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    await PipelineTestKit.WriteAsync(stream, Encoding.ASCII.GetBytes(
                        "GET / HTTP/1.1\r\n"
                        + "Host: a\r\n"
                        + "X-Long: " + new string('a', 40) + "\r\n"
                        + "\r\n"));

                    var reason = await stream.Closed.Task.WaitAsync(WaitTimeout);
                    Assert.AreEqual("http-protocol-error", reason, "协议错误应按协议错误收口。");

                    var text = SentText(stream);
                    StringAssert.Contains(text, "HTTP/1.1 431  RequestHeaderFieldsTooLarge", "应回写 431 状态行。");
                    StringAssert.Contains(text, "431 RequestHeaderFieldsTooLarge", "应回写 431 响应体。");
                    Assert.AreEqual(0, capturing.Count, "协议异常不应再派发到链尾。");
                }
                finally
                {
                    await PipelineTestKit.StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 不带升级报头的普通请求不进入升级协商，直接透传给下游。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpProtocolHandler_NonUpgradeRequest_PassesThrough()
        {
            var options = PipelineTestKit.CreateOptions(new HttpRequestSplitter());
            var stream = new TestRawByteStream("http-plain");
            var capturing = new CapturingInboundHandler();
            var target = new StubUpgradeTarget(CreateUpgradeResponse());

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new HttpResponseEncoder());
                pipeline.AddLast(new OutboundSerializingHandler());
                pipeline.AddLast(new HttpRequestDecoder());
                pipeline.AddLast(new HttpProtocolHandler(null, null, target));
                pipeline.AddLast(capturing);

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel("http-plain"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    await PipelineTestKit.WriteAsync(stream, Encoding.ASCII.GetBytes(
                        "GET /plain HTTP/1.1\r\n"
                        + "Host: a\r\n"
                        + "\r\n"));

                    Assert.IsTrue(await capturing.WaitCountAsync(1, WaitTimeout), "普通请求应透传到下游。");

                    var message = (HttpMessage)await capturing.FirstAsync(WaitTimeout);
                    Assert.AreEqual("/plain", message.Url, "应原样透传请求。");
                    Assert.AreEqual(0, target.Calls, "普通请求不应触发升级协商。");
                }
                finally
                {
                    await PipelineTestKit.StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 已登记会话时，握手写成功应走会话升级：标记已升级并由会话切换分帧器。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpProtocolHandler_UpgradeAccepted_UpgradesRegisteredSession()
        {
            var http = new HttpRequestSplitter();
            var upgraded = new FixedLengthSplitter(4);
            var barrier = new SwitchableSplitter(http, upgraded);
            http.Barrier = barrier;

            var options = PipelineTestKit.CreateOptions(barrier);
            var stream = new TestRawByteStream("http-session-upgrade");
            var channel = new TestChannel("http-session-upgrade");
            var capturing = new CapturingInboundHandler();
            var target = new StubUpgradeTarget(CreateUpgradeResponse());

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new HttpResponseEncoder());
                pipeline.AddLast(new OutboundSerializingHandler());
                pipeline.AddLast(new HttpRequestDecoder());
                pipeline.AddLast(new HttpProtocolHandler(barrier, upgraded, target));
                pipeline.AddLast(capturing);

                var session = CreateSession(channel, pipeline, barrier, upgraded);
                channel.SetAttribute(SessionAttributeKey, session);

                var executor = new ChannelPipelineExecutor(
                    stream, channel, options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    await PipelineTestKit.WriteAsync(stream, UpgradeRequest());

                    Assert.IsTrue(
                        await WaitUntilAsync(() => IsSessionUpgraded(session), WaitTimeout),
                        "握手写成功后应把会话标记为已升级。");
                    Assert.AreSame(upgraded, barrier.Current, "会话升级应把分帧器切到升级分帧器。");
                    Assert.AreEqual(0, capturing.Count, "升级请求本身不再下发给业务。");
                }
                finally
                {
                    await PipelineTestKit.StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 升级请求与首个 WebSocket 帧在同一 TCP 段到达时，101 写完后首帧应按原始帧下发。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpProtocolHandler_UpgradeWithFirstFrameInSameSegment_ForwardsRawFrame()
        {
            var http = new HttpRequestSplitter();
            var upgraded = new FixedLengthSplitter(4);
            var barrier = new SwitchableSplitter(http, upgraded);
            http.Barrier = barrier;

            var options = PipelineTestKit.CreateOptions(barrier);
            var stream = new TestRawByteStream("http-coalesced-upgrade");
            var channel = new TestChannel("http-coalesced-upgrade");
            var capturing = new FrameSnapshotHandler();
            var target = new StubUpgradeTarget(CreateUpgradeResponse());
            var firstFrame = Encoding.ASCII.GetBytes("WSP!");

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new HttpResponseEncoder());
                pipeline.AddLast(new OutboundSerializingHandler());
                pipeline.AddLast(new HttpRequestDecoder());
                pipeline.AddLast(new HttpProtocolHandler(barrier, upgraded, target));
                pipeline.AddLast(capturing);

                var session = CreateSession(channel, pipeline, barrier, upgraded);
                channel.SetAttribute(SessionAttributeKey, session);

                var executor = new ChannelPipelineExecutor(
                    stream, channel, options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    await PipelineTestKit.WriteAsync(
                        stream,
                        Concat(UpgradeRequest(), firstFrame));

                    var payload = await capturing.FirstAsync(WaitTimeout);
                    CollectionAssert.AreEqual(
                        firstFrame,
                        (byte[])payload,
                        "同一段里 101 之后的首帧应按原始字节下发，不能被当成 HTTP 报文。");

                    StringAssert.Contains(
                        SentText(stream),
                        "HTTP/1.1 101  SwitchingProtocols",
                        "先写出 101 握手响应。");
                }
                finally
                {
                    await PipelineTestKit.StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 升级后的无边界分帧器在单次读取交出 4 倍单帧上限时，应按上限切成多帧按序透传，
        /// 既不能丢字节，也不能按分帧协议错误收口。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpProtocolHandler_Upgrade后单次到达超过分帧上限_切块透传且不丢字节()
        {
            const int frameLimit = 64 * 1024;
            const int payloadLength = frameLimit * 4;

            var http = new HttpRequestSplitter();
            var upgraded = new TakeAvailableSplitter(frameLimit);
            var barrier = new SwitchableSplitter(http, upgraded);
            http.Barrier = barrier;

            var options = PipelineTestKit.CreateOptions(barrier);
            var stream = new TestRawByteStream("http-upgrade-chunk-split");
            var channel = new TestChannel("http-upgrade-chunk-split");
            var capturing = new FrameSnapshotHandler();
            var target = new StubUpgradeTarget(CreateUpgradeResponse());

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new HttpResponseEncoder());
                pipeline.AddLast(new OutboundSerializingHandler());
                pipeline.AddLast(new HttpRequestDecoder());
                pipeline.AddLast(new HttpProtocolHandler(barrier, upgraded, target));
                pipeline.AddLast(capturing);

                // 会话与生产链一致：解码器据 IsUpgraded 放行原始帧，不会把升级后的字节再当 HTTP 报文解析。
                var session = CreateSession(channel, pipeline, barrier, upgraded);
                channel.SetAttribute(SessionAttributeKey, session);

                var executor = new ChannelPipelineExecutor(
                    stream, channel, options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    await PipelineTestKit.WriteAsync(stream, UpgradeRequest());

                    // 等会话真正标记已升级再写数据：只看屏障会与 IsUpgraded 的写入错开一个窗口。
                    Assert.IsTrue(
                        await WaitUntilAsync(() => IsSessionUpgraded(session), WaitTimeout),
                        "101 写成功后应完成会话升级。");
                    Assert.AreSame(upgraded, barrier.Current, "升级后应切到无边界分帧器。");

                    var payload = new byte[payloadLength];
                    for (var i = 0; i < payload.Length; i++)
                        payload[i] = (byte)(i % 251);

                    // 一次性写入 4 倍上限：只有整段落在同一次 ReadAsync 里，才会走到按上限切块的分支；
                    // 拆成多次小块写会让每次读取天然不超限，覆盖不到目标路径。
                    await PipelineTestKit.WriteAsync(stream, payload);

                    Assert.IsTrue(
                        await WaitUntilAsync(
                            () => stream.Closed.Task.IsCompleted || ReceivedLength(capturing) >= payloadLength,
                            WaitTimeout),
                        "升级后的 256KB 未在等待上限内全部下发。");

                    var reason = stream.Closed.Task.IsCompleted ? stream.Closed.Task.Result : null;
                    Assert.IsNull(reason, "切块透传不应触发收口，实际收口原因：" + reason);

                    var segments = ReceivedSegments(capturing);
                    var receivedLength = 0;
                    foreach (var segment in segments)
                        receivedLength += segment.Length;

                    Assert.AreEqual(payloadLength, receivedLength, "升级后收到的总字节数应与写入量一致。");
                    Assert.IsTrue(
                        segments.Count >= payloadLength / frameLimit,
                        "单次交出 " + payloadLength + " 字节且单帧上限 " + frameLimit
                            + " 时至少应切成 " + (payloadLength / frameLimit) + " 帧，实际 " + segments.Count + " 帧。");

                    foreach (var segment in segments)
                    {
                        Assert.IsTrue(
                            segment.Length <= frameLimit,
                            "单帧长度不得超过分帧上限 " + frameLimit + "，实际 " + segment.Length + " 字节。");
                    }

                    var received = new byte[receivedLength];
                    var offset = 0;
                    foreach (var segment in segments)
                    {
                        Buffer.BlockCopy(segment, 0, received, offset, segment.Length);
                        offset += segment.Length;
                    }

                    CollectionAssert.AreEqual(payload, received, "切块后应按原顺序还原全部字节，不能丢字节或重排。");
                }
                finally
                {
                    await PipelineTestKit.StopAsync(executor);
                }
            }
        }

        /// <summary>
        /// 畸形 chunk-size 行应按协议错误回写 400 并收口。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpProtocolHandler_MalformedChunkedRequest_Writes400AndCloses()
        {
            var options = PipelineTestKit.CreateOptions(new HttpRequestSplitter());
            var stream = new TestRawByteStream("http-chunked");
            var capturing = new CapturingInboundHandler();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new HttpResponseEncoder());
                pipeline.AddLast(new OutboundSerializingHandler());
                pipeline.AddLast(new HttpRequestDecoder());
                pipeline.AddLast(new HttpProtocolHandler(null, null, null));
                pipeline.AddLast(capturing);

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel("http-chunked"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    await PipelineTestKit.WriteAsync(stream, Encoding.ASCII.GetBytes(
                        "POST /chunked HTTP/1.1\r\n"
                        + "Host: a\r\n"
                        + "Transfer-Encoding: chunked\r\n"
                        + "\r\n"
                        + "zz\r\n"));

                    var reason = await stream.Closed.Task.WaitAsync(WaitTimeout);
                    Assert.AreEqual("http-protocol-error", reason, "畸形 chunk-size 应按协议错误收口。");

                    var text = SentText(stream);
                    StringAssert.Contains(text, "HTTP/1.1 400  BadRequest", "应回写 400 状态行。");
                    Assert.AreEqual(0, capturing.Count, "协议异常不应再派发到链尾。");
                }
                finally
                {
                    await PipelineTestKit.StopAsync(executor);
                }
            }
        }

        private static byte[] UpgradeRequest()
        {
            return Encoding.ASCII.GetBytes(
                "GET /chat HTTP/1.1\r\n"
                + "Host: a\r\n"
                + "Upgrade: websocket\r\n"
                + "Connection: Upgrade\r\n"
                + "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n"
                + "\r\n");
        }

        /// <summary>响应实体的构造函数是 internal，测试通过反射创建实例。</summary>
        private static HttpResponse CreateUpgradeResponse()
        {
            var response = (HttpResponse)Activator.CreateInstance(typeof(HttpResponse), true);
            response.Protocal = "HTTP/1.1";
            response.Status = HttpStatusCode.SwitchingProtocols;
            response.Headers["Upgrade"] = "websocket";
            response.Headers["Connection"] = "Upgrade";
            return response;
        }

        private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (!condition())
            {
                if (DateTime.UtcNow >= deadline)
                    return false;

                await Task.Delay(10).ConfigureAwait(false);
            }

            return true;
        }

        private static string SentText(TestRawByteStream stream)
        {
            var builder = new StringBuilder();
            foreach (var frame in stream.Sent)
                builder.Append(Encoding.ASCII.GetString(frame));

            return builder.ToString();
        }

        /// <summary>已下发的帧字节总量，用于带上限地等待全部字节到齐。</summary>
        private static int ReceivedLength(FrameSnapshotHandler handler)
        {
            var total = 0;
            foreach (var item in handler.Items)
            {
                var segment = item as byte[];
                if (segment != null)
                    total += segment.Length;
            }

            return total;
        }

        /// <summary>按到达顺序取出链尾快照的帧字节；快照已在回调内复制，断言时可安全读取。</summary>
        private static List<byte[]> ReceivedSegments(FrameSnapshotHandler handler)
        {
            var segments = new List<byte[]>();
            foreach (var item in handler.Items)
            {
                var segment = item as byte[];
                if (segment != null)
                    segments.Add(segment);
            }

            return segments;
        }

        /// <summary>
        /// 连接会话是包内部类型，测试按属性键与反射构造，避免为了单测放宽可见性。
        /// </summary>
        private const string SessionAttributeKey = "saea.http.session";

        private static readonly Type SessionType =
            typeof(HttpProtocolHandler).Assembly.GetType("SAEA.Http.Base.Net.HttpConnectionSession", true);

        private static object CreateSession(
            IChannel channel,
            IChannelPipeline pipeline,
            SwitchableSplitter barrier,
            IFrameSplitter upgraded)
        {
            return Activator.CreateInstance(
                SessionType,
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { channel, null, pipeline, barrier, upgraded },
                null);
        }

        private static bool IsSessionUpgraded(object session)
        {
            var property = SessionType.GetProperty("IsUpgraded", BindingFlags.Instance | BindingFlags.NonPublic);
            return (bool)property.GetValue(session);
        }

        private static byte[] Concat(byte[] first, byte[] second)
        {
            var result = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, result, 0, first.Length);
            Buffer.BlockCopy(second, 0, result, first.Length, second.Length);
            return result;
        }

        /// <summary>
        /// 原始帧快照处理器：升级后下发的是池化帧，必须在回调内复制字节，否则断言时已被归还。
        /// </summary>
        private sealed class FrameSnapshotHandler : InboundHandlerAdapter
        {
            private readonly ConcurrentQueue<object> _items = new ConcurrentQueue<object>();
            private readonly TaskCompletionSource<object> _first =
                new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            public IReadOnlyCollection<object> Items
            {
                get { return _items; }
            }

            public override Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
            {
                var pooled = msg as PooledMessage;
                var snapshot = pooled != null ? (object)pooled.Frame.ToArray() : msg;
                _items.Enqueue(snapshot);
                _first.TrySetResult(snapshot);
                return Task.CompletedTask;
            }

            public async Task<object> FirstAsync(TimeSpan timeout)
            {
                return await _first.Task.WaitAsync(timeout).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 升级判定桩：按构造参数返回握手响应或拒绝，并记录调用次数。
        /// </summary>
        private sealed class StubUpgradeTarget : IHttpUpgradeTarget
        {
            private readonly HttpResponse _response;
            private int _calls;

            public StubUpgradeTarget(HttpResponse response)
            {
                _response = response;
            }

            public int Calls
            {
                get { return Volatile.Read(ref _calls); }
            }

            public Task<HttpResponse> TryUpgradeAsync(IChannelHandlerContext ctx, HttpMessage request)
            {
                Interlocked.Increment(ref _calls);
                return Task.FromResult(_response);
            }
        }

        /// <summary>
        /// 支持排空关闭的内存字节流：记录排空规格后按普通关闭结束，模拟读完剩余字节再关闭连接。
        /// </summary>
        private sealed class RecordingDrainedByteStream : IRawByteStream, IDrainedCloseStream
        {
            private readonly TestRawByteStream _inner;

            public RecordingDrainedByteStream(string id)
            {
                _inner = new TestRawByteStream(id);
            }

            public TestRawByteStream Inner
            {
                get { return _inner; }
            }

            public int DrainCount { get; private set; }

            public string DrainReason { get; private set; }

            public long DrainBytes { get; private set; }

            public int DrainTimeoutMs { get; private set; }

            public string ChannelId
            {
                get { return _inner.ChannelId; }
            }

            public PipeReader Input
            {
                get { return _inner.Input; }
            }

            public Task SendAsync(ReadOnlyMemory<byte> data, IDisposable owner)
            {
                return _inner.SendAsync(data, owner);
            }

            public void Pause()
            {
                _inner.Pause();
            }

            public void Resume()
            {
                _inner.Resume();
            }

            public Task CloseAsync(string reason)
            {
                return _inner.CloseAsync(reason);
            }

            public Task DrainAndCloseAsync(string reason, long drainBytes, int timeoutMs)
            {
                DrainCount++;
                DrainReason = reason;
                DrainBytes = drainBytes;
                DrainTimeoutMs = timeoutMs;
                return _inner.CloseAsync(reason);
            }
        }

        /// <summary>
        /// 发送必然失败的内存字节流：仍负责归还所有者，但把写异常抛给链。
        /// </summary>
        private sealed class FailingRawByteStream : IRawByteStream
        {
            private readonly TestRawByteStream _inner;

            public FailingRawByteStream(string id)
            {
                _inner = new TestRawByteStream(id);
            }

            public TestRawByteStream Inner
            {
                get { return _inner; }
            }

            public string ChannelId
            {
                get { return _inner.ChannelId; }
            }

            public PipeReader Input
            {
                get { return _inner.Input; }
            }

            public Task SendAsync(ReadOnlyMemory<byte> data, IDisposable owner)
            {
                if (owner != null)
                {
                    try { owner.Dispose(); }
                    catch (Exception) { }
                }

                throw new InvalidOperationException("模拟传输层写失败。");
            }

            public void Pause()
            {
                _inner.Pause();
            }

            public void Resume()
            {
                _inner.Resume();
            }

            public Task CloseAsync(string reason)
            {
                return _inner.CloseAsync(reason);
            }
        }
    }
}