using System;
using System.Buffers;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.Caching;

namespace SAEA.Common.Tests.Caching
{
    [TestClass]
    public class MemoryPoolManagerTests
    {
        #region GetTier Tests

        [TestMethod]
        public void GetTier_SmallBuffer_ReturnsSmall()
        {
            Assert.AreEqual(BufferSizeTier.Small, MemoryPoolManager.GetTier(1024));
            Assert.AreEqual(BufferSizeTier.Small, MemoryPoolManager.GetTier(4095));
            Assert.AreEqual(BufferSizeTier.Small, MemoryPoolManager.GetTier(1));
        }

        [TestMethod]
        public void GetTier_MediumBuffer_ReturnsMedium()
        {
            Assert.AreEqual(BufferSizeTier.Medium, MemoryPoolManager.GetTier(4096));
            Assert.AreEqual(BufferSizeTier.Medium, MemoryPoolManager.GetTier(32768));
            Assert.AreEqual(BufferSizeTier.Medium, MemoryPoolManager.GetTier(65535));
        }

        [TestMethod]
        public void GetTier_LargeBuffer_ReturnsLarge()
        {
            Assert.AreEqual(BufferSizeTier.Large, MemoryPoolManager.GetTier(65536));
            Assert.AreEqual(BufferSizeTier.Large, MemoryPoolManager.GetTier(102400));
            Assert.AreEqual(BufferSizeTier.Large, MemoryPoolManager.GetTier(1048576));
        }

        [TestMethod]
        public void GetTier_ZeroSize_ReturnsSmall()
        {
            Assert.AreEqual(BufferSizeTier.Small, MemoryPoolManager.GetTier(0));
        }

        [TestMethod]
        public void GetTier_NegativeSize_ReturnsSmall()
        {
            Assert.AreEqual(BufferSizeTier.Small, MemoryPoolManager.GetTier(-1));
        }

        #endregion

        #region Rent Tests

        [TestMethod]
        public void Rent_SmallSize_ReturnsValidBuffer()
        {
            var buffer = MemoryPoolManager.Rent(1024);
            Assert.IsNotNull(buffer);
            Assert.IsTrue(buffer.Length >= 1024);
        }

        [TestMethod]
        public void Rent_MediumSize_ReturnsValidBuffer()
        {
            var buffer = MemoryPoolManager.Rent(32768);
            Assert.IsNotNull(buffer);
            Assert.IsTrue(buffer.Length >= 32768);
        }

        [TestMethod]
        public void Rent_LargeSize_ReturnsValidBuffer()
        {
            var buffer = MemoryPoolManager.Rent(131072);
            Assert.IsNotNull(buffer);
            Assert.IsTrue(buffer.Length >= 131072);
        }

        [TestMethod]
        public void Rent_ZeroSize_ReturnsBuffer()
        {
            var buffer = MemoryPoolManager.Rent(0);
            Assert.IsNotNull(buffer);
        }

        [TestMethod]
        public void Rent_NegativeSize_ReturnsBuffer()
        {
            var buffer = MemoryPoolManager.Rent(-1);
            Assert.IsNotNull(buffer);
        }

        #endregion

        #region Return Tests

        [TestMethod]
        public void Return_SmallBuffer_ReturnsSuccessfully()
        {
            var buffer = MemoryPoolManager.Rent(1024);
            MemoryPoolManager.Return(buffer, 1024);
            // Should not throw
        }

        [TestMethod]
        public void Return_MediumBuffer_ReturnsSuccessfully()
        {
            var buffer = MemoryPoolManager.Rent(32768);
            MemoryPoolManager.Return(buffer, 32768);
            // Should not throw
        }

        [TestMethod]
        public void Return_LargeBuffer_ReturnsSuccessfully()
        {
            var buffer = MemoryPoolManager.Rent(131072);
            MemoryPoolManager.Return(buffer, 131072);
            // Should not throw
        }

        [TestMethod]
        public void Return_WithOriginalSizeMinusOne_AutoDetectsTier()
        {
            var buffer = MemoryPoolManager.Rent(1024);
            MemoryPoolManager.Return(buffer); // originalSize = -1, auto-detect
            // Should not throw
        }

        [TestMethod]
        public void Return_NullBuffer_DoesNotThrow()
        {
            MemoryPoolManager.Return(null, 1024);
            // Should not throw
        }

        #endregion

        #region RentPooled Tests

        [TestMethod]
        public void RentPooled_SmallSize_ReturnsPooledBuffer()
        {
            using (var pooled = MemoryPoolManager.RentPooled(1024))
            {
                Assert.IsNotNull(pooled);
                Assert.IsNotNull(pooled.Buffer);
                Assert.AreEqual(1024, pooled.Length);
                Assert.IsTrue(pooled.Capacity >= 1024);
                Assert.AreEqual(BufferSizeTier.Small, pooled.Tier);
            }
        }

