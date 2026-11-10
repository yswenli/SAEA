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
*文件名： WsHandshakeTests
*版本号： net10.0
*唯一标识：b8a2f20f-8c07-458e-abcb-dd464b992016
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 19:14:00
*描述：WebSocket 握手分帧与协议切换用例，含握手上限与 101 应答
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 19:14:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，WebSocket 握手分帧与协议切换用例
*
*****************************************************************************/
using System;
using System.Buffers;
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
    /// 握手分帧与协议切换：握手块独占首帧、切帧后继续切数据帧、切换上限取两阶段较大值。
    /// </summary>
    [TestClass]
    public class WsHandshakeTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 切换屏障的上限取握手报头上限与帧上限的较大值：帧上限更大时按帧上限，否则按握手上限。
        /// </summary>
        [TestMethod]
        public void WsHandshakeSplitter_SwitchableBarrier_TakesLargerLimitOfBothPhases()
        {
            var handshakeLimit = new WsHandshakeSplitter().MaxFrameLength;
            var largeFrame = new WsFrameSplitter(1024 * 1024);
            var smallFrame = new WsFrameSplitter(1024);

            var withLargeFrame = new SwitchableSplitter(new WsHandshakeSplitter(), largeFrame);
            var withSmallFrame = new SwitchableSplitter(new WsHandshakeSplitter(), smallFrame);

            Assert.AreEqual(largeFrame.MaxFrameLength, withLargeFrame.MaxFrameLength,
                "帧上限更大时屏障上限应取帧上限。");
            Assert.AreEqual(handshakeLimit, withSmallFrame.MaxFrameLength,
                "帧上限更小时屏障上限应取握手报头上限，避免握手块被截断。");
        }

        /// <summary>
        /// 握手块到齐后置停切并允许切到帧分帧器，剩余字节由帧分帧器继续切分。
        /// </summary>
        [TestMethod]
        public void WsHandshakeSplitter_HeaderComplete_StopsAfterCurrentThenSwitchesToFrameSplitter()
        {
            var request = WsFrameBuilder.HandshakeRequest();
            var handshake = new WsHandshakeSplitter();
            var frames = new WsFrameSplitter(1024 * 1024);
            var barrier = new SwitchableSplitter(handshake, frames);
            handshake.Barrier = barrier;

            long consumed;
            ReadOnlySequence<byte> block;

            Assert.IsFalse(handshake.TrySplit(new ReadOnlySequence<byte>(request, 0, request.Length - 2), out consumed, out block),
                "报头结束符未到齐时不能产出握手块。");

            Assert.IsTrue(handshake.TrySplit(new ReadOnlySequence<byte>(request), out consumed, out block),
                "报头到齐后应产出握手块。");
            Assert.AreEqual(request.Length, (int)consumed, "握手块应整体消费。");
            Assert.AreEqual(request.Length, (int)block.Length, "握手块应包含整段报头。");
            Assert.IsTrue(barrier.StopAfterCurrent, "产出握手块后必须停切，等切换完成再继续。");

            barrier.SwitchTo(frames);

            Assert.IsFalse(barrier.StopAfterCurrent, "切换完成后屏障应解除。");

            var payload = Encoding.UTF8.GetBytes("hi");
            var frame = WsFrameBuilder.Frame((byte)WSProtocalType.Text, payload);

            Assert.IsTrue(frames.TrySplit(new ReadOnlySequence<byte>(frame), out consumed, out block),
                "切换后应能切出完整 wire frame。");
            Assert.AreEqual(frame.Length, (int)consumed, "帧应整体消费。");
        }

        /// <summary>
        /// 服务端收到握手请求并紧随一帧数据时：先回 101，再把该帧解成业务消息交给下游。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task WsHandshakeHandler_RequestFollowedByFrame_Writes101ThenDeliversMessage()
        {
            var key = "dGhlIHNhbXBsZSBub25jZQ==";
            var request = WsFrameBuilder.HandshakeRequest(key);
            var payload = Encoding.UTF8.GetBytes("first-frame");
            var frame = WsFrameBuilder.Frame((byte)WSProtocalType.Text, payload);

            var handshake = new WsHandshakeSplitter();
            var frames = new WsFrameSplitter(1024 * 1024);
            var barrier = new SwitchableSplitter(handshake, frames);
            handshake.Barrier = barrier;

            var options = new ChannelPipelineOptions
            {
                Splitter = barrier,
                MaxBufferedBytes = (long)barrier.MaxFrameLength + FrameConstants.P_Head,
                ReadBufferSize = 64 * 1024,
                MaxConcurrentMessages = 1,
                QueueCapacity = 16,
                DispatchMode = ChannelDispatchMode.Queued,
                Guard = NullChannelGuard.Instance,
                SplitterSwitchTimeout = TimeSpan.FromSeconds(3)
            };

            var stream = new TestRawByteStream("ws-handshake");
            var capturing = new CapturingInboundHandler();
            var state = new WsHandshakeState();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new WsMessageEncoder(false, state));
                pipeline.AddLast(new OutboundSerializingHandler());
                pipeline.AddLast(WsHandshakeHandler.CreateServer(barrier, frames, null, state));
                pipeline.AddLast(new WsFrameDecoder(true, 1024 * 1024));
                pipeline.AddLast(capturing);
                pipeline.AddLast(new ExceptionTerminalHandler());

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel("ws-handshake"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    await PipelineTestKit.WriteAsync(stream, PipelineTestKit.Concat(request, frame))
                        .ConfigureAwait(false);

                    Assert.IsTrue(await capturing.WaitCountAsync(1, WaitTimeout).ConfigureAwait(false),
                        "握手紧随的数据帧应被帧解码器解出并派发。");

                    var message = (WSProtocal)await capturing.FirstAsync(WaitTimeout).ConfigureAwait(false);
                    Assert.AreEqual((byte)WSProtocalType.Text, message.Type, "数据帧类型应保持 Text。");
                    CollectionAssert.AreEqual(payload, message.Content.ToArray(), "帧体应原样还原。");
                    Assert.IsTrue(state.IsCompleted, "握手成功后应置位握手完成标记。");

                    var sent = Encoding.ASCII.GetString(stream.GetAllSentBytes());
                    Assert.IsTrue(sent.StartsWith("HTTP/1.1 101 Switching Protocols", StringComparison.Ordinal),
                        "服务端应先回 101，实际应答为：" + sent);
                    Assert.IsTrue(sent.Contains(WsFrameBuilder.ComputeAccept(key)),
                        "应答必须携带由客户端 Key 推出的 Sec-WebSocket-Accept。");
                }
                finally
                {
                    await PipelineTestKit.StopAsync(executor).ConfigureAwait(false);
                }
            }
        }
    }
}