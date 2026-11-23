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
*文件名： HttpCookieLazyParseTests
*版本号： net10.0
*唯一标识：65d7837f-5fcd-4fd8-b414-ab94ce861638
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/26 10:30:00
*描述：Cookie 报头按需解析的结果与读取行为用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/26 10:30:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，锁定无报头返回非 null 空集合、多等号与无等号按空值、空段跳过、同名后者覆盖与重复读取同一实例
*
*****************************************************************************/
using System;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.Http.Base;

namespace SAEA.Http.Tests
{
    /// <summary>Cookie 报头按需解析：未读不解析，读到时报头原文按原规则解析。</summary>
    [TestClass]
    public class HttpCookieLazyParseTests
    {
        /// <summary>
        /// 没有 Cookie 报头时首次读取应得到非 null 的空集合，且重复读取返回同一实例。
        /// </summary>
        [TestMethod]
        public void Cookies_NoCookieHeader_ReturnsEmptyCollection()
        {
            var bytes = Bytes(
                "GET /c HTTP/1.1\r\n"
                + "Host: h\r\n"
                + "\r\n");

            RequestDataReader.Analysis(bytes.AsSpan(), out var message);

            Assert.IsNotNull(message.Cookies, "没有 Cookie 报头时应返回非 null 的空集合。");
            Assert.AreEqual(0, message.Cookies.Count, "没有 Cookie 报头时集合应为空。");
            var firstRead = message.Cookies;
            var secondRead = message.Cookies;

            Assert.AreSame(firstRead, secondRead, "重复读取应返回同一个集合实例。");
        }

        /// <summary>
        /// 一个 Cookie 报头内的各段按分号解析：无等号与多等号按空值，值做 URL 与 HTML 解码。
        /// </summary>
        [TestMethod]
        public void Cookies_CookieHeader_ParsesPairsWithEdgeCases()
        {
            var bytes = Bytes(
                "GET /c HTTP/1.1\r\n"
                + "Host: h\r\n"
                + "Cookie: a=1; b=2; c=%E4%B8%AD; bare; d=x=y; empty=\r\n"
                + "\r\n");

            RequestDataReader.Analysis(bytes.AsSpan(), out var message);

            Assert.AreEqual(6, message.Cookies.Count, "六段 Cookie 都应解析进集合。");
            Assert.AreEqual("1", message.Cookies["a"].Value, "普通值应原样保留。");
            Assert.AreEqual("2", message.Cookies["b"].Value, "第二个 Cookie 也应解析。");
            Assert.AreEqual("中", message.Cookies["c"].Value, "值应做 URL 解码。");
            Assert.AreEqual(string.Empty, message.Cookies["bare"].Value, "没有等号的 Cookie 值应为空串。");
            Assert.AreEqual(string.Empty, message.Cookies["d"].Value, "出现多个等号时值应为空串。");
            Assert.AreEqual(string.Empty, message.Cookies["empty"].Value, "等号后为空时值应为空串。");
        }

        /// <summary>
        /// 连续分号产生的空段不建键，含空白的分段按其名字去空白后入集合。
        /// </summary>
        [TestMethod]
        public void Cookies_EmptySegments_AreIgnored()
        {
            var bytes = Bytes(
                "GET /c HTTP/1.1\r\n"
                + "Host: h\r\n"
                + "Cookie: a=1;;; b=2;\r\n"
                + "\r\n");

            RequestDataReader.Analysis(bytes.AsSpan(), out var message);

            Assert.AreEqual(2, message.Cookies.Count, "空段不应产生键。");
            Assert.IsFalse(message.Cookies.ContainsKey(string.Empty), "空段不能生成空前缀的键。");
            Assert.AreEqual("2", message.Cookies["b"].Value, "空段之后的分段应正常解析。");
        }

        /// <summary>
        /// 同名 Cookie 后出现的值覆盖先出现的值。
        /// </summary>
        [TestMethod]
        public void Cookies_DuplicateName_LastValueWins()
        {
            var bytes = Bytes(
                "GET /c HTTP/1.1\r\n"
                + "Host: h\r\n"
                + "Cookie: dup=1; keep=0; dup=2\r\n"
                + "\r\n");

            RequestDataReader.Analysis(bytes.AsSpan(), out var message);

            Assert.AreEqual(2, message.Cookies.Count, "同名 Cookie 只保留一个键。");
            Assert.AreEqual("2", message.Cookies["dup"].Value, "同名 Cookie 应取后出现的值。");
        }

