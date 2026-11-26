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
*文件名： ContentDispositionHelperTests
*版本号： net10.0
*唯一标识：8e590b14-ffc3-4ae5-b2b1-b4689b2d7ab7
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/26 17:46:21
*描述：Content-Disposition 头值生成的用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/26 17:46:21
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖 ASCII、中文、inline、头注入、路径与空输入
*
*****************************************************************************/
using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.Http.Common;

namespace SAEA.Http.Tests
{
    /// <summary>
    /// Content-Disposition 头值生成的用例：ASCII 回退名与 RFC 5987 扩展值并存。
    /// </summary>
    [TestClass]
    public class ContentDispositionHelperTests
    {
        /// <summary>
        /// ASCII 文件名应生成 attachment 前缀与 quoted-string 回退名，并带 UTF-8 扩展值。
        /// </summary>
        [TestMethod]
        public void Build_AsciiFileName_ReturnsAttachmentWithRfc5987()
        {
            var value = ContentDispositionHelper.Build("report.pdf");

            StringAssert.StartsWith(value, "attachment; filename=\"report.pdf\"");

            StringAssert.Contains(value, "filename*=UTF-8''report.pdf");
        }

        /// <summary>
        /// 中文文件名在回退名里逐字变为下划线，扩展值使用 UTF-8 百分号编码。
        /// </summary>
        [TestMethod]
        public void Build_ChineseFileName_UsesUnderscoreFallbackAndUtf8ExtendedValue()
        {
            var value = ContentDispositionHelper.Build("报告.pdf");

            Assert.AreEqual("attachment; filename=\"__.pdf\"; filename*=UTF-8''%E6%8A%A5%E5%91%8A.pdf", value);
        }

        /// <summary>
        /// inline 为 true 时前缀应为 inline。
        /// </summary>
        [TestMethod]
        public void Build_InlineTrue_ReturnsInlinePrefix()
        {
            var value = ContentDispositionHelper.Build("report.pdf", true);

            StringAssert.StartsWith(value, "inline; filename=\"report.pdf\"");
        }

        /// <summary>
        /// 文件名里的 CR/LF 属于头注入字符，结果中不得保留。
        /// </summary>
        [TestMethod]
        public void Build_FileNameWithCrLf_RemovesControlCharacters()
        {
            var value = ContentDispositionHelper.Build("a\r\nb.txt");

            Assert.IsFalse(value.Contains("\r"), "结果不应含 CR");
            Assert.IsFalse(value.Contains("\n"), "结果不应含 LF");
            StringAssert.Contains(value, "filename=\"ab.txt\"");
        }

        /// <summary>
        /// 带目录的文件名只保留最后一段。
        /// </summary>
        [TestMethod]
        public void Build_PathWithDirectories_KeepsLastSegmentOnly()
        {
            var value = ContentDispositionHelper.Build("../secret/report.pdf");

            StringAssert.Contains(value, "filename=\"report.pdf\"");
            Assert.IsFalse(value.Contains("secret"), "结果不应含目录部分");
        }

        /// <summary>
        /// null 或空白文件名不生成头值。
        /// </summary>
        [TestMethod]
        public void Build_NullOrWhiteSpace_ReturnsNull()
        {
            Assert.IsNull(ContentDispositionHelper.Build(null), "null 应返回 null");
            Assert.IsNull(ContentDispositionHelper.Build("   "), "空白应返回 null");
        }
    }
}