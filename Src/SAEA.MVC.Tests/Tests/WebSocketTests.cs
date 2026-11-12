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
*文件名： WebSocketTests
*版本号： net10.0
*唯一标识：1616d1c4-5d0f-42eb-9754-36bf7ee2a916
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 18:07:54
*描述：明文 WebSocket 升级与非法升级用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 18:07:54
*修改人： yswenli
*版本号： net10.0
*描述：新增，验证 WS 回显与普通 GET 被拒绝
*
*****************************************************************************/
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.Tests.Tests.Support;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// 明文 WebSocket 的端到端用例：升级后消息回显，以及非升级请求被拒绝。
    /// </summary>
    [TestClass]
    public class WebSocketTests
    {
        /// <summary>
        /// WS 升级后客户端发送文本，服务端把同一文本回写。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Ws_ClientEcho_Works()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                {
                    var socket = await WebSocketProbe.ConnectAsync(BaseUrl(port) + "/echo/open", false, cancellation.Token)
                        .ConfigureAwait(false);

                    try
                    {
                        var reply = await WebSocketProbe.SendAndReceiveTextAsync(socket, "ws-hello", cancellation.Token)
                            .ConfigureAwait(false);

                        Assert.AreEqual("ws-hello", reply, "WS 通道上的文本消息应原样回显");
                    }
                    finally
                    {
                        await WebSocketProbe.CloseAsync(socket, cancellation.Token).ConfigureAwait(false);

                        socket.Dispose();
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
        /// 不带升级头的普通 GET 打到 WebSocket 入口时返回 400。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Ws_NonUpgradeGet_Returns400()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/echo/open").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode, "缺少升级头的普通 GET 应返回 400");
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
            return "ws://" + TestAppFactory.LoopbackHost + ":" + port;
        }
    }
}