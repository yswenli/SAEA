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
*命名空间：SAEA.Http.Tests
*文件名： ByteRangeHeaderTests
*版本号： net10.0
*唯一标识：7d6d7a17-499b-48a3-bb28-0e97e31fe32d
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/26 10:00:00
*描述：单区间 Range 头解析的用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/26 10:00:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖三种单区间写法、截断、非法语法与 0 字节文件边界
*
*****************************************************************************/
using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.Http.Common;

namespace SAEA.Http.Tests
{
    /// <summary>
    /// 单区间 Range 头解析用例：只接受 bytes=a-b、bytes=a- 与 bytes=-N，其余按忽略处理。
    /// </summary>
    [TestClass]
    public class ByteRangeHeaderTests
    {
        /// <summary>
        /// 明确首末字节的区间应给出闭区间与对应长度。
        /// </summary>
        [TestMethod]
        public void Parse_FirstRange_ReturnsSatisfiable()
        {
            var result = ByteRangeHeader.Parse("bytes=0-3", 10);

            Assert.AreEqual(ByteRangeParseKind.Satisfiable, result.Kind);
            Assert.AreEqual(0L, result.Start);
            Assert.AreEqual(3L, result.End);
            Assert.AreEqual(4L, result.Length);
        }

        /// <summary>
        /// 省略末字节时应一直取到文件末尾。
        /// </summary>
        [TestMethod]
        public void Parse_OpenEnded_ReturnsToFileEnd()
        {
            var result = ByteRangeHeader.Parse("bytes=5-", 10);

            Assert.AreEqual(ByteRangeParseKind.Satisfiable, result.Kind);
            Assert.AreEqual(5L, result.Start);
            Assert.AreEqual(9L, result.End);
            Assert.AreEqual(5L, result.Length);
        }

        /// <summary>
        /// 只给后缀长度时应返回文件尾部对应字节。
        /// </summary>
        [TestMethod]
        public void Parse_Suffix_ReturnsTailBytes()
        {
            var result = ByteRangeHeader.Parse("bytes=-3", 10);

            Assert.AreEqual(ByteRangeParseKind.Satisfiable, result.Kind);
            Assert.AreEqual(7L, result.Start);
            Assert.AreEqual(9L, result.End);
            Assert.AreEqual(3L, result.Length);
        }

        /// <summary>
        /// 末字节超出文件长度时应截断到最后一个字节。
        /// </summary>
        [TestMethod]
        public void Parse_EndBeyondTotal_ClampsToLastByte()
        {
            var result = ByteRangeHeader.Parse("bytes=0-100", 4);

            Assert.AreEqual(ByteRangeParseKind.Satisfiable, result.Kind);
            Assert.AreEqual(0L, result.Start);
            Assert.AreEqual(3L, result.End);
            Assert.AreEqual(4L, result.Length);
        }

        /// <summary>
        /// 起始位置等于文件长度时没有可返回的字节，应判为不可满足。
        /// </summary>
        [TestMethod]
        public void Parse_StartAtTotal_ReturnsUnsatisfiable()
        {
            var result = ByteRangeHeader.Parse("bytes=10-", 10);

            Assert.AreEqual(ByteRangeParseKind.Unsatisfiable, result.Kind);
            Assert.AreEqual(0L, result.Length);
        }

        /// <summary>
        /// 末字节小于首字节属于非法区间，应忽略 Range。
        /// </summary>
        [TestMethod]
        public void Parse_EndBeforeStart_Ignored()
        {
            var result = ByteRangeHeader.Parse("bytes=5-3", 10);

            Assert.AreEqual(ByteRangeParseKind.Ignore, result.Kind);
        }

        /// <summary>
        /// 数字无法解析时按语法非法忽略 Range。
        /// </summary>
        [TestMethod]
        public void Parse_NonNumeric_Ignored()
        {
            var result = ByteRangeHeader.Parse("bytes=abc", 10);

            Assert.AreEqual(ByteRangeParseKind.Ignore, result.Kind);
        }

