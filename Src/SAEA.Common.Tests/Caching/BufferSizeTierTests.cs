using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.Caching;

namespace SAEA.Common.Tests.Caching
{
    [TestClass]
    public class BufferSizeTierTests
    {
        [TestMethod]
        public void BufferSizeTier_Small_HasValueZero()
        {
            Assert.AreEqual(0, (int)BufferSizeTier.Small);
        }

        [TestMethod]
        public void BufferSizeTier_Medium_HasValueOne()
        {
            Assert.AreEqual(1, (int)BufferSizeTier.Medium);
        }

        [TestMethod]
        public void BufferSizeTier_Large_HasValueTwo()
        {
            Assert.AreEqual(2, (int)BufferSizeTier.Large);
        }

        [TestMethod]
        public void BufferSizeTier_IsDefined()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(BufferSizeTier), BufferSizeTier.Small));
            Assert.IsTrue(System.Enum.IsDefined(typeof(BufferSizeTier), BufferSizeTier.Medium));
            Assert.IsTrue(System.Enum.IsDefined(typeof(BufferSizeTier), BufferSizeTier.Large));
        }
    }
}
