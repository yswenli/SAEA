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
*文件名： InheritedActionRoutingTests
*版本号： net10.0
*唯一标识：e05ccb61-5b3d-47cf-85ea-120d155266bf
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 15:13:26
*描述：基类 action 在 GET 与 POST 下可见性不同的端到端用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 15:13:26
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖 GET 只取声明层而返回 404、POST 含继承成员而解析成功
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
    /// 基类 action 可见性用例：GET 只看声明层，POST 含继承成员。
    /// </summary>
    [TestClass]
    public class InheritedActionRoutingTests
    {
        /// <summary>
        /// 基类声明的 action 在派生类的 GET 下不可达（404），在 POST 下可解析。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task RouteTable_InheritedAction_GetReturns404_PostResolves()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                {
                    using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/inheritedactionbaseprobe/inherited").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "GET /inheritedactionbaseprobe/inherited 应返回 200");

                        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        Assert.AreEqual("inherited-base", body, "响应正文应为基类 Inherited 动作的返回值");
                    }

                    using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/inheritedactionprobe/own").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "GET /inheritedactionprobe/own 应返回 200");

                        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        Assert.AreEqual("inherited-own", body, "响应正文应为派生类 Own 动作的返回值");
                    }

                    // GET 先命中并写入未找到的负缓存，后续 POST 使用独立键，仍应解析到基类 action
                    using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/inheritedactionprobe/inherited").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, "GET /inheritedactionprobe/inherited 应返回 404");
                    }

                    using (var response = await TestAppFactory.PostFormAsync(client, BaseUrl(port) + "/inheritedactionprobe/inherited", "x=1").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "POST /inheritedactionprobe/inherited 应返回 200");

                        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        Assert.AreEqual("inherited-base", body, "POST 应解析到基类声明的 Inherited 动作");
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
