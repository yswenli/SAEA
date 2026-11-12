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
*文件名： LifecycleTests
*版本号： net10.0
*唯一标识：d99633cd-5dc3-4227-b9e9-c11970f45818
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 18:07:54
*描述：服务停止后重启与端口释放用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 18:07:54
*修改人： yswenli
*版本号： net10.0
*描述：新增，验证 Stop 后同一实例可在原端口重新启动
*
*****************************************************************************/
using System.Net;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.Tests.Tests.Support;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// 服务生命周期用例：停止后端口释放，同一实例可按原端口重新监听并继续处理请求。
    /// </summary>
    [TestClass]
    public class LifecycleTests
    {
        /// <summary>
        /// 停止后端口被释放，同一实例再次启动仍能在原端口正常响应。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Stop_ThenStart_ReleasesPort()
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
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "首次启动后 GET /basic/index 应返回 200");

                    await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }

                app.Stop();

                // 再次启动要重新 bind 同一端口：能启动就说明上一次监听已经释放。
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/basic/index").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "重启后同一端口应继续响应请求");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("basic-index-ok", body, "重启后的响应正文应与首次启动一致");
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