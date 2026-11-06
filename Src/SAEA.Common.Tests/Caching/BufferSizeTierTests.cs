using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Common.Caching;

namespace SAEA.Common.Tests.Caching
{
    /// <summary>
    /// 校验缓冲区分档枚举的定义与取值
    /// </summary>
    [TestClass]
    public class BufferSizeTierTests
    {
        /// <summary>
        /// 验证小档位 Small 的枚举值为 0
        /// </summary>
        [TestMethod]
        public void BufferSizeTier_Small_HasValueZero()
        {
            Assert.AreEqual(0, (int)BufferSizeTier.Small);
        }

        /// <summary>
        /// 验证中档位 Medium 的枚举值为 1
        /// </summary>
        [TestMethod]
        public void BufferSizeTier_Medium_HasValueOne()
        {
            Assert.AreEqual(1, (int)BufferSizeTier.Medium);
        }

        /// <summary>
        /// 验证大档位 Large 的枚举值为 2
        /// </summary>
        [TestMethod]
        public void BufferSizeTier_Large_HasValueTwo()
        {
            Assert.AreEqual(2, (int)BufferSizeTier.Large);
        }

        /// <summary>
        /// 验证三个档位成员都被 Enum.IsDefined 识别为已定义值
        /// </summary>
        [TestMethod]
        public void BufferSizeTier_IsDefined()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(BufferSizeTier), BufferSizeTier.Small));
            Assert.IsTrue(System.Enum.IsDefined(typeof(BufferSizeTier), BufferSizeTier.Medium));
            Assert.IsTrue(System.Enum.IsDefined(typeof(BufferSizeTier), BufferSizeTier.Large));
        }
    }
}
