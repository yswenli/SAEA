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
*文件名： LengthFieldSplitterTests
*版本号： v26.11.5.1
*唯一标识：9365be29-3613-45b2-b021-a526b70749b6
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：LengthFieldSplitterTests
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：LengthFieldSplitterTests
*
*****************************************************************************/
using System;
using System.Buffers;
using System.Buffers.Binary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Splitters
{
    /// <summary>
    /// 长度字段分帧器：半包/粘包、剥离口径与单帧长度上限。
    /// </summary>
    [TestClass]
    public class LengthFieldSplitterTests
    {
        /// <summary>
        /// 输入不足 9 字节头部（0、1、8 字节）时返回 false，消耗量为 0 且帧为空
        /// </summary>
        [TestMethod]
        public void TrySplit_ShorterThanHead_ReturnsFalse()
        {
            var splitter = LengthFieldSplitter.Default(1024);

            foreach (var length in new[] { 0, 1, 8 })
            {
                var buffered = new byte[length];
                long consumed;
                ReadOnlySequence<byte> frame;
                var result = splitter.TrySplit(new ReadOnlySequence<byte>(buffered), out consumed, out frame);

                Assert.IsFalse(result, $"输入 {length} 字节不足头部，应返回 false。");
                Assert.AreEqual(0, consumed);
                Assert.IsTrue(frame.IsEmpty);
            }
        }

        /// <summary>
        /// 完整帧一次消耗头部加 body；Default 剥离 8 字节长度字段只留类型与 body，LegacyCompatible 保留整帧
        /// </summary>
        [TestMethod]
        public void TrySplit_CompleteFrame_ConsumesHeadPlusBody_AndDefaultStripsLengthField()
        {
            var body = new byte[] { 0xA1, 0xA2, 0xA3, 0xA4, 0xA5 };
            var raw = BuildFrame(0x07, body);

            long consumed;
            ReadOnlySequence<byte> frame;
            Assert.IsTrue(LengthFieldSplitter.Default(1024).TrySplit(new ReadOnlySequence<byte>(raw), out consumed, out frame));
            Assert.AreEqual(FrameConstants.P_Head + body.Length, consumed);
            Assert.AreEqual(1 + body.Length, frame.Length);
            Assert.AreEqual((byte)0x07, frame.First.Span[0]);
            CollectionAssert.AreEqual(body, frame.Slice(1).ToArray());

            long legacyConsumed;
            ReadOnlySequence<byte> legacyFrame;
            Assert.IsTrue(LengthFieldSplitter.LegacyCompatible(1024).TrySplit(new ReadOnlySequence<byte>(raw), out legacyConsumed, out legacyFrame));
            Assert.AreEqual(consumed, legacyConsumed);
            Assert.AreEqual(raw.Length, legacyFrame.Length);
            Assert.AreEqual(frame.First.Span[0], legacyFrame.First.Span[FrameConstants.P_LEN]);
            CollectionAssert.AreEqual(frame.ToArray(), legacyFrame.Slice(FrameConstants.P_LEN).ToArray());
        }

        /// <summary>
        /// body 按 1、2、3、7 字节分批到达时，不足整帧不产出帧，收齐后正常产出
        /// </summary>
        [TestMethod]
        public void TrySplit_BodyArrivingInChunks_ProducesFrame()
        {
            var body = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
            var raw = BuildFrame(0x42, body);
            var splitter = LengthFieldSplitter.Default(1024);

            foreach (var chunk in new[] { 1, 2, 3, 7 })
            {
                for (var prefix = 1; prefix < raw.Length; prefix += chunk)
                {
                    var partial = raw.AsMemory(0, prefix);
                    long consumed;
                    ReadOnlySequence<byte> frame;
                    Assert.IsFalse(splitter.TrySplit(new ReadOnlySequence<byte>(partial), out consumed, out frame),
                        $"只收到 {prefix} 字节时不应产出帧。");
                }

                long fullConsumed;
                ReadOnlySequence<byte> fullFrame;
                Assert.IsTrue(splitter.TrySplit(new ReadOnlySequence<byte>(raw), out fullConsumed, out fullFrame), $"按 {chunk} 字节切分后应能产出帧。");
                Assert.AreEqual(FrameConstants.P_Head + body.Length, fullConsumed);
                Assert.AreEqual(1 + body.Length, fullFrame.Length);
                CollectionAssert.AreEqual(body, fullFrame.Slice(1).ToArray());
            }
        }

        /// <summary>
        /// body 长度超过单帧上限或为负数时抛 FrameProtocolException
        /// </summary>
        [TestMethod]
        public void TrySplit_BodyLengthOverLimit_ThrowsFrameProtocolException()
        {
            var splitter = LengthFieldSplitter.Default(4);
            var overLimit = BuildFrame(0x01, new byte[5]);

            Assert.ThrowsException<FrameProtocolException>(() => SplitIgnoringResult(splitter, overLimit));

            var negative = new byte[FrameConstants.P_Head];
            BinaryPrimitives.WriteInt64LittleEndian(negative, -1);
            Assert.ThrowsException<FrameProtocolException>(() => SplitIgnoringResult(splitter, negative));
        }

        /// <summary>
        /// 三帧粘连时依次切出，消耗量之和等于缓冲长度且切完后缓冲排空
        /// </summary>
        [TestMethod]
        public void TrySplit_ThreeConcatenatedFrames_ProducesAllThenDrainsBuffer()
        {
            var first = BuildFrame(0x01, new byte[0]);
            var second = BuildFrame(0x02, new byte[] { 1, 2, 3 });
            var third = BuildFrame(0x03, new byte[] { 4, 5, 6, 7, 8, 9, 10 });

            var buffer = new byte[first.Length + second.Length + third.Length];
            Buffer.BlockCopy(first, 0, buffer, 0, first.Length);
            Buffer.BlockCopy(second, 0, buffer, first.Length, second.Length);
            Buffer.BlockCopy(third, 0, buffer, first.Length + second.Length, third.Length);

            var splitter = LengthFieldSplitter.Default(1024);
            var offset = 0L;
            var remaining = new ReadOnlySequence<byte>(buffer);

            long consumed;
            ReadOnlySequence<byte> frame;

            Assert.IsTrue(splitter.TrySplit(remaining, out consumed, out frame));
            Assert.AreEqual(first.Length, consumed);
            Assert.AreEqual(1, frame.Length);
            remaining = remaining.Slice(consumed);
            offset += consumed;

            Assert.IsTrue(splitter.TrySplit(remaining, out consumed, out frame));
            Assert.AreEqual(second.Length, consumed);
            CollectionAssert.AreEqual(new byte[] { 0x02, 1, 2, 3 }, frame.ToArray());
            remaining = remaining.Slice(consumed);
            offset += consumed;

            Assert.IsTrue(splitter.TrySplit(remaining, out consumed, out frame));
            Assert.AreEqual(third.Length, consumed);
            CollectionAssert.AreEqual(new byte[] { 0x03, 4, 5, 6, 7, 8, 9, 10 }, frame.ToArray());
            remaining = remaining.Slice(consumed);
            offset += consumed;

            Assert.AreEqual(buffer.Length, offset);
            Assert.IsTrue(remaining.IsEmpty, "三帧全部切完后缓冲应排空。");
        }

        /// <summary>
        /// 空 body 的帧合法：只消耗 9 字节头部，产出仅含 1 字节类型的帧
        /// </summary>
        [TestMethod]
        public void TrySplit_EmptyBody_IsValid()
        {
            var raw = BuildFrame(0x33, new byte[0]);

            long consumed;
            ReadOnlySequence<byte> frame;
            Assert.IsTrue(LengthFieldSplitter.Default(0).TrySplit(new ReadOnlySequence<byte>(raw), out consumed, out frame));
            Assert.AreEqual(FrameConstants.P_Head, consumed);
            Assert.AreEqual(1, frame.Length);
            Assert.AreEqual((byte)0x33, frame.First.Span[0]);
        }

        /// <summary>
        /// 1、2、3、4、8 字节长度字段在大小端下都能正确读出并切出帧
        /// </summary>
        [TestMethod]
        public void ReadLength_SupportsOneTwoThreeFourAndEightByteFieldsInBothEndianness()
        {
            var body = new byte[] { 0x11, 0x22, 0x33 };
            foreach (var fieldLength in new[] { 2, 3, 4, 8 })
            {
                foreach (var littleEndian in new[] { true, false })
                {
                    var raw = BuildCustomFrame(fieldLength, body.Length, body, littleEndian);
                    var splitter = new LengthFieldSplitter(1024, 0, fieldLength, 0, fieldLength, littleEndian);

                    long consumed;
                    ReadOnlySequence<byte> frame;
                    Assert.IsTrue(splitter.TrySplit(new ReadOnlySequence<byte>(raw), out consumed, out frame),
                        $"lengthFieldLength={fieldLength}, littleEndian={littleEndian} 应能切出帧。");
                    Assert.AreEqual(fieldLength + body.Length, consumed);
                    CollectionAssert.AreEqual(body, frame.ToArray());
                }
            }

            var single = BuildCustomFrame(1, body.Length, body, true);
            var singleSplitter = new LengthFieldSplitter(1024, 0, 1, 0, 1, true);
            long singleConsumed;
            ReadOnlySequence<byte> singleFrame;
            Assert.IsTrue(singleSplitter.TrySplit(new ReadOnlySequence<byte>(single), out singleConsumed, out singleFrame));
            Assert.AreEqual(1 + body.Length, singleConsumed);
            CollectionAssert.AreEqual(body, singleFrame.ToArray());
        }

        /// <summary>
        /// 上限为负、字段偏移为负、字段宽度不支持、剥离字节为负或超过头部时抛对应异常，负 lengthAdjustment 合法
        /// </summary>
        [TestMethod]
        public void Constructor_InvalidArguments_Throws()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new LengthFieldSplitter(-1));
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => new LengthFieldSplitter(1024, lengthFieldOffset: -1));
            Assert.ThrowsException<ArgumentException>(
                () => new LengthFieldSplitter(1024, lengthFieldLength: 5));
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => new LengthFieldSplitter(1024, initialBytesToStrip: -1));
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => new LengthFieldSplitter(1024, initialBytesToStrip: FrameConstants.P_Head + 1));

            // 负的 lengthAdjustment 合法：用于长度字段含头部的协议。
            var splitter = new LengthFieldSplitter(1024, 0, 8, -8, 0, true);
            Assert.AreEqual(1024, splitter.MaxFrameLength);
        }

        private static void SplitIgnoringResult(IFrameSplitter splitter, byte[] buffered)
        {
            long consumed;
            ReadOnlySequence<byte> frame;
            splitter.TrySplit(new ReadOnlySequence<byte>(buffered), out consumed, out frame);
        }

        private static byte[] BuildFrame(byte type, byte[] body)
        {
            var raw = new byte[FrameConstants.P_Head + body.Length];
            BinaryPrimitives.WriteInt64LittleEndian(raw.AsSpan(0, FrameConstants.P_LEN), body.Length);
            raw[FrameConstants.P_LEN] = type;
            Buffer.BlockCopy(body, 0, raw, FrameConstants.P_Head, body.Length);
            return raw;
        }

        private static byte[] BuildCustomFrame(int fieldLength, int bodyLength, byte[] body, bool littleEndian)
        {
            var raw = new byte[fieldLength + body.Length];
            var field = raw.AsSpan(0, fieldLength);
            var value = (long)bodyLength;
            for (var i = 0; i < fieldLength; i++)
            {
                var shift = littleEndian ? i : fieldLength - 1 - i;
                field[i] = (byte)(value >> (8 * shift));
            }
            Buffer.BlockCopy(body, 0, raw, fieldLength, body.Length);
            return raw;
        }
    }
}