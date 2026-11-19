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
*文件名： TwoSegmentVerbTests
*版本号： net10.0
*唯一标识：b7d2e548-9c31-4a86-8f57-2d0a6e19c4f3
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/17 21:00:00
*描述：两段式路由按 action 声明动词拦截的端到端用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/17 21:00:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖只声明 GET、只声明 POST、同时声明与都不声明以及默认路由的动词判定
*
*****************************************************************************/
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.Tests.Tests.Support;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// 两段式路由的动词用例：真端口上验证 405 判定与 Allow 头，并确认未声明动词时行为不变。
    /// </summary>
    [TestClass]
    public class TwoSegmentVerbTests
    {
        /// <summary>
        /// 只声明 GET 的动作收到 GET 时正常执行。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task TwoSegment_GetOnlyAction_GetReturns200()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/verbprobe/getonly").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "GET /verbprobe/getonly 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("verbprobe-get-only", body, "响应正文应为 GetOnly 动作的返回值");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 只声明 GET 的动作收到 POST 时返回 405，Allow 为 GET 与隐式允许的 HEAD。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task TwoSegment_GetOnlyAction_PostReturns405WithGetAllow()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.PostFormAsync(client, BaseUrl(port) + "/verbprobe/getonly", "x=1").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode, "POST /verbprobe/getonly 应返回 405");

                    // Allow 属实体报头，HttpClient 会把值按逗号拆成多项，可能在响应头或内容头上
                    var allow = ReadAllowValues(response);

                    Assert.IsNotNull(allow, "405 响应必须带 Allow 头");
                    CollectionAssert.AreEqual(new[] { "GET", "HEAD" }, allow, "只声明 GET 时 Allow 应给出 GET 与隐式允许的 HEAD");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 只声明 GET 的动作收到 PUT 时返回 405：PUT 走 GET 分支，必须按请求行的方法判定。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task TwoSegment_GetOnlyAction_PutReturns405()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await SendAsync(client, HttpMethod.Put, BaseUrl(port) + "/verbprobe/getonly").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode, "PUT /verbprobe/getonly 应返回 405");

                    var allow = ReadAllowValues(response);

                    Assert.IsNotNull(allow, "405 响应必须带 Allow 头");
                    CollectionAssert.Contains(allow, "GET", "Allow 头应声明 GET");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 只声明 POST 的动作收到 POST 时正常执行。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task TwoSegment_PostOnlyAction_PostReturns200()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.PostFormAsync(client, BaseUrl(port) + "/verbprobe/postonly", "x=1").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "POST /verbprobe/postonly 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("verbprobe-post-only", body, "响应正文应为 PostOnly 动作的返回值");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 只声明 POST 的动作收到 GET 时返回 405，Allow 只声明 POST。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task TwoSegment_PostOnlyAction_GetReturns405WithPostAllow()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/verbprobe/postonly").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode, "GET /verbprobe/postonly 应返回 405");

                    var allow = ReadAllowValues(response);

                    Assert.IsNotNull(allow, "405 响应必须带 Allow 头");
                    CollectionAssert.AreEqual(new[] { "POST" }, allow, "只声明 POST 时 Allow 应只有 POST");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 同时声明 GET 与 POST 的动作两种动词都放行。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task TwoSegment_BothVerbsAction_GetPostReturn200AndPutReturns405()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                {
                    using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/verbprobe/both").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "GET /verbprobe/both 应返回 200");
                    }

                    using (var response = await TestAppFactory.PostFormAsync(client, BaseUrl(port) + "/verbprobe/both", "x=1").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "POST /verbprobe/both 应返回 200");
                    }

                    // 两种动词都不匹配时 Allow 按 GET、POST 顺序拼接，顺带验证并集格式
                    using (var response = await SendAsync(client, HttpMethod.Put, BaseUrl(port) + "/verbprobe/both").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode, "PUT /verbprobe/both 应返回 405");

                        var allow = ReadAllowValues(response);

                        Assert.IsNotNull(allow, "405 响应必须带 Allow 头");
                        CollectionAssert.AreEqual(new[] { "GET", "HEAD", "POST" }, allow, "同时声明 GET 与 POST 时 Allow 应按 GET、HEAD、POST 顺序列出");
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
        /// 未声明动词的动作对 GET、POST、PUT 全部放行，证明改动不破坏既有行为。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task TwoSegment_NoVerbAction_GetPostPutReturn200()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                {
                    using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/verbprobe/any").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "GET /verbprobe/any 应返回 200");

                        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        Assert.AreEqual("verbprobe-any", body, "响应正文应为 Any 动作的返回值");
                    }

                    using (var response = await TestAppFactory.PostFormAsync(client, BaseUrl(port) + "/verbprobe/any", "x=1").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "POST /verbprobe/any 应返回 200");
                    }

                    using (var response = await SendAsync(client, HttpMethod.Put, BaseUrl(port) + "/verbprobe/any").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "PUT /verbprobe/any 应返回 200");
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
        /// 默认路由同样按声明动词判定：GET 放行，POST 返回 405。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task DefaultRoute_GetOnlyAction_PostReturns405WithGetAllow()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port, configure: instance => instance.SetDefault("verbprobe", "getonly"));

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                {
                    using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "默认路由的 GET 应返回 200");

                        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        Assert.AreEqual("verbprobe-get-only", body, "默认路由应命中 VerbProbeController.GetOnly");
                    }

                    using (var response = await TestAppFactory.PostFormAsync(client, BaseUrl(port) + "/", "x=1").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode, "默认路由的 POST 应返回 405");

                        var allow = ReadAllowValues(response);

                        Assert.IsNotNull(allow, "405 响应必须带 Allow 头");
                        CollectionAssert.Contains(allow, "GET", "Allow 头应声明 GET");
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
        /// 发送指定方法的请求，并沿用工厂的连接就绪重试。
        /// </summary>
        /// <param name="client">请求客户端</param>
        /// <param name="method">请求方法</param>
        /// <param name="url">完整地址</param>
        /// <returns>响应</returns>
        private static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url)
        {
            return TestAppFactory.SendWithReadyRetryAsync(client, () => new HttpRequestMessage(method, url));
        }

        /// <summary>
        /// 读取响应里的 Allow 头：该头按实体报头归类，可能在响应头或内容头上，值会被拆成多项。
        /// </summary>
        /// <param name="response">HTTP 响应</param>
        /// <returns>Allow 的各个方法名，顺序即服务端声明顺序；不存在时返回 null</returns>
        private static string[] ReadAllowValues(HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues("Allow", out var values))
                return values.ToArray();

            if (response.Content != null && response.Content.Headers.TryGetValues("Allow", out var contentValues))
                return contentValues.ToArray();

            return null;
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