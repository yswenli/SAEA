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
*文件名： QueueMessageEncoderTests
*版本号： net10.0
*唯一标识：a8f5c073-2d14-4e69-b7c8-1f0a4e6b93d7
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 20:40:00
*描述：队列出站编码用例：线格式布局、UTF-8 长度口径与批量拼接
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 20:40:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖队列出站编码的线格式布局、UTF-8 长度口径与批量拼接
*
*****************************************************************************/
using System;
using System.Buffers.Binary;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.Caching;
using SAEA.QueueSocket.Net;
using SAEA.QueueSocket.Tests.Tests.Support;
using SAEA.QueueSocket.Type;

namespace SAEA.QueueSocket.Tests
{
    /// <summary>
    /// 队列编码器：把 <see cref="QueueSocketMsg"/> 写成
    /// <c>[1B type][4B total][4B nLen][name][4B tLen][topic][data]</c>。
    /// </summary>
    [TestClass]
    public class QueueMessageEncoderTests
    {
        /// <summary>
        /// 带名称、主题与数据段时，各字段的偏移与长度字段口径必须与线格式一致。
        /// </summary>
        [TestMethod]
        public void QueueMessageEncoder_PublishWithData_MatchesWireLayout()
        {
            var payload = QueueFrameBuilder.Payload(5);
            var msg = QueueFrameBuilder.Publish("producer", "topic-a", payload);

            var frame = QueueMessageEncoder.Encode(msg);

            var nameBytes = Encoding.UTF8.GetBytes("producer");
            var topicBytes = Encoding.UTF8.GetBytes("topic-a");
            var total = 12 + nameBytes.Length + topicBytes.Length + payload.Length;

            Assert.AreEqual(1 + total, frame.Length, "整帧长度应为 1 + 长度字段值。");
            Assert.AreEqual((byte)QueueSocketMsgType.Publish, frame[0], "首字节应为消息类型。");
            Assert.AreEqual(total, BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(1, 4)), "长度字段应覆盖类型字节之后的全部内容。");
            Assert.AreEqual(nameBytes.Length, BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(5, 4)), "名称长度应按字节数写入。");

            var offset = 9;
            CollectionAssert.AreEqual(nameBytes, Slice(frame, offset, nameBytes.Length), "名称应按 UTF-8 原样写入。");
            offset += nameBytes.Length;

            Assert.AreEqual(topicBytes.Length, BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(offset, 4)), "主题长度应按字节数写入。");
            offset += 4;

            CollectionAssert.AreEqual(topicBytes, Slice(frame, offset, topicBytes.Length), "主题应按 UTF-8 原样写入。");
            offset += topicBytes.Length;

            CollectionAssert.AreEqual(payload, Slice(frame, offset, payload.Length), "数据段应原样写入。");
            Assert.AreEqual(frame.Length, offset + payload.Length, "数据段之后不得有多余字节。");
        }

        /// <summary>
        /// 空名称、空主题与无数据段时，长度字段应恰为固定字段总长 12。
        /// </summary>
        [TestMethod]
        public void QueueMessageEncoder_EmptyFields_WritesMinimalTotal()
        {
            var frame = QueueMessageEncoder.Encode(QueueFrameBuilder.Ping());

            Assert.AreEqual(13, frame.Length, "最小帧整帧长度应为 13。");
            Assert.AreEqual(12, BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(1, 4)), "最小帧长度字段应为 12。");
            Assert.AreEqual(0, BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(5, 4)), "空名称的长度字段应为 0。");
            Assert.AreEqual(0, BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(9, 4)), "空主题的长度字段应为 0。");
        }

        /// <summary>
        /// 中文名称与主题应按 UTF-8 字节数写长度，而不是字符数。
        /// </summary>
        [TestMethod]
        public void QueueMessageEncoder_Utf8Names_WritesByteLength()
        {
            var msg = QueueFrameBuilder.Publish("生产者", "主题", null);

            var frame = QueueMessageEncoder.Encode(msg);

            var nameBytes = Encoding.UTF8.GetBytes("生产者");
            var topicBytes = Encoding.UTF8.GetBytes("主题");

            Assert.AreEqual(nameBytes.Length, BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(5, 4)), "名称长度应按 UTF-8 字节数写入。");

            var topicLengthOffset = 9 + nameBytes.Length;
            Assert.AreEqual(topicBytes.Length, BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(topicLengthOffset, 4)), "主题长度应按 UTF-8 字节数写入。");

            CollectionAssert.AreEqual(nameBytes, Slice(frame, 9, nameBytes.Length), "名称应按 UTF-8 原样写入。");
            CollectionAssert.AreEqual(topicBytes, Slice(frame, topicLengthOffset + 4, topicBytes.Length), "主题应按 UTF-8 原样写入。");
        }

        /// <summary>
        /// 兼容入口对 null 消息应直接拒绝。
        /// </summary>
        [TestMethod]
        public void QueueMessageEncoder_NullMessage_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => QueueMessageEncoder.Encode(null));
        }

        /// <summary>
        /// 兼容入口 <see cref="QueueCoder.Encode(QueueSocketMsg)"/> 应与编码器输出完全一致。
        /// </summary>
        [TestMethod]
        public void QueueCoder_Encode_DelegatesToEncoder()
        {
            var msg = QueueFrameBuilder.Publish("p", "t", QueueFrameBuilder.Payload(6));

            var byCoder = QueueCoder.Encode(msg);
            var byEncoder = QueueMessageEncoder.Encode(msg);

            CollectionAssert.AreEqual(byEncoder, byCoder, "QueueCoder.Encode 应委托给 QueueMessageEncoder。");
        }

        /// <summary>
        /// 同一写入器连续编码三帧时，结果应与逐帧编码再拼接逐字节一致。
        /// </summary>
        [TestMethod]
        public void QueueMessageEncoder_EncodeTo_AppendsFramesSequentially()
        {
            var payload = QueueFrameBuilder.Payload(9);

            var expected = new byte[0];
            for (var i = 0; i < 3; i++)
            {
                expected = PipelineTestKit.Concat(
                    expected,
                    QueueMessageEncoder.Encode(QueueFrameBuilder.Publish("p", "t", payload)));
            }

            var writer = new PooledBufferWriter(64);
            try
            {
                for (var i = 0; i < 3; i++)
                {
                    QueueMessageEncoder.EncodeTo(writer, QueueFrameBuilder.Publish("p", "t", payload));
                }

                CollectionAssert.AreEqual(expected, writer.WrittenSpan.ToArray(), "连续编码应与逐帧编码拼接一致。");
            }
            finally
            {
                writer.Dispose();
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