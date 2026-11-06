using System;
using System.Buffers;
using System.Buffers.Binary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.Caching;
using SAEA.Sockets.Base;
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Splitters
{
    [TestClass]
    public class LengthFieldFrameEncoderTests
    {
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

        [TestMethod]
        public void Encode_TypeCodeZeroAnd255_AreAccepted()
        {
            var body = new byte[] { 0xAA };

            var zero = Encode(new LengthFieldFrameEncoder(), body, 0);
            Assert.AreEqual((byte)0, zero[FrameConstants.P_LEN]);

            var max = Encode(new LengthFieldFrameEncoder(), body, 255);
            Assert.AreEqual((byte)255, max[FrameConstants.P_LEN]);
        }

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

        [TestMethod]
        public void Encode_EmptyBody_WritesExactlyNineBytesWithZeroLength()
        {
            var frame = Encode(new LengthFieldFrameEncoder(), Array.Empty<byte>(), 0x42);

            Assert.AreEqual(FrameConstants.P_Head, frame.Length);
            Assert.AreEqual(0L, BinaryPrimitives.ReadInt64LittleEndian(frame.AsSpan(0, FrameConstants.P_LEN)));
            Assert.AreEqual((byte)0x42, frame[FrameConstants.P_LEN]);
        }

        [TestMethod]
        public void Encode_TypeCodeOutOfRange_Throws()
        {
            var encoder = new LengthFieldFrameEncoder();

            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => Encode(encoder, new byte[] { 1 }, -1));
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => Encode(encoder, new byte[] { 1 }, 256));
        }

        [TestMethod]
        public void Encode_NullWriter_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(
                () => new LengthFieldFrameEncoder().Encode(new byte[] { 1 }, 0, null));
        }

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