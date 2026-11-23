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
*文件名： RouteTokenTests
*版本号： net10.0
*唯一标识：951c5d4b-1d7a-4f86-8c09-75423e9f832f
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/18 10:45:00
*描述：[controller] 与 [action] 令牌的端到端与构建期校验用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 15:13:26
*修改人： yswenli
*版本号： net10.0
*描述：InternalsVisibleTo 打开后改为直调 AttributeRouteTable.Build，去掉类型全名字符串与反射拆包
*
*****************************************************************************/
using SAEA.MVC.Attributes;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.Routing;
using SAEA.MVC.Tests.Tests.Support;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// [controller] 与 [action] 令牌用例：前四条在真端口上验证替换结果，
    /// 后五条直调 internal Build 验证构建期拒绝与冲突。
    /// 负向类型定义为本类内部的私有类型，不继承 Controller，因此不会被 RegistAll 扫进控制器集合。
    /// </summary>
    [TestClass]
    public class RouteTokenTests
    {
        /// <summary>
        /// 方法级模板的 [controller] 与 [action] 分别替换为类名、方法名，旧的方法名地址不再命中。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task RouteTable_TokenTemplate_MatchesControllerAndAction()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                {
                    using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/routetoken/echo/5").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "GET /routetoken/echo/5 应返回 200");

                        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        Assert.AreEqual("token-echo:5", body, "[controller] 应解析为 routetoken、[action] 应解析为 echo");
                    }

                    // 旧方法名地址不能再命中属性路由。末段用非数字，避免被请求解析当成 id 剥掉后落进两段式兜底：
                    // 数字末段会被 RequestDataReader 移到 query.id，Url 变成 /routetoken/echoasync，
                    // 两段式再按忽略大小写精确匹配到 EchoAsync，那是既有两段式行为，与本令牌改动无关
                    using (var stale = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/routetoken/echoasync/abc").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.NotFound, stale.StatusCode, "[action] 已剥掉 Async 后缀，/routetoken/echoasync/abc 不应再命中");
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
        /// 类级 [controller] 与方法级 [action] 各自从自己的声明取值并拼接。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task RouteTable_ClassAndMethodTokens_EachResolvedFromOwnDeclaration()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/token/routetokenprefix/echo/7").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "GET /token/routetokenprefix/echo/7 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("token-prefix:7", body, "类级 token/[controller] 与方法级 [action]/{id} 应拼接成完整路径");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 类级模板里的 [action] 按每个 action 分别替换，两个 action 各走自己的路径。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task RouteTable_ClassTemplateActionToken_GivesEachActionOwnPath()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                {
                    using (var list = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/tokenv/list").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, list.StatusCode, "GET /tokenv/list 应返回 200");

                        var body = await list.Content.ReadAsStringAsync().ConfigureAwait(false);

                        Assert.AreEqual("tokenv-list", body, "类级 [action] 应为 List 解析出 list");
                    }

                    using (var detail = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/tokenv/detail").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, detail.StatusCode, "GET /tokenv/detail 应返回 200");

                        var body = await detail.Content.ReadAsStringAsync().ConfigureAwait(false);

                        Assert.AreEqual("tokenv-detail", body, "类级 [action] 应为 Detail 解析出 detail，且不与 List 冲突");
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
        /// [Controller] 与 [ACTION] 与全小写令牌等价，替换结果参与忽略大小写的路径匹配。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task RouteTable_MixedCaseTokens_MatchesLowercasePath()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/tokencase/routetokencase/echo/9").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "[Controller] 与 [ACTION] 应被识别并替换，请求应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("tokencase:9", body, "大小写混写的令牌替换结果应是小写的 routetokencase 与 echo");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 用户原始写法：多段静态前缀 + [controller] + [action] + 路径参数，异步方法名剥掉 Async 后命中。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task RouteTable_FullTemplateWithStaticPrefix_MatchesStrippedAction()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                {
                    var url = BaseUrl(port) + "/api/v1/routetokenapi/get/5";

                    using (var response = await TestAppFactory.GetAsync(client, url).ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "GET /api/v1/routetokenapi/get/5 应返回 200");

                        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        Assert.AreEqual("token-api:5", body, "[action] 应剥掉 Async 后缀，多段静态前缀与令牌应正常拼接");
                    }

                    // 前面已有一次成功的 GET，连接已就绪，这里不再走就绪重试
                    using (var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, url)).ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode, "POST 同路径应因只声明 [HttpGet] 返回 405");

                        var allow = ReadAllowHeader(response);

                        Assert.IsNotNull(allow, "405 响应必须带 Allow 头");

                        StringAssert.Contains(allow, "GET", "Allow 头应声明 GET");
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
        /// 未知令牌 [foo] 在构建期抛 ArgumentException。
        /// </summary>
        [TestMethod]
        public void Build_UnknownToken_ThrowsArgumentException()
        {
            var exception = Assert.ThrowsExactly<ArgumentException>(
                () => BuildAttributeRouteTable(typeof(UnknownTokenRoute)));

            StringAssert.Contains(exception.Message, "[foo]", "异常消息应指出未知令牌 [foo]");
            StringAssert.Contains(exception.Message, "只支持 [controller] 与 [action]", "异常消息应说明只支持两个令牌");
            StringAssert.Contains(exception.Message, "独占一段", "异常消息应说明令牌必须独占一段");
        }

        /// <summary>
        /// 令牌与字面量混写（v1-[action]）在构建期抛 ArgumentException。
        /// </summary>
        [TestMethod]
        public void Build_TokenMixedWithLiteral_ThrowsArgumentException()
        {
            var exception = Assert.ThrowsExactly<ArgumentException>(
                () => BuildAttributeRouteTable(typeof(MixedTokenRoute)));

            StringAssert.Contains(exception.Message, "v1-[action]", "异常消息应指出混写段 v1-[action]");
            StringAssert.Contains(exception.Message, "独占一段", "异常消息应说明令牌必须独占一段");
        }

        /// <summary>
        /// 未闭合的 [controller 在构建期抛 ArgumentException，不能静默当成静态段。
        /// </summary>
        [TestMethod]
        public void Build_UnclosedToken_ThrowsArgumentException()
        {
            var exception = Assert.ThrowsExactly<ArgumentException>(
                () => BuildAttributeRouteTable(typeof(UnclosedTokenRoute)));

            StringAssert.Contains(exception.Message, "[controller", "异常消息应指出未闭合段 [controller");
            StringAssert.Contains(exception.Message, "独占一段", "异常消息应说明令牌必须独占一段");
        }

        /// <summary>
        /// 方法名恰好是 Async 时 [action] 剥掉后缀后为空，构建期抛 ArgumentException。
        /// </summary>
        [TestMethod]
        public void Build_ActionNameIsOnlyAsync_ThrowsArgumentException()
        {
            var exception = Assert.ThrowsExactly<ArgumentException>(
                () => BuildAttributeRouteTable(typeof(AsyncOnlyActionRoute)));

            StringAssert.Contains(exception.Message, "[action]", "异常消息应指出令牌 [action]");
            StringAssert.Contains(exception.Message, "AsyncOnlyActionRoute/Async", "异常消息应指出声明位置");
            StringAssert.Contains(exception.Message, "为空", "异常消息应说明去掉后缀后为空");
        }

        /// <summary>
        /// Foo 与 FooAsync 都用 [action] 且动词相同时落到同一路径，构建期抛冲突异常。
        /// </summary>
        [TestMethod]
        public void Build_FooAndFooAsyncWithActionToken_ThrowsConflictWithBothOwners()
        {
            var exception = Assert.ThrowsExactly<InvalidOperationException>(
                () => BuildAttributeRouteTable(typeof(TokenConflictRoute)));

            StringAssert.Contains(exception.Message, "属性路由冲突", "异常消息应说明是属性路由冲突");
            StringAssert.Contains(exception.Message, "TokenConflictRoute/Foo", "异常消息应包含第一个声明的 Controller/Action");
            StringAssert.Contains(exception.Message, "TokenConflictRoute/FooAsync", "异常消息应包含第二个声明的 Controller/Action");
        }

        /// <summary>
        /// 直调 internal 的 AttributeRouteTable.Build，把只在测试内定义的类型交给构建期校验。
        /// </summary>
        /// <param name="controllerTypes">参与构建的类型，不要求继承 Controller</param>
        private static void BuildAttributeRouteTable(params Type[] controllerTypes)
        {
            AttributeRouteTable.Build(new RouteTable(), new List<Type>(controllerTypes));
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

        /// <summary>
        /// 声明未知令牌，构建期应被拒绝。
        /// </summary>
        private class UnknownTokenRoute
        {
            /// <summary>
            /// 非法模板入口。
            /// </summary>
            /// <returns>固定文本</returns>
            [Route("token/neg/[foo]")]
            public string Index()
            {
                return "unknown";
            }
        }

        /// <summary>
        /// 声明令牌与字面量混写，构建期应被拒绝。
        /// </summary>
        private class MixedTokenRoute
        {
            /// <summary>
            /// 非法模板入口。
            /// </summary>
            /// <returns>固定文本</returns>
            [Route("token/neg/v1-[action]")]
            public string Index()
            {
                return "mixed";
            }
        }

        /// <summary>
        /// 声明未闭合的令牌，构建期应被拒绝。
        /// </summary>
        private class UnclosedTokenRoute
        {
            /// <summary>
            /// 非法模板入口。
            /// </summary>
            /// <returns>固定文本</returns>
            [Route("token/neg/[controller")]
            public string Index()
            {
                return "unclosed";
            }
        }

        /// <summary>
        /// 方法名恰好是 Async，[action] 剥掉后缀后为空。
        /// </summary>
        private class AsyncOnlyActionRoute
        {
            /// <summary>
            /// 后缀剥离后为空的入口。
            /// </summary>
            /// <returns>固定文本</returns>
            [HttpGetAttribute("token/neg/[action]")]
            public string Async()
            {
                return "async";
            }
        }

        /// <summary>
        /// Foo 与 FooAsync 都用 [action] 且动词相同，构建期应报冲突。
        /// </summary>
        private class TokenConflictRoute
        {
            /// <summary>
            /// 冲突声明之一。
            /// </summary>
            /// <returns>固定文本</returns>
            [HttpGetAttribute("token/neg/conflict/[action]")]
            public string Foo()
            {
                return "foo";
            }

            /// <summary>
            /// 冲突声明之二，[action] 剥掉 Async 后与 Foo 落到同一路径。
            /// </summary>
            /// <returns>固定文本</returns>
            [HttpGetAttribute("token/neg/conflict/[action]")]
            public string FooAsync()
            {
                return "foo-async";
            }
        }
    }
}