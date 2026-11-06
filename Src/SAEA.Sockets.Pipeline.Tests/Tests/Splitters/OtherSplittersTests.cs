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

        [TestMethod]
        public void DelimiterSplitter_OverLimitWithoutDelimiter_ThrowsFrameProtocolException()
        {
            var splitter = new DelimiterSplitter((byte)'\n', 4);
            var buffered = new byte[] { 1, 2, 3, 4, 5 };

            Assert.ThrowsException<FrameProtocolException>(() => SplitIgnoringResult(splitter, buffered));
        }

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

        [TestMethod]
        public void DelimiterSplitter_Constructor_InvalidArguments_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() => new DelimiterSplitter((byte[])null, 16));
            Assert.ThrowsException<ArgumentException>(() => new DelimiterSplitter(new byte[0], 16));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new DelimiterSplitter((byte)'\n', 0));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new DelimiterSplitter((byte)'\n', -1));
        }

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