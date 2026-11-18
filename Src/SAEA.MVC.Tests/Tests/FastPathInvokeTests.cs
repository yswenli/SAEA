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
*文件名： FastPathInvokeTests
*版本号： net10.0
*唯一标识：a6178ff9-b8ed-47ad-8efc-ad4d0a2525a5
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/18 13:47:00
*描述：未配置 ActionTimeout 时直调快路径的端到端用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/18 13:47:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，锁住直调快路径的 200、500 文案与异步结果取值
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
    /// 未配置 ActionTimeout 时 action 直调快路径的用例：不走任务包装也不能改变状态码与异常文案。
    /// </summary>
    [TestClass]
    public class FastPathInvokeTests
    {
        /// <summary>
        /// 未配置 ActionTimeout 时同步 action 直接执行，返回 200 与固定正文。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task MethodInvoke_SyncActionWithoutActionTimeout_Returns200()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/fastpath/syncok").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "未配置动作超时的同步 action 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("fastpath-sync-ok", body, "同步 action 的返回文本应原样写出，实际正文：" + body);
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 未配置 ActionTimeout 时同步 action 抛异常仍返回 500，文案保持旧路径 AggregateException 的形态。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task MethodInvoke_SyncActionThrows_Returns500WithLegacyMessage()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/fastpath/syncthrows").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode, "action 抛异常应返回 500");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    // 旧路径由 Task.Run + Wait 抛 AggregateException，正文是「请求发生异常：」加上它的 Message；
                    // 反射调用本身抛的是 TargetInvocationException，所以文案里看到的是它的固定描述
                    Assert.AreEqual("请求发生异常：One or more errors occurred. (Exception has been thrown by the target of an invocation.)", body, "500 文案应与未优化前的 AggregateException 形态逐字一致，实际正文：" + body);
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 未配置 ActionTimeout 时异步 action 也要等到任务完成并取出 Task&lt;string&gt; 的结果。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task MethodInvoke_AsyncActionWithoutActionTimeout_ReturnsTaskResult()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/fastpath/asyncok").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "未配置动作超时的异步 action 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("fastpath-async-ok", body, "异步 action 应取到 Task<string> 的结果，实际正文：" + body);
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