/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MessageSocket.Tests
*文件名： MessageDecoderTests
*版本号： net10.0
*唯一标识：5a19e7c3-b6d4-4f82-8e07-1d3f6b2a9c58
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 11:30:00
*描述：消息解码器与分帧器的跨段、粘包、边界与非法帧用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 11:30:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，消息解码器与分帧器用例
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.MessageSocket.Model.Communication;
using SAEA.MessageSocket.Tests.Tests.Support;
using SAEA.Sockets.Model;
using SAEA.Sockets.Pipeline;

namespace SAEA.MessageSocket.Tests
{
    /// <summary>
    /// 消息解码器：整帧解成 <see cref="ChatMessage"/>，心跳与空帧不产出消息，非法帧按协议错误上报。
    /// </summary>
    [TestClass]
    public class MessageDecoderTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 同一帧拆成两次写入时，分帧器累加、解码器只解整帧，最终产出一条完整消息。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task MessageDecoder_SplitAcrossReads_ProducesSingleMessage()
        {
            var frame = MessageFrameBuilder.Build(
                MessageFrameBuilder.Text(ChatMessageType.PrivateMessage, "split-payload"));

            await WithPipelineAsync("message-decoder-split", 4096, async (stream, capturing) =>
            {
                // 只发前 4 字节：连 9 字节帧头都没到齐，分帧器必须把残帧留在管道里等待。
                await PipelineTestKit.WriteAsync(stream, Slice(frame, 0, 4));
                await Task.Delay(50);
                Assert.AreEqual(0, capturing.Count, "帧未到齐时不得产出消息。");

                await PipelineTestKit.WriteAsync(stream, Slice(frame, 4, frame.Length - 4));

                Assert.IsTrue(await capturing.WaitCountAsync(1, WaitTimeout), "帧到齐后应产出一条消息。");

                // 再等一小段时间，确认没有把同一帧拆成两条。
                await Task.Delay(100);
                Assert.AreEqual(1, capturing.Count, "一次完整帧只能产出一条消息。");

                var message = (ChatMessage)await capturing.FirstAsync(WaitTimeout);

                Assert.AreEqual(ChatMessageType.PrivateMessage, message.Type, "协议类型应原样保留。");
                Assert.AreEqual("split-payload", message.Content, "消息内容应完整还原。");
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 两帧合在一次写入里时，分帧器应切出两条消息且顺序不变。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task MessageDecoder_TwoFramesInOneRead_ProducesTwoMessagesInOrder()
        {
            var first = MessageFrameBuilder.Build(MessageFrameBuilder.Text(ChatMessageType.Subscribe, "channel-a"));
            var second = MessageFrameBuilder.Build(MessageFrameBuilder.Text(ChatMessageType.UnSubscribe, "channel-b"));

            await WithPipelineAsync("message-decoder-sticky", 4096, async (stream, capturing) =>
            {
                await PipelineTestKit.WriteAsync(stream, PipelineTestKit.Concat(first, second));

                Assert.IsTrue(await capturing.WaitCountAsync(2, WaitTimeout), "两帧应各产出一条消息。");

                var messages = new List<object>(capturing.Messages);

                var firstMessage = (ChatMessage)messages[0];
                Assert.AreEqual(ChatMessageType.Subscribe, firstMessage.Type, "第一条应是第一帧的类型。");
                Assert.AreEqual("channel-a", firstMessage.Content, "第一条内容应与第一帧一致。");

                var secondMessage = (ChatMessage)messages[1];
                Assert.AreEqual(ChatMessageType.UnSubscribe, secondMessage.Type, "第二条应是第二帧的类型。");
                Assert.AreEqual("channel-b", secondMessage.Content, "第二条内容应与第二帧一致。");
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 心跳帧只用于保活，不得产出业务消息。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task MessageDecoder_HeartbeatFrame_ProducesNoMessage()
        {
            await WithPipelineAsync("message-decoder-heartbeat", 4096, async (stream, capturing) =>
            {
                await PipelineTestKit.WriteAsync(stream, MessageFrameBuilder.Heartbeat());

                await Task.Delay(150);

                Assert.AreEqual(0, capturing.Count, "心跳帧不得产出业务消息。");
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 长度字段为 0 的普通帧没有帧体可反序列化，不产出消息。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task MessageDecoder_EmptyBody_ProducesNoMessage()
        {
            var frame = MessageFrameBuilder.BuildRaw(0, (byte)SocketProtocalType.ChatMessage, null);

            await WithPipelineAsync("message-decoder-empty", 4096, async (stream, capturing) =>
            {
                await PipelineTestKit.WriteAsync(stream, frame);

                await Task.Delay(150);

                Assert.AreEqual(0, capturing.Count, "空帧体不得产出消息。");
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 帧体长度恰好等于上限时应被接受。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task MessageDecoder_BodyAtMaxFrameLength_IsAccepted()
        {
            var frame = MessageFrameBuilder.Build(
                MessageFrameBuilder.Text(ChatMessageType.ChannelMessage, new string('x', 64)));

            var bodyLength = frame.Length - MessageFrameBuilder.HeaderLength;

            await WithPipelineAsync("message-decoder-max", bodyLength, async (stream, capturing) =>
            {
                await PipelineTestKit.WriteAsync(stream, frame);

                Assert.IsTrue(await capturing.WaitCountAsync(1, WaitTimeout), "帧体恰好等于上限时应产出一条消息。");
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 帧体长度超过上限时按协议错误上报，且不产出消息。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task MessageDecoder_BodyAboveMaxFrameLength_Throws()
        {
            var frame = MessageFrameBuilder.Build(
                MessageFrameBuilder.Text(ChatMessageType.ChannelMessage, new string('y', 64)));

            var bodyLength = frame.Length - MessageFrameBuilder.HeaderLength;

            await AssertProtocolErrorAsync("message-decoder-over-max", frame, bodyLength - 1, "非法的数据帧长度")
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 长度字段为负数时按协议错误上报，且不产出消息。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task MessageDecoder_NegativeBodyLength_Throws()
        {
            var frame = MessageFrameBuilder.BuildRaw(-1, (byte)SocketProtocalType.ChatMessage, null);

            await AssertProtocolErrorAsync("message-decoder-negative", frame, 4096, "非法的数据帧长度")
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 帧体不是合法的 protobuf 时按协议错误上报，且不产出消息。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task MessageDecoder_InvalidProtobufBody_Throws()
        {
            // 0xFF 的字段号与线类型组合非法，反序列化必然失败。
            var frame = MessageFrameBuilder.BuildRaw(
                1, (byte)SocketProtocalType.ChatMessage, new byte[] { 0xFF });

            await AssertProtocolErrorAsync("message-decoder-garbage", frame, 4096, "消息帧反序列化失败")
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 解码后帧体复制成独立数组，管道与帧缓冲释放后消息内容仍可读。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task MessageDecoder_BodyCopied_SurvivesFrameRelease()
        {
            var content = new string('z', 2048);
            var frame = MessageFrameBuilder.Build(MessageFrameBuilder.Text(ChatMessageType.GroupMessage, content));

            ChatMessage captured = null;

            await WithPipelineAsync("message-decoder-copy", 8192, async (stream, capturing) =>
            {
                await PipelineTestKit.WriteAsync(stream, frame);

                Assert.IsTrue(await capturing.WaitCountAsync(1, WaitTimeout), "整帧应产出一条消息。");

                var taken = capturing.TakeAll();
                Assert.AreEqual(1, taken.Length, "整帧应只产出一条消息。");
                captured = (ChatMessage)taken[0];
            }).ConfigureAwait(false);

            // 到这里管道与帧缓冲都已释放，消息内容仍必须完整可读。
            Assert.IsNotNull(captured, "应捕获到一条消息。");
            Assert.AreEqual(content, captured.Content, "消息内容应复制成独立缓冲。");
        }

        /// <summary>
        /// 装配一条带异常捕获的解码链，断言写入非法帧只上报协议错误、不产出消息。
        /// </summary>
        /// <param name="channelId">连接标识。</param>
        /// <param name="frame">非法帧字节。</param>
        /// <param name="maxFrameLength">长度字段口径的帧体上限。</param>
        /// <param name="expectedMessage">协议错误消息中的固定片段。</param>
        /// <returns>该用例的异步执行任务。</returns>
        private static async Task AssertProtocolErrorAsync(
            string channelId,
            byte[] frame,
            int maxFrameLength,
            string expectedMessage)
        {
            var options = PipelineTestKit.CreateOptions(MessageFrameSplitter.Create(maxFrameLength));
            var stream = new TestRawByteStream(channelId);
            var capturing = new CapturingInboundHandler();
            var exceptions = new CapturingExceptionHandler();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new MessageDecoder());
                pipeline.AddLast(capturing);
                pipeline.AddLast(exceptions);
                pipeline.AddLast(new ExceptionTerminalHandler());

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel(channelId), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    await PipelineTestKit.WriteAsync(stream, frame);

                    var error = await exceptions.FirstAsync(WaitTimeout);
                    StringAssert.Contains(error.Message, expectedMessage, "应上报协议错误。");
                    Assert.AreEqual(0, capturing.Count, "非法帧不得产出业务消息。");
                }
                finally
                {
                    await PipelineTestKit.StopAsync(executor).ConfigureAwait(false);
                    capturing.DisposeAll();
                }
            }
        }

        /// <summary>
        /// 按给定帧体上限装配一条解码链并执行用例，结束后统一收口并归还消息缓冲。
        /// </summary>
        /// <param name="channelId">连接标识。</param>
        /// <param name="maxFrameLength">长度字段口径的帧体上限。</param>
        /// <param name="test">用例主体，接收字节流与捕获处理器。</param>
        /// <returns>用例执行完成后的任务。</returns>
        private static async Task WithPipelineAsync(
            string channelId,
            int maxFrameLength,
            Func<TestRawByteStream, CapturingInboundHandler, Task> test)
        {
            var options = PipelineTestKit.CreateOptions(MessageFrameSplitter.Create(maxFrameLength));
            var stream = new TestRawByteStream(channelId);
            var capturing = new CapturingInboundHandler();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new MessageDecoder());
                pipeline.AddLast(capturing);
                pipeline.AddLast(new ExceptionTerminalHandler());

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel(channelId), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();
                    await test(stream, capturing).ConfigureAwait(false);
                }
                finally
                {
                    await PipelineTestKit.StopAsync(executor).ConfigureAwait(false);
                    capturing.DisposeAll();
                }
            }
        }

        private static byte[] Slice(byte[] source, int offset, int count)
        {
            var result = new byte[count];
            Buffer.BlockCopy(source, offset, result, 0, count);
            return result;
        }
    }
}