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
*文件名： AsyncSuffixFallbackTests
*版本号： net10.0
*唯一标识：9442a0ad-7f20-4be8-a295-d7870549fce2
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/17 17:55:17
*描述：两段式路由 Async 后缀回退的端到端用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/17 17:55:17
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖省略 Async 命中、带 Async 照常命中、精确名优先与属性路由不受影响
*
*****************************************************************************/
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.Tests.Tests.Support;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// 两段式路由的 Async 后缀回退用例：普通控制器在真端口上验证省略后缀可命中，
    /// 精确名优先，以及属性路由仍按模板匹配。
    /// </summary>
    [TestClass]
    public class AsyncSuffixFallbackTests
    {
        /// <summary>
        /// 只声明 PlainAsync 的动作，两段式地址省略 Async 后应命中并返回其返回值。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task ResolveActions_AsyncOnlyAction_PlainPathReturns200()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/asyncfallback/plain").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "GET /asyncfallback/plain 应回退到 PlainAsync 并返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("fallback-plain", body, "响应正文应为 PlainAsync 的返回值");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 带完整 Async 后缀的地址仍然按精确名命中，回退不影响原有写法。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task ResolveActions_AsyncOnlyAction_FullAsyncPathStillWorks()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/asyncfallback/plainasync").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "GET /asyncfallback/plainasync 应精确命中 PlainAsync 并返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("fallback-plain", body, "响应正文应为 PlainAsync 的返回值");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// Both 与 BothAsync 同时声明时，省略后缀的地址必须命中同步版本。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task ResolveActions_BothNamesDeclared_ExactNameWins()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/asyncfallback/both").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "GET /asyncfallback/both 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("fallback-both-sync", body, "精确名优先，应命中同步的 Both 而不是 BothAsync");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 只声明属性路由模板的动作按模板命中；模板之外的地址即使控制器存在也返回 404，
        /// 证明属性路由不按 action 方法名匹配。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task AttributeRoute_TemplateDeclared_ActionNameNotUsed()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                {
                    using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/asyncfallback/attr-only").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "GET /asyncfallback/attr-only 应按模板命中 AttrOnlyAsync 并返回 200");

                        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        Assert.AreEqual("fallback-attr", body, "响应正文应为 AttrOnlyAsync 的返回值");
                    }

                    // attr 既不在模板里，也不是 AttrOnlyAsync 去掉 Async 后的名字，两段式与属性路由都不应命中；
                    // 站点根目录是空的临时目录，不会有静态文件兜住这个地址
                    using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/asyncfallback/attr").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, "GET /asyncfallback/attr 应返回 404，属性路由不得按方法名匹配");
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