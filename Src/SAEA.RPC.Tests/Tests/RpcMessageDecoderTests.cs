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
*命名空间：SAEA.RPC.Tests
*文件名： RpcMessageDecoderTests
*版本号： net10.0
*唯一标识：3562f7b8-6747-465e-a998-d864386d368f
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 17:05:00
*描述：RPC 入站解码用例：跨段、粘包、边界帧长与非法长度字段
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 17:05:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖 RPC 入站解码的跨段、粘包、边界帧长与非法长度字段
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.RPC.Model;
using SAEA.RPC.Net;
using SAEA.RPC.Tests.Tests.Support;
using SAEA.Sockets.Pipeline;

namespace SAEA.RPC.Tests
{
    /// <summary>
    /// RPC 解码器：整帧解成 <see cref="RSocketMsg"/>，非法长度字段按协议错误上报。
    /// </summary>
    [TestClass]
    public class RpcMessageDecoderTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 同一帧拆成两次写入时，分帧器累加、解码器只解整帧，最终产出一条完整消息。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task RpcMessageDecoder_SplitAcrossReads_ProducesSingleMessage()
        {
            var payload = RpcFrameBuilder.Payload(10);
            var frame = RpcFrameBuilder.Build(RpcFrameBuilder.Request(7, "HelloService", "SayHello", payload));

