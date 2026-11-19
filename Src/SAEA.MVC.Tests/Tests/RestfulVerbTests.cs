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
*文件名： RestfulVerbTests
*版本号： net10.0
*唯一标识：d1dcc7e1-1f9d-48b9-ab83-39a58c74e0da
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 16:02:33
*描述：RESTful 动词用例，覆盖 PUT/DELETE/PATCH 可达性、HEAD 语义与 OPTIONS 自动应答
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 16:20:00
*修改人： yswenli
*版本号： net10.0
*描述：补「只声明 [Route] 的属性路由」用例；OPTIONS-only 的 405 改为断言 Allow 精确为 OPTIONS
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.Attributes;
using SAEA.MVC.Routing;
using SAEA.MVC.Tests.Tests.Support;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// RESTful 动词用例：真端口上验证新动词可达、HEAD 只回报头、OPTIONS 自动应答与 Allow 口径。
    /// </summary>
    [TestClass]
    public class RestfulVerbTests
    {
        /// <summary>
        /// 两段式只声明 PUT 的动作收到 PUT 时正常执行。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Restful_TwoSegmentPutAction_PutReturns200()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await SendAsync(client, HttpMethod.Put, BaseUrl(port) + "/restverb/putonly").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "PUT /restverb/putonly 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("rest-put-only", body, "响应正文应为 PutOnly 动作的返回值");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 两段式只声明 DELETE 的动作收到 DELETE 时正常执行。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Restful_TwoSegmentDeleteAction_DeleteReturns200()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await SendAsync(client, HttpMethod.Delete, BaseUrl(port) + "/restverb/deleteonly").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "DELETE /restverb/deleteonly 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("rest-delete-only", body, "响应正文应为 DeleteOnly 动作的返回值");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 两段式只声明 PATCH 的动作收到 PATCH 时正常执行。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Restful_TwoSegmentPatchAction_PatchReturns200()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await SendAsync(client, HttpMethod.Patch, BaseUrl(port) + "/restverb/patchonly").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "PATCH /restverb/patchonly 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("rest-patch-only", body, "响应正文应为 PatchOnly 动作的返回值");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 只声明 PUT 的两段式动作收到 POST 时返回 405，Allow 只列出 PUT。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Restful_TwoSegmentPutOnlyAction_PostReturns405WithPutAllow()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.PostFormAsync(client, BaseUrl(port) + "/restverb/putonly", "x=1").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode, "POST /restverb/putonly 应返回 405");

                    var allow = ReadAllowValues(response);

                    Assert.IsNotNull(allow, "405 响应必须带 Allow 头");
                    CollectionAssert.AreEqual(new[] { "PUT" }, allow, "只声明 PUT 时 Allow 应只有 PUT");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 属性路由只声明 PUT 的动作收到 PUT 时命中并带上路径参数。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Restful_AttributePutRoute_PutReturns200()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await SendAsync(client, HttpMethod.Put, BaseUrl(port) + "/rest/put/42").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "PUT /rest/put/42 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("rest-attr-put-42", body, "响应正文应带上路径参数");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 属性路由只声明 DELETE 的动作收到 DELETE 时命中并带上路径参数。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Restful_AttributeDeleteRoute_DeleteReturns200()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await SendAsync(client, HttpMethod.Delete, BaseUrl(port) + "/rest/delete/7").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "DELETE /rest/delete/7 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("rest-attr-delete-7", body, "响应正文应带上路径参数");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 属性路由只声明 PATCH 的动作收到 PATCH 时命中并带上路径参数。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Restful_AttributePatchRoute_PatchReturns200()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await SendAsync(client, HttpMethod.Patch, BaseUrl(port) + "/rest/patch/9").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "PATCH /rest/patch/9 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("rest-attr-patch-9", body, "响应正文应带上路径参数");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 只声明 GET 的属性路由收到 HEAD 时返回 200、正文为空，Content-Length 与 GET 正文长度一致。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Restful_HeadOnGetOnlyAttributeRoute_Returns200WithEmptyBody()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                {
                    long getLength;

                    using (var getResponse = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/rest/head-get").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, getResponse.StatusCode, "GET /rest/head-get 应返回 200");

                        var getBody = await getResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

                        getLength = getBody.Length;

                        Assert.IsTrue(getLength > 0, "GET 正文不能为空，否则无法验证 Content-Length");
                    }

                    using (var headResponse = await SendAsync(client, HttpMethod.Head, BaseUrl(port) + "/rest/head-get").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, headResponse.StatusCode, "HEAD /rest/head-get 应返回 200");

                        var headBody = await headResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

                        Assert.AreEqual(string.Empty, headBody, "HEAD 响应不应带正文");
                        Assert.AreEqual(getLength, headResponse.Content.Headers.ContentLength, "HEAD 的 Content-Length 应等于 GET 正文长度");
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
        /// 两段式只声明 GET 的动作收到 HEAD 时同样只回报头。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Restful_HeadOnGetOnlyTwoSegmentAction_Returns200WithoutBody()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await SendAsync(client, HttpMethod.Head, BaseUrl(port) + "/verbprobe/getonly").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "HEAD /verbprobe/getonly 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual(string.Empty, body, "HEAD 响应不应带正文");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 同路径同时声明 GET 与 HEAD 时，HEAD 命中显式声明的 HEAD 动作而不是 GET 别名。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Restful_HeadWithExplicitDeclaration_HeadHitsHeadAction()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await SendAsync(client, HttpMethod.Head, BaseUrl(port) + "/rest/head-explicit").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "HEAD /rest/head-explicit 应返回 200");

                    // 两个动作的返回值长度不同：18 表示命中显式 HEAD，19 表示误用了 GET 别名
                    Assert.AreEqual((long)"rest-head-explicit".Length, response.Content.Headers.ContentLength, "HEAD 应命中显式声明 HEAD 的动作");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 显式声明 OPTIONS 的属性路由收到 OPTIONS 时执行该动作。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Restful_OptionsWithExplicitDeclaration_ReturnsActionText()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await SendAsync(client, HttpMethod.Options, BaseUrl(port) + "/rest/options-explicit").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "OPTIONS /rest/options-explicit 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("rest-options-explicit", body, "显式声明 OPTIONS 时应执行该动作");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 只声明 GET 的属性路由收到 OPTIONS 时回 204，Allow 为 GET 与隐式允许的 HEAD，且不执行动作。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Restful_OptionsOnGetOnlyAttributeRoute_Returns204WithGetAndHead()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await SendAsync(client, HttpMethod.Options, BaseUrl(port) + "/rest/options-auto").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode, "OPTIONS /rest/options-auto 应返回 204");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual(string.Empty, body, "自动应答不应执行动作，正文必须为空");

                    var allow = ReadAllowValues(response);

                    Assert.IsNotNull(allow, "204 响应必须带 Allow 头");
                    CollectionAssert.AreEqual(new[] { "GET", "HEAD" }, allow, "声明 GET 时 Allow 应给出 GET 与 HEAD");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 未声明动词的 action 收到 OPTIONS 时回 204，Allow 精确为六个可声明动词且不含 OPTIONS。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Restful_OptionsOnAnyVerbAction_Returns204WithSixMethods()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await SendAsync(client, HttpMethod.Options, BaseUrl(port) + "/restverb/anyverb").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode, "OPTIONS /restverb/anyverb 应返回 204");

                    var allow = ReadAllowValues(response);

                    Assert.IsNotNull(allow, "204 响应必须带 Allow 头");

                    // OPTIONS 是框架协议级自动应答，不属于资源声明；HEAD 由 GET 隐含，因此精确为这六项
                    Assert.AreEqual("GET, HEAD, POST, PUT, DELETE, PATCH", string.Join(", ", allow), "未声明动词时 Allow 应精确为六个可声明动词");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 只声明 [Route]、不带谓词特性的属性路由收到 OPTIONS 时回 204，Allow 精确为六个可声明动词且不执行动作。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Restful_OptionsOnUnrestrictedAttributeRoute_Returns204WithSixMethods()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await SendAsync(client, HttpMethod.Options, BaseUrl(port) + "/rest/route-only").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode, "OPTIONS /rest/route-only 应返回 204");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual(string.Empty, body, "自动应答不应执行动作，正文必须为空");

                    var allow = ReadAllowValues(response);

                    Assert.IsNotNull(allow, "204 响应必须带 Allow 头");

                    // 不限谓词不等于声明 OPTIONS，因此仍然只有六个可声明动词
                    Assert.AreEqual("GET, HEAD, POST, PUT, DELETE, PATCH", string.Join(", ", allow), "不限谓词时 Allow 应精确为六个可声明动词");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 只声明 [Route] 的属性路由收到 GET 时仍然正常执行，不限谓词的条目不能被 OPTIONS 修复一并打死。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Restful_GetOnUnrestrictedAttributeRoute_Returns200()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/rest/route-only").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "GET /rest/route-only 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("rest-route-only", body, "响应正文应为 RouteOnly 动作的返回值");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 同时声明 GET 与 POST 的属性路由收到 PUT 时返回 405，Allow 精确为 GET, HEAD, POST。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Restful_PutOnGetPostRoute_Returns405WithGetHeadPost()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await SendAsync(client, HttpMethod.Put, BaseUrl(port) + "/rest/both").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode, "PUT /rest/both 应返回 405");

                    var allow = ReadAllowValues(response);

                    Assert.IsNotNull(allow, "405 响应必须带 Allow 头");
                    CollectionAssert.AreEqual(new[] { "GET", "HEAD", "POST" }, allow, "Allow 应按 GET、HEAD、POST 顺序列出");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 只声明 HEAD 的属性路由收到其它动词时返回 405，Allow 只有 HEAD，不回补 GET。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Restful_HeadOnlyDeclaration_OtherVerbReturns405WithHeadOnly()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.PostFormAsync(client, BaseUrl(port) + "/rest/head-only", "x=1").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode, "POST /rest/head-only 应返回 405");

                    var allow = ReadAllowValues(response);

                    Assert.IsNotNull(allow, "405 响应必须带 Allow 头");
                    CollectionAssert.AreEqual(new[] { "HEAD" }, allow, "只声明 HEAD 时 Allow 不应回补 GET");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 只声明 OPTIONS 的属性路由收到其它动词时返回 405，Allow 精确为 OPTIONS。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Restful_OptionsOnlyDeclaration_OtherVerbReturns405WithOptionsAllow()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/rest/options-only").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode, "GET /rest/options-only 应返回 405");

                    var allow = ReadAllowValues(response);

                    Assert.IsNotNull(allow, "显式声明 OPTIONS 时 405 响应必须带 Allow 头");
                    CollectionAssert.AreEqual(new[] { "OPTIONS" }, allow, "只显式声明 OPTIONS 时 Allow 应只有 OPTIONS");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 同一静态路径与同一新动词被两个 action 声明时，构建期抛异常且消息含两个归属。
        /// </summary>
        [TestMethod]
        public void Restful_Build_SamePathSamePut_ThrowsWithBothOwners()
        {
            var exception = Assert.ThrowsException<InvalidOperationException>(
                () => AttributeRouteTable.Build(new RouteTable(), new List<Type> { typeof(NegativePutRouteA), typeof(NegativePutRouteB) }));

            StringAssert.Contains(exception.Message, "NegativePutRouteA/Index", "异常消息应包含第一个声明的 Controller/Action");
            StringAssert.Contains(exception.Message, "NegativePutRouteB/Index", "异常消息应包含第二个声明的 Controller/Action");
            StringAssert.Contains(exception.Message, "PUT", "异常消息应指出冲突的动词");
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
        /// <returns>Allow 的各个方法名，顺序即服务端输出顺序；不存在时返回 null</returns>
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

        /// <summary>
        /// 冲突声明之一：与 NegativePutRouteB 同路径同动词。
        /// </summary>
        private class NegativePutRouteA
        {
            /// <summary>
            /// 冲突声明之一。
            /// </summary>
            /// <returns>固定文本</returns>
            [HttpPutAttribute("restverb-negput")]
            public string Index()
            {
                return "a";
            }
        }

        /// <summary>
        /// 冲突声明之二：与 NegativePutRouteA 同路径同动词。
        /// </summary>
        private class NegativePutRouteB
        {
            /// <summary>
            /// 冲突声明之二。
            /// </summary>
            /// <returns>固定文本</returns>
            [HttpPutAttribute("restverb-negput")]
            public string Index()
            {
                return "b";
            }
        }
    }
}