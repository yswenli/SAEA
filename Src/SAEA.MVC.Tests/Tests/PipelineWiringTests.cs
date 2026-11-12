/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.MVC.Tests
*文件名： PipelineWiringTests
*版本号： net10.0
*唯一标识：f66115d5-ffb5-4d7a-bed5-fca60d98a0f2
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 18:07:54
*描述：处理器链装配与 TLS 入口的配置时机用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 18:07:54
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖启动前后可配置性与装配回调按连接生效
*
*****************************************************************************/
using System;
using System.Net;
using System.Security.Authentication;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.Tests.Tests.Support;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// 处理器链装配回调与 TLS 入口在启动前后的可配置性，以及装配回调按连接生效的行为。
    /// </summary>
    [TestClass]
    public class PipelineWiringTests
    {
        /// <summary>
        /// 服务启动后再赋值处理器链装配回调应被拒绝。
        /// </summary>
        [TestMethod]
        public void ConfigurePipeline_AfterStart_ThrowsInvalidOperationException()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                Assert.ThrowsException<InvalidOperationException>(
                    () => app.ConfigurePipeline = (id, pipeline) => { },
                    "启动后再赋值处理器链装配回调应抛 InvalidOperationException");
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 装配回调在每条连接上执行一次：一次请求对应一次链活动通知，且请求本身仍正常完成。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task ConfigurePipeline_OncePerConnection_ReceivesChannelActive()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var handler = new RecordingHandler();

            var app = TestAppFactory.CreateApp(rootName, port, a => a.ConfigurePipeline = (id, pipeline) => pipeline.AddLast(handler));

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/basic/index").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "装配回调挂载的链不应影响正常请求");

                    await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }

                Assert.AreEqual(1, handler.ChannelActiveCount, "一条连接只应收到一次链活动通知");
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// TLS 配置回调为 null 时应立即拒绝。
        /// </summary>
        [TestMethod]
        public void UseSsl_NullConfigure_ThrowsArgumentNullException()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                Assert.ThrowsException<ArgumentNullException>(
                    () => app.UseSsl(null),
                    "TLS 配置回调为 null 时应抛 ArgumentNullException");
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 服务启动后再配置 TLS 应被拒绝。
        /// </summary>
        [TestMethod]
        public void UseSsl_AfterStart_ThrowsInvalidOperationException()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                Assert.ThrowsException<InvalidOperationException>(
                    () => app.UseSsl(options => options.Protocols = SslProtocols.Tls12),
                    "启动后再配置 TLS 应抛 InvalidOperationException");
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