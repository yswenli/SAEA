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
*命名空间：SAEA.QueueSocket.Tests
*文件名： QueueMessageDecoderTests
*版本号： net10.0
*唯一标识：f3a2d641-9b7e-4a05-8d1c-6e5f0b9c7a32
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 20:40:00
*描述：队列入站解码用例：跨段、粘包、边界帧长与非法长度字段
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 20:40:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖队列入站解码的跨段、粘包、边界帧长与非法长度字段
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.QueueSocket.Model;
using SAEA.QueueSocket.Net;
using SAEA.QueueSocket.Tests.Tests.Support;
using SAEA.QueueSocket.Type;
using SAEA.Sockets.Pipeline;

namespace SAEA.QueueSocket.Tests
{
    /// <summary>
    /// 队列解码器：整帧解成 <see cref="QueueMsg"/>，非法长度字段按协议错误上报。
    /// </summary>
    [TestClass]
    public class QueueMessageDecoderTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 同一帧拆成两次写入时，分帧器累加、解码器只解整帧，最终产出一条完整消息。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task QueueMessageDecoder_SplitAcrossReads_ProducesSingleMessage()
        {
            var payload = QueueFrameBuilder.Payload(10);
            var frame = QueueFrameBuilder.Build(QueueFrameBuilder.Publish("producer", "topic-a", payload));

