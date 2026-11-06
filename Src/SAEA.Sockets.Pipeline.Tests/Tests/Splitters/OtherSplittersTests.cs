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
*文件名： OtherSplittersTests
*版本号： v26.11.5.1
*唯一标识：4509ff26-9fd2-4f58-a2fe-d67d01305a19
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：OtherSplittersTests
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：OtherSplittersTests
*
*****************************************************************************/
using System;
using System.Buffers;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Splitters
{
    /// <summary>
    /// 分隔符、行、定长三种分帧器的切分行为与参数校验。
    /// </summary>
    [TestClass]
    public class OtherSplittersTests
    {
        /// <summary>
        /// 分隔符未收全时不产出帧，收全后切出 "hello" 并剥离两字节分隔符
        /// </summary>
        [TestMethod]
        public void DelimiterSplitter_DelimiterSpanningTwoSegments_ProducesFrame()
        {
            var splitter = new DelimiterSplitter(new byte[] { 0xAA, 0xBB }, 64);
            var head = Encoding.ASCII.GetBytes("hello");
            var full = new byte[head.Length + 2];
            Buffer.BlockCopy(head, 0, full, 0, head.Length);
            full[head.Length] = 0xAA;
            full[head.Length + 1] = 0xBB;

            long consumed;
            ReadOnlySequence<byte> frame;

            Assert.IsFalse(splitter.TrySplit(new ReadOnlySequence<byte>(new ReadOnlyMemory<byte>(full, 0, 4)), out consumed, out frame),
                "只收到部分帧体时不应产出帧。");

            Assert.IsTrue(splitter.TrySplit(new ReadOnlySequence<byte>(full), out consumed, out frame));
            Assert.AreEqual(full.Length, consumed);
            CollectionAssert.AreEqual(head, frame.ToArray());
        }

        /// <summary>
        /// 单次输入含多帧时按顺序逐帧切出，每帧消耗量包含分隔符
        /// </summary>
        [TestMethod]
        public void DelimiterSplitter_MultipleFramesInOneInput_ProducesAllInOrder()
        {
            var splitter = new DelimiterSplitter((byte)'|', 64);
            var buffered = Encoding.ASCII.GetBytes("a|bb|ccc|");
            var expected = new[] { "a", "bb", "ccc" };

            var offset = 0L;
            foreach (var item in expected)
            {
                long consumed;
                ReadOnlySequence<byte> frame;
                Assert.IsTrue(splitter.TrySplit(new ReadOnlySequence<byte>(buffered).Slice(offset),
                    out consumed, out frame));
                Assert.AreEqual(item + "|", Encoding.ASCII.GetString(buffered, (int)offset, (int)consumed));
                Assert.AreEqual(item, Encoding.ASCII.GetString(frame.ToArray()));
                offset += consumed;
            }

            Assert.AreEqual(buffered.Length, offset);
        }

        /// <summary>
        /// 缓冲超过单帧上限仍未出现分隔符时抛 FrameProtocolException
        /// </summary>
        [TestMethod]
        public void DelimiterSplitter_OverLimitWithoutDelimiter_ThrowsFrameProtocolException()
        {
            var splitter = new DelimiterSplitter((byte)'\n', 4);
            var buffered = new byte[] { 1, 2, 3, 4, 5 };

            Assert.ThrowsException<FrameProtocolException>(() => SplitIgnoringResult(splitter, buffered));
        }

        /// <summary>
        /// stripDelimiter 为 false 时帧内容保留结尾的分隔符
        /// </summary>
        [TestMethod]
        public void DelimiterSplitter_StripDelimiterFalse_KeepsDelimiter()
        {
            var splitter = new DelimiterSplitter((byte)'|', 64, stripDelimiter: false);
            var buffered = Encoding.ASCII.GetBytes("abc|");

            long consumed;
            ReadOnlySequence<byte> frame;
            Assert.IsTrue(splitter.TrySplit(new ReadOnlySequence<byte>(buffered), out consumed, out frame));
            Assert.AreEqual(4, consumed);
            Assert.AreEqual("abc|", Encoding.ASCII.GetString(frame.ToArray()));
        }

        /// <summary>
        /// 四字节分隔符按首次出现位置切分，连续两段分隔各自结束一帧
        /// </summary>
        [TestMethod]
        public void DelimiterSplitter_MultiByteDelimiter_MatchesFirstOccurrence()
        {
            var delimiter = new byte[] { (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' };
            var splitter = new DelimiterSplitter(delimiter, 64);
            var buffered = Encoding.ASCII.GetBytes("a\r\n\r\nb\r\n\r\n");

            long consumed;
            ReadOnlySequence<byte> frame;

            Assert.IsTrue(splitter.TrySplit(new ReadOnlySequence<byte>(buffered), out consumed, out frame));
            Assert.AreEqual(5, consumed);
            Assert.AreEqual("a", Encoding.ASCII.GetString(frame.ToArray()));

            var rest = new ReadOnlySequence<byte>(buffered).Slice(consumed);
            Assert.IsTrue(splitter.TrySplit(rest, out consumed, out frame));
            Assert.AreEqual(5, consumed);
            Assert.AreEqual("b", Encoding.ASCII.GetString(frame.ToArray()));
        }

        /// <summary>
        /// 分隔符为 null 或空数组时抛 ArgumentException，单帧上限为 0 或负数时抛 ArgumentOutOfRangeException
        /// </summary>
        [TestMethod]
        public void DelimiterSplitter_Constructor_InvalidArguments_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() => new DelimiterSplitter((byte[])null, 16));
            Assert.ThrowsException<ArgumentException>(() => new DelimiterSplitter(new byte[0], 16));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new DelimiterSplitter((byte)'\n', 0));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new DelimiterSplitter((byte)'\n', -1));
        }

        /// <summary>
        /// CRLF 结尾的行剥掉回车符，帧内容为 "abc"
        /// </summary>
        [TestMethod]
        public void LineSplitter_CrLfLine_StripsCarriageReturn()
        {
            var splitter = new LineSplitter(64);
            var buffered = Encoding.ASCII.GetBytes("abc\r\n");

            long consumed;
            ReadOnlySequence<byte> frame;
            Assert.IsTrue(splitter.TrySplit(new ReadOnlySequence<byte>(buffered), out consumed, out frame));
            Assert.AreEqual(5, consumed);
            Assert.AreEqual("abc", Encoding.ASCII.GetString(frame.ToArray()));
        }

        /// <summary>
        /// LF 结尾的两行依次切出 "abc" 与 "def"，行内容保持不变
        /// </summary>
        [TestMethod]
        public void LineSplitter_LfLine_KeepsContent()
        {
            var splitter = new LineSplitter(64);
            var buffered = Encoding.ASCII.GetBytes("abc\ndef\n");

            long consumed;
            ReadOnlySequence<byte> frame;

            Assert.IsTrue(splitter.TrySplit(new ReadOnlySequence<byte>(buffered), out consumed, out frame));
            Assert.AreEqual(4, consumed);
            Assert.AreEqual("abc", Encoding.ASCII.GetString(frame.ToArray()));

            var rest = new ReadOnlySequence<byte>(buffered).Slice(consumed);
            Assert.IsTrue(splitter.TrySplit(rest, out consumed, out frame));
            Assert.AreEqual(4, consumed);
            Assert.AreEqual("def", Encoding.ASCII.GetString(frame.ToArray()));
        }

        /// <summary>
        /// 连续两个换行之间产出长度为 0 的空帧
        /// </summary>
        [TestMethod]
        public void LineSplitter_EmptyLine_ProducesEmptyFrame()
        {
            var splitter = new LineSplitter(64);
            var buffered = Encoding.ASCII.GetBytes("a\n\nb\n");

            long consumed;
            ReadOnlySequence<byte> frame;

            Assert.IsTrue(splitter.TrySplit(new ReadOnlySequence<byte>(buffered), out consumed, out frame));
            Assert.AreEqual("a", Encoding.ASCII.GetString(frame.ToArray()));

            var rest = new ReadOnlySequence<byte>(buffered).Slice(consumed);
            Assert.IsTrue(splitter.TrySplit(rest, out consumed, out frame));
            Assert.AreEqual(1, consumed);
            Assert.IsTrue(frame.IsEmpty, "连续两个换行之间应产出空帧。");
        }

        /// <summary>
        /// 输入不足一个定长帧时返回 false，消耗量为 0 且帧为空
        /// </summary>
        [TestMethod]
        public void FixedLengthSplitter_LessThanOneFrame_ReturnsFalse()
        {
            var splitter = new FixedLengthSplitter(4);

            long consumed;
            ReadOnlySequence<byte> frame;
            Assert.IsFalse(splitter.TrySplit(new ReadOnlySequence<byte>(new byte[] { 1, 2, 3 }), out consumed, out frame));
            Assert.AreEqual(0, consumed);
            Assert.IsTrue(frame.IsEmpty);
        }

        /// <summary>
        /// 输入恰好一帧时切出全部字节，MaxFrameLength 等于该帧长度
        /// </summary>
        [TestMethod]
        public void FixedLengthSplitter_ExactlyOneFrame_ProducesFrame()
        {
            var splitter = new FixedLengthSplitter(4);
            var buffered = new byte[] { 1, 2, 3, 4 };

            long consumed;
            ReadOnlySequence<byte> frame;
            Assert.IsTrue(splitter.TrySplit(new ReadOnlySequence<byte>(buffered), out consumed, out frame));
            Assert.AreEqual(4, consumed);
            CollectionAssert.AreEqual(buffered, frame.ToArray());
            Assert.AreEqual(4, splitter.MaxFrameLength);
        }

        /// <summary>
        /// 9 字节输入按 3 字节帧长依次切出三帧且顺序正确
        /// </summary>
        [TestMethod]
        public void FixedLengthSplitter_MultipleFrames_ProducesAllInOrder()
        {
            var splitter = new FixedLengthSplitter(3);
            var buffered = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            var expected = new[] { new byte[] { 1, 2, 3 }, new byte[] { 4, 5, 6 }, new byte[] { 7, 8, 9 } };

            var offset = 0L;
            foreach (var item in expected)
            {
                long consumed;
                ReadOnlySequence<byte> frame;
                Assert.IsTrue(splitter.TrySplit(new ReadOnlySequence<byte>(buffered).Slice(offset),
                    out consumed, out frame));
                Assert.AreEqual(3, consumed);
                CollectionAssert.AreEqual(item, frame.ToArray());
                offset += consumed;
            }

            Assert.AreEqual(buffered.Length, offset);
        }

        /// <summary>
        /// 帧长为 0 或负数时抛 ArgumentOutOfRangeException
        /// </summary>
        [TestMethod]
        public void FixedLengthSplitter_Constructor_ZeroOrNegative_Throws()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new FixedLengthSplitter(0));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new FixedLengthSplitter(-1));
        }

        private static void SplitIgnoringResult(IFrameSplitter splitter, byte[] buffered)
        {
            long consumed;
            ReadOnlySequence<byte> frame;
            splitter.TrySplit(new ReadOnlySequence<byte>(buffered), out consumed, out frame);
        }
    }
}