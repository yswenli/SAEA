using System;
using System.Buffers;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.Caching;

namespace SAEA.Common.Tests.Caching
{
    /// <summary>
    /// 校验内存池管理器的分档判定、租借归还、统计计数与并发安全
    /// </summary>
    [TestClass]
    public class MemoryPoolManagerTests
    {
        #region GetTier Tests

        /// <summary>
        /// 验证小于 4KB 阈值的长度归入 Small 档
        /// </summary>
        [TestMethod]
        public void GetTier_SmallBuffer_ReturnsSmall()
        {
            Assert.AreEqual(BufferSizeTier.Small, MemoryPoolManager.GetTier(1024));
            Assert.AreEqual(BufferSizeTier.Small, MemoryPoolManager.GetTier(4095));
            Assert.AreEqual(BufferSizeTier.Small, MemoryPoolManager.GetTier(1));
        }

        /// <summary>
        /// 验证 4KB 到 64KB 以下（含 65535）的长度归入 Medium 档
        /// </summary>
        [TestMethod]
        public void GetTier_MediumBuffer_ReturnsMedium()
        {
            Assert.AreEqual(BufferSizeTier.Medium, MemoryPoolManager.GetTier(4096));
            Assert.AreEqual(BufferSizeTier.Medium, MemoryPoolManager.GetTier(32768));
            Assert.AreEqual(BufferSizeTier.Medium, MemoryPoolManager.GetTier(65535));
        }

        /// <summary>
        /// 验证 64KB 及以上的长度归入 Large 档
        /// </summary>
        [TestMethod]
        public void GetTier_LargeBuffer_ReturnsLarge()
        {
            Assert.AreEqual(BufferSizeTier.Large, MemoryPoolManager.GetTier(65536));
            Assert.AreEqual(BufferSizeTier.Large, MemoryPoolManager.GetTier(102400));
            Assert.AreEqual(BufferSizeTier.Large, MemoryPoolManager.GetTier(1048576));
        }

        /// <summary>
        /// 验证长度为 0 时按 Small 档处理
        /// </summary>
        [TestMethod]
        public void GetTier_ZeroSize_ReturnsSmall()
        {
            Assert.AreEqual(BufferSizeTier.Small, MemoryPoolManager.GetTier(0));
        }

        /// <summary>
        /// 验证负数长度不抛异常，按 Small 档处理
        /// </summary>
        [TestMethod]
        public void GetTier_NegativeSize_ReturnsSmall()
        {
            Assert.AreEqual(BufferSizeTier.Small, MemoryPoolManager.GetTier(-1));
        }

        #endregion

        #region Rent Tests

        /// <summary>
        /// 验证从 Small 池租借的数组非空且长度不小于请求值
        /// </summary>
        [TestMethod]
        public void Rent_SmallSize_ReturnsValidBuffer()
        {
            var buffer = MemoryPoolManager.Rent(1024);
            Assert.IsNotNull(buffer);
            Assert.IsTrue(buffer.Length >= 1024);
        }

        /// <summary>
        /// 验证从 Medium 池租借的数组非空且长度不小于请求值
        /// </summary>
        [TestMethod]
        public void Rent_MediumSize_ReturnsValidBuffer()
        {
            var buffer = MemoryPoolManager.Rent(32768);
            Assert.IsNotNull(buffer);
            Assert.IsTrue(buffer.Length >= 32768);
        }

        /// <summary>
        /// 验证从 Large 池租借的数组非空且长度不小于请求值
        /// </summary>
        [TestMethod]
        public void Rent_LargeSize_ReturnsValidBuffer()
        {
            var buffer = MemoryPoolManager.Rent(131072);
            Assert.IsNotNull(buffer);
            Assert.IsTrue(buffer.Length >= 131072);
        }

        /// <summary>
        /// 验证请求长度为 0 时仍返回可用数组而不抛异常
        /// </summary>
        [TestMethod]
        public void Rent_ZeroSize_ReturnsBuffer()
        {
            var buffer = MemoryPoolManager.Rent(0);
            Assert.IsNotNull(buffer);
        }

        /// <summary>
        /// 验证请求长度为负数时仍返回可用数组而不抛异常
        /// </summary>
        [TestMethod]
        public void Rent_NegativeSize_ReturnsBuffer()
        {
            var buffer = MemoryPoolManager.Rent(-1);
            Assert.IsNotNull(buffer);
        }

        #endregion

        #region Return Tests

        /// <summary>
        /// 验证归还 Small 缓冲并同时传入原始长度时不抛异常
        /// </summary>
        [TestMethod]
        public void Return_SmallBuffer_ReturnsSuccessfully()
        {
            var buffer = MemoryPoolManager.Rent(1024);
            MemoryPoolManager.Return(buffer, 1024);
            // Should not throw
        }

