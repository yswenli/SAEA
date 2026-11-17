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
*命名空间：SAEA.MVC.Tests
*文件名： OutputCacheQueryTests
*版本号： net10.0
*唯一标识：d3f5b091-2e64-4a78-9c15-7b8e0a34d6c2
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/17 22:00:00
*描述：输出缓存键包含规范化 query 的端到端用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/17 22:00:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖 query 值不同、参数顺序不同与路径参数不同三种缓存键场景
*
*****************************************************************************/
using System;
using System.Net;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.Tests.Tests.Support;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// 输出缓存 query 键的端到端用例：真端口上验证 query 值参与缓存键、参数顺序无关且路径参数不共用条目。
    /// </summary>
    [TestClass]
    public class OutputCacheQueryTests
    {
        /// <summary>
        /// 同一路径下 query 值不同时不能命中彼此的缓存，第二次请求必须重新执行并回显新的 tag。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task OutputCache_QueryValueChanged_ReturnsFreshResponse()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            // 路径值随机，避免进程内缓存被其它用例或重复运行污染
            var id = Guid.NewGuid().ToString("N");

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                {
                    string firstBody;

                    using (var first = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/cacheprobe/" + id + "?tag=1").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, first.StatusCode, "第一次 GET 带 tag=1 应返回 200");

                        firstBody = await first.Content.ReadAsStringAsync().ConfigureAwait(false);
                    }

                    var firstParts = SplitBody(firstBody);

                    Assert.AreEqual("1", firstParts[1], "第一次请求应回显 tag=1");

                    using (var second = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/cacheprobe/" + id + "?tag=2").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, second.StatusCode, "第二次 GET 带 tag=2 应返回 200");

                        var secondBody = await second.Content.ReadAsStringAsync().ConfigureAwait(false);

                        var secondParts = SplitBody(secondBody);

                        Assert.AreEqual("2", secondParts[1], "第二次请求必须回显 tag=2，不能命中 tag=1 的缓存");
                        Assert.AreEqual(
                            int.Parse(firstParts[2]) + 1,
                            int.Parse(secondParts[2]),
                            "第二次请求应真实执行一次，执行计数加一");
                    }
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// query 参数顺序不同时必须得到同一个缓存键：第二次请求命中缓存，正文与执行计数均不变。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task OutputCache_QueryOrderChanged_SharesCacheEntry()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            var id = Guid.NewGuid().ToString("N");

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                {
                    string firstBody;

                    using (var first = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/cacheprobe/" + id + "?a=1&b=2").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, first.StatusCode, "GET 带 a=1&b=2 应返回 200");

                        firstBody = await first.Content.ReadAsStringAsync().ConfigureAwait(false);
                    }

                    using (var second = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/cacheprobe/" + id + "?b=2&a=1").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, second.StatusCode, "GET 带 b=2&a=1 应返回 200");

                        var secondBody = await second.Content.ReadAsStringAsync().ConfigureAwait(false);

                        Assert.AreEqual(firstBody, secondBody, "参数顺序不同必须得到同一个缓存键并返回相同正文");
                        Assert.AreEqual(
                            int.Parse(SplitBody(firstBody)[2]),
                            int.Parse(SplitBody(secondBody)[2]),
                            "第二次请求应命中缓存，执行计数不变");
                    }
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 不同路径参数各自回显自己的 id，不共用同一条缓存。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task OutputCache_DifferentPathParameters_DoNotShareCacheEntry()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            var firstId = Guid.NewGuid().ToString("N");
            var secondId = Guid.NewGuid().ToString("N");

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                {
                    using (var first = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/cacheprobe/" + firstId).ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, first.StatusCode, "第一次 GET 应返回 200");

                        var firstBody = await first.Content.ReadAsStringAsync().ConfigureAwait(false);

                        StringAssert.StartsWith(firstBody, firstId + "|", "第一次请求应回显自己的 id");
                    }

                    using (var second = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/cacheprobe/" + secondId).ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, second.StatusCode, "第二次 GET 应返回 200");

                        var secondBody = await second.Content.ReadAsStringAsync().ConfigureAwait(false);

                        StringAssert.StartsWith(secondBody, secondId + "|", "不同路径参数不能共用同一条输出缓存");
                    }
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 按分隔符拆分正文为 id、tag、执行次数三段。
        /// </summary>
        /// <param name="body">响应正文，形如 id|tag|count</param>
        /// <returns>固定三段的数组</returns>
        private static string[] SplitBody(string body)
        {
            var parts = body.Split('|');

            Assert.AreEqual(3, parts.Length, "响应正文应形如 id|tag|count，实际为：" + body);

            return parts;
        }

        /// <summary>
        /// 拼出本用例服务的回环基地址。
        /// </summary>
        /// <param name="port">监听端口</param>
        /// <returns>不含路径的基地址</returns>
        private static string BaseUrl(int port)
        {
            return "http://" + TestAppFactory.LoopbackHost + ":" + port;
        }
    }
}