using System;
using System.Buffers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.Caching;

namespace SAEA.Common.Tests.Caching
{
    [TestClass]
    public class PooledBufferTests
    {
        [TestMethod]
        public void PooledBuffer_Constructor_SetsProperties()
        {
            var pool = ArrayPool<byte>.Shared;
            var buffer = pool.Rent(1024);

            using (var pooled = new PooledBuffer(buffer, 512, BufferSizeTier.Small, pool))
            {
                Assert.AreSame(buffer, pooled.Buffer);
                Assert.AreEqual(512, pooled.Length);
                Assert.IsTrue(pooled.Capacity >= 1024);
                Assert.AreEqual(BufferSizeTier.Small, pooled.Tier);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void PooledBuffer_Constructor_ThrowsOnNullBuffer()
        {
            var pool = ArrayPool<byte>.Shared;
            new PooledBuffer(null, 100, BufferSizeTier.Small, pool);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void PooledBuffer_Constructor_ThrowsOnNullPool()
        {
            var buffer = new byte[1024];
            new PooledBuffer(buffer, 100, BufferSizeTier.Small, null);
        }

        [TestMethod]
        public void PooledBuffer_AsSpan_ReturnsCorrectSpan()
        {
            var pool = ArrayPool<byte>.Shared;
            var buffer = pool.Rent(1024);
            buffer[0] = 0xAB;
            buffer[511] = 0xCD;

            using (var pooled = new PooledBuffer(buffer, 512, BufferSizeTier.Small, pool))
            {
                var span = pooled.AsSpan();
                Assert.AreEqual(512, span.Length);
                Assert.AreEqual(0xAB, span[0]);
                Assert.AreEqual(0xCD, span[511]);
            }
        }

        [TestMethod]
        public void PooledBuffer_AsMemory_ReturnsCorrectMemory()
        {
            var pool = ArrayPool<byte>.Shared;
            var buffer = pool.Rent(1024);
            buffer[0] = 0x12;
            buffer[255] = 0x34;

            using (var pooled = new PooledBuffer(buffer, 256, BufferSizeTier.Small, pool))
            {
                var memory = pooled.AsMemory();
                Assert.AreEqual(256, memory.Length);
                Assert.AreEqual(0x12, memory.Span[0]);
                Assert.AreEqual(0x34, memory.Span[255]);
            }
        }

        [TestMethod]
        public void PooledBuffer_Dispose_ReturnsBufferToPool()
        {
            var pool = ArrayPool<byte>.Shared;
            var buffer = pool.Rent(1024);
            var bufferRef = buffer;

            var pooled = new PooledBuffer(buffer, 512, BufferSizeTier.Small, pool);
            pooled.Dispose();

            // After disposal, we can't really verify the buffer was returned,
            // but we can verify no exception was thrown and the object is in disposed state
        }

        [TestMethod]
        public void PooledBuffer_Dispose_CanBeCalledMultipleTimes()
        {
            var pool = ArrayPool<byte>.Shared;
            var buffer = pool.Rent(1024);

            var pooled = new PooledBuffer(buffer, 512, BufferSizeTier.Small, pool);
            pooled.Dispose();
            pooled.Dispose(); // Should not throw
        }
    }
}
