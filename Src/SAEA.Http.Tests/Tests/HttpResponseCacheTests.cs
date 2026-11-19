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
*修改时间：2026/11/19 20:00:41
*修改人： yswenli
*版本号： net10.0
*描述：补充并发不同秒数下 Max-Age 不错配的用例，覆盖秒数与文本分两次读写的竞态窗口
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;

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
        /// 同一秒内的重复写入应给出格式一致、过期时刻等于当前时刻加缓存秒数的 Expires。
        /// </summary>
        [TestMethod]
        public void SetCached_RepeatedWithinSameSecond_KeepsExpiresFormatAndOffset()
        {
            var first = CreateResponse();
            var second = CreateResponse();

            first.SetCached(new HttpContentResult("x"), "0,60");

            second.SetCached(new HttpContentResult("x"), "0,60");

            Assert.AreEqual("Max-Age=60", first.Headers[CacheControlKey], "首段为 0 时应写 Max-Age，实际值：" + Read(first, CacheControlKey));
            Assert.IsTrue(first.Headers.ContainsKey("Expires"), "首段为 0 时应写 Expires");

            var firstExpires = ParseGmt(Read(first, "Expires"));
            var secondExpires = ParseGmt(Read(second, "Expires"));

            var offset = (firstExpires - DateTime.Now).TotalSeconds;

            Assert.IsTrue(offset > 55 && offset <= 65, "Expires 应为当前时刻加 60 秒，实际偏移：" + offset);

            // 两次调用之间可能跨过整秒，文本最多相差 1 秒
            Assert.IsTrue(Math.Abs((secondExpires - firstExpires).TotalSeconds) <= 1, "同一秒内重复写入的 Expires 不应跳变");
        }

        /// <summary>
        /// 缓存秒数变化时 Max-Age 与 Expires 都要跟着重算，不能命中上一次的文本。
        /// </summary>
        [TestMethod]
        public void SetCached_DifferentSeconds_RecomputesMaxAgeAndExpires()
        {
            var cached = CreateResponse();

            cached.SetCached(new HttpContentResult("x"), "0,60");

            var fresh = CreateResponse();

            fresh.SetCached(new HttpContentResult("x"), "0,30");

            Assert.AreEqual("Max-Age=30", fresh.Headers[CacheControlKey], "秒数变化后 Max-Age 必须重算，实际值：" + Read(fresh, CacheControlKey));

            var offset = (ParseGmt(Read(fresh, "Expires")) - DateTime.Now).TotalSeconds;

            Assert.IsTrue(offset > 25 && offset <= 35, "秒数变化后 Expires 必须按新时长重算，实际偏移：" + offset);
        }

        /// <summary>
        /// 段内带空格或显式正号时仍按 int.Parse 的规则解析，结果与紧凑写法一致。
        /// </summary>
        [TestMethod]
        public void SetCached_SpaceAndExplicitSign_ParsedLikeIntParse()
        {
            var response = CreateResponse();

            response.SetCached(new HttpContentResult("x"), " +0 , 60 ");

            Assert.AreEqual("Max-Age=60", response.Headers[CacheControlKey], "空格与显式正号应按 int.Parse 规则接受，实际值：" + Read(response, CacheControlKey));
            Assert.AreEqual(HttpStatusCode.OK, response.Status, "首段解析为 0 时应置 200");
        }

        /// <summary>
        /// 超出 int 范围的段仍然抛 OverflowException，不能被静默截断。
        /// </summary>
        [TestMethod]
        public void SetCached_OverflowValue_ThrowsOverflowException()
        {
            var response = CreateResponse();

            Assert.ThrowsException<OverflowException>(() => response.SetCached(new HttpContentResult("x"), "99999999999,60"));
        }

        /// <summary>
        /// 多于两段时按不缓存处理：段数判定先于取值，多余段仍要能解析成整数。
        /// </summary>
        [TestMethod]
        public void SetCached_ExtraSegment_WritesNoCache()
        {
            var response = CreateResponse();

            response.SetCached(new HttpContentResult("x"), "0,60,30");

            Assert.AreEqual("no-cache", response.Headers[CacheControlKey], "三段输入应按不缓存处理，实际值：" + Read(response, CacheControlKey));
        }

        /// <summary>
        /// 多个线程用两种不同缓存秒数并发调用时，每个响应都必须拿到本次秒数对应的 Max-Age。
        /// 断言的正是「读到半新半旧组合」的窗口：若秒数与文本分两次读写，
        /// 某线程读到匹配的秒数后、读取文本之前会被另一线程改写文本，
        /// 于是本次写入的响应带上了别人的时长；旧实现会命中该窗口，整体替换的条目不会。
        /// </summary>
        [TestMethod]
        public void SetCached_ConcurrentDifferentSeconds_NeverMixesMaxAge()
        {
            const int workers = 8;

            const int perWorker = 20000;

            var failures = new ConcurrentQueue<string>();

            var threads = new Thread[workers];

            for (var w = 0; w < workers; w++)
            {
                var index = w;

                threads[w] = new Thread(() =>
                {
                    var response = CreateResponse();

                    for (var i = 0; i < perWorker; i++)
                    {
                        // 线程间奇偶错开，保证同一时刻既有 60 也有 300 在写
                        var seconds = (i + index) % 2 == 0 ? 60 : 300;

                        response.SetCached(new HttpContentResult("x"), "0," + seconds);

                        var expected = "Max-Age=" + seconds;

                        var actual = Read(response, CacheControlKey);

                        if (actual != expected && failures.Count < 5)
                        {
                            failures.Enqueue("第 " + index + " 号线程第 " + i + " 次：期望 " + expected + "，实际 " + actual);
                        }
                    }
                })
                {
                    IsBackground = true
                };

                threads[w].Start();
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }

            Assert.IsTrue(failures.IsEmpty, "并发写入出现 Max-Age 与本次秒数不匹配：" + string.Join("；", failures));
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

        /// <summary>
        /// 按 RFC1123 解析报头里的 GMT 文本，不做时区换算：服务端写出的就是本地时钟值
        /// </summary>
        private static DateTime ParseGmt(string value)
        {
            return DateTime.ParseExact(value, "r", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None);
        }
    }
}