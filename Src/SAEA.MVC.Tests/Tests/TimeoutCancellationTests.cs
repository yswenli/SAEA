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
*文件名： TimeoutCancellationTests
*版本号： net10.0
*唯一标识：4bfdd119-e3e7-4699-a503-c172ea4c221b
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/17 17:44:45
*描述：文本内容类型、404/405 文案与超时取消的端到端用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/17 18:40:00
*修改人： yswenli
*版本号： net10.0
*描述：端到端用例覆盖默认内容类型、404 动词文案、405 完整路径、令牌居中形参绑定与超时取消返回 408；超时探针改由 WebConfig.ActionTimeout 单独配置
*
*****************************************************************************/
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.Tests.Controllers;
using SAEA.MVC.Tests.Tests.Support;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// 文本内容类型、404/405 文案与超时取消的用例：文案用例走真端口，
    /// 超时用例先把宿主动作超时改成 1 秒，再断言动作能观察到取消令牌。
    /// </summary>
    [TestClass]
    public class TimeoutCancellationTests
    {
        /// <summary>
        /// 只给编码的构造函数默认内容类型应为 text/plain。
        /// </summary>
        [TestMethod]
        public void ContentResult_DefaultContentType_IsTextPlain()
        {
            var result = new SAEA.MVC.Results.ContentResult("x", Encoding.UTF8);

            Assert.AreEqual("text/plain; charset=utf-8", result.ContentType, "默认内容类型应为 text/plain; charset=utf-8");
        }

        /// <summary>
        /// 两段式路由下未知 action 的 404 文案用请求动词的短名，不能出现特性名 HttpPost。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task TwoSegment_UnknownPostAction_MessageUsesPostVerb()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.PostFormAsync(client, BaseUrl(port) + "/verbprobe/nosuchaction", "x=1").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, "未知 action 应返回 404");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    StringAssert.Contains(body, "当前请求为:POST", "404 文案应输出请求方法 POST");

                    Assert.IsFalse(body.Contains("HttpPost"), "404 文案不应出现特性名 HttpPost，实际正文：" + body);
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 两段式路由下 PUT 到未知 action 的 404 文案输出请求行的真实方法，而不是按 isPost 落成 GET。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task TwoSegment_UnknownPutAction_MessageUsesPutVerb()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.SendWithReadyRetryAsync(client, () => new HttpRequestMessage(HttpMethod.Put, BaseUrl(port) + "/timeoutcancel/no-such-action")
                {
                    Content = new StringContent(string.Empty)
                }).ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, "PUT 到未知 action 应返回 404");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    StringAssert.Contains(body, "当前请求为:PUT", "404 文案应输出请求行里的真实方法 PUT，实际正文：" + body);
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 令牌夹在两个普通形参之间时，框架按声明顺序把令牌插回原位，普通参数不会错位。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task MixedParameters_TokenInMiddle_BindsInDeclaredOrder()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/timeoutcancel/mixedparameters?name=abc&id=42").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "GET /timeoutcancel/mixedparameters 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("abc|42|True|False", body, "令牌应插回声明位置，普通参数按名称绑定且不越界，实际正文：" + body);
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 属性路由命中但动词不符时，405 文案保留被数字末段剥离前的完整路径。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task AttributeRoute_WrongVerbOnNumericPath_MessageKeepsFullPath()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.PostFormAsync(client, BaseUrl(port) + "/timeoutcancel/verb405/123", "x=1").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode, "POST /timeoutcancel/verb405/123 应返回 405");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    StringAssert.Contains(body, "/timeoutcancel/verb405/123", "405 文案应保留完整路径，实际正文：" + body);
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 两段式路由命中但动词不符时，405 文案同样保留被数字末段剥离前的完整路径。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task TwoSegment_WrongVerbOnNumericPath_MessageKeepsFullPath()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.PostFormAsync(client, BaseUrl(port) + "/timeoutcancel/getonlyecho/123", "x=1").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode, "POST /timeoutcancel/getonlyecho/123 应返回 405");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    StringAssert.Contains(body, "/timeoutcancel/getonlyecho/123", "405 文案应保留完整路径，实际正文：" + body);
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 异步动作的取消令牌在超时后被取消，请求返回 408。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Timeout_AsyncActionWithCancellationToken_CancelsAndReturns408()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            TimeoutCancelController.ResetSignals();

            var signal = TimeoutCancelController.AsyncCancelled;

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                {
                    await ArmShortTimeoutAsync(client, port).ConfigureAwait(false);

                    using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/timeoutcancel/slowasync").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.RequestTimeout, response.StatusCode, "超时的异步动作应返回 408");
                    }
                }

                Assert.IsTrue(await WaitAsync(signal.Task, TimeSpan.FromSeconds(5)).ConfigureAwait(false), "动作应在 5 秒内观察到取消令牌被取消");
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 同步动作能从上下文读到被取消的请求令牌，请求返回 408。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Timeout_SyncAction_RequestAbortedIsCancelled()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            TimeoutCancelController.ResetSignals();

            var signal = TimeoutCancelController.SyncCancelled;

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                {
                    await ArmShortTimeoutAsync(client, port).ConfigureAwait(false);

                    using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/timeoutcancel/slowsync").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.RequestTimeout, response.StatusCode, "超时的同步动作应返回 408");
                    }
                }

                var cancelled = await Task.Run(() => signal.Wait(TimeSpan.FromSeconds(5))).ConfigureAwait(false);

                Assert.IsTrue(cancelled, "同步动作应在 5 秒内观察到 RequestAborted 被取消");
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 把宿主的动作超时改成 1 秒；动作超时与连接空闲回收是两项独立配置，
        /// 调小动作超时不会提前回收连接，408 能正常写出。
        /// </summary>
        /// <param name="client">请求客户端</param>
        /// <param name="port">监听端口</param>
        /// <returns>该步骤的异步执行任务</returns>
        private static async Task ArmShortTimeoutAsync(HttpClient client, int port)
        {
            using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/timeoutcancel/armshorttimeout").ConfigureAwait(false))
            {
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "动作超时配置入口应返回 200");
            }
        }

        /// <summary>
        /// 在指定时间内等待任务完成。
        /// </summary>
        /// <param name="task">待等待的任务</param>
        /// <param name="timeout">最长等待时间</param>
        /// <returns>任务在时限内完成时返回 true</returns>
        private static async Task<bool> WaitAsync(Task task, TimeSpan timeout)
        {
            var completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);

            return ReferenceEquals(completed, task);
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