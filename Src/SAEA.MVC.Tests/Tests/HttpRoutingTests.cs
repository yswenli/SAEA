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
*文件名： HttpRoutingTests
*版本号： net10.0
*唯一标识：e72e4ecd-3d2e-4229-a8d1-bbefe89eec8d
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 18:05:27
*描述：HTTP 路由、参数绑定、过滤器与静态文件端到端用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 19:42:05
*修改人： yswenli
*版本号： net10.0
*描述：补 POST 分支的未知 action 404 用例，覆盖重复请求命中的缓存路径
*
*****************************************************************************/
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.Tests.Attributes;
using SAEA.MVC.Tests.Tests.Support;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// MVC 应用在本机回环上的 HTTP 端到端用例：路由、表单绑定、JSON、过滤器顺序与静态文件。
    /// </summary>
    [TestClass]
    public class HttpRoutingTests
    {
        /// <summary>
        /// GET 路由到 Index 动作，返回 200 与动作的固定文本。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Controller_HttpGet_Returns200AndBody()
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
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "GET /basic/index 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("basic-index-ok", body, "响应正文应为 Index 动作的返回值");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// urlencoded 表单 POST 的参数被绑定到动作形参并回显。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Controller_HttpPostForm_BindsParameter()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.PostFormAsync(client, BaseUrl(port) + "/basic/echo", "name=saea-post").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "POST /basic/echo 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("basic-echo:saea-post", body, "表单 name 字段应绑定到动作形参");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 控制器存在但动作不存在时返回 404。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Controller_UnknownAction_Returns404()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/basic/notexistsaction").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, "未知动作应返回 404");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 控制器存在但 POST 动作不存在时返回 404，重复请求同样返回 404。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Controller_UnknownPostAction_Returns404()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                {
                    // 连续请求两次：第一次解析未命中，第二次走路由缓存，二者都必须是 404
                    for (var attempt = 1; attempt <= 2; attempt++)
                    {
                        using (var response = await TestAppFactory.PostFormAsync(client, BaseUrl(port) + "/basic/notexistsaction", "name=saea-post").ConfigureAwait(false))
                        {
                            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, "第 " + attempt + " 次 POST 未知动作应返回 404");
                        }
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
        /// 返回 JsonResult 的动作带 application/json 内容类型，正文可解析。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Controller_JsonAction_ReturnsApplicationJson()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/basic/jsondata").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "GET /basic/jsondata 应返回 200");

                    var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;

                    StringAssert.Contains(contentType, "application/json", "JSON 动作的 Content-Type 应包含 application/json");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    using (var document = JsonDocument.Parse(body))
                    {
                        Assert.AreEqual("saea", document.RootElement.GetProperty("name").GetString(), "JSON 正文应包含 name 字段");
                        Assert.AreEqual(42, document.RootElement.GetProperty("value").GetInt32(), "JSON 正文应包含 value 字段");
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
        /// 动作过滤器先于 action 执行：两者写入同一记录表，顺序必须是 filter → action。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task ActionFilter_ExecutesBeforeAction()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            OrderRecordingFilterAttribute.Records.Clear();

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/basic/filtered").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "GET /basic/filtered 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("filtered-ok", body, "响应正文应为 Filtered 动作的返回值");

                    CollectionAssert.AreEqual(
                        new[] { "filter", "action" },
                        OrderRecordingFilterAttribute.Records,
                        "过滤器必须在 action 之前执行");
                }
            }
            finally
            {
                OrderRecordingFilterAttribute.Records.Clear();
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 站点根目录下真实存在的静态文件按原内容返回。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task StaticFile_Exists_ReturnsContent()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var rootDirectory = TestAppFactory.GetRootDirectory(rootName);

            var fileName = "probe-" + Guid.NewGuid().ToString("N") + ".txt";
            var content = "static-probe-" + Guid.NewGuid().ToString("N");

            File.WriteAllText(Path.Combine(rootDirectory, fileName), content, Encoding.UTF8);

            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/" + fileName).ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "已存在的静态文件应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual(content, body, "静态文件正文应与磁盘内容一致");
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