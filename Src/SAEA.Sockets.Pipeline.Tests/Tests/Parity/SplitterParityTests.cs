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
*命名空间：SAEA.Sockets.Pipeline.Tests.Tests.Parity
*文件名： SplitterParityTests
*版本号： v26.11.5.1
*唯一标识：b6a0f8cf-55c3-4a2f-8204-e4f9c1b8fa88
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：LengthFieldSplitter 与既有 BaseCoder.DecodeStream 的分帧对照： 同一批字节分别喂给两条实现，逐帧比对长度字段、类型、帧体与消费字节数
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：LengthFieldSplitter 与既有 BaseCoder.DecodeStream 的分帧对照： 同一批字节分别喂给两条实现，逐帧比对长度字段、类型、帧体与消费字节数
*
*****************************************************************************/
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.Caching;
using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Parity
{
    /// <summary>
    /// LengthFieldSplitter 与既有 BaseCoder.DecodeStream 的分帧对照：
    /// 同一批字节分别喂给两条实现，逐帧比对长度字段、类型、帧体与消费字节数。
    /// </summary>
    [TestClass]
    public class SplitterParityTests
    {
        private const int MaxBodyLength = 4096;
        private const int RandomSeed = 20261105;

        [TestMethod]
        public void LegacyCompatible_RandomChunks_MatchesBaseCoderDecodeStream()
        {
            var rng = new Random(RandomSeed);
            var frames = new List<byte[]>();
            for (var i = 0; i < 24; i++)
            {
                var bodyLength = rng.Next(0, 41);
                var type = (byte)rng.Next(0, 256);
                frames.Add(BuildWireFrame(type, RandomBody(rng, bodyLength)));
            }

            var wire = Concat(frames);
            var chunks = SplitRandomly(wire, rng);

            var observed = ObserveWithCoder(chunks);
            var legacy = FeedSplitter(LengthFieldSplitter.LegacyCompatible(MaxBodyLength), chunks);

            AssertLegacyMatchesCoder(observed, legacy);
            Assert.AreEqual(24, legacy.Count, "24 个连着写入的帧应全部产出。");
        }

        [TestMethod]
        public void DefaultAndLegacyCompatible_AgreeOnConsumed_AndOnlyStripLengthField()
        {
            var rng = new Random(RandomSeed);
            var wire = Concat(new[]
            {
                BuildWireFrame(0x00, Array.Empty<byte>()),
                BuildWireFrame(0xFF, RandomBody(rng, 1)),
                BuildWireFrame(0x42, RandomBody(rng, FrameConstants.P_Head)),
                BuildWireFrame(0x07, RandomBody(rng, 37))
            });
            var chunks = SplitRandomly(wire, rng);

            var observed = ObserveWithCoder(chunks);
            var legacy = FeedSplitter(LengthFieldSplitter.LegacyCompatible(MaxBodyLength), chunks);
            var def = FeedSplitter(LengthFieldSplitter.Default(MaxBodyLength), chunks);

            AssertLegacyMatchesCoder(observed, legacy);
            AssertDefaultMatchesLegacy(legacy, def);
        }

        [TestMethod]
        public void BoundaryBodyLengths_AndTypeBounds_MatchBaseCoder()
        {
            var bodyLengths = new[]
            {
                0,
                1,
                FrameConstants.P_Head - 1,
                FrameConstants.P_Head,
                FrameConstants.P_Head + 1
            };
            var types = new byte[] { 0, 255 };

            foreach (var type in types)
            {
                foreach (var bodyLength in bodyLengths)
                {
                    var wire = BuildWireFrame(type, SequenceBody(bodyLength));
                    var chunkings = new List<byte[]>[]
                    {
                        new List<byte[]> { wire },
                        SplitFixed(wire, 1),
                        SplitWithPattern(wire, new[] { 3, 2, 4, 2, 1, 7 })
                    };

                    foreach (var chunks in chunkings)
                    {
                        var observed = ObserveWithCoder(chunks);
                        var legacy = FeedSplitter(LengthFieldSplitter.LegacyCompatible(MaxBodyLength), chunks);
                        var def = FeedSplitter(LengthFieldSplitter.Default(MaxBodyLength), chunks);

                        Assert.AreEqual(1, observed.Count,
                            $"type={type}, bodyLen={bodyLength} 应产出且仅产出一帧。");
                        AssertLegacyMatchesCoder(observed, legacy);
                        AssertDefaultMatchesLegacy(legacy, def);
                    }
                }
            }
        }

        [TestMethod]
        public void ThreeConcatenatedFrames_SingleChunk_MatchesBaseCoder()
        {
            var wire = Concat(new[]
            {
                BuildWireFrame(0x00, Array.Empty<byte>()),
                BuildWireFrame(0xFF, new byte[] { 1, 2, 3 }),
                BuildWireFrame(0x42, SequenceBody(13))
            });
            var chunks = new List<byte[]> { wire };

            var observed = ObserveWithCoder(chunks);
            var legacy = FeedSplitter(LengthFieldSplitter.LegacyCompatible(MaxBodyLength), chunks);
            var def = FeedSplitter(LengthFieldSplitter.Default(MaxBodyLength), chunks);

            Assert.AreEqual(3, observed.Count, "一次输入里的三帧应全部产出。");
            Assert.AreEqual(3, legacy.Count);
            Assert.AreEqual(3, def.Count);
            AssertLegacyMatchesCoder(observed, legacy);
            AssertDefaultMatchesLegacy(legacy, def);
        }

        [TestMethod]
        public void CrossSegmentFrames_HeaderAndBodySplit_MatchesBaseCoder()
        {
            var wire = Concat(new[]
            {
                BuildWireFrame(0x05, SequenceBody(40)),
                BuildWireFrame(0x06, Array.Empty<byte>()),
                BuildWireFrame(0x07, SequenceBody(120))
            });

            // 固定切分模式：头部在段边界处被拆开，帧体也跨多段。
            var chunks = SplitWithPattern(wire, new[] { 4, 5, 6, 13, 1, 9 });

            var observed = ObserveWithCoder(chunks);
            var legacy = FeedSplitter(LengthFieldSplitter.LegacyCompatible(MaxBodyLength), chunks);
            var def = FeedSplitter(LengthFieldSplitter.Default(MaxBodyLength), chunks);

            Assert.AreEqual(3, observed.Count, "跨段输入的三帧应全部产出。");
            AssertLegacyMatchesCoder(observed, legacy);
            AssertDefaultMatchesLegacy(legacy, def);
        }

        private static List<ObservedFrame> ObserveWithCoder(IList<byte[]> chunks)
        {
            var observer = new FrameObserver();
            var coder = new BaseCoder();
            for (var i = 0; i < chunks.Count; i++)
                coder.DecodeStream(chunks[i], observer, observer.OnHeart, observer.OnFile);
            return observer.Frames;
        }

        private static List<SplitResult> FeedSplitter(IFrameSplitter splitter, IList<byte[]> chunks)
        {
            var results = new List<SplitResult>();

            // 保留原始分片边界，构造真正的多段只读序列，覆盖跨段扫描。
            var buffer = BuildSequence(chunks);
            var consumed = 0L;

            while (true)
            {
                long frameConsumed;
                ReadOnlySequence<byte> frame;
                if (!splitter.TrySplit(buffer.Slice(consumed), out frameConsumed, out frame))
                    break;

                results.Add(new SplitResult((int)frameConsumed, frame.ToArray()));
                consumed += frameConsumed;
            }

            Assert.AreEqual(buffer.Length, consumed, "输入排空后不应残留字节。");
            return results;
        }

        private static ReadOnlySequence<byte> BuildSequence(IList<byte[]> chunks)
        {
            if (chunks.Count == 0) return ReadOnlySequence<byte>.Empty;

            var first = new ChunkSegment(chunks[0]);
            var last = first;
            for (var i = 1; i < chunks.Count; i++)
                last = last.Append(chunks[i]);

            return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        }

        private sealed class ChunkSegment : ReadOnlySequenceSegment<byte>
        {
            public ChunkSegment(ReadOnlyMemory<byte> memory)
            {
                Memory = memory;
            }

            public ChunkSegment Append(ReadOnlyMemory<byte> memory)
            {
                var next = new ChunkSegment(memory) { RunningIndex = RunningIndex + Memory.Length };
                Next = next;
                return next;
            }
        }

        private static void AssertLegacyMatchesCoder(IList<ObservedFrame> observed, IList<SplitResult> legacy)
        {
            Assert.AreEqual(observed.Count, legacy.Count, "BaseCoder 与 LegacyCompatible 产出的帧数量不一致。");

            for (var i = 0; i < observed.Count; i++)
            {
                var expected = observed[i];
                var frame = legacy[i].Frame;

                Assert.AreEqual(FrameConstants.P_Head + (int)expected.BodyLength, legacy[i].Consumed,
                    $"第 {i} 帧 consumed 应等于 P_Head + bodyLen。");
                Assert.AreEqual(FrameConstants.P_Head + (int)expected.BodyLength, frame.Length,
                    $"第 {i} 帧长度应等于 P_Head + bodyLen。");
                Assert.AreEqual(expected.BodyLength,
                    BinaryPrimitives.ReadInt64LittleEndian(frame.AsSpan(0, FrameConstants.P_LEN)),
                    $"第 {i} 帧长度字段与 BodyLength 不一致。");
                Assert.AreEqual(expected.Type, frame[FrameConstants.P_LEN],
                    $"第 {i} 帧类型字节不一致。");
                CollectionAssert.AreEqual(expected.Content, frame.AsSpan(FrameConstants.P_Head).ToArray(),
                    $"第 {i} 帧帧体不一致。");
            }
        }

        private static void AssertDefaultMatchesLegacy(IList<SplitResult> legacy, IList<SplitResult> def)
        {
            Assert.AreEqual(legacy.Count, def.Count, "Default 与 LegacyCompatible 产出的帧数量不一致。");

            for (var i = 0; i < legacy.Count; i++)
            {
                var legacyFrame = legacy[i].Frame;
                var defaultFrame = def[i].Frame;

                Assert.AreEqual(legacy[i].Consumed, def[i].Consumed, $"第 {i} 帧 consumed 不一致。");
                Assert.AreEqual(legacyFrame.Length - FrameConstants.P_LEN, defaultFrame.Length,
                    $"第 {i} 帧 Default 应比 Legacy 短 {FrameConstants.P_LEN} 字节。");
                Assert.AreEqual(legacyFrame[FrameConstants.P_LEN], defaultFrame[0],
                    $"第 {i} 帧 Default 首字节应等于 Legacy 的类型字节。");
                CollectionAssert.AreEqual(
                    legacyFrame.AsSpan(FrameConstants.P_Head).ToArray(),
                    defaultFrame.AsSpan(1).ToArray(),
                    $"第 {i} 帧 Default 的帧体应与 Legacy 剥掉头部后的字节一致。");
            }
        }

        private static byte[] BuildWireFrame(byte type, byte[] body)
        {
            using (var writer = new PooledBufferWriter(FrameConstants.P_Head + body.Length + 16))
            {
                new BaseSocketProtocal(type, body.AsMemory()).WriteTo(writer);
                return writer.WrittenSpan.ToArray();
            }
        }

        private static List<byte[]> SplitRandomly(byte[] wire, Random rng)
        {
            return SplitWithPattern(wire, new[] { 1, 2, 3, 7, 13 }, rng);
        }

        private static List<byte[]> SplitFixed(byte[] wire, int size)
        {
            return SplitWithPattern(wire, new[] { size });
        }

        private static List<byte[]> SplitWithPattern(byte[] wire, int[] pattern)
        {
            return SplitWithPattern(wire, pattern, null);
        }

        private static List<byte[]> SplitWithPattern(byte[] wire, int[] pattern, Random rng)
        {
            var chunks = new List<byte[]>();
            var offset = 0;
            var index = 0;
            while (offset < wire.Length)
            {
                var size = rng == null ? pattern[index % pattern.Length] : pattern[rng.Next(pattern.Length)];
                index++;
                if (size > wire.Length - offset)
                    size = wire.Length - offset;
                chunks.Add(wire.AsSpan(offset, size).ToArray());
                offset += size;
            }
            return chunks;
        }

        private static byte[] Concat(IList<byte[]> parts)
        {
            var total = 0;
            for (var i = 0; i < parts.Count; i++)
                total += parts[i].Length;

            var buffer = new byte[total];
            var offset = 0;
            for (var i = 0; i < parts.Count; i++)
            {
                Buffer.BlockCopy(parts[i], 0, buffer, offset, parts[i].Length);
                offset += parts[i].Length;
            }
            return buffer;
        }

        private static byte[] RandomBody(Random rng, int length)
        {
            var body = new byte[length];
            rng.NextBytes(body);
            return body;
        }

        private static byte[] SequenceBody(int length)
        {
            var body = new byte[length];
            for (var i = 0; i < length; i++)
                body[i] = (byte)(i + 1);
            return body;
        }

        private sealed class SplitResult
        {
            public readonly int Consumed;
            public readonly byte[] Frame;

            public SplitResult(int consumed, byte[] frame)
            {
                Consumed = consumed;
                Frame = frame;
            }
        }

        private sealed class ObservedFrame
        {
            public readonly long BodyLength;
            public readonly byte Type;
            public readonly byte[] Content;

            public ObservedFrame(long bodyLength, byte type, byte[] content)
            {
                BodyLength = bodyLength;
                Type = type;
                Content = content;
            }
        }

        /// <summary>
        /// 记录 BaseCoder.DecodeStream 的全部产出。Heart（type=1 且 body 为空）与 BigData（type=3）
        /// 不进入 OnFrame，分别由 onHeart / onFile 回调补齐，保证与分帧器的产出序列一一对应。
        /// </summary>
        private sealed class FrameObserver : IFrameHandler
        {
            public readonly List<ObservedFrame> Frames = new List<ObservedFrame>();

            public void OnFrame(in SocketFrame frame)
            {
                Frames.Add(new ObservedFrame(frame.BodyLength, frame.Type, frame.Content.ToArray()));
            }

            public void OnHeart(DateTime heartAt)
            {
                Frames.Add(new ObservedFrame(0, (byte)SocketProtocalType.Heart, Array.Empty<byte>()));
            }

            public void OnFile(ReadOnlySpan<byte> content)
            {
                Frames.Add(new ObservedFrame(content.Length, (byte)SocketProtocalType.BigData, content.ToArray()));
            }
        }
    }
}