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
*文件名： MessageEncoderTests
*版本号： net10.0
*唯一标识：e2b7f48a-3c91-4d56-b08f-6a4e1c9d7b23
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 11:30:00
*描述：消息编码器的线格式布局与往返用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 11:30:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，消息编码器用例
*
*****************************************************************************/
using System;
using System.Buffers.Binary;
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
    /// 消息编码器：把 <see cref="ChatMessage"/> 写成 <c>[8B bodyLength 小端][1B type][body]</c>。
    /// </summary>
    [TestClass]
    public class MessageEncoderTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 长度字段只计帧体，类型字节固定写在帧头第 9 个字节。
        /// </summary>
        [TestMethod]
        public void MessageEncoder_TextMessage_MatchesWireLayout()
        {
            var message = MessageFrameBuilder.Text(ChatMessageType.PrivateMessage, "hello");

            var frame = MessageEncoder.Encode(message);

            Assert.IsTrue(frame.Length > MessageFrameBuilder.HeaderLength, "整帧长度应大于帧头长度。");

            var bodyLength = frame.Length - MessageFrameBuilder.HeaderLength;

            Assert.AreEqual(
                (long)bodyLength,
                BinaryPrimitives.ReadInt64LittleEndian(frame.AsSpan(0, FrameConstants.P_LEN)),
                "长度字段应小端写入帧体长度，不含帧头。");
            Assert.AreEqual(
                (byte)SocketProtocalType.ChatMessage,
                frame[FrameConstants.P_LEN],
                "类型字节应为聊天消息帧类型。");
        }

        /// <summary>
        /// 编码后再经处理器链解码，协议类型、内容与时间戳应完整还原。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task MessageEncoder_ThenDecoder_RoundTripsFields()
        {
            var message = MessageFrameBuilder.Text(ChatMessageType.Subscribe, "频道一");
            var frame = MessageEncoder.Encode(message);

            var options = PipelineTestKit.CreateOptions(MessageFrameSplitter.Create(4096));
            var stream = new TestRawByteStream("message-encoder-roundtrip");
            var capturing = new CapturingInboundHandler();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new MessageDecoder());
                pipeline.AddLast(capturing);
                pipeline.AddLast(new ExceptionTerminalHandler());

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel("message-encoder-roundtrip"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    await PipelineTestKit.WriteAsync(stream, frame);

                    Assert.IsTrue(await capturing.WaitCountAsync(1, WaitTimeout), "编码后的帧应能解出一条消息。");

                    var decoded = (ChatMessage)await capturing.FirstAsync(WaitTimeout);

                    Assert.AreEqual(message.Type, decoded.Type, "协议类型应完整还原。");
                    Assert.AreEqual(message.Content, decoded.Content, "消息内容应完整还原。");
                    Assert.AreEqual(message.DateTime, decoded.DateTime, "时间戳应完整还原。");
                }
                finally
                {
                    await PipelineTestKit.StopAsync(executor).ConfigureAwait(false);
                    capturing.DisposeAll();
                }
            }
        }

        /// <summary>
        /// 空内容也应编码成合法帧，解码后内容为空字符串。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task MessageEncoder_EmptyContent_RoundTripsEmptyString()
        {
            var frame = MessageEncoder.Encode(MessageFrameBuilder.Text(ChatMessageType.Login, ""));

            var options = PipelineTestKit.CreateOptions(MessageFrameSplitter.Create(4096));
            var stream = new TestRawByteStream("message-encoder-empty");
            var capturing = new CapturingInboundHandler();

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new MessageDecoder());
                pipeline.AddLast(capturing);
                pipeline.AddLast(new ExceptionTerminalHandler());

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel("message-encoder-empty"), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    await PipelineTestKit.WriteAsync(stream, frame);

                    Assert.IsTrue(await capturing.WaitCountAsync(1, WaitTimeout), "空内容帧应能解出一条消息。");

                    var decoded = (ChatMessage)await capturing.FirstAsync(WaitTimeout);

                    Assert.AreEqual(ChatMessageType.Login, decoded.Type, "协议类型应完整还原。");
                    Assert.AreEqual(string.Empty, decoded.Content, "空内容应还原为空字符串。");
                }
                finally
                {
                    await PipelineTestKit.StopAsync(executor).ConfigureAwait(false);
                    capturing.DisposeAll();
                }
            }
        }

        /// <summary>
        /// 离线编码入口对 null 消息应直接拒绝。
        /// </summary>
        [TestMethod]
        public void MessageEncoder_NullMessage_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => MessageEncoder.Encode(null));
        }
    }
}