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
*文件名： AttributeRoutingTests
*版本号： net10.0
*唯一标识：c565d275-69fc-4cdb-a3c4-1ebfe681ba19
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/17 18:20:00
*描述：属性路由端到端用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/17 18:20:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖静态路径、路径参数、动词限定、405、两段式兜底、单段路由、编码段、WS 入口、404 与输出缓存
*
*****************************************************************************/
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.Tests.Tests.Support;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// 属性路由的端到端用例：真端口上验证匹配顺序、路径参数绑定、动词限定与既有兜底行为。
    /// </summary>
    [TestClass]
    public class AttributeRoutingTests
    {
        /// <summary>
        /// 多段静态路径命中属性路由并执行动作。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task AttributeRoute_StaticMultiSegmentPath_Returns200AndBody()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/routedemo/api/static").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "GET /routedemo/api/static 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("routedemo-static-ok", body, "多段静态路径应命中 StaticPath 动作");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 单段路径参数命中并把解码后的值绑定到动作形参。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task AttributeRoute_PathParameter_BindsActionArgument()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/routedemo/echo/42").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "GET /routedemo/echo/42 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("routedemo-echo:42", body, "路径参数 42 应绑定到 Echo 的 id 形参");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 路径参数覆盖同名 query 参数。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task AttributeRoute_PathParameter_OverridesSameNameQuery()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/routedemo/echo/abc?id=999").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "GET /routedemo/echo/abc?id=999 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("routedemo-echo:abc", body, "路径参数应覆盖同名 query 的 999");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 类级模板与方法级模板按 / 拼接后命中。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task AttributeRoute_ClassAndMethodTemplate_Concatenated()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/routedemo/prefix/child").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "GET /routedemo/prefix/child 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("routedemo-prefix-child-ok", body, "类级 routedemo/prefix 与方法级 child 应拼接为完整路径");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 同一路径的 GET 声明命中 GET 分支。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task AttributeRoute_SamePathDifferentVerbs_GetHitsGetAction()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/routedemo/verbs").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "GET /routedemo/verbs 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("routedemo-verbs-get", body, "GET 请求应命中 VerbsGet 动作");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 同一路径的 POST 声明命中 POST 分支。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task AttributeRoute_SamePathDifferentVerbs_PostHitsPostAction()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.PostFormAsync(client, BaseUrl(port) + "/routedemo/verbs", "x=1").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "POST /routedemo/verbs 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("routedemo-verbs-post", body, "POST 请求应命中 VerbsPost 动作");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 只声明 GET 的路径收到 POST 时返回 405，并在响应头给出 Allow。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task AttributeRoute_PostToGetOnlyPath_Returns405WithAllowHeader()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.PostFormAsync(client, BaseUrl(port) + "/routedemo/getonly", "x=1").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode, "POST /routedemo/getonly 应返回 405");

                    // Allow 属实体报头，HttpClient 可能把它归到响应头或内容头
                    var allow = ReadAllowHeader(response);

                    Assert.IsNotNull(allow, "405 响应必须带 Allow 头");

                    StringAssert.Contains(allow, "GET", "Allow 头应声明 GET");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 属性路由表存在但该地址无条目时，两段式约定仍可用。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task AttributeRoute_NotMatched_FallsBackToTwoSegmentRouting()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/basic/index").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "属性路由未命中时 /basic/index 仍应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("basic-index-ok", body, "两段式兜底应命中 BasicController.Index");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 只有一段的属性路由也能命中，不再落到静态文件分支。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task AttributeRoute_SingleSegmentPath_Returns200AndBody()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/route-health").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "GET /route-health 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("routedemo-health-ok", body, "单段属性路由应命中 Health 动作");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 静态段比较忽略大小写，路径参数保留请求原文。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task AttributeRoute_MixedCasePath_MatchesAndKeepsParameterCase()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/ROUTEDEMO/CASE/AbC").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "大小写混合的路径应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("AbC", body, "路径参数应保留请求原文大小写");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 尾斜杠与无尾斜杠等价。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task AttributeRoute_TrailingSlash_MatchesSameRoute()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/routedemo/echo/abc/").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "带尾斜杠的路径应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("routedemo-echo:abc", body, "尾斜杠应被归一化后命中同一路由");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// %2F 解码出 / 时该段不能被当成一个参数，整条路径不匹配并回落到 404。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task AttributeRoute_EncodedSlash_DoesNotCrossSegment()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/routedemo/echo/a%2Fb").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, "%2F 越段的请求不应命中单段路径参数");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 静态段里的 %20 解码为空格后命中。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task AttributeRoute_EncodedSpaceInStaticSegment_Matches()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/routedemo/space/hello%20world").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "含 %20 的静态段应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("routedemo-space-ok", body, "解码后的空格静态段应命中 SpaceSegment 动作");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 用 [Route] 声明的 WS 入口可以升级并回显文本。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task AttributeRoute_WebSocketEndpoint_UpgradesAndEchoes()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                {
                    var socket = await WebSocketProbe.ConnectAsync(
                            "ws://" + TestAppFactory.LoopbackHost + ":" + port + "/routedemo/ws",
                            false,
                            cancellation.Token)
                        .ConfigureAwait(false);

                    try
                    {
                        var reply = await WebSocketProbe.SendAndReceiveTextAsync(socket, "ws-route-hello", cancellation.Token)
                            .ConfigureAwait(false);

                        Assert.AreEqual("ws-route-hello", reply, "[Route] 声明的 WS 入口应完成升级并原样回显");
                    }
                    finally
                    {
                        await WebSocketProbe.CloseAsync(socket, cancellation.Token).ConfigureAwait(false);

                        socket.Dispose();
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
        /// 完全未命中的地址返回 404。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task AttributeRoute_NoMatch_Returns404()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/definitely-not-a-route").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, "未命中任何路由的地址应返回 404");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 带输出缓存的路径参数动作按实际路径分段缓存，不同 id 不共用缓存内容。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task OutputCache_PathParameter_DoesNotShareCacheEntry()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            // 路径值随机，避免进程内缓存被其它用例或重复运行污染
            var firstId = "c" + Guid.NewGuid().ToString("N");
            var secondId = "c" + Guid.NewGuid().ToString("N");

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                {
                    using (var first = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/routedemo/cached/" + firstId).ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, first.StatusCode, "第一次 GET /routedemo/cached/{id} 应返回 200");

                        var firstBody = await first.Content.ReadAsStringAsync().ConfigureAwait(false);

                        Assert.AreEqual("routedemo-cached:" + firstId, firstBody, "第一次请求应返回自己的 id");
                    }

                    using (var second = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/routedemo/cached/" + secondId).ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, second.StatusCode, "第二次 GET /routedemo/cached/{id} 应返回 200");

                        var secondBody = await second.Content.ReadAsStringAsync().ConfigureAwait(false);

                        Assert.AreEqual("routedemo-cached:" + secondId, secondBody, "不同路径参数不能共用同一条输出缓存");
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
        /// 读取响应里的 Allow 头：该头按实体报头归类，可能在响应头或内容头上。
        /// </summary>
        /// <param name="response">HTTP 响应</param>
        /// <returns>Allow 头值；不存在时返回 null</returns>
        private static string ReadAllowHeader(HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues("Allow", out var values))
                return string.Join(",", values);

            if (response.Content != null && response.Content.Headers.TryGetValues("Allow", out var contentValues))
                return string.Join(",", contentValues);

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