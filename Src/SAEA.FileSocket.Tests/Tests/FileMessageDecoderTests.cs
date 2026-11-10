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
*命名空间：SAEA.FileSocket.Tests
*文件名： FileMessageDecoderTests
*版本号： net10.0
*唯一标识：4df835cc-1fe5-4890-b69d-86f400f8e206
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 12:05:00
*描述：文件传输入站解码用例：跨段、粘包与帧体边界长度
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 12:05:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖文件传输入站解码的跨段、粘包与边界长度
*
*****************************************************************************/
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.FileSocket.Tests.Tests.Support;
using SAEA.Sockets.Base;
using SAEA.Sockets.Model;
using SAEA.Sockets.Pipeline;

namespace SAEA.FileSocket.Tests
{
    /// <summary>
    /// 文件传输解码器：整帧解成 <see cref="BaseSocketProtocal"/>，心跳帧不产生业务消息。
    /// </summary>
    [TestClass]
    public class FileMessageDecoderTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 同一帧拆成两次写入时，分帧器累加、解码器只解整帧，最终产出一条完整消息。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task FileMessageDecoder_SplitAcrossReads_ProducesSingleMessage()
        {
            var body = FileFrameBuilder.Payload(10);
            var frame = FileFrameBuilder.Build((byte)SocketProtocalType.BigData, body);

