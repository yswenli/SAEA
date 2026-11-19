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
*文件名： RequestDelegateTests
*版本号： net10.0
*唯一标识：75ff8f69-ed12-4243-b4b6-959bf9acdcd2
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 13:36:00
*描述：RequestDelegate 路径的端到端用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 13:36:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖 delegate 自行写出响应后按短连接收尾的路径
*
*****************************************************************************/
using System;
using System.Net;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.Tests.Tests.Support;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// RequestDelegate 路径的用例：delegate 只写不显式关闭，由 MVC 收尾分支写出并关闭连接。
    /// </summary>
    [TestClass]
    public class RequestDelegateTests
    {
        /// <summary>
        /// delegate 只写正文不显式关闭连接，由 MVC 的收尾分支写出并关闭连接后返回 200 与正文。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task HttpHandleAsync_RequestDelegate_WritesDelegateResponse()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();

            // RequestDelegate 只在 Start 时挂到 WebConfig，必须在 Start 之前订阅
            var app = TestAppFactory.CreateApp(rootName, port, configured =>
            {
                configured.OnRequestDelegate += context =>
                {
                    context.Response.Status = HttpStatusCode.OK;
                    context.Response.ContentType = "text/plain; charset=utf-8";
                    context.Response.Write("delegate-ok");

                    // 故意不调 End：报文与关闭都由 HttpHandleAsync 的收尾分支完成，
                    // 该分支缺失时客户端拿不到任何响应
                };
            });

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                {
                    // 10 秒是为了让回退修复后的用例快速失败，而不是长时间挂起
                    client.Timeout = TimeSpan.FromSeconds(10);

                    using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/requestdelegate/probe").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "delegate 只写正文时应收尾为 200");

                        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        Assert.AreEqual("delegate-ok", body, "正文应取自 delegate 的写出内容，实际正文：" + body);
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