        [TestMethod]
        public void RentPooled_MediumSize_ReturnsPooledBuffer()
        {
            using (var pooled = MemoryPoolManager.RentPooled(32768))
            {
                Assert.IsNotNull(pooled);
                Assert.IsNotNull(pooled.Buffer);
                Assert.AreEqual(32768, pooled.Length);
                Assert.IsTrue(pooled.Capacity >= 32768);
                Assert.AreEqual(BufferSizeTier.Medium, pooled.Tier);
            }
        }

        [TestMethod]
        public void RentPooled_LargeSize_ReturnsPooledBuffer()
        {
            using (var pooled = MemoryPoolManager.RentPooled(131072))
            {
                Assert.IsNotNull(pooled);
                Assert.IsNotNull(pooled.Buffer);
                Assert.AreEqual(131072, pooled.Length);
                Assert.IsTrue(pooled.Capacity >= 131072);
                Assert.AreEqual(BufferSizeTier.Large, pooled.Tier);
            }
        }

        [TestMethod]
        public void RentPooled_Dispose_ReturnsBufferToPool()
        {
            var pooled = MemoryPoolManager.RentPooled(1024);
            pooled.Dispose();
            // Should not throw, buffer returned to pool
        }

        #endregion

        #region GetStatistics Tests

        [TestMethod]
        public void GetStatistics_ReturnsValidStatistics()
        {
            var stats = MemoryPoolManager.GetStatistics();
            // Just verify it doesn't throw and returns a valid struct
            Assert.IsTrue(stats.TotalRented >= 0);
            Assert.IsTrue(stats.TotalReturned >= 0);
            Assert.IsTrue(stats.ActiveBuffers >= 0);
        }

        [TestMethod]
        public void GetStatistics_AfterRent_IncrementsRented()
        {
            var beforeStats = MemoryPoolManager.GetStatistics();
            var buffer = MemoryPoolManager.Rent(1024);
            var afterStats = MemoryPoolManager.GetStatistics();

            Assert.IsTrue(afterStats.TotalRented >= beforeStats.TotalRented + 1);

            MemoryPoolManager.Return(buffer, 1024);
        }

        [TestMethod]
        public void GetStatistics_AfterReturn_IncrementsReturned()
        {
            var buffer = MemoryPoolManager.Rent(1024);
            var beforeStats = MemoryPoolManager.GetStatistics();
            MemoryPoolManager.Return(buffer, 1024);
            var afterStats = MemoryPoolManager.GetStatistics();

            Assert.IsTrue(afterStats.TotalReturned >= beforeStats.TotalReturned + 1);
        }

        #endregion

        #region Concurrent Access Tests

        [TestMethod]
        public void Rent_Return_ConcurrentAccess_WorksCorrectly()
        {
            const int iterations = 100;
            const int concurrency = 10;

            Parallel.For(0, concurrency, i =>
            {
                for (int j = 0; j < iterations; j++)
                {
                    var buffer = MemoryPoolManager.Rent(1024);
                    Assert.IsNotNull(buffer);
                    MemoryPoolManager.Return(buffer, 1024);
                }
            });

            // Verify statistics
            var stats = MemoryPoolManager.GetStatistics();
            Assert.IsTrue(stats.TotalRented >= iterations * concurrency);
            Assert.IsTrue(stats.TotalReturned >= iterations * concurrency);
        }

        [TestMethod]
        public void RentPooled_ConcurrentAccess_WorksCorrectly()
        {
            const int iterations = 50;
            const int concurrency = 10;

            Parallel.For(0, concurrency, i =>
            {
                for (int j = 0; j < iterations; j++)
                {
                    using (var pooled = MemoryPoolManager.RentPooled(4096))
                    {
                        Assert.IsNotNull(pooled);
                        Assert.IsNotNull(pooled.Buffer);
                    }
                }
            });

            // Verify statistics
            var stats = MemoryPoolManager.GetStatistics();
            Assert.IsTrue(stats.TotalRented >= iterations * concurrency);
        }

        #endregion

        #region Constants Tests

        [TestMethod]
        public void Constants_AreCorrect()
        {
            Assert.AreEqual(4 * 1024, MemoryPoolManager.SmallThreshold);
            Assert.AreEqual(64 * 1024, MemoryPoolManager.MediumThreshold);
            Assert.AreEqual(1024 * 1024, MemoryPoolManager.LargeThreshold);
        }

        #endregion
    }
}
