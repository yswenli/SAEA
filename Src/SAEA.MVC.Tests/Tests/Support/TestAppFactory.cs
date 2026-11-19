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
*命名空间：SAEA.MVC.Tests.Tests.Support
*文件名： TestAppFactory
*版本号： net10.0
*唯一标识：afd67d6a-1fc7-4bc6-b407-f70b7ba54249
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 18:05:27
*描述：MVC 端到端用例的宿主工厂与就绪重试
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 18:05:27
*修改人： yswenli
*版本号： net10.0
*描述：新增，提供随机端口、临时站点根目录、自签证书与带重试的请求发送
*
*****************************************************************************/
using SAEA.MVC.Hosting;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using SAEA.Common.IO;
using SAEA.Sockets.Ssl;

namespace SAEA.MVC.Tests.Tests.Support
{
    /// <summary>
    /// MVC 端到端用例的宿主工厂：随机回环端口、临时站点根目录、自签证书与带就绪重试的请求发送。
    /// </summary>
    public static class TestAppFactory
    {
        /// <summary>
        /// 用例统一使用的回环地址。
        /// </summary>
        public const string LoopbackHost = "127.0.0.1";

        private const int ReadyIntervalMilliseconds = 100;

        private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 取一个当前空闲的回环端口，先占后放，避免用例之间撞端口。
        /// </summary>
        /// <returns>可监听的端口号</returns>
        public static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);

            listener.Start();

            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        /// <summary>
        /// 生成一个只含安全字符的临时站点根目录名。
        /// </summary>
        /// <returns>根目录名，相对当前运行根目录</returns>
        public static string CreateRootName()
        {
            return "mvc-tests-" + Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// 取临时站点根目录的物理路径，不存在时创建。
        /// </summary>
        /// <param name="rootName">根目录名</param>
        /// <returns>根目录完整路径</returns>
        public static string GetRootDirectory(string rootName)
        {
            return PathHelper.GetCurrentPath(rootName);
        }

        /// <summary>
        /// 删除临时站点根目录，删不掉不影响用例结论。
        /// </summary>
        /// <param name="rootName">根目录名</param>
        public static void DeleteRootDirectory(string rootName)
        {
            try
            {
                var directory = PathHelper.GetCurrentPath(rootName);

                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        /// <summary>
        /// 创建 MVC 应用实例，站点根目录指向临时目录。
        /// </summary>
        /// <param name="rootName">根目录名</param>
        /// <param name="port">监听端口</param>
        /// <param name="configure">启动前的应用配置回调，可为 null</param>
        /// <returns>尚未启动的应用实例</returns>
        public static SAEAMvcApplication CreateApp(string rootName, int port, Action<SAEAMvcApplication> configure = null)
        {
            var app = new SAEAMvcApplication(rootName, port);

            if (configure != null)
                configure(app);

            return app;
        }

        /// <summary>
        /// 停止应用并吞掉异常：收尾阶段服务可能已停止或端口已释放，失败不应覆盖用例结论。
        /// </summary>
        /// <param name="app">待停止的应用实例，可为 null</param>
        public static void StopQuietly(SAEAMvcApplication app)
        {
            if (app == null)
                return;

            try
            {
                app.Stop();
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 创建测试用证书：主题与 IP SAN 均为 127.0.0.1，用完由调用方按服务停止之后释放。
        /// </summary>
        /// <returns>自签证书与其私钥</returns>
        public static SaeaCertificateBundle CreateSelfSignedCertificate()
        {
            return SaeaCertificateFactory.CreateSelfSigned(new SaeaCertificateOptions
            {
                Subject = "CN=127.0.0.1",
                DnsNames = new[] { "localhost" },
                IpAddresses = new[] { "127.0.0.1" }
            });
        }

        /// <summary>
        /// 创建请求客户端，超时固定为 20 秒。
        /// </summary>
        /// <param name="acceptAnyCertificate">true 时跳过服务端证书校验，仅用于自签证书的 HTTPS 用例</param>
        /// <returns>请求客户端</returns>
        public static HttpClient CreateHttpClient(bool acceptAnyCertificate = false)
        {
            var handler = new HttpClientHandler();

            if (acceptAnyCertificate)
                handler.ServerCertificateCustomValidationCallback = (message, certificate, chain, errors) => true;

            return new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(20)
            };
        }

        /// <summary>
        /// 发 GET 请求，连接尚未就绪时在 10 秒内重试。
        /// </summary>
        /// <param name="client">请求客户端</param>
        /// <param name="url">完整地址</param>
        /// <returns>响应</returns>
        public static Task<HttpResponseMessage> GetAsync(HttpClient client, string url)
        {
            return SendWithReadyRetryAsync(client, () => new HttpRequestMessage(HttpMethod.Get, url));
        }

        /// <summary>
        /// 发 urlencoded 表单 POST，连接尚未就绪时在 10 秒内重试。
        /// </summary>
        /// <param name="client">请求客户端</param>
        /// <param name="url">完整地址</param>
        /// <param name="form">表单正文，如 name=saea</param>
        /// <returns>响应</returns>
        public static Task<HttpResponseMessage> PostFormAsync(HttpClient client, string url, string form)
        {
            return SendWithReadyRetryAsync(client, () => new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(form, Encoding.UTF8, "application/x-www-form-urlencoded")
            });
        }

        /// <summary>
        /// 发送请求并按响应读取正文，同时释放响应对象。
        /// </summary>
        /// <param name="client">请求客户端</param>
        /// <param name="requestFactory">每次尝试都新建请求的工厂，重试时不能复用请求对象</param>
        /// <returns>响应正文</returns>
        public static async Task<string> ReadContentAsync(HttpClient client, Func<HttpRequestMessage> requestFactory)
        {
            using (var response = await SendWithReadyRetryAsync(client, requestFactory).ConfigureAwait(false))
            {
                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 发送请求：首次连接被拒绝时按 100 毫秒间隔重试，最长 10 秒。
        /// </summary>
        /// <param name="client">请求客户端</param>
        /// <param name="requestFactory">请求工厂，每次尝试新建请求</param>
        /// <returns>响应</returns>
        public static async Task<HttpResponseMessage> SendWithReadyRetryAsync(HttpClient client, Func<HttpRequestMessage> requestFactory)
        {
            var deadline = DateTime.UtcNow + ReadyTimeout;

            while (true)
            {
                try
                {
                    return await client.SendAsync(requestFactory()).ConfigureAwait(false);
                }
                catch (HttpRequestException) when (DateTime.UtcNow < deadline)
                {
                    await Task.Delay(ReadyIntervalMilliseconds).ConfigureAwait(false);
                }
            }
        }
    }
}