        /// <summary>
        /// unit 不是 bytes 时应忽略 Range。
        /// </summary>
        [TestMethod]
        public void Parse_UnsupportedUnit_Ignored()
        {
            var result = ByteRangeHeader.Parse("items=0-1", 10);

            Assert.AreEqual(ByteRangeParseKind.Ignore, result.Kind);
        }

        /// <summary>
        /// 逗号分隔的多区间不在支持范围内，应按忽略处理回 200 全量。
        /// </summary>
        [TestMethod]
        public void Parse_MultipleRanges_Ignored()
        {
            var result = ByteRangeHeader.Parse("bytes=0-1,3-4", 10);

            Assert.AreEqual(ByteRangeParseKind.Ignore, result.Kind);
        }

        /// <summary>
        /// 后缀长度为 0 时没有可返回的字节，应判为不可满足。
        /// </summary>
        [TestMethod]
        public void Parse_ZeroSuffixLength_ReturnsUnsatisfiable()
        {
            var result = ByteRangeHeader.Parse("bytes=-0", 10);

            Assert.AreEqual(ByteRangeParseKind.Unsatisfiable, result.Kind);
        }

        /// <summary>
        /// 0 字节文件上的合法区间没有可满足的字节，应判为不可满足。
        /// </summary>
        [TestMethod]
        public void Parse_EmptyFileRange_ReturnsUnsatisfiable()
        {
            var result = ByteRangeHeader.Parse("bytes=0-0", 0);

            Assert.AreEqual(ByteRangeParseKind.Unsatisfiable, result.Kind);
            Assert.AreEqual(0L, result.Length);
        }

        /// <summary>
        /// 0 字节文件上的合法后缀同样不可满足，不能拼出 0--1 这种非法区间。
        /// </summary>
        [TestMethod]
        public void Parse_EmptyFileSuffix_ReturnsUnsatisfiable()
        {
            var result = ByteRangeHeader.Parse("bytes=-5", 0);

            Assert.AreEqual(ByteRangeParseKind.Unsatisfiable, result.Kind);
        }

        /// <summary>
        /// 0 字节文件上省略末字节的合法区间也不可满足。
        /// </summary>
        [TestMethod]
        public void Parse_EmptyFileOpenEnded_ReturnsUnsatisfiable()
        {
            var result = ByteRangeHeader.Parse("bytes=0-", 0);

            Assert.AreEqual(ByteRangeParseKind.Unsatisfiable, result.Kind);
        }

        /// <summary>
        /// 0 字节文件上的非法语法仍按忽略处理，不因文件长度为 0 变成 416。
        /// </summary>
        [TestMethod]
        public void Parse_EmptyFileInvalidSyntax_Ignored()
        {
            var result = ByteRangeHeader.Parse("bytes=abc", 0);

            Assert.AreEqual(ByteRangeParseKind.Ignore, result.Kind);
        }

        /// <summary>
        /// unit 大小写与两侧空白不应影响解析结果。
        /// </summary>
        [TestMethod]
        public void Parse_UnitCaseAndWhitespace_ReturnsSatisfiable()
        {
            var result = ByteRangeHeader.Parse(" BYTES = 0-3 ", 10);

            Assert.AreEqual(ByteRangeParseKind.Satisfiable, result.Kind);
            Assert.AreEqual(0L, result.Start);
            Assert.AreEqual(3L, result.End);
            Assert.AreEqual(4L, result.Length);
        }

        /// <summary>
        /// 超出 long 范围的数字按解析失败忽略 Range，不能抛异常。
        /// </summary>
        [TestMethod]
        public void Parse_HugeNumber_Ignored()
        {
            var result = ByteRangeHeader.Parse("bytes=99999999999999999999-", 10);

            Assert.AreEqual(ByteRangeParseKind.Ignore, result.Kind);
        }
    }
}