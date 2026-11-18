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
*文件名： AsyncActionDispatchTests
*版本号： net10.0
*唯一标识：8e4b7c1d-3a52-4f69-b0d8-6c91427e5fa3
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/18 16:30:00
*描述：异步 action 与异步响应写出路径的端到端用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/18 16:30:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖异步 action 的 200、500 文案形态、ActionTimeout 408 与异步 JSON 结果
*
*****************************************************************************/
using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.Tests.Tests.Support;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// 异步 action 与异步写出路径的用例：请求仍走真实端口，断言状态码、正文与内容类型
    /// 与同步入口保持一致，异步只改变等待方式，不改变响应本身。
    /// </summary>
    [TestClass]
    public class AsyncActionDispatchTests
    {
        /// <summary>
        /// 异步 action 返回 200，正文取自 Task&lt;string&gt; 的结果。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpHandleAsync_AsyncAction_Returns200WithBody()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/asyncaction/ok").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "异步 action 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("asyncaction-ok", body, "异步 action 的正文应取自任务结果，实际正文：" + body);
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 异步 action 抛异常返回 500，正文模板与同步抛异常逐字节一致，
        /// 差别只在被包装的内部异常文案：同步走反射包装，异步是动作自己抛出的异常。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpHandleAsync_AsyncActionThrows_Returns500InSyncMessageShape()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                {
                    string asyncBody;

                    using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/asyncaction/throws").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode, "异步 action 抛异常应返回 500");

                        asyncBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    }

                    string syncBody;

                    // 同一次运行内取同步抛异常的正文作基线：只比模板，不比内部异常描述
                    using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/fastpath/syncthrows").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode, "同步 action 抛异常应返回 500");

                        syncBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    }

                    Assert.AreEqual("请求发生异常：One or more errors occurred. (asyncaction-boom)", asyncBody, "异步入口应只包一层 AggregateException，实际正文：" + asyncBody);

                    Assert.AreEqual("请求发生异常：One or more errors occurred. (Exception has been thrown by the target of an invocation.)", syncBody, "同步入口的 500 文案应保持旧路径形态，实际正文：" + syncBody);

                    Assert.AreEqual(WrapperPrefix(syncBody), WrapperPrefix(asyncBody), "异步与同步入口的 500 文案模板必须逐字节一致");
                    Assert.AreEqual(WrapperSuffix(syncBody), WrapperSuffix(asyncBody), "异步与同步入口的 500 文案结尾必须逐字节一致");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 配置 ActionTimeout 后，超过时限的异步 action 返回 408。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpHandleAsync_SlowAsyncActionWithActionTimeout_Returns408()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                {
                    using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/asyncaction/armshorttimeout").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "动作超时配置入口应返回 200");
                    }

                    using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/asyncaction/slow").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.RequestTimeout, response.StatusCode, "超时的异步 action 应返回 408");
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
        /// 异步 action 返回 JsonResult 时，正文可解析且内容类型为 application/json。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpHandleAsync_AsyncJsonResult_ReturnsJsonBodyAndContentType()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/asyncaction/jsondata").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "异步 JSON 入口应返回 200");

                    var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;

                    StringAssert.Contains(contentType, "application/json", "异步 JSON 结果的 Content-Type 应包含 application/json");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    using (var document = JsonDocument.Parse(body))
                    {
                        Assert.AreEqual("saea", document.RootElement.GetProperty("name").GetString(), "JSON 正文的 name 应为 saea");
                        Assert.AreEqual(42, document.RootElement.GetProperty("value").GetInt32(), "JSON 正文的 value 应为 42");
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
        /// 取 500 文案里第一个左括号之前的部分，即 AggregateException 的固定前缀。
        /// </summary>
        /// <param name="body">500 响应正文</param>
        /// <returns>含左括号的前缀</returns>
        private static string WrapperPrefix(string body)
        {
            return body.Substring(0, body.IndexOf('(') + 1);
        }

        /// <summary>
        /// 取 500 文案里最后一个右括号之后的部分，即 AggregateException 的固定结尾。
        /// </summary>
        /// <param name="body">500 响应正文</param>
        /// <returns>从右括号开始的结尾</returns>
        private static string WrapperSuffix(string body)
        {
            return body.Substring(body.LastIndexOf(')'));
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