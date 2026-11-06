using System;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Splitters
{
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

            int consumed;
            ReadOnlyMemory<byte> frame;

            Assert.IsFalse(splitter.TrySplit(new ReadOnlyMemory<byte>(full, 0, 4), out consumed, out frame),
                "只收到部分帧体时不应产出帧。");

            Assert.IsTrue(splitter.TrySplit(full, out consumed, out frame));
            Assert.AreEqual(full.Length, consumed);
            CollectionAssert.AreEqual(head, frame.ToArray());
        }

        [TestMethod]
        public void DelimiterSplitter_MultipleFramesInOneInput_ProducesAllInOrder()
        {
            var splitter = new DelimiterSplitter((byte)'|', 64);
            var buffered = Encoding.ASCII.GetBytes("a|bb|ccc|");
            var expected = new[] { "a", "bb", "ccc" };

            var offset = 0;
            foreach (var item in expected)
            {
                int consumed;
                ReadOnlyMemory<byte> frame;
                Assert.IsTrue(splitter.TrySplit(new ReadOnlyMemory<byte>(buffered, offset, buffered.Length - offset),
                    out consumed, out frame));
                Assert.AreEqual(item + "|", Encoding.ASCII.GetString(buffered, offset, consumed));
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

            int consumed;
            ReadOnlyMemory<byte> frame;
            Assert.IsTrue(splitter.TrySplit(buffered, out consumed, out frame));
            Assert.AreEqual(4, consumed);
            Assert.AreEqual("abc|", Encoding.ASCII.GetString(frame.ToArray()));
        }

        [TestMethod]
        public void DelimiterSplitter_MultiByteDelimiter_MatchesFirstOccurrence()
        {
            var delimiter = new byte[] { (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' };
            var splitter = new DelimiterSplitter(delimiter, 64);
            var buffered = Encoding.ASCII.GetBytes("a\r\n\r\nb\r\n\r\n");

            int consumed;
            ReadOnlyMemory<byte> frame;

            Assert.IsTrue(splitter.TrySplit(buffered, out consumed, out frame));
            Assert.AreEqual(5, consumed);
            Assert.AreEqual("a", Encoding.ASCII.GetString(frame.ToArray()));

            var rest = new ReadOnlyMemory<byte>(buffered, consumed, buffered.Length - consumed);
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

            int consumed;
            ReadOnlyMemory<byte> frame;
            Assert.IsTrue(splitter.TrySplit(buffered, out consumed, out frame));
            Assert.AreEqual(5, consumed);
            Assert.AreEqual("abc", Encoding.ASCII.GetString(frame.ToArray()));
        }

        [TestMethod]
        public void LineSplitter_LfLine_KeepsContent()
        {
            var splitter = new LineSplitter(64);
            var buffered = Encoding.ASCII.GetBytes("abc\ndef\n");

            int consumed;
            ReadOnlyMemory<byte> frame;

            Assert.IsTrue(splitter.TrySplit(buffered, out consumed, out frame));
            Assert.AreEqual(4, consumed);
            Assert.AreEqual("abc", Encoding.ASCII.GetString(frame.ToArray()));

            var rest = new ReadOnlyMemory<byte>(buffered, consumed, buffered.Length - consumed);
            Assert.IsTrue(splitter.TrySplit(rest, out consumed, out frame));
            Assert.AreEqual(4, consumed);
            Assert.AreEqual("def", Encoding.ASCII.GetString(frame.ToArray()));
        }

        [TestMethod]
        public void LineSplitter_EmptyLine_ProducesEmptyFrame()
        {
            var splitter = new LineSplitter(64);
            var buffered = Encoding.ASCII.GetBytes("a\n\nb\n");

            int consumed;
            ReadOnlyMemory<byte> frame;

            Assert.IsTrue(splitter.TrySplit(buffered, out consumed, out frame));
            Assert.AreEqual("a", Encoding.ASCII.GetString(frame.ToArray()));

            var rest = new ReadOnlyMemory<byte>(buffered, consumed, buffered.Length - consumed);
            Assert.IsTrue(splitter.TrySplit(rest, out consumed, out frame));
            Assert.AreEqual(1, consumed);
            Assert.IsTrue(frame.IsEmpty, "连续两个换行之间应产出空帧。");
        }

        [TestMethod]
        public void FixedLengthSplitter_LessThanOneFrame_ReturnsFalse()
        {
            var splitter = new FixedLengthSplitter(4);

            int consumed;
            ReadOnlyMemory<byte> frame;
            Assert.IsFalse(splitter.TrySplit(new byte[] { 1, 2, 3 }, out consumed, out frame));
            Assert.AreEqual(0, consumed);
            Assert.IsTrue(frame.IsEmpty);
        }

        [TestMethod]
        public void FixedLengthSplitter_ExactlyOneFrame_ProducesFrame()
        {
            var splitter = new FixedLengthSplitter(4);
            var buffered = new byte[] { 1, 2, 3, 4 };

            int consumed;
            ReadOnlyMemory<byte> frame;
            Assert.IsTrue(splitter.TrySplit(buffered, out consumed, out frame));
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

            var offset = 0;
            foreach (var item in expected)
            {
                int consumed;
                ReadOnlyMemory<byte> frame;
                Assert.IsTrue(splitter.TrySplit(new ReadOnlyMemory<byte>(buffered, offset, buffered.Length - offset),
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
            int consumed;
            ReadOnlyMemory<byte> frame;
            splitter.TrySplit(buffered, out consumed, out frame);
        }
    }
}