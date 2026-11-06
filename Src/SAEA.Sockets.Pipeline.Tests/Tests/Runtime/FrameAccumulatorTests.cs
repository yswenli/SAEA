using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.Caching;
using SAEA.Sockets.Pipeline;

namespace SAEA.Sockets.Pipeline.Tests.Tests.Runtime
{
    [TestClass]
    public class FrameAccumulatorTests
    {
        [TestMethod]
        public void Append_ConsecutiveChunks_ConcatenatesContent()
        {
            using (var accumulator = new FrameAccumulator())
            {
                accumulator.Append(new byte[] { 1, 2, 3 });
                accumulator.Append(new byte[] { 4, 5 });
                accumulator.Append(new byte[] { 6 });

                Assert.AreEqual(6, accumulator.BufferedLength);
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5, 6 }, accumulator.Buffered.ToArray());
            }
        }

        [TestMethod]
        public void Consume_Partial_LeavesOnlyRemainingBytes()
        {
            using (var accumulator = new FrameAccumulator())
            {
                accumulator.Append(new byte[] { 1, 2, 3, 4, 5 });

                accumulator.Consume(2);

                Assert.AreEqual(3, accumulator.BufferedLength);
                CollectionAssert.AreEqual(new byte[] { 3, 4, 5 }, accumulator.Buffered.ToArray());
            }
        }

        [TestMethod]
        public void Consume_All_ResetsCursorsAndNextAppendStartsFromZero()
        {
            using (var accumulator = new FrameAccumulator())
            {
                accumulator.Append(new byte[] { 1, 2, 3 });
                accumulator.Consume(3);

                Assert.AreEqual(0, accumulator.BufferedLength);
                Assert.IsTrue(accumulator.Buffered.IsEmpty);

                accumulator.Append(new byte[] { 9, 8 });

                Assert.AreEqual(2, accumulator.BufferedLength);
                CollectionAssert.AreEqual(new byte[] { 9, 8 }, accumulator.Buffered.ToArray());
            }
        }

        [TestMethod]
        public void Consume_OutOfRange_ThrowsArgumentOutOfRangeException()
        {
            using (var accumulator = new FrameAccumulator())
            {
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => accumulator.Consume(1));
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => accumulator.Consume(-1));

                accumulator.Append(new byte[] { 1, 2, 3, 4, 5 });

                Assert.ThrowsException<ArgumentOutOfRangeException>(() => accumulator.Consume(6));
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => accumulator.Consume(-1));

                accumulator.Consume(0);
                Assert.AreEqual(5, accumulator.BufferedLength);
            }
        }

        [TestMethod]
        public void RepeatedAppendConsume_CompactsThenExpands_KeepsContentAndBalancesPool()
        {
            var accumulator = new FrameAccumulator();
            var expected = new List<byte>();
            var expectedStart = 0;
            var seed = 0;

            for (var i = 0; i < 4; i++)
            {
                var chunk = BuildChunk(300, seed);
                seed += chunk.Length;
                accumulator.Append(chunk);
                expected.AddRange(chunk);
            }

            accumulator.Consume(800);
            expectedStart += 800;

            // 此时 _start > 0 且剩余 400 字节；追加 900 字节会先原地紧凑，容量仍够、不扩容。
            var compactChunk = BuildChunk(900, seed);
            seed += compactChunk.Length;
            accumulator.Append(compactChunk);
            expected.AddRange(compactChunk);

            // 追加 1000 字节时紧凑后仍不够，触发一次真正的扩容。
            var expandChunk = BuildChunk(1000, seed);
            seed += expandChunk.Length;
            accumulator.Append(expandChunk);
            expected.AddRange(expandChunk);

            Assert.AreEqual(2300, accumulator.BufferedLength);
            CollectionAssert.AreEqual(
                expected.GetRange(expectedStart, expected.Count - expectedStart).ToArray(),
                accumulator.Buffered.ToArray());

            accumulator.Consume(2100);
            expectedStart += 2100;
            Assert.AreEqual(200, accumulator.BufferedLength);

            // 稳态循环：每次净增 0 字节，_start 持续前移，靠原地紧凑回收空间，不应再租借新缓冲。
            var rentedBeforeLoop = TotalRented(MemoryPoolManager.GetStatistics());
            for (var i = 0; i < 100000; i++)
            {
                var head = new byte[] { (byte)i, (byte)(i >> 8), (byte)(i >> 16) };
                accumulator.Append(head);
                expected.AddRange(head);
                accumulator.Consume(1);
                expectedStart++;

                var tail = new byte[] { (byte)(i + 1) };
                accumulator.Append(tail);
                expected.AddRange(tail);
                accumulator.Consume(3);
                expectedStart += 3;
            }

            var rentedAfterLoop = TotalRented(MemoryPoolManager.GetStatistics());
            Assert.AreEqual(rentedBeforeLoop, rentedAfterLoop, "稳态循环应只走原地紧凑，不再租借新缓冲。");
            Assert.AreEqual(200, accumulator.BufferedLength);
            CollectionAssert.AreEqual(
                expected.GetRange(expectedStart, expected.Count - expectedStart).ToArray(),
                accumulator.Buffered.ToArray());

            var returnedBeforeDispose = TotalReturned(MemoryPoolManager.GetStatistics());
            accumulator.Dispose();
            var returnedAfterDispose = TotalReturned(MemoryPoolManager.GetStatistics());
            Assert.AreEqual(returnedBeforeDispose + 1, returnedAfterDispose, "Dispose 应把当前缓冲归还内存池且只归还一次。");
        }

        [TestMethod]
        public void ResetAndDispose_ReturnPooledBufferExactlyOnce()
        {
            var accumulator = new FrameAccumulator();
            accumulator.Append(new byte[] { 1, 2, 3 });

            var returnedBeforeReset = TotalReturned(MemoryPoolManager.GetStatistics());
            accumulator.Reset();
            accumulator.Reset();
            var returnedAfterReset = TotalReturned(MemoryPoolManager.GetStatistics());

            Assert.AreEqual(returnedBeforeReset + 1, returnedAfterReset, "Reset 应归还缓冲且只归还一次。");
            Assert.AreEqual(0, accumulator.BufferedLength);
            Assert.IsTrue(accumulator.Buffered.IsEmpty);

            // Reset 之后同一实例仍可用：重新租借缓冲并从下标 0 开始写。
            accumulator.Append(new byte[] { 9 });
            CollectionAssert.AreEqual(new byte[] { 9 }, accumulator.Buffered.ToArray());

            var returnedBeforeDispose = TotalReturned(MemoryPoolManager.GetStatistics());
            accumulator.Dispose();
            accumulator.Dispose();
            var returnedAfterDispose = TotalReturned(MemoryPoolManager.GetStatistics());

            Assert.AreEqual(returnedBeforeDispose + 1, returnedAfterDispose, "重复 Dispose 不得重复归还。");
            Assert.AreEqual(0, accumulator.BufferedLength);
            Assert.IsTrue(accumulator.Buffered.IsEmpty);
        }

        private static byte[] BuildChunk(int length, int seed)
        {
            var data = new byte[length];
            for (var i = 0; i < length; i++)
            {
                data[i] = (byte)(seed + i);
            }
            return data;
        }

        private static long TotalRented(MemoryPoolStatistics statistics)
        {
            return statistics.SmallPoolRented + statistics.MediumPoolRented + statistics.LargePoolRented;
        }

        private static long TotalReturned(MemoryPoolStatistics statistics)
        {
            return statistics.SmallPoolReturned + statistics.MediumPoolReturned + statistics.LargePoolReturned;
        }
    }
}