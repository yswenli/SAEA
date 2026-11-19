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
*文件名： TlsTests
*版本号： net10.0
*唯一标识：5629858e-7656-413f-ad0e-005ffc68f370
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 18:07:54
*描述：HTTPS 与 WSS 端到端用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 18:07:54
*修改人： yswenli
*版本号： net10.0
*描述：新增，验证同一端口承载 HTTPS 与 WSS
*
*****************************************************************************/
using SAEA.MVC.Hosting;
using System;
using System.Net;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.Tests.Tests.Support;
using SAEA.Sockets.Ssl;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// TLS 入口的端到端用例：同一端口承载 HTTPS 请求与 WSS 升级，证书与协议在启动前配置。
    /// </summary>
    [TestClass]
    public class TlsTests
    {
        /// <summary>
        /// HTTPS GET 返回 200 与动作正文。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Https_Get_Returns200AndBody()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var bundle = TestAppFactory.CreateSelfSignedCertificate();
            var app = CreateHttpsApp(rootName, port, bundle);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient(true))
                using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/basic/index").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "HTTPS GET /basic/index 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("basic-index-ok", body, "HTTPS 响应正文应为 Index 动作的返回值");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                bundle.Dispose();
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// HTTPS 表单 POST 的参数被绑定到动作形参并回显。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Https_PostForm_BindsParameter()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var bundle = TestAppFactory.CreateSelfSignedCertificate();
            var app = CreateHttpsApp(rootName, port, bundle);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient(true))
                using (var response = await TestAppFactory.PostFormAsync(client, BaseUrl(port) + "/basic/echo", "name=saea-tls").ConfigureAwait(false))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "HTTPS POST /basic/echo 应返回 200");

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Assert.AreEqual("basic-echo:saea-tls", body, "HTTPS 通道上的表单字段应绑定到动作形参");
                }
            }
            finally
            {
                TestAppFactory.StopQuietly(app);
                bundle.Dispose();
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// WSS 升级后客户端发送文本，服务端把同一文本回写。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task Wss_ClientEcho_Works()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var bundle = TestAppFactory.CreateSelfSignedCertificate();
            var app = CreateHttpsApp(rootName, port, bundle);

            try
            {
                app.Start();

                using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                {
                    var socket = await WebSocketProbe.ConnectAsync(BaseUrl(port, "wss") + "/echo/open", true, cancellation.Token)
                        .ConfigureAwait(false);

                    try
                    {
                        var reply = await WebSocketProbe.SendAndReceiveTextAsync(socket, "wss-hello", cancellation.Token)
                            .ConfigureAwait(false);

                        Assert.AreEqual("wss-hello", reply, "WSS 通道上的文本消息应原样回显");
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
                bundle.Dispose();
                TestAppFactory.DeleteRootDirectory(rootName);
            }
        }

        /// <summary>
        /// 按固定 TLS 1.2 与自签证书创建应用。
        /// </summary>
        /// <param name="rootName">站点根目录名</param>
        /// <param name="port">监听端口</param>
        /// <param name="bundle">自签证书，生命周期由调用方管理</param>
        /// <returns>尚未启动的应用实例</returns>
        private static SAEAMvcApplication CreateHttpsApp(string rootName, int port, SaeaCertificateBundle bundle)
        {
            return TestAppFactory.CreateApp(rootName, port, app => app.UseSsl(options =>
            {
                options.Protocols = SslProtocols.Tls12;

                options.ServerCertificate = bundle.Certificate;
            }));
        }

        /// <summary>
        /// 拼出本用例服务的回环基地址。
        /// </summary>
        /// <param name="port">监听端口</param>
        /// <param name="scheme">http 或 https、ws 或 wss</param>
        /// <returns>不含路径的基地址</returns>
        private static string BaseUrl(int port, string scheme = "https")
        {
            return scheme + "://" + TestAppFactory.LoopbackHost + ":" + port;
        }
    }
}