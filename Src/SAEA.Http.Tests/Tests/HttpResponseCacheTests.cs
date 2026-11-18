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
*文件名： HttpResponseCacheTests
*版本号： net10.0
*唯一标识：0af1d539-8e70-4a12-a766-d4d8db3e5c1b
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/18 13:50:00
*描述：缓存判定参数解析的用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/18 13:50:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，锁住手工分段解析对合法值、单值、空格与非法输入的容忍行为
*
*****************************************************************************/
using System;
using System.Net;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.Http.Model;

namespace SAEA.Http.Tests
{
    /// <summary>
    /// 缓存判定参数解析的用例：合法两段值写 Max-Age，段数不足按不缓存处理，非法值仍抛出解析异常。
    /// </summary>
    [TestClass]
    public class HttpResponseCacheTests
    {
        private const string CacheControlKey = "Cache-Control";

        /// <summary>
        /// 合法两段值应写 Max-Age 并把状态码置为 200。
        /// </summary>
        [TestMethod]
        public void SetCached_ValidPair_WritesMaxAge()
        {
            var response = CreateResponse();

            response.SetCached(new HttpContentResult("x"), "60,60");

            Assert.AreEqual("Max-Age=60", response.Headers[CacheControlKey], "合法两段值应写成 Max-Age，实际值：" + Read(response, CacheControlKey));
            Assert.AreEqual(HttpStatusCode.OK, response.Status, "首段为 60 时应置 200");
        }

        /// <summary>
        /// 只有一段时按不缓存处理，状态行不会写成 Max-Age。
        /// </summary>
        [TestMethod]
        public void SetCached_SingleValue_WritesNoCache()
        {
            var response = CreateResponse();

            response.SetCached(new HttpContentResult("x"), "60");

            Assert.AreEqual("no-cache", response.Headers[CacheControlKey], "单段输入应写成 no-cache，实际值：" + Read(response, CacheControlKey));
        }

        /// <summary>
        /// 逗号后带空格仍是合法两段值，与不带空格的结果一致。
        /// </summary>
        [TestMethod]
        public void SetCached_SpaceAfterComma_WritesMaxAge()
        {
            var response = CreateResponse();

            response.SetCached(new HttpContentResult("x"), "60, 60");

            Assert.AreEqual("Max-Age=60", response.Headers[CacheControlKey], "段内前后空格应被忽略，实际值：" + Read(response, CacheControlKey));
        }

        /// <summary>
        /// 非数字段仍抛出 FormatException，不能被吞掉静默降级成 no-cache。
        /// </summary>
        [TestMethod]
        public void SetCached_NonNumeric_ThrowsFormatException()
        {
            var response = CreateResponse();

            Assert.ThrowsException<FormatException>(() => response.SetCached(new HttpContentResult("x"), "abc,60"));
        }

        /// <summary>
        /// 空串按空段解析，同样抛出 FormatException。
        /// </summary>
        [TestMethod]
        public void SetCached_EmptyString_ThrowsFormatException()
        {
            var response = CreateResponse();

            Assert.ThrowsException<FormatException>(() => response.SetCached(new HttpContentResult("x"), string.Empty));
        }

        /// <summary>
        /// 首段为负数时按不缓存处理并写 no-cache。
        /// </summary>
        [TestMethod]
        public void SetCached_NegativeFlag_WritesNoCache()
        {
            var response = CreateResponse();

            response.SetCached(new HttpContentResult("x"), "-1,60");

            Assert.AreEqual("no-cache", response.Headers[CacheControlKey], "首段小于 0 应写成 no-cache，实际值：" + Read(response, CacheControlKey));
        }

        /// <summary>
        /// 响应实体的构造函数是 internal，测试通过反射创建实例，不扩大产品公开面。
        /// </summary>
        private static HttpResponse CreateResponse()
        {
            return (HttpResponse)Activator.CreateInstance(typeof(HttpResponse), true);
        }

        private static string Read(HttpResponse response, string key)
        {
            return response.Headers.TryGetValue(key, out var value) ? value : "<缺失>";
        }
    }
}