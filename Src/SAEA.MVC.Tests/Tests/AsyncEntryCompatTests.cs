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
 *文件名： AsyncEntryCompatTests
 *版本号： net10.0
 *唯一标识：0e0695ed-3b36-4753-ba0f-fd219615e03a
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/18 17:35:00
 *描述：自定义上下文只覆盖同步或异步入口时的派发入口选择用例
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/18 17:35:00
 *修改人： yswenli
 *版本号： net10.0
 *描述：新增，锁定只重写同步 HttpHandle 的派生类不被异步入口绕过，以及异步覆盖仍正常生效
 *
 *****************************************************************************/
using System;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.Http;
using SAEA.Http.Base;
using SAEA.Http.Model;
using SAEA.MVC.Tests.Tests.Support;
using SAEA.Sockets.Pipeline;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// 自定义上下文分别只覆盖同步或异步入口时，请求应由对应入口处理。
    /// </summary>
    [TestClass]
    public class AsyncEntryCompatTests
    {
        /// <summary>
        /// 派生类只重写同步 <see cref="SAEA.MVC.HttpContext.HttpHandle"/> 时，请求应走同步入口。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpDispatchHandler_SyncOnlyOverride_InvokesSyncEntry()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();

            TestAppFactory.GetRootDirectory(rootName);

            var host = new WebHost(typeof(SyncOnlyContext), rootName, port, isZiped: false);

            try
            {
                host.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                {
                    var body = await TestAppFactory.ReadContentAsync(
                        client,
                        () => new HttpRequestMessage(HttpMethod.Get, BaseUrl(port) + "/compat/sync")).ConfigureAwait(false);

                    Assert.AreEqual("sync-override", body, "只重写同步入口的派生类应处理本次请求，实际正文：" + body);
                }
            }
            finally
            {
                StopHostQuietly(host);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 派生类只重写异步 <see cref="SAEA.MVC.HttpContext.HttpHandleAsync"/> 时，请求应走异步入口。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpDispatchHandler_AsyncOnlyOverride_InvokesAsyncEntry()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();

            TestAppFactory.GetRootDirectory(rootName);

            var host = new WebHost(typeof(AsyncOnlyContext), rootName, port, isZiped: false);

            try
            {
                host.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                {
                    var body = await TestAppFactory.ReadContentAsync(
                        client,
                        () => new HttpRequestMessage(HttpMethod.Get, BaseUrl(port) + "/compat/async")).ConfigureAwait(false);

                    Assert.AreEqual("async-override", body, "只重写异步入口的派生类应处理本次请求，实际正文：" + body);
                }
            }
            finally
            {
                StopHostQuietly(host);
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
        /// 停止宿主并吞掉异常：收尾阶段服务可能已停止，失败不应覆盖用例结论。
        /// </summary>
        /// <param name="host">待停止的宿主实例，可为 null</param>
        private static void StopHostQuietly(WebHost host)
        {
            if (host == null)
                return;

            try
            {
                host.Stop();
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 只重写同步入口的派生上下文，直接在同步入口写出固定正文，不调用基类实现。
        /// </summary>
        public class SyncOnlyContext : SAEA.MVC.HttpContext
        {
            /// <summary>
            /// 以宿主服务创建上下文。
            /// </summary>
            /// <param name="webHost">Web 宿主</param>
            public SyncOnlyContext(IWebHost webHost) : base(webHost)
            {
            }

            /// <summary>
            /// 同步处理并直接结束响应，用于验证派发不会绕过该覆盖。
            /// </summary>
            /// <param name="channel">连接通道</param>
            /// <param name="httpMessage">Http消息</param>
            public override void HttpHandle(IChannel channel, HttpMessage httpMessage)
            {
                InitContext(channel, httpMessage);

                Response.SetCached(new ContentResult("sync-override"), "-1,-1");

                Response.End();
            }
        }

        /// <summary>
        /// 只重写异步入口的派生上下文，用于验证异步覆盖仍被派发到。
        /// </summary>
        public class AsyncOnlyContext : SAEA.MVC.HttpContext
        {
            /// <summary>
            /// 以宿主服务创建上下文。
            /// </summary>
            /// <param name="webHost">Web 宿主</param>
            public AsyncOnlyContext(IWebHost webHost) : base(webHost)
            {
            }

            /// <summary>
            /// 异步处理并直接结束响应。
            /// </summary>
            /// <param name="channel">连接通道</param>
            /// <param name="httpMessage">Http消息</param>
            /// <returns>响应写出完成后的任务</returns>
            public override Task HttpHandleAsync(IChannel channel, HttpMessage httpMessage)
            {
                InitContext(channel, httpMessage);

                Response.SetCached(new ContentResult("async-override"), "-1,-1");

                return Response.EndAsync();
            }
        }
    }
}