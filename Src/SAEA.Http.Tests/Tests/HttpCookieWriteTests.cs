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
*文件名： HttpCookieWriteTests
*版本号： net10.0
*唯一标识：cd20953d-c097-43b8-8906-39d47c0593c1
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 18:52:00
*描述：Set-Cookie 序列化用例：未设置的属性不输出，已设置的属性按 Domain、Expires、Path 顺序输出
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 18:52:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，锁定空域与未设置过期时间时不输出非法属性值
*
*****************************************************************************/
using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SAEA.Http.Tests
{
    /// <summary>Set-Cookie 文本只输出已设置的属性，长度估算与实际输出一致。</summary>
    [TestClass]
    public class HttpCookieWriteTests
    {
        /// <summary>默认 Cookie 不输出 Domain 与 Expires，只保留 Path，客户端才可能回传。</summary>
        [TestMethod]
        public void HttpCookie_DefaultAttributes_OmitsDomainAndExpires()
        {
            var cookie = new HttpCookie("k", "v");

            var text = cookie.ToString();

            Assert.AreEqual("Set-Cookie: k=v; Path=/", text, "默认 Cookie 只应输出 Path。");
            Assert.IsFalse(text.Contains("Domain="), "空域不应输出 Domain=。");
            Assert.IsFalse(text.Contains("Expires="), "未设置过期时间不应输出 Expires=。");
            Assert.IsFalse(text.Contains("Session"), "未设置过期时间不应写出字面量 Session。");
            Assert.IsTrue(text.Contains("Path=/"), "Path 必须保留。");
            Assert.IsTrue(text.StartsWith("Set-Cookie: k=v", StringComparison.Ordinal), "前缀应是标准的 Set-Cookie 行首。");
        }

        /// <summary>显式设置域与过期时间时，两属性都要输出且顺序为 Domain、Expires、Path。</summary>
        [TestMethod]
        public void HttpCookie_WithDomainAndExpires_WritesBothInOrder()
        {
            var expires = new DateTime(2030, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            var cookie = new HttpCookie("token", "abc123", expires, "/app", "example.com");

            var text = cookie.ToString();

            Assert.AreEqual("Set-Cookie: token=abc123; Domain=example.com; Expires=Wed, 02 Jan 2030 03:04:05 GMT; Path=/app",
                text, "已设置的属性应按 Domain、Expires、Path 顺序输出，日期为 GMT 格式。");

            var domainIndex = text.IndexOf("Domain=", StringComparison.Ordinal);
            var expiresIndex = text.IndexOf("Expires=", StringComparison.Ordinal);
            var pathIndex = text.IndexOf("Path=", StringComparison.Ordinal);

            Assert.IsTrue(domainIndex < expiresIndex && expiresIndex < pathIndex, "属性顺序必须是 Domain、Expires、Path。");
        }

        /// <summary>默认 Cookie 下发的 Set-Cookie 行必须能被标准 Cookie 容器接受，否则客户端不会回传。</summary>
        [TestMethod]
        public void HttpCookie_DefaultAttributes_IsAcceptedByCookieContainer()
        {
            var cookie = new HttpCookie("SESSIONID", "abc123");
            var text = cookie.ToString();

            var container = new System.Net.CookieContainer();
            var uri = new Uri("http://example.com/");

            // CookieContainer 只接受报头的值部分，不含 "Set-Cookie: " 前缀
            container.SetCookies(uri, text.Substring("Set-Cookie: ".Length));

            var parsed = container.GetCookies(uri)["SESSIONID"];

            Assert.IsNotNull(parsed, "标准 Cookie 容器应能收下服务端下发的会话 Cookie。");
            Assert.AreEqual("abc123", parsed.Value, "回传的 Cookie 值应与下发值一致。");
        }

        /// <summary>长度估算必须与实际输出的字符数一致，否则预置容量要么偏大要么触发扩容。</summary>
        [TestMethod]
        public void HttpCookie_EstimatedLength_MatchesActualText()
        {
            var getEstimatedLength = typeof(HttpCookie).GetMethod("GetEstimatedLength",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(getEstimatedLength, "GetEstimatedLength 应存在，否则本用例失去意义。");

            var cookies = new[]
            {
                new HttpCookie("k", "v"),
                new HttpCookie("token", "abc123", new DateTime(2030, 1, 2, 3, 4, 5, DateTimeKind.Utc), "/app", "example.com")
            };

            foreach (var cookie in cookies)
            {
                var estimated = (int)getEstimatedLength.Invoke(cookie, null);

                Assert.AreEqual(cookie.ToString().Length, estimated, "估算长度应与实际输出等长。");
            }
        }
    }
}