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
*命名空间：SAEA.WebSocket.Tests
*文件名： WsFrameDecoderTests
*版本号： net10.0
*唯一标识：7f4eb43c-8b4f-4421-8107-09a92f813046
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 19:10:00
*描述：WebSocket 帧解码器的分片重组、控制帧穿插与协议错误用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 19:10:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，WebSocket 帧解码器用例
*
*****************************************************************************/
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.Sockets.Pipeline;
using SAEA.WebSocket.Model;
using SAEA.WebSocket.Tests.Tests.Support;
using SAEA.WebSocket.Type;

namespace SAEA.WebSocket.Tests
{
    /// <summary>
    /// WebSocket 帧解码器：分片重组按消息边界产出，控制帧可穿插，非法交织与超限按协议错误拒绝。
    /// </summary>
    [TestClass]
    public class WsFrameDecoderTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        private const int FrameLimit = 1024 * 1024;

        /// <summary>
        /// 分片消息中间插入 Ping 时，Ping 先于重组完成的消息产出，消息内容按分片顺序拼接。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task WsFrameDecoder_FragmentedMessageWithInterleavedPing_ReassemblesWholeMessage()
        {
            var first = WsFrameBuilder.Frame((byte)WSProtocalType.Text, Encoding.UTF8.GetBytes("hello"), fin: false);
            var ping = WsFrameBuilder.Frame((byte)WSProtocalType.Ping, Encoding.UTF8.GetBytes("p"));
            var last = WsFrameBuilder.Frame((byte)WSProtocalType.Cont, Encoding.UTF8.GetBytes(" world"));

            var input = PipelineTestKit.Concat(first, PipelineTestKit.Concat(ping, last));

            await WithDecoderPipelineAsync("ws-fragments", FrameLimit, FrameLimit, async (stream, capturing) =>
            {
                await PipelineTestKit.WriteAsync(stream, input).ConfigureAwait(false);

                Assert.IsTrue(await capturing.WaitCountAsync(2, WaitTimeout).ConfigureAwait(false),
                    "应产出穿插的 Ping 与重组后的整条消息。");

                var messages = capturing.Messages;
                var control = (WSProtocal)GetAt(messages, 0);
                var message = (WSProtocal)GetAt(messages, 1);

                Assert.AreEqual((byte)WSProtocalType.Ping, control.Type, "控制帧应立即产出，不必等待整条消息结束。");
                Assert.AreEqual((byte)WSProtocalType.Text, message.Type, "重组后的消息类型取起始数据帧的 opcode。");
                Assert.AreEqual("hello world", Encoding.UTF8.GetString(message.Content.ToArray()),
                    "分片帧体应按到达顺序拼接。");
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 上一条分片消息未结束时又收到新的数据帧，按 <c>ws-interleaved-data-frame</c> 拒绝。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task WsFrameDecoder_NewDataFrameBeforeFragmentsEnd_ThrowsInterleavedDataFrame()
        {
            var first = WsFrameBuilder.Frame((byte)WSProtocalType.Text, Encoding.UTF8.GetBytes("part"), fin: false);
            var second = WsFrameBuilder.Frame((byte)WSProtocalType.Text, Encoding.UTF8.GetBytes("other"));

            await WithDecoderPipelineAsync("ws-interleaved", FrameLimit, FrameLimit, async (stream, capturing, errors) =>
            {
                await PipelineTestKit.WriteAsync(stream, PipelineTestKit.Concat(first, second)).ConfigureAwait(false);

                var ex = await errors.FirstAsync(WaitTimeout).ConfigureAwait(false);

                Assert.AreEqual(WebSocketProtocolErrors.InterleavedDataFrame, ex.Message,
                    "未结束的分片消息不能被新的数据帧打断。");
                Assert.AreEqual(0, capturing.Count, "协议错误之前不应产出任何业务消息。");
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 分片重组后的总长超过消息上限时按 <c>ws-message-too-large</c> 拒绝。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task WsFrameDecoder_FragmentsExceedMaxMessageSize_ThrowsMessageTooLarge()
        {
            const int maxMessageSize = 32;

            var first = WsFrameBuilder.Frame((byte)WSProtocalType.Binary, WsFrameBuilder.Payload(20), fin: false);
            var second = WsFrameBuilder.Frame((byte)WSProtocalType.Cont, WsFrameBuilder.Payload(20));

            await WithDecoderPipelineAsync("ws-too-large", FrameLimit, maxMessageSize, async (stream, capturing, errors) =>
            {
                await PipelineTestKit.WriteAsync(stream, PipelineTestKit.Concat(first, second)).ConfigureAwait(false);

                var ex = await errors.FirstAsync(WaitTimeout).ConfigureAwait(false);

                Assert.AreEqual(WebSocketProtocolErrors.MessageTooLarge, ex.Message,
                    "单帧未超限但重组后超限同样要拒绝。");
                Assert.AreEqual(0, capturing.Count, "超限消息不应产出到下游。");
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 服务端方向收到未带掩码的帧时按 <c>ws-mask-required</c> 拒绝。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task WsFrameDecoder_UnmaskedFrameWhenMaskRequired_ThrowsMissingMask()
        {
            var frame = WsFrameBuilder.Frame((byte)WSProtocalType.Text, Encoding.UTF8.GetBytes("x"), masked: false);

            await WithDecoderPipelineAsync("ws-mask", FrameLimit, FrameLimit, async (stream, capturing, errors) =>
            {
                await PipelineTestKit.WriteAsync(stream, frame).ConfigureAwait(false);

                var ex = await errors.FirstAsync(WaitTimeout).ConfigureAwait(false);

                Assert.AreEqual(WebSocketProtocolErrors.MissingMask, ex.Message,
                    "客户端发往服务端的帧必须带掩码。");
                Assert.AreEqual(0, capturing.Count, "缺掩码的帧不应产出到下游。");
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 取快照里指定下标的元素；快照为并发队列视图，不支持索引器。
        /// </summary>
        /// <param name="messages">消息快照。</param>
        /// <param name="index">目标下标。</param>
        /// <returns>该下标上的消息。</returns>
        private static object GetAt(System.Collections.Generic.IReadOnlyCollection<object> messages, int index)
        {
            var i = 0;
            foreach (var item in messages)
            {
                if (i++ == index)
                    return item;
            }

            Assert.Fail("快照里缺少下标 " + index + " 的消息。");
            return null;
        }

        /// <summary>
        /// 按给定上限装配一条「分帧 → 解码 → 捕获」的链并执行用例，结束后统一收口。
        /// </summary>
        /// <param name="channelId">连接标识。</param>
        /// <param name="maxFrameLength">单帧帧体上限。</param>
        /// <param name="maxMessageSize">单条消息上限。</param>
        /// <param name="test">用例主体，接收字节流与捕获处理器。</param>
        /// <returns>用例执行完成后的任务。</returns>
        private static Task WithDecoderPipelineAsync(
            string channelId,
            int maxFrameLength,
            int maxMessageSize,
            Func<TestRawByteStream, CapturingInboundHandler, Task> test)
        {
            return WithDecoderPipelineAsync(channelId, maxFrameLength, maxMessageSize,
                (stream, capturing, errors) => test(stream, capturing));
        }

        /// <summary>
        /// 按给定上限装配一条「分帧 → 解码 → 捕获」的链并执行用例，结束后统一收口。
        /// </summary>
        /// <param name="channelId">连接标识。</param>
        /// <param name="maxFrameLength">单帧帧体上限。</param>
        /// <param name="maxMessageSize">单条消息上限。</param>
        /// <param name="test">用例主体，接收字节流、捕获处理器与异常捕获处理器。</param>
        /// <returns>用例执行完成后的任务。</returns>
        private static async Task WithDecoderPipelineAsync(
            string channelId,
            int maxFrameLength,
            int maxMessageSize,
            Func<TestRawByteStream, CapturingInboundHandler, CapturingExceptionHandler, Task> test)
        {
            var options = PipelineTestKit.CreateOptions(new WsFrameSplitter(maxFrameLength));
            var stream = new TestRawByteStream(channelId);
            var capturing = new CapturingInboundHandler();
            var errors = new CapturingExceptionHandler();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new WsFrameDecoder(true, maxMessageSize));
                pipeline.AddLast(errors);
                pipeline.AddLast(capturing);
                pipeline.AddLast(new ExceptionTerminalHandler());

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel(channelId), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();
                    await test(stream, capturing, errors).ConfigureAwait(false);
                }
                finally
                {
                    await PipelineTestKit.StopAsync(executor).ConfigureAwait(false);
                }
            }
        }
    }
}