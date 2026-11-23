/****************************************************************************
 * 
   ____    _    _____    _      ____             _        _   
  / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
  \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __| 
   ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ 
  |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                               
  
 *Copyright (c) yswenli All Rights Reserved.
 *CLR版本： net10.0
 *机器名称：WALLE
 *公司名称：yswenli
 *命名空间：SAEA.MVC.Tests
 *文件名： RequestHookTests
 *版本号： net10.0
 *唯一标识：9c2f8f4e-4f0a-4d0e-9a38-2a1a6a5f2a10
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/24 10:00:00
 *描述：路由前钩子与自定义上下文类型挂载点的端到端用例
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/24 10:00:00
 *修改人： yswenli
 *版本号： net10.0
 *描述：新增，锁定 OnRequestExecuting 能在路由前接管请求（接口、静态文件、HEAD、POST、异常、连接复用），
 *      以及 ContextType 能替换默认上下文
 *
 *****************************************************************************/
using SAEA.Http.Model;
using SAEA.MVC.Hosting;
using SAEA.MVC.Results;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.Tests.Tests.Support;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// 自定义上下文通过路由前钩子接管请求，以及按配置替换上下文类型的用例。
    /// </summary>
    [TestClass]
    public class RequestHookTests
    {
        /// <summary>
        /// 钩子返回结果时应跳过路由、动作不执行，并按普通结果写回状态码与自定义报头。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task OnRequestExecuting_ReturnsResult_SkipsRouting()
        {
            await WithHookHostAsync(async (port, client) =>
            {
                using (var blocked = await TestAppFactory.SendWithReadyRetryAsync(
                    client,
                    () => new HttpRequestMessage(HttpMethod.Get, BaseUrl(port) + "/basic/index?block=1")).ConfigureAwait(false))
                {
                    var body = await blocked.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual(HttpStatusCode.Unauthorized, blocked.StatusCode, "钩子接管后应返回钩子给出的状态码");
                    Assert.AreEqual("hook-blocked", body, "钩子接管后不应执行动作，实际正文：" + body);
                    Assert.IsTrue(blocked.Headers.Contains("X-Hook"), "钩子写入的报头应随结果一起写出");
                }

                var passed = await TestAppFactory.ReadContentAsync(
                    client,
                    () => new HttpRequestMessage(HttpMethod.Get, BaseUrl(port) + "/basic/index")).ConfigureAwait(false);

                Assert.AreEqual("basic-index-ok", passed, "钩子返回 null 时应继续路由到动作，实际正文：" + passed);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 钩子返回 302 与 Set-Cookie 时，两个报头都应写出，且不再执行动作。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task OnRequestExecuting_ReturnsRedirect_WritesHeaders()
        {
            // 302 由测试自己判定，客户端不能自动跟随，否则看到的是跳转后的动作结果
            await WithHookHostAsync(async (port, client) =>
            {
                using (var response = await TestAppFactory.SendWithReadyRetryAsync(
                    client,
                    () => new HttpRequestMessage(HttpMethod.Get, BaseUrl(port) + "/basic/index?redirect=1")).ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.Found, response.StatusCode, "钩子返回 302 时应原样写出状态码");
                    Assert.AreEqual("/basic/index", response.Headers.Location?.OriginalString, "Location 报头应由钩子写入");
                    Assert.IsTrue(response.Headers.TryGetValues("Set-Cookie", out var cookies), "Set-Cookie 报头应由钩子写入");
                    Assert.IsTrue(string.Join(";", cookies).Contains("hook_sid=abc"), "Set-Cookie 内容应与钩子写入的一致");
                }
            }, allowAutoRedirect: false).ConfigureAwait(false);
        }

        /// <summary>
        /// 静态文件同样要经过钩子：被拦时返回钩子的结果，放行时才读取磁盘文件。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task OnRequestExecuting_CoversStaticFile()
        {
            await WithHookHostAsync(async (port, client) =>
            {
                using (var blocked = await TestAppFactory.SendWithReadyRetryAsync(
                    client,
                    () => new HttpRequestMessage(HttpMethod.Get, BaseUrl(port) + "/probe.txt?block=1")).ConfigureAwait(false))
                {
                    var body = await blocked.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual(HttpStatusCode.Unauthorized, blocked.StatusCode, "静态文件请求也要先过钩子");
                    Assert.AreEqual("hook-blocked", body, "被拦时不应返回磁盘内容，实际正文：" + body);
                }

                var passed = await TestAppFactory.ReadContentAsync(
                    client,
                    () => new HttpRequestMessage(HttpMethod.Get, BaseUrl(port) + "/probe.txt")).ConfigureAwait(false);

                Assert.AreEqual("static-ok", passed, "钩子放行后应正常读取静态文件，实际正文：" + passed);
            }, prepareRoot: root => File.WriteAllText(Path.Combine(root, "probe.txt"), "static-ok")).ConfigureAwait(false);
        }

        /// <summary>
        /// HEAD 被拦时只回报头，正文必须为空。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task OnRequestExecuting_HeadRequest_HasNoBody()
        {
            await WithHookHostAsync(async (port, client) =>
            {
                using (var response = await TestAppFactory.SendWithReadyRetryAsync(
                    client,
                    () => new HttpRequestMessage(HttpMethod.Head, BaseUrl(port) + "/basic/index?block=1")).ConfigureAwait(false))
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode, "HEAD 也要经过钩子");
                    Assert.AreEqual(string.Empty, body, "HEAD 的正文应被抑制，实际正文：" + body);
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 钩子放行后，POST 表单仍要完成参数合并并绑定到动作参数。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task OnRequestExecuting_PassesPost_FormBindingStillWorks()
        {
            await WithHookHostAsync(async (port, client) =>
            {
                using (var response = await TestAppFactory.PostFormAsync(
                    client, BaseUrl(port) + "/basic/echo", "name=saea").ConfigureAwait(false))
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "钩子放行的 POST 应正常执行");
                    Assert.AreEqual("basic-echo:saea", body, "表单绑定不应受钩子影响，实际正文：" + body);
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 钩子抛出的异常应由管道统一转成 500，而不是打断连接。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task OnRequestExecuting_Throws_Returns500()
        {
            await WithHookHostAsync(async (port, client) =>
            {
                using (var response = await TestAppFactory.SendWithReadyRetryAsync(
                    client,
                    () => new HttpRequestMessage(HttpMethod.Get, BaseUrl(port) + "/basic/index?hookthrow=1")).ConfigureAwait(false))
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode, "钩子抛异常应转成 500");
                    Assert.IsTrue(body.Contains("hook-boom"), "500 正文应带出钩子的异常信息，实际正文：" + body);
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 同一条连接上先后被拦与放行，上下文复用不应把上一次的响应状态带进下一次请求。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task OnRequestExecuting_ConnectionReuse_NoStateLeak()
        {
            await WithHookHostAsync(async (port, client) =>
            {
                using (var blocked = await TestAppFactory.SendWithReadyRetryAsync(
                    client,
                    () => new HttpRequestMessage(HttpMethod.Get, BaseUrl(port) + "/basic/index?block=1")).ConfigureAwait(false))
                {
                    await blocked.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual(HttpStatusCode.Unauthorized, blocked.StatusCode, "首次请求应被钩子拦截");
                }

                using (var passed = await TestAppFactory.SendWithReadyRetryAsync(
                    client,
                    () => new HttpRequestMessage(HttpMethod.Get, BaseUrl(port) + "/basic/index")).ConfigureAwait(false))
                {
                    var body = await passed.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual(HttpStatusCode.OK, passed.StatusCode, "复用上下文的第二个请求应放行");
                    Assert.AreEqual("basic-index-ok", body, "第二个请求不应带出上一次的拦截结果，实际正文：" + body);
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 通过配置指定上下文类型时，该类型应被真正实例化并处理请求。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task ContextType_FromConfig_InstallsCustomContext()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();

            TestAppFactory.GetRootDirectory(rootName);

            var config = new SAEAMvcApplicationConfig
            {
                Root = rootName,
                Port = port,
                ContextType = typeof(HookContext)
            };

            var app = new SAEAMvcApplication(config);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                {
                    var body = await TestAppFactory.ReadContentAsync(
                        client,
                        () => new HttpRequestMessage(HttpMethod.Get, BaseUrl(port) + "/basic/index?block=1")).ConfigureAwait(false);

                    Assert.AreEqual("hook-blocked", body, "配置的上下文类型应被实例化并接管请求，实际正文：" + body);
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 钩子放行前写入的报头（令牌轮换的 Set-Cookie 形态）应保留到最终响应上。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task OnRequestExecuting_PassesAndKeepsHeaders()
        {
            await WithHookHostAsync(async (port, client) =>
            {
                using (var response = await TestAppFactory.SendWithReadyRetryAsync(
                    client,
                    () => new HttpRequestMessage(HttpMethod.Get, BaseUrl(port) + "/basic/index?hdr=1")).ConfigureAwait(false))
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "放行后应正常返回动作结果");
                    Assert.AreEqual("basic-index-ok", body, "放行后应执行动作本身，实际正文：" + body);
                    Assert.IsTrue(response.Headers.Contains("X-Rotation"), "钩子在放行前写入的报头应保留");
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 钩子里能读到 Query、Cookie、Authorization 与相对地址：门禁的所有判据都不依赖参数合并。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task OnRequestExecuting_SeesCookiesHeadersAndUrl()
        {
            await WithHookHostAsync(async (port, client) =>
            {
                using (var response = await TestAppFactory.SendWithReadyRetryAsync(
                    client,
                    () =>
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl(port) + "/basic/index?probe=1");
                        request.Headers.Add("Authorization", "Bearer probe-token");
                        request.Headers.Add("Cookie", "hook_sid=abc123");
                        return request;
                    }).ConfigureAwait(false))
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "探针请求应被钩子接管");
                    StringAssert.Contains(body, "cookie=abc123", "钩子应能读到请求 Cookie，实际正文：" + body);
                    StringAssert.Contains(body, "auth=Bearer probe-token", "钩子应能读到 Authorization，实际正文：" + body);
                    StringAssert.Contains(body, "url=/basic/index?probe=1", "钩子应能读到带查询串的相对地址，实际正文：" + body);
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 同一地址先被拦、再凭请求头放行，第二次必须真正执行动作，说明拦截结果没有被缓存复用。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task OnRequestExecuting_SameUrlAfterBlock_IsNotCached()
        {
            await WithHookHostAsync(async (port, client) =>
            {
                var url = BaseUrl(port) + "/basic/index?gate=1";

                using (var blocked = await TestAppFactory.SendWithReadyRetryAsync(
                    client,
                    () => new HttpRequestMessage(HttpMethod.Get, url)).ConfigureAwait(false))
                {
                    var body = await blocked.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual(HttpStatusCode.Unauthorized, blocked.StatusCode, "无凭证时该地址应被拦");
                    Assert.AreEqual("gate-blocked", body, "被拦时应返回钩子正文，实际正文：" + body);
                }

                using (var allowed = await TestAppFactory.SendWithReadyRetryAsync(
                    client,
                    () =>
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, url);
                        request.Headers.Add("X-Allow", "1");
                        return request;
                    }).ConfigureAwait(false))
                {
                    var body = await allowed.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual(HttpStatusCode.OK, allowed.StatusCode, "带凭证后同一地址应放行");
                    Assert.AreEqual("basic-index-ok", body, "放行后应执行动作本身，实际正文：" + body);
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// OPTIONS 预检也要先过钩子，被拦时不能进入自动应答分支。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task OnRequestExecuting_CoversOptions()
        {
            await WithHookHostAsync(async (port, client) =>
            {
                using (var blocked = await TestAppFactory.SendWithReadyRetryAsync(
                    client,
                    () => new HttpRequestMessage(HttpMethod.Options, BaseUrl(port) + "/basic/index?block=1")).ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.Unauthorized, blocked.StatusCode, "OPTIONS 预检也要经过钩子");
                }

                using (var passed = await TestAppFactory.SendWithReadyRetryAsync(
                    client,
                    () => new HttpRequestMessage(HttpMethod.Options, BaseUrl(port) + "/basic/index")).ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.NoContent, passed.StatusCode, "钩子放行后 OPTIONS 仍走原有自动应答");
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 传入不派生自 MVC 上下文的类型时应在构造期报错，避免请求期变成「找不到任何内容」。
        /// </summary>
        [TestMethod]
        public void ContextType_NotDerivedFromMvcContext_Throws()
        {
            var rootName = TestAppFactory.CreateRootName();

            TestAppFactory.GetRootDirectory(rootName);

            try
            {
                var thrown = Assert.ThrowsExactly<ArgumentException>(
                    () => new SAEAMvcApplication(typeof(string), rootName, TestAppFactory.GetFreePort()));

                Assert.AreEqual("contextType", thrown.ParamName, "异常应指出来源参数");
                Assert.IsTrue(
                    thrown.Message.Contains("SAEA.MVC.Hosting.HttpContext"),
                    "异常消息应写明要求的基类型，实际消息：" + thrown.Message);
            }
            finally
            {
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 不指定上下文类型时，配置构造出的应用仍使用默认上下文并正常路由。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task ContextType_Null_UsesDefaultContext()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();

            TestAppFactory.GetRootDirectory(rootName);

            var config = new SAEAMvcApplicationConfig
            {
                Root = rootName,
                Port = port
            };

            var app = new SAEAMvcApplication(config);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                {
                    var body = await TestAppFactory.ReadContentAsync(
                        client,
                        () => new HttpRequestMessage(HttpMethod.Get, BaseUrl(port) + "/basic/index")).ConfigureAwait(false);

                    Assert.AreEqual("basic-index-ok", body, "未指定上下文类型时应使用默认上下文，实际正文：" + body);
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 只覆写异步版本时，请求管道应调用异步钩子接管请求。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task OnRequestExecutingAsync_OnlyAsyncOverride_IsInvoked()
        {
            await WithHookHostAsync(async (port, client) =>
            {
                using (var blocked = await TestAppFactory.SendWithReadyRetryAsync(
                    client,
                    () => new HttpRequestMessage(HttpMethod.Get, BaseUrl(port) + "/basic/index?asyncblock=1")).ConfigureAwait(false))
                {
                    var body = await blocked.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual(HttpStatusCode.Unauthorized, blocked.StatusCode, "只覆写异步版本时也应短路路由");
                    Assert.AreEqual("async-blocked", body, "异步钩子的结果应写出，实际正文：" + body);
                }

                var passed = await TestAppFactory.ReadContentAsync(
                    client,
                    () => new HttpRequestMessage(HttpMethod.Get, BaseUrl(port) + "/basic/index")).ConfigureAwait(false);

                Assert.AreEqual("basic-index-ok", passed, "异步钩子返回 null 时应继续路由，实际正文：" + passed);
            }, contextType: typeof(AsyncHookContext)).ConfigureAwait(false);
        }

        /// <summary>
        /// 同时覆写同步与异步版本时，以异步版本为准。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task OnRequestExecutingAsync_BothOverridden_AsyncWins()
        {
            await WithHookHostAsync(async (port, client) =>
            {
                var body = await TestAppFactory.ReadContentAsync(
                    client,
                    () => new HttpRequestMessage(HttpMethod.Get, BaseUrl(port) + "/basic/index?both=1")).ConfigureAwait(false);

                Assert.AreEqual("async-wins", body, "同时覆写时管道应以异步版本为准，实际正文：" + body);
            }, contextType: typeof(BothHookContext)).ConfigureAwait(false);
        }

        /// <summary>
        /// 以指定上下文类型起一个宿主并在结束时收尾。
        /// </summary>
        /// <param name="body">用例主体，入参为监听端口与请求客户端</param>
        /// <param name="prepareRoot">启动前对站点根目录的准备动作，可为 null</param>
        /// <param name="allowAutoRedirect">客户端是否自动跟随 302</param>
        /// <param name="contextType">请求上下文类型，为 null 时用 <see cref="HookContext"/></param>
        /// <returns>用例执行完成后的任务</returns>
        private static async Task WithHookHostAsync(
            Func<int, HttpClient, Task> body,
            Action<string> prepareRoot = null,
            bool allowAutoRedirect = true,
            Type contextType = null)
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();

            var root = TestAppFactory.GetRootDirectory(rootName);

            if (prepareRoot != null)
                prepareRoot(root);

            var app = new SAEAMvcApplication(contextType ?? typeof(HookContext), rootName, port);

            try
            {
                app.Start();

                var handler = new HttpClientHandler
                {
                    AllowAutoRedirect = allowAutoRedirect
                };

                using (handler)
                using (var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) })
                {
                    await body(port, client).ConfigureAwait(false);
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

        /// <summary>
        /// 带路由前钩子的派生上下文：命中约定参数时接管请求，其余请求交回默认路由。
        /// </summary>
        public class HookContext : SAEA.MVC.Hosting.HttpContext
        {
            /// <summary>
            /// 以宿主服务创建上下文。
            /// </summary>
            /// <param name="webHost">Web 宿主</param>
            public HookContext(IWebHost webHost) : base(webHost)
            {
            }

            /// <summary>
            /// 路由前判定：按约定参数分别验证拦截、重定向与异常三条路径，未命中时交回默认路由。
            /// </summary>
            /// <returns>接管结果；未命中时返回 null</returns>
            public override IHttpResult OnRequestExecuting()
            {
                if (Request.Query.TryGetValue("block", out var value) && value == "1")
                {
                    Response.Headers["X-Hook"] = "hit";

                    return new ContentResult("hook-blocked", HttpStatusCode.Unauthorized);
                }

                // 入口令牌换 Cookie 的形态：一次性下发 Cookie 并把地址栏里的参数摘掉
                if (Request.Query.TryGetValue("redirect", out var redirect) && redirect == "1")
                {
                    Response.Headers["Set-Cookie"] = "hook_sid=abc; Path=/; HttpOnly";
                    Response.Headers["Location"] = "/basic/index";

                    return new ContentResult(string.Empty, HttpStatusCode.Found);
                }

                if (Request.Query.TryGetValue("hookthrow", out var hookThrow) && hookThrow == "1")
                {
                    throw new InvalidOperationException("hook-boom");
                }

                // 同址开关：同一个 URL 先按缺凭证拦掉，再凭请求头放行，用来验证拦截结果不会被缓存复用
                if (Request.Query.TryGetValue("gate", out var gate) && gate == "1")
                {
                    if (!(Request.Headers.TryGetValue("X-Allow", out var allow) && allow == "1"))
                    {
                        return new ContentResult("gate-blocked", HttpStatusCode.Unauthorized);
                    }
                }

                // 放行但要在响应上留痕：对应令牌轮换时先写 Set-Cookie 再继续路由
                if (Request.Query.TryGetValue("hdr", out var hdr) && hdr == "1")
                {
                    Response.Headers["X-Rotation"] = "rotated";
                }

                // 探针：把门禁真正依赖的四类判据读出来，证明它们在路由前已经可用
                if (Request.Query.TryGetValue("probe", out var probe) && probe == "1")
                {
                    var cookie = Request.Cookies.TryGetValue("hook_sid", out var hookCookie) ? hookCookie.Value : "none";
                    var auth = Request.Headers.TryGetValue("Authorization", out var header) ? header : "none";

                    return new ContentResult(
                        "cookie=" + cookie + ";auth=" + auth + ";url=" + Request.RelativeUrl,
                        HttpStatusCode.OK);
                }

                return base.OnRequestExecuting();
            }
        }

        /// <summary>
        /// 只覆写异步钩子的派生上下文：命中 asyncblock=1 时接管，其余交回默认路由。
        /// </summary>
        public class AsyncHookContext : SAEA.MVC.Hosting.HttpContext
        {
            /// <summary>
            /// 以宿主服务创建上下文。
            /// </summary>
            /// <param name="webHost">Web 宿主</param>
            public AsyncHookContext(IWebHost webHost) : base(webHost)
            {
            }

            /// <summary>
            /// 路由前异步判定：命中 asyncblock=1 时返回接管结果，未命中转调基类（基类再转调同步版本）。
            /// </summary>
            /// <returns>接管结果；未命中时为 null</returns>
            public override Task<IHttpResult> OnRequestExecutingAsync()
            {
                if (Request.Query.TryGetValue("asyncblock", out var value) && value == "1")
                {
                    return Task.FromResult<IHttpResult>(
                        new ContentResult("async-blocked", HttpStatusCode.Unauthorized));
                }

                return base.OnRequestExecutingAsync();
            }
        }

        /// <summary>
        /// 同时覆写同步与异步钩子的派生上下文：用来确定管道到底认哪一个。
        /// </summary>
        public class BothHookContext : SAEA.MVC.Hosting.HttpContext
        {
            /// <summary>
            /// 以宿主服务创建上下文。
            /// </summary>
            /// <param name="webHost">Web 宿主</param>
            public BothHookContext(IWebHost webHost) : base(webHost)
            {
            }

            /// <summary>
            /// 同步版本：若被调用会产生 sync-wins。
            /// </summary>
            /// <returns>固定结果</returns>
            public override IHttpResult OnRequestExecuting()
            {
                return new ContentResult("sync-wins", HttpStatusCode.OK);
            }

            /// <summary>
            /// 异步版本：管道应优先采用这一份。
            /// </summary>
            /// <returns>固定结果</returns>
            public override Task<IHttpResult> OnRequestExecutingAsync()
            {
                return Task.FromResult<IHttpResult>(new ContentResult("async-wins", HttpStatusCode.OK));
            }
        }
    }
}