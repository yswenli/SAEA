using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.Caching;

namespace SAEA.Common.Tests.Caching
{
    [TestClass]
    public class MemoryPoolStatisticsTests
    {
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