            await WithPipelineAsync("queue-decoder-split", 1024, async (stream, capturing) =>
            {
                // 只发前 3 字节：连 5 字节帧头都没到齐，分帧器必须把残帧留在管道里等待。
                await PipelineTestKit.WriteAsync(stream, Slice(frame, 0, 3));
                await Task.Delay(50);
                Assert.AreEqual(0, capturing.Count, "帧未到齐时不得产出消息。");

                await PipelineTestKit.WriteAsync(stream, Slice(frame, 3, frame.Length - 3));

                Assert.IsTrue(await capturing.WaitCountAsync(1, WaitTimeout), "帧到齐后应产出一条消息。");

                // 再等一小段时间，确认没有把同一帧拆成两条。
                await Task.Delay(100);
                Assert.AreEqual(1, capturing.Count, "一次完整帧只能产出一条消息。");

                var msg = (QueueMsg)await capturing.FirstAsync(WaitTimeout);

                Assert.AreEqual(QueueSocketMsgType.Publish, msg.Type, "消息类型应原样保留。");
                Assert.AreEqual("producer", msg.Name, "名称应原样还原。");
                Assert.AreEqual("topic-a", msg.Topic, "主题应原样还原。");
                Assert.AreEqual(payload.Length, msg.Data.Length, "数据段长度应与原帧一致。");
                CollectionAssert.AreEqual(payload, msg.Data.ToArray(), "数据段应完整还原。");
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 两帧合在一次写入里时，分帧器应切出两条消息且顺序不变。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task QueueMessageDecoder_TwoFramesInOneRead_ProducesTwoMessagesInOrder()
        {
            var firstPayload = QueueFrameBuilder.Payload(3);
            var secondPayload = QueueFrameBuilder.Payload(11);

            var first = QueueFrameBuilder.Build(QueueFrameBuilder.Publish("p1", "topic-1", firstPayload));
            var second = QueueFrameBuilder.Build(
                new QueueSocketMsg(QueueSocketMsgType.Data, "p2", "topic-2", secondPayload));

            await WithPipelineAsync("queue-decoder-sticky", 1024, async (stream, capturing) =>
            {
                await PipelineTestKit.WriteAsync(stream, PipelineTestKit.Concat(first, second));

                Assert.IsTrue(await capturing.WaitCountAsync(2, WaitTimeout), "两帧应各产出一条消息。");

                var messages = new List<object>(capturing.Messages);

                var firstMessage = (QueueMsg)messages[0];
                Assert.AreEqual(QueueSocketMsgType.Publish, firstMessage.Type, "第一条消息应是发布帧。");
                Assert.AreEqual("p1", firstMessage.Name, "第一条名称应与第一帧一致。");
                Assert.AreEqual("topic-1", firstMessage.Topic, "第一条主题应与第一帧一致。");
                CollectionAssert.AreEqual(firstPayload, firstMessage.Data.ToArray(), "第一条数据段应与第一帧一致。");

                var secondMessage = (QueueMsg)messages[1];
                Assert.AreEqual(QueueSocketMsgType.Data, secondMessage.Type, "第二条消息应是数据帧。");
                Assert.AreEqual("p2", secondMessage.Name, "第二条名称应与第二帧一致。");
                Assert.AreEqual("topic-2", secondMessage.Topic, "第二条主题应与第二帧一致。");
                CollectionAssert.AreEqual(secondPayload, secondMessage.Data.ToArray(), "第二条数据段应与第二帧一致。");
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 最小帧（长度字段恰为 12）应被接受，名称、主题与数据段全部为空。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task QueueMessageDecoder_MinimalFrame_ProducesEmptyFields()
        {
            var frame = QueueFrameBuilder.Build(QueueFrameBuilder.Ping());

            await WithPipelineAsync("queue-decoder-minimal", 1024, async (stream, capturing) =>
            {
                await PipelineTestKit.WriteAsync(stream, frame);

                Assert.IsTrue(await capturing.WaitCountAsync(1, WaitTimeout), "最小帧应产出一条消息。");

                var msg = (QueueMsg)await capturing.FirstAsync(WaitTimeout);

                Assert.AreEqual(QueueSocketMsgType.Ping, msg.Type, "类型应原样保留。");
                Assert.IsNull(msg.Name, "空名称应还原为 null。");
                Assert.IsNull(msg.Topic, "空主题应还原为 null。");
                Assert.AreEqual(0, msg.Data.Length, "无数据段时长度应为 0。");
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 数据段应复制成独立缓冲，帧与管道释放后仍可读。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task QueueMessageDecoder_DataCopied_SurvivesFrameRelease()
        {
            var payload = QueueFrameBuilder.Payload(4096);
            var frame = QueueFrameBuilder.Build(QueueFrameBuilder.Publish("big", "topic-big", payload));

            QueueMsg captured = null;

            try
            {
                await WithPipelineAsync("queue-decoder-copy", 8192, async (stream, capturing) =>
                {
                    await PipelineTestKit.WriteAsync(stream, frame);

                    Assert.IsTrue(await capturing.WaitCountAsync(1, WaitTimeout), "整帧应产出一条消息。");

                    // 取出消息，避免收口时被统一释放，从而验证跨收口仍可读取缓冲。
                    var taken = capturing.TakeAll();
                    Assert.AreEqual(1, taken.Length, "整帧应只产出一条消息。");
                    captured = (QueueMsg)taken[0];
                }).ConfigureAwait(false);

                // 到这里管道与帧缓冲都已释放，消息持有的池化缓冲仍必须完整可读。
                CollectionAssert.AreEqual(payload, captured.Data.ToArray(), "数据段应复制成独立缓冲。");
            }
            finally
            {
                if (captured != null) captured.Dispose();
            }
        }

        /// <summary>
        /// 长度字段小于固定字段总长时按协议错误上报，且不产出消息。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task QueueMessageDecoder_TotalBelowFixedLength_Throws()
        {
            // 声明 total=11，使分帧器切出 12 字节的帧，解码器据此判定长度非法。
            var frame = QueueFrameBuilder.BuildRaw((byte)QueueSocketMsgType.Publish, 11, null, null, null);

            await AssertProtocolErrorAsync("queue-decoder-total", frame, "非法的队列帧长度").ConfigureAwait(false);
        }

        /// <summary>
        /// 名称长度字段越过帧尾时按协议错误上报。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task QueueMessageDecoder_NameLengthBeyondFrame_Throws()
        {
            var frame = QueueFrameBuilder.BuildRaw(
                (byte)QueueSocketMsgType.Publish, 12, null, null, null, nameLength: 100);

            await AssertProtocolErrorAsync("queue-decoder-nlen", frame, "队列帧名称长度越界").ConfigureAwait(false);
        }

        /// <summary>
        /// 主题长度字段越过帧尾时按协议错误上报。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task QueueMessageDecoder_TopicLengthBeyondFrame_Throws()
        {
            var frame = QueueFrameBuilder.BuildRaw(
                (byte)QueueSocketMsgType.Publish, 12, null, null, null, topicLength: 50);

            await AssertProtocolErrorAsync("queue-decoder-tlen", frame, "队列帧主题长度越界").ConfigureAwait(false);
        }

        /// <summary>
        /// 类型字节不在协议枚举范围内时按协议错误上报。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task QueueMessageDecoder_InvalidType_Throws()
        {
            var frame = QueueFrameBuilder.BuildRaw(0, 12, null, null, null);

            await AssertProtocolErrorAsync("queue-decoder-type", frame, "非法的队列消息类型").ConfigureAwait(false);
        }

        /// <summary>
        /// 装配一条带异常捕获的解码链，断言写入非法帧只上报协议错误、不产出消息。
        /// </summary>
        /// <param name="channelId">连接标识。</param>
        /// <param name="frame">非法帧字节。</param>
        /// <param name="expectedMessage">协议错误消息中的固定片段。</param>
        /// <returns>该用例的异步执行任务。</returns>
        private static async Task AssertProtocolErrorAsync(string channelId, byte[] frame, string expectedMessage)
        {
            var options = PipelineTestKit.CreateOptions(QueueFrameSplitter.Create(1024));
            var stream = new TestRawByteStream(channelId);
            var capturing = new CapturingInboundHandler();
            var exceptions = new CapturingExceptionHandler();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new QueueMessageDecoder());
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
        /// <param name="maxFrameLength">长度字段口径的帧长上限。</param>
        /// <param name="test">用例主体，接收字节流与捕获处理器。</param>
        /// <returns>用例执行完成后的任务。</returns>
        private static async Task WithPipelineAsync(
            string channelId,
            int maxFrameLength,
            Func<TestRawByteStream, CapturingInboundHandler, Task> test)
        {
            var options = PipelineTestKit.CreateOptions(QueueFrameSplitter.Create(maxFrameLength));
            var stream = new TestRawByteStream(channelId);
            var capturing = new CapturingInboundHandler();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new QueueMessageDecoder());
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