        /// <summary>
        /// 验证归还 Medium 缓冲并同时传入原始长度时不抛异常
        /// </summary>
        [TestMethod]
        public void Return_MediumBuffer_ReturnsSuccessfully()
        {
            var buffer = MemoryPoolManager.Rent(32768);
            MemoryPoolManager.Return(buffer, 32768);
            // Should not throw
        }

        /// <summary>
        /// 验证归还 Large 缓冲并同时传入原始长度时不抛异常
        /// </summary>
        [TestMethod]
        public void Return_LargeBuffer_ReturnsSuccessfully()
        {
            var buffer = MemoryPoolManager.Rent(131072);
            MemoryPoolManager.Return(buffer, 131072);
            // Should not throw
        }

        /// <summary>
        /// 验证不传原始长度（默认 -1）时能自动识别档位并完成归还
        /// </summary>
        [TestMethod]
        public void Return_WithOriginalSizeMinusOne_AutoDetectsTier()
        {
            var buffer = MemoryPoolManager.Rent(1024);
            MemoryPoolManager.Return(buffer); // originalSize = -1, auto-detect
            // Should not throw
        }

        /// <summary>
        /// 验证归还 null 数组时被忽略而不抛异常
        /// </summary>
        [TestMethod]
        public void Return_NullBuffer_DoesNotThrow()
        {
            MemoryPoolManager.Return(null, 1024);
            // Should not throw
        }

        #endregion

        #region RentPooled Tests

        /// <summary>
        /// 验证租借 PooledBuffer 后长度等于请求值、容量不小于请求值且档位为 Small
        /// </summary>
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

        /// <summary>
        /// 验证租借 PooledBuffer 后长度等于请求值、容量不小于请求值且档位为 Medium
        /// </summary>
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

        /// <summary>
        /// 验证租借 PooledBuffer 后长度等于请求值、容量不小于请求值且档位为 Large
        /// </summary>
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

        /// <summary>
        /// 验证 PooledBuffer 释放时缓冲回到池中且不抛异常
        /// </summary>
        [TestMethod]
        public void RentPooled_Dispose_ReturnsBufferToPool()
        {
            var pooled = MemoryPoolManager.RentPooled(1024);
            pooled.Dispose();
            // Should not throw, buffer returned to pool
        }

        #endregion

        #region GetStatistics Tests

        /// <summary>
        /// 验证统计快照可正常获取且各计数值非负
        /// </summary>
        [TestMethod]
        public void GetStatistics_ReturnsValidStatistics()
        {
            var stats = MemoryPoolManager.GetStatistics();
            // Just verify it doesn't throw and returns a valid struct
            Assert.IsTrue(stats.TotalRented >= 0);
            Assert.IsTrue(stats.TotalReturned >= 0);
            Assert.IsTrue(stats.ActiveBuffers >= 0);
        }

        /// <summary>
        /// 验证一次租借后总租借计数至少增加 1
        /// </summary>
        [TestMethod]
        public void GetStatistics_AfterRent_IncrementsRented()
        {
            var beforeStats = MemoryPoolManager.GetStatistics();
            var buffer = MemoryPoolManager.Rent(1024);
            var afterStats = MemoryPoolManager.GetStatistics();

            Assert.IsTrue(afterStats.TotalRented >= beforeStats.TotalRented + 1);

            MemoryPoolManager.Return(buffer, 1024);
        }

        /// <summary>
        /// 验证一次归还后总归还计数至少增加 1
        /// </summary>
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

        /// <summary>
        /// 验证 10 线程各 100 次并发租借归还数组时数据不错乱且统计累计正确
        /// </summary>
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

        /// <summary>
        /// 验证 10 线程各 50 次并发租借并释放 PooledBuffer 时不出错且租借计数正确
        /// </summary>
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

        /// <summary>
        /// 验证三档阈值常量分别为 4KB、64KB 与 1MB
        /// </summary>
        [TestMethod]
        public void Constants_AreCorrect()
        {
            Assert.AreEqual(4 * 1024, GetThresholdConstant(nameof(MemoryPoolManager.SmallThreshold)));
            Assert.AreEqual(64 * 1024, GetThresholdConstant(nameof(MemoryPoolManager.MediumThreshold)));
            Assert.AreEqual(1024 * 1024, GetThresholdConstant(nameof(MemoryPoolManager.LargeThreshold)));
        }

        /// <summary>
        /// 读取 <see cref="MemoryPoolManager"/> 上公开的阈值常量，避免与字面量直接比较被判定为恒真
        /// </summary>
        private static int GetThresholdConstant(string name)
        {
            var field = typeof(MemoryPoolManager).GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(field, $"未找到公开常量 {name}。");
            return (int)field!.GetRawConstantValue();
        }

        #endregion
    }
}