            await WithPipelineAsync("rpc-decoder-split", 1024, async (stream, capturing) =>
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

                var msg = (RSocketMsg)await capturing.FirstAsync(WaitTimeout);

                Assert.AreEqual((byte)RSocketMsgType.Request, msg.Type, "消息类型应原样保留。");
                Assert.AreEqual(7L, msg.SequenceNumber, "请求序号应原样保留。");
                Assert.AreEqual("HelloService", msg.ServiceName, "服务名应原样还原。");
                Assert.AreEqual("SayHello", msg.MethodName, "方法名应原样还原。");
                CollectionAssert.AreEqual(payload, msg.Data, "数据段应完整还原。");
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 两帧合在一次写入里时，分帧器应切出两条消息且顺序不变。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task RpcMessageDecoder_TwoFramesInOneRead_ProducesTwoMessagesInOrder()
        {
            var firstPayload = RpcFrameBuilder.Payload(3);
            var secondPayload = RpcFrameBuilder.Payload(11);

            var first = RpcFrameBuilder.Build(RpcFrameBuilder.Response(101, firstPayload));
            var second = RpcFrameBuilder.Build(RpcFrameBuilder.Request(102, "GroupService", "Join", secondPayload));

            await WithPipelineAsync("rpc-decoder-sticky", 1024, async (stream, capturing) =>
            {
                await PipelineTestKit.WriteAsync(stream, PipelineTestKit.Concat(first, second));

                Assert.IsTrue(await capturing.WaitCountAsync(2, WaitTimeout), "两帧应各产出一条消息。");

                var messages = new List<object>(capturing.Messages);

                var firstMessage = (RSocketMsg)messages[0];
                var secondMessage = (RSocketMsg)messages[1];

                Assert.AreEqual((byte)RSocketMsgType.Response, firstMessage.Type, "第一条消息应是应答帧。");
                Assert.AreEqual(101L, firstMessage.SequenceNumber, "第一条序号应与第一帧一致。");
                CollectionAssert.AreEqual(firstPayload, firstMessage.Data, "第一条数据段应与第一帧一致。");

                Assert.AreEqual((byte)RSocketMsgType.Request, secondMessage.Type, "第二条消息应是请求帧。");
                Assert.AreEqual(102L, secondMessage.SequenceNumber, "第二条序号应与第二帧一致。");
                Assert.AreEqual("GroupService", secondMessage.ServiceName, "第二条服务名应与第二帧一致。");
                Assert.AreEqual("Join", secondMessage.MethodName, "第二条方法名应与第二帧一致。");
                CollectionAssert.AreEqual(secondPayload, secondMessage.Data, "第二条数据段应与第二帧一致。");
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 最小帧（长度字段恰为 20）应被接受，服务名、方法名与数据段全部为空。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task RpcMessageDecoder_MinimalTotal_ProducesEmptyFields()
        {
            var frame = RpcFrameBuilder.Build(RpcFrameBuilder.Ping());

            await WithPipelineAsync("rpc-decoder-minimal", 1024, async (stream, capturing) =>
            {
                await PipelineTestKit.WriteAsync(stream, frame);

                Assert.IsTrue(await capturing.WaitCountAsync(1, WaitTimeout), "最小帧应产出一条消息。");

                var msg = (RSocketMsg)await capturing.FirstAsync(WaitTimeout);

                Assert.AreEqual((byte)RSocketMsgType.Ping, msg.Type, "类型应原样保留。");
                Assert.AreEqual(20u, msg.Total, "最小帧的长度字段应为 20。");
                Assert.AreEqual(string.Empty, msg.ServiceName, "空服务名应还原为空串。");
                Assert.AreEqual(string.Empty, msg.MethodName, "空方法名应还原为空串。");
                Assert.IsNull(msg.Data, "无数据段时应保持 null。");
                Assert.IsFalse(msg.IsPooled, "复制出来的数据段不依赖池化缓冲。");
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 数据段应复制成独立数组，帧被释放后仍可读。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task RpcMessageDecoder_DataCopied_SurvivesFrameRelease()
        {
            var payload = RpcFrameBuilder.Payload(4096);
            var frame = RpcFrameBuilder.Build(RpcFrameBuilder.Request(9, "HelloService", "Echo", payload));

            RSocketMsg captured = null;

            await WithPipelineAsync("rpc-decoder-copy", 8192, async (stream, capturing) =>
            {
                await PipelineTestKit.WriteAsync(stream, frame);

                Assert.IsTrue(await capturing.WaitCountAsync(1, WaitTimeout), "整帧应产出一条消息。");
                captured = (RSocketMsg)await capturing.FirstAsync(WaitTimeout);
            }).ConfigureAwait(false);

            // 到这里管道与帧缓冲都已释放，复制出来的数据段仍必须完整可读。
            CollectionAssert.AreEqual(payload, captured.Data, "数据段应复制成独立数组。");
            Assert.IsFalse(captured.IsPooled, "复制出来的数据段不依赖池化缓冲。");
        }

        /// <summary>
        /// 长度字段小于固定字段总长时按协议错误上报，且不产出消息。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task RpcMessageDecoder_TotalBelowFixedLength_Throws()
        {
            // 声明 total=19，实际只写 20 字节，使分帧器按 1+19 切出一条长度足以进入解码器的帧。
            var frame = RpcFrameBuilder.BuildRaw((byte)RSocketMsgType.Ping, 19, 0, null, null, null);
            frame = Slice(frame, 0, 20);

            await AssertProtocolErrorAsync("rpc-decoder-total", frame, "非法的RPC帧长度").ConfigureAwait(false);
        }

        /// <summary>
        /// 服务名长度字段越过帧尾时按协议错误上报。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task RpcMessageDecoder_ServiceLengthBeyondFrame_Throws()
        {
            var frame = RpcFrameBuilder.BuildRaw(
                (byte)RSocketMsgType.Request, 20, 1, null, null, null, serviceLength: 100);

            await AssertProtocolErrorAsync("rpc-decoder-slen", frame, "RPC帧服务名长度越界").ConfigureAwait(false);
        }

        /// <summary>
        /// 方法名长度字段越过帧尾时按协议错误上报。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task RpcMessageDecoder_MethodLengthBeyondFrame_Throws()
        {
            var frame = RpcFrameBuilder.BuildRaw(
                (byte)RSocketMsgType.Request, 20, 1, null, null, null, methodLength: 100);

            await AssertProtocolErrorAsync("rpc-decoder-mlen", frame, "RPC帧方法名长度越界").ConfigureAwait(false);
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
            var options = PipelineTestKit.CreateOptions(RpcFrameSplitter.Create(1024));
            var stream = new TestRawByteStream(channelId);
            var capturing = new CapturingInboundHandler();
            var exceptions = new CapturingExceptionHandler();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new RpcMessageDecoder());
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
                }
            }
        }

        /// <summary>
        /// 按给定帧体上限装配一条解码链并执行用例，结束后统一收口。
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
            var options = PipelineTestKit.CreateOptions(RpcFrameSplitter.Create(maxFrameLength));
            var stream = new TestRawByteStream(channelId);
            var capturing = new CapturingInboundHandler();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new RpcMessageDecoder());
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