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
*文件名： FileMessageEncoderTests
*版本号： net10.0
*唯一标识：757b5248-6cee-41a0-954e-92edf0f7f295
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 12:05:00
*描述：文件传输出站编码用例：协议对象到线格式的完整往返
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 12:05:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖文件传输出站编码与原始帧透传
*
*****************************************************************************/
using System;
using System.Collections.Generic;
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
    /// 文件传输出站编码器：<see cref="BaseSocketProtocal"/> 写成完整线格式，原始帧原样透传。
    /// </summary>
    [TestClass]
    public class FileMessageEncoderTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 带帧体的协议对象应编码成 <c>[8B 长度][1B 类型][body]</c>，长度字段按小端写入。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task FileMessageEncoder_WithBody_WritesFullWireFrame()
        {
            var body = FileFrameBuilder.Payload(16);

            var sent = await EncodeAsync(
                "file-encoder-body",
                new BaseSocketProtocal((byte)SocketProtocalType.BigData, body));

            var expected = FileFrameBuilder.Build((byte)SocketProtocalType.BigData, body);
            CollectionAssert.AreEqual(expected, sent, "编码结果应与线格式逐字节一致。");
        }

        /// <summary>
        /// 空帧体的应答帧只写 9 字节帧头，长度字段为 0。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task FileMessageEncoder_EmptyBody_WritesHeaderOnly()
        {
            var sent = await EncodeAsync(
                "file-encoder-empty",
                new BaseSocketProtocal((byte)SocketProtocalType.AllowReceive, ReadOnlyMemory<byte>.Empty));

            Assert.AreEqual(9, sent.Length, "空帧体只应写出帧头。");
            CollectionAssert.AreEqual(
                FileFrameBuilder.Build((byte)SocketProtocalType.AllowReceive, null),
                sent,
                "空帧体的编码结果应与线格式一致。");
        }

        /// <summary>
        /// 已经是原始帧的消息应原样交给传输层，不重复编码。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task FileMessageEncoder_RawFrame_PassesThroughUnchanged()
        {
            var bytes = new byte[] { 1, 2, 3, 4, 5 };

            var sent = await EncodeAsync("file-encoder-raw", new RawFrame(bytes, null));

            CollectionAssert.AreEqual(bytes, sent, "原始帧应原样写出。");
        }

        /// <summary>
        /// 装配一条只含编码器与串行器的出站链，写出一次消息并取回传输层收到的字节。
        /// </summary>
        /// <param name="channelId">连接标识。</param>
        /// <param name="message">待写出消息。</param>
        /// <returns>传输层收到的第一帧字节。</returns>
        private static async Task<byte[]> EncodeAsync(string channelId, object message)
        {
            var options = PipelineTestKit.CreateOptions(FileFrameSplitter.Create(1024));
            var stream = new TestRawByteStream(channelId);

            using (var pipeline = ChannelPipeline.Create(options))
            {
                pipeline.AddLast(new FileMessageEncoder());
                pipeline.AddLast(new OutboundSerializingHandler());
                pipeline.AddLast(new ExceptionTerminalHandler());

                var executor = new ChannelPipelineExecutor(
                    stream, new TestChannel(channelId), options, CancellationToken.None);
                executor.Attach(pipeline);

                try
                {
                    executor.Start();

                    await pipeline.WriteAsync(message).WaitAsync(WaitTimeout).ConfigureAwait(false);

                    var deadline = DateTime.UtcNow + WaitTimeout;
                    while (stream.Sent.Count == 0 && DateTime.UtcNow < deadline)
                        await Task.Delay(10).ConfigureAwait(false);

                    var frames = new List<byte[]>(stream.Sent);
                    Assert.AreEqual(1, frames.Count, "一次写只应交给传输层一帧。");

                    return frames[0];
                }
                finally
                {
                    await PipelineTestKit.StopAsync(executor).ConfigureAwait(false);
                }
            }
        }
    }
}