        /// <summary>
        /// 显式给 Cookies 赋值后不再回落到报头解析，读取到的就是赋值的集合。
        /// </summary>
        [TestMethod]
        public void Cookies_ExplicitAssignment_OverridesHeaderParse()
        {
            var bytes = Bytes(
                "GET /c HTTP/1.1\r\n"
                + "Host: h\r\n"
                + "Cookie: a=1; b=2\r\n"
                + "\r\n");

            RequestDataReader.Analysis(bytes.AsSpan(), out var message);

            var assigned = new HttpCookies();

            assigned["only"] = new SAEA.Http.HttpCookie("only", "x");

            message.Cookies = assigned;

            Assert.AreSame(assigned, message.Cookies, "显式赋值后应返回赋值的集合实例。");
            Assert.AreEqual(1, message.Cookies.Count, "显式赋值后不应再解析报头。");
            Assert.AreEqual("x", message.Cookies["only"].Value, "显式赋值的 Cookie 应可读取。");
        }

        /// <summary>
        /// 先读集合再读单值入口不影响集合内容，两者语义一致。
        /// </summary>
        [TestMethod]
        public void Cookies_ReadCollectionThenHeaderValue_StaysConsistent()
        {
            var bytes = Bytes(
                "GET /c HTTP/1.1\r\n"
                + "Host: h\r\n"
                + "Cookie: sid=abc; x=1\r\n"
                + "\r\n");

            RequestDataReader.Analysis(bytes.AsSpan(), out var message);

            Assert.AreEqual(2, message.Cookies.Count, "应解析出两个 Cookie。");

            Assert.IsTrue(message.TryGetCookieValue("sid", out var value), "已解析后按名查找仍应命中。");
            Assert.AreEqual("abc", value, "按名查找的值应与集合一致。");
            Assert.IsFalse(message.TryGetCookieValue("nope", out var missing), "不存在的名字应返回 false。");
            Assert.IsNull(missing, "不存在的名字应把值置为 null。");
        }

        /// <summary>
        /// 按名查找不建立集合：未读集合时也能命中，且规则与集合解析一致。
        /// </summary>
        [TestMethod]
        public void Cookies_TryGetCookieValueWithoutParsing_UsesSameRules()
        {
            var bytes = Bytes(
                "GET /c HTTP/1.1\r\n"
                + "Host: h\r\n"
                + "Cookie: sid=%E4%B8%AD; bare; multi=a=b; sid2=%20x%20\r\n"
                + "\r\n");

            RequestDataReader.Analysis(bytes.AsSpan(), out var message);

            Assert.IsTrue(message.TryGetCookieValue("sid", out var sid), "未读集合时按名查找也应命中。");
            Assert.AreEqual("中", sid, "按名查找的值应与集合解析一样做 URL 解码。");

            Assert.IsTrue(message.TryGetCookieValue("bare", out var bare), "无等号的分段也应命中。");
            Assert.AreEqual(string.Empty, bare, "无等号的分段值应为空串。");

            Assert.IsTrue(message.TryGetCookieValue("multi", out var multi), "多等号的分段也应命中。");
            Assert.AreEqual(string.Empty, multi, "多等号的分段值应为空串。");

            Assert.IsTrue(message.TryGetCookieValue("sid2", out var sid2), "名字后带空值的分段也应命中。");
            Assert.AreEqual(" x ", sid2, "值两侧的空白按原规则保留。");

            Assert.IsFalse(message.TryGetCookieValue("SID", out var upper), "名字区分大小写。");
            Assert.IsNull(upper, "名字大小写不匹配时应返回 null。");

            // 按名查找之后再读集合，解析结果必须与按名查找一致
            Assert.AreEqual("中", message.Cookies["sid"].Value, "按名查找不能改变后续集合解析的结果。");
        }

        private static byte[] Bytes(string text)
        {
            return Encoding.UTF8.GetBytes(text);
        }
    }
}