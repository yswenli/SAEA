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
*文件名： BigDataPipelineTests
*版本号： net10.0
*唯一标识：5de012c8-bd6e-4df4-bd48-6e0abbddccd3
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 15:13:26
*描述：大数据结果跳过普通响应收尾的端到端用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 16:54:00
*修改人： yswenli
*版本号： net10.0
*描述：补 HEAD 命中大数据入口的用例，断言正文为空、Content-Length 为完整长度且之后连接仍可用
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
    /// 大数据结果收尾用例：IBigDataResult 由自身写出响应，不再走普通响应的 SetCached 与 Send/End。
    /// </summary>
    [TestClass]
    public class BigDataPipelineTests
    {
        /// <summary>
        /// 大数据响应正文与源数据逐字节一致且没有 Cache-Control，普通响应则带 Cache-Control。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task RequestPipeline_BigDataResult_SkipsResponseFinalize()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                {
                    using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/bigdataprobe/payload").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "GET /bigdataprobe/payload 应返回 200");
                        Assert.AreEqual("application/octet-stream", response.Content.Headers.ContentType?.MediaType, "大数据结果应按声明的 Content-Type 输出");
                        Assert.IsFalse(HasHeader(response, "Cache-Control"), "IBigDataResult 不应走 SetCached，因此没有 Cache-Control 头");

                        var body = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                        var expected = BuildPayload();

                        Assert.AreEqual(expected.Length, body.Length, "大数据正文长度应与源数据一致");

                        for (var i = 0; i < expected.Length; i++)
                        {
                            if (expected[i] != body[i])
                            {
                                Assert.Fail("大数据正文第 " + i + " 字节应为 " + expected[i] + "，实际为 " + body[i]);
                            }
                        }
                    }

                    using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/bigdataprobe/plain").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "GET /bigdataprobe/plain 应返回 200");

                        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        Assert.AreEqual("bigdata-plain", body, "普通响应正文应为 Plain 动作的返回值");
                        Assert.IsTrue(HasHeader(response, "Cache-Control"), "普通响应应走 SetCached，带 Cache-Control 头");
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
        /// HEAD 命中大数据入口时只回报头：正文为空，Content-Length 仍按完整正文长度给出。
        /// </summary>
        /// <returns>该用例的异步执行任务。</returns>
        [TestMethod]
        public async Task RequestPipeline_BigDataResultHead_ReturnsContentLengthWithoutBody()
        {
            var port = TestAppFactory.GetFreePort();
            var rootName = TestAppFactory.CreateRootName();
            var app = TestAppFactory.CreateApp(rootName, port);

            try
            {
                app.Start();

                using (var client = TestAppFactory.CreateHttpClient())
                {
                    var url = BaseUrl(port) + "/bigdataprobe/payload";

                    using (var response = await TestAppFactory.SendWithReadyRetryAsync(client, () => new HttpRequestMessage(HttpMethod.Head, url)).ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "HEAD /bigdataprobe/payload 应返回 200");

                        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        Assert.AreEqual(string.Empty, body, "HEAD 响应不应带正文");
                        Assert.AreEqual((long)(64 * 1024), response.Content.Headers.ContentLength, "HEAD 的 Content-Length 应等于完整正文长度");
                        Assert.IsFalse(HasHeader(response, "Cache-Control"), "IBigDataResult 不应走 SetCached，因此没有 Cache-Control 头");
                    }

                    using (var response = await TestAppFactory.GetAsync(client, BaseUrl(port) + "/bigdataprobe/plain").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "HEAD 之后同客户端 GET 普通入口仍应返回 200");
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
        /// 生成与控制器一致的 64 KB 期望数据。
        /// </summary>
        /// <returns>期望字节</returns>
        private static byte[] BuildPayload()
        {
            var payload = new byte[64 * 1024];

            for (var i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)(i % 251);
            }

            return payload;
        }

        /// <summary>
        /// 判断响应头里是否存在指定标头。
        /// </summary>
        /// <param name="response">HTTP 响应</param>
        /// <param name="name">标头名</param>
        /// <returns>存在时返回 true</returns>
        private static bool HasHeader(HttpResponseMessage response, string name)
        {
            return response.Headers.TryGetValues(name, out _);
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
