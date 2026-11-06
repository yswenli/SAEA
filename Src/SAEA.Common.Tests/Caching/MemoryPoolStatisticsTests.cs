using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.Caching;

namespace SAEA.Common.Tests.Caching
{
    /// <summary>
    /// 校验内存池统计快照的默认值、合计值与活动数计算
    /// </summary>
    [TestClass]
    public class MemoryPoolStatisticsTests
    {
        /// <summary>
        /// 验证新建快照的六项分档计数初始值均为 0
        /// </summary>
        [TestMethod]
        public void MemoryPoolStatistics_DefaultValues_AreZero()
        {
            var stats = new MemoryPoolStatistics();

            Assert.AreEqual(0, stats.SmallPoolRented);
            Assert.AreEqual(0, stats.SmallPoolReturned);
            Assert.AreEqual(0, stats.MediumPoolRented);
            Assert.AreEqual(0, stats.MediumPoolReturned);
            Assert.AreEqual(0, stats.LargePoolRented);
            Assert.AreEqual(0, stats.LargePoolReturned);
        }

        /// <summary>
        /// 验证总租借数为三档租借数之和
        /// </summary>
        [TestMethod]
        public void MemoryPoolStatistics_TotalRented_CalculatesCorrectly()
        {
            var stats = new MemoryPoolStatistics
            {
                SmallPoolRented = 10,
                MediumPoolRented = 20,
                LargePoolRented = 30
            };

            Assert.AreEqual(60, stats.TotalRented);
        }

        /// <summary>
        /// 验证总归还数为三档归还数之和
        /// </summary>
        [TestMethod]
        public void MemoryPoolStatistics_TotalReturned_CalculatesCorrectly()
        {
            var stats = new MemoryPoolStatistics
            {
                SmallPoolReturned = 5,
                MediumPoolReturned = 10,
                LargePoolReturned = 15
            };

            Assert.AreEqual(30, stats.TotalReturned);
        }

        /// <summary>
        /// 验证活动缓冲数为总租借数减总归还数（170 减 135 得 35）
        /// </summary>
        [TestMethod]
        public void MemoryPoolStatistics_ActiveBuffers_CalculatesCorrectly()
        {
            var stats = new MemoryPoolStatistics
            {
                SmallPoolRented = 100,
                SmallPoolReturned = 80,
                MediumPoolRented = 50,
                MediumPoolReturned = 40,
                LargePoolRented = 20,
                LargePoolReturned = 15
            };

            // Total rented: 170, Total returned: 135, Active: 35
            Assert.AreEqual(35, stats.ActiveBuffers);
        }

        /// <summary>
        /// 验证六项分档计数属性都可写入并按原值读出
        /// </summary>
        [TestMethod]
        public void MemoryPoolStatistics_CanSetProperties()
        {
            var stats = new MemoryPoolStatistics
            {
                SmallPoolRented = 1,
                SmallPoolReturned = 2,
                MediumPoolRented = 3,
                MediumPoolReturned = 4,
                LargePoolRented = 5,
                LargePoolReturned = 6
            };

            Assert.AreEqual(1, stats.SmallPoolRented);
            Assert.AreEqual(2, stats.SmallPoolReturned);
            Assert.AreEqual(3, stats.MediumPoolRented);
            Assert.AreEqual(4, stats.MediumPoolReturned);
            Assert.AreEqual(5, stats.LargePoolRented);
            Assert.AreEqual(6, stats.LargePoolReturned);
        }
    }
}