            await WithPipelineAsync("file-decoder-split", 1024, async (stream, capturing) =>
            {
                // 只发前 4 字节：长度字段都没到齐，分帧器必须把残帧留在管道里等待。
                await PipelineTestKit.WriteAsync(stream, Slice(frame, 0, 4));
                await Task.Delay(50);
                Assert.AreEqual(0, capturing.Count, "帧未到齐时不得产出消息。");

                await PipelineTestKit.WriteAsync(stream, Slice(frame, 4, frame.Length - 4));

                Assert.IsTrue(await capturing.WaitCountAsync(1, WaitTimeout), "帧到齐后应产出一条消息。");

                // 再等一小段时间，确认没有把同一帧拆成两条。
                await Task.Delay(100);
                Assert.AreEqual(1, capturing.Count, "一次完整帧只能产出一条消息。");

                var protocal = (BaseSocketProtocal)await capturing.FirstAsync(WaitTimeout);

                Assert.AreEqual((byte)SocketProtocalType.BigData, protocal.Type, "帧类型应原样保留。");
                Assert.AreEqual(body.Length, protocal.Content.Length, "帧体长度应与长度字段一致。");
                CollectionAssert.AreEqual(body, protocal.Content.ToArray(), "帧体应完整复制到协议对象。");
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 两次完整帧合在一次写入里时，分帧器应切出两条消息且顺序不变。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task FileMessageDecoder_TwoFramesInOneRead_ProducesTwoMessagesInOrder()
        {
            var firstBody = FileFrameBuilder.Payload(3);
            var secondBody = FileFrameBuilder.Payload(7);

            var first = FileFrameBuilder.Build((byte)SocketProtocalType.RequestSend, firstBody);
            var second = FileFrameBuilder.Build((byte)SocketProtocalType.BigData, secondBody);

            await WithPipelineAsync("file-decoder-sticky", 1024, async (stream, capturing) =>
            {
                await PipelineTestKit.WriteAsync(stream, PipelineTestKit.Concat(first, second));

                Assert.IsTrue(await capturing.WaitCountAsync(2, WaitTimeout), "两帧应各产出一条消息。");

                var messages = new System.Collections.Generic.List<object>(capturing.Messages);

                var firstMessage = (BaseSocketProtocal)messages[0];
                var secondMessage = (BaseSocketProtocal)messages[1];

                Assert.AreEqual((byte)SocketProtocalType.RequestSend, firstMessage.Type, "第一条消息应是请求帧。");
                CollectionAssert.AreEqual(firstBody, firstMessage.Content.ToArray(), "第一条帧体应与第一帧一致。");

                Assert.AreEqual((byte)SocketProtocalType.BigData, secondMessage.Type, "第二条消息应是文件块帧。");
                CollectionAssert.AreEqual(secondBody, secondMessage.Content.ToArray(), "第二条帧体应与第二帧一致。");
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 帧体长度为 0 的心跳帧按既有语义丢弃，不产生业务消息。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task FileMessageDecoder_EmptyHeartFrame_ProducesNoMessage()
        {
            var frame = FileFrameBuilder.Build((byte)SocketProtocalType.Heart, null);

            await WithPipelineAsync("file-decoder-heart", 1024, async (stream, capturing) =>
            {
                await PipelineTestKit.WriteAsync(stream, frame);
                await Task.Delay(150);

                Assert.AreEqual(0, capturing.Count, "心跳帧不应产出业务消息。");
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 帧体长度为 0 的非心跳帧仍是合法业务帧，应产出帧体为空的协议对象。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task FileMessageDecoder_ZeroBodyNonHeartFrame_ProducesEmptyMessage()
        {
            var frame = FileFrameBuilder.Build((byte)SocketProtocalType.AllowReceive, null);

            await WithPipelineAsync("file-decoder-zero", 1024, async (stream, capturing) =>
            {
                await PipelineTestKit.WriteAsync(stream, frame);

                Assert.IsTrue(await capturing.WaitCountAsync(1, WaitTimeout), "零长度帧体应答应产出一条消息。");

                var protocal = (BaseSocketProtocal)await capturing.FirstAsync(WaitTimeout);

                Assert.AreEqual((byte)SocketProtocalType.AllowReceive, protocal.Type, "帧类型应原样保留。");
                Assert.AreEqual(0, protocal.BodyLength, "帧体长度应为 0。");
                Assert.IsTrue(protocal.Content.IsEmpty, "帧体应为空。");
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 帧体长度为 1 时，帧头与帧体的边界不能错位。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task FileMessageDecoder_SingleByteBody_KeepsBoundary()
        {
            var body = new byte[] { 0x5A };
            var frame = FileFrameBuilder.Build((byte)SocketProtocalType.BigData, body);

            await WithPipelineAsync("file-decoder-one", 1024, async (stream, capturing) =>
            {
                await PipelineTestKit.WriteAsync(stream, frame);

                Assert.IsTrue(await capturing.WaitCountAsync(1, WaitTimeout), "单字节帧体应产出一条消息。");

                var protocal = (BaseSocketProtocal)await capturing.FirstAsync(WaitTimeout);

                Assert.AreEqual(1, protocal.BodyLength, "帧体长度应为 1。");
                CollectionAssert.AreEqual(body, protocal.Content.ToArray(), "单字节帧体应原样保留。");
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 帧体长度等于上限时仍应被接受，不得提前判定超限。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task FileMessageDecoder_BodyAtMaxLength_ProducesFullMessage()
        {
            const int maxFrameLength = 64;
            var body = FileFrameBuilder.Payload(maxFrameLength);
            var frame = FileFrameBuilder.Build((byte)SocketProtocalType.BigData, body);

            await WithPipelineAsync("file-decoder-max", maxFrameLength, async (stream, capturing) =>
            {
                await PipelineTestKit.WriteAsync(stream, frame);

                Assert.IsTrue(await capturing.WaitCountAsync(1, WaitTimeout), "达到上限的帧体应被接受。");

                var protocal = (BaseSocketProtocal)await capturing.FirstAsync(WaitTimeout);

                Assert.AreEqual(maxFrameLength, protocal.BodyLength, "帧体长度应等于上限值。");
                CollectionAssert.AreEqual(body, protocal.Content.ToArray(), "上限长度的帧体应完整保留。");
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 按给定帧体上限装配一条解码链并执行用例，结束后统一收口。
        /// </summary>
        /// <param name="channelId">连接标识。</param>
        /// <param name="maxFrameLength">帧体长度上限。</param>
        /// <param name="test">用例主体，接收字节流与捕获处理器。</param>
        /// <returns>用例执行完成后的任务。</returns>
        private static async Task WithPipelineAsync(
            string channelId,
            int maxFrameLength,
            Func<TestRawByteStream, CapturingInboundHandler, Task> test)
        {
            var options = PipelineTestKit.CreateOptions(FileFrameSplitter.Create(maxFrameLength));
            var stream = new TestRawByteStream(channelId);
            var capturing = new CapturingInboundHandler();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new FileMessageDecoder());
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