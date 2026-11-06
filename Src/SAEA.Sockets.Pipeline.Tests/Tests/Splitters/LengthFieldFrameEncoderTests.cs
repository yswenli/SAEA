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
*命名空间：SAEA.Sockets.Pipeline.Tests.Tests.Splitters
*文件名： LengthFieldFrameEncoderTests
*版本号： v26.11.5.1
*唯一标识：e84b1a3c-1250-4857-ac42-a11482ac2344
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：LengthFieldFrameEncoderTests
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：LengthFieldFrameEncoderTests
*
*****************************************************************************/
using System;
using System.Buffers;
using System.Buffers.Binary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.Caching;
using SAEA.Sockets.Base;
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Splitters
{
    /// <summary>
    /// 长度字段编码器：body 长度口径、类型字节与旧协议逐字节一致。
    /// </summary>
    [TestClass]
    public class LengthFieldFrameEncoderTests
    {
        /// <summary>
        /// 长度字段写入 body 长度而非总长度，类型字节位于长度字段之后，body 原样跟随
        /// </summary>
        [TestMethod]
        public void Encode_WritesBodyLengthNotTotalLength_AndTypeByte()
        {
            var body = new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50 };
            var frame = Encode(new LengthFieldFrameEncoder(), body, 0x07);

            Assert.AreEqual(FrameConstants.P_Head + body.Length, frame.Length);
            Assert.AreEqual((long)body.Length, BinaryPrimitives.ReadInt64LittleEndian(frame.AsSpan(0, FrameConstants.P_LEN)));
            Assert.AreNotEqual((long)(body.Length + FrameConstants.P_Head),
                BinaryPrimitives.ReadInt64LittleEndian(frame.AsSpan(0, FrameConstants.P_LEN)));
            Assert.AreEqual((byte)0x07, frame[FrameConstants.P_LEN]);
            CollectionAssert.AreEqual(body, frame.AsSpan(FrameConstants.P_Head).ToArray());
        }

        /// <summary>
        /// 类型码 0 与 255 属于合法边界，写入后与入参一致
        /// </summary>
        [TestMethod]
        public void Encode_TypeCodeZeroAnd255_AreAccepted()
        {
            var body = new byte[] { 0xAA };

            var zero = Encode(new LengthFieldFrameEncoder(), body, 0);
            Assert.AreEqual((byte)0, zero[FrameConstants.P_LEN]);

            var max = Encode(new LengthFieldFrameEncoder(), body, 255);
            Assert.AreEqual((byte)255, max[FrameConstants.P_LEN]);
        }

        /// <summary>
        /// 编码输出与 BaseSocketProtocal.WriteTo 写出的字节逐字节一致
        /// </summary>
        [TestMethod]
        public void Encode_MatchesBaseSocketProtocalWriteTo_ByteForByte()
        {
            var body = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };
            const byte type = 0x5A;

            var expected = new byte[FrameConstants.P_Head + body.Length];
            using (var writer = new PooledBufferWriter(64))
            {
                new BaseSocketProtocal(type, body.AsMemory()).WriteTo(writer);
                writer.WrittenSpan.CopyTo(expected);
                Assert.AreEqual(expected.Length, writer.WrittenCount);
            }

            var actual = Encode(new LengthFieldFrameEncoder(), body, type);
            CollectionAssert.AreEqual(expected, actual);
        }

        /// <summary>
        /// 空 body 只写出 9 字节（8 字节长度字段 0 加 1 字节类型）
        /// </summary>
        [TestMethod]
        public void Encode_EmptyBody_WritesExactlyNineBytesWithZeroLength()
        {
            var frame = Encode(new LengthFieldFrameEncoder(), Array.Empty<byte>(), 0x42);

            Assert.AreEqual(FrameConstants.P_Head, frame.Length);
            Assert.AreEqual(0L, BinaryPrimitives.ReadInt64LittleEndian(frame.AsSpan(0, FrameConstants.P_LEN)));
            Assert.AreEqual((byte)0x42, frame[FrameConstants.P_LEN]);
        }

        /// <summary>
        /// 类型码小于 0 或大于 255 时抛 ArgumentOutOfRangeException
        /// </summary>
        [TestMethod]
        public void Encode_TypeCodeOutOfRange_Throws()
        {
            var encoder = new LengthFieldFrameEncoder();

            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => Encode(encoder, new byte[] { 1 }, -1));
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => Encode(encoder, new byte[] { 1 }, 256));
        }

        /// <summary>
        /// 目标 writer 为 null 时抛 ArgumentNullException
        /// </summary>
        [TestMethod]
        public void Encode_NullWriter_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(
                () => new LengthFieldFrameEncoder().Encode(new byte[] { 1 }, 0, null));
        }

        /// <summary>
        /// 写入已有内容的 writer 时追加在末尾，原有前缀字节保持不变
        /// </summary>
        [TestMethod]
        public void Encode_AppendsAfterExistingContent_LeavesPrefixUntouched()
        {
            var writer = new ArrayBufferWriter<byte>(8);
            var prefix = new byte[] { 0xEE, 0xEE };
            writer.Write(prefix);

            new LengthFieldFrameEncoder().Encode(new byte[] { 1, 2, 3 }, 0x09, writer);

            var span = writer.WrittenSpan;
            CollectionAssert.AreEqual(prefix, span.Slice(0, prefix.Length).ToArray());
            Assert.AreEqual(3L, BinaryPrimitives.ReadInt64LittleEndian(span.Slice(prefix.Length, FrameConstants.P_LEN)));
            Assert.AreEqual((byte)0x09, span[prefix.Length + FrameConstants.P_LEN]);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, span.Slice(prefix.Length + FrameConstants.P_Head).ToArray());
        }

        private static byte[] Encode(IFrameEncoder encoder, byte[] body, int typeCode)
        {
            using (var writer = new PooledBufferWriter(128))
            {
                encoder.Encode(body, typeCode, writer);
                return writer.WrittenSpan.ToArray();
            }
        }
    }
}