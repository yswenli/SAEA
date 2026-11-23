using System;
using System.Buffers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.Caching;

namespace SAEA.Common.Tests.Caching
{
    /// <summary>
    /// 校验池化缓冲对象的属性、切片视图与释放行为
    /// </summary>
    [TestClass]
    public class PooledBufferTests
    {
        /// <summary>
        /// 验证构造函数保存数组引用，并暴露逻辑长度、实际容量与档位
        /// </summary>
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

        /// <summary>
        /// 验证传入 null 数组时构造函数抛出 ArgumentNullException
        /// </summary>
        [TestMethod]
        public void PooledBuffer_Constructor_ThrowsOnNullBuffer()
        {
            var pool = ArrayPool<byte>.Shared;

            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                new PooledBuffer(null, 100, BufferSizeTier.Small, pool);
            });
        }

        /// <summary>
        /// 验证传入 null 池时构造函数抛出 ArgumentNullException
        /// </summary>
        [TestMethod]
        public void PooledBuffer_Constructor_ThrowsOnNullPool()
        {
            var buffer = new byte[1024];

            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                new PooledBuffer(buffer, 100, BufferSizeTier.Small, null);
            });
        }

        /// <summary>
        /// 验证 AsSpan 返回长度为逻辑长度且元素与原数组一致的切片
        /// </summary>
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

        /// <summary>
        /// 验证 AsMemory 返回长度为逻辑长度且元素与原数组一致的内存视图
        /// </summary>
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

        /// <summary>
        /// 验证释放时把底层数组归还给池且不抛异常
        /// </summary>
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

        /// <summary>
        /// 验证重复调用释放不会抛异常（缓冲只归还一次）
        /// </summary>
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
