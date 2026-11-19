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
*文件名： ReflectionMetadataCacheTests
*版本号： net10.0
*唯一标识：0c9a41d7-6e58-4f23-b1c7-8a2d5f30e916
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 19:32:00
*描述：反射元数据缓存的并发与重复执行用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 19:32:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖并发访问同一 action 时元数据缓存的一致性与过滤器仍逐次执行
*
 *****************************************************************************/
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.Tests.Attributes;
using SAEA.MVC.Tests.Tests.Support;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// 反射元数据缓存的用例：形参、任务结果属性与过滤器方法按 action 缓存后，
    /// 并发请求仍要各自拿到正确结果，过滤器也不能因缓存而少执行。
    /// </summary>
    [TestClass]
    public class ReflectionMetadataCacheTests
    {
        /// <summary>
        /// 16 个并发客户端反复请求同一属性路由，每个请求都必须拿到自己那份路径参数结果。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task ReflectionCache_ConcurrentRequests_KeepPerRequestResult()
        {
            const int workers = 16;

            const int perWorker = 20;

            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            var failures = new ConcurrentQueue<string>();

            try
            {
                app.Start();

                var tasks = new Task[workers];

                for (var w = 0; w < workers; w++)
                {
                    var id = (w + 1).ToString();

                    tasks[w] = Task.Run(async () =>
                    {
                        using (var client = TestAppFactory.CreateHttpClient())
                        {
                            for (var i = 0; i < perWorker; i++)
                            {
                                var url = BaseUrl(port) + "/api/v1/routetokenapi/get/" + id;

                                using (var response = await TestAppFactory.GetAsync(client, url).ConfigureAwait(false))
                                {
                                    if (response.StatusCode != HttpStatusCode.OK)
                                    {
                                        failures.Enqueue("状态码 " + (int)response.StatusCode + "，id=" + id);

                                        continue;
                                    }

                                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                                    if (body != "token-api:" + id)
                                    {
                                        failures.Enqueue("正文 " + body + "，期望 token-api:" + id);
                                    }
                                }
                            }
                        }
                    });
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);

                Assert.IsTrue(failures.IsEmpty, "并发请求出现异常结果：" + string.Join("；", failures));
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 同一带过滤器 action 连请求两次，过滤器必须每次执行，缓存只复用方法元数据。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task ReflectionCache_RepeatedFilteredRequests_RunFilterEveryTime()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            OrderRecordingFilterAttribute.Records.Clear();

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                {
                    for (var i = 0; i < 2; i++)
                    {
                        using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/basic/filtered").ConfigureAwait(false))
                        {
                            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "GET /basic/filtered 应返回 200");

                            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                            Assert.AreEqual("filtered-ok", body, "响应正文应为 Filtered 动作的返回值");
                        }
                    }
                }

                CollectionAssert.AreEqual(
                    new[] { "filter", "action", "filter", "action" },
                    OrderRecordingFilterAttribute.Records,
                    "过滤器方法被缓存后仍必须逐次执行");
            }
            finally
            {
                OrderRecordingFilterAttribute.Records.Clear();
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        private static string BaseUrl(int port)
        {
            return "http://" + TestAppFactory.LoopbackHost + ":" + port;
        }
    }
}