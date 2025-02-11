using SAEA.Common;
using SAEA.Http;
using SAEA.Http.Model;

using System;
using System.Net;
using System.Text;

namespace SAEA.HttpTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine($"WebHost Test -- {DateTimeHelper.Now}");

            WebHost webHost = new WebHost(port: 18080);

            webHost.SetCrossDomainHeaders("token", "auth");

            //自定义处理
            //webHost.OnRequestDelegate += WebHost_OnRequestDelegate;

            webHost.OnException += WebHost_OnException;

            webHost.Start();

            ConsoleHelper.WriteLine("WebHost 已启动");

            ConsoleHelper.WriteLine("请在浏览器中输入URL:http://127.0.0.1:18080");

            ConsoleHelper.WriteLine("回车结束");

            Console.ReadLine();

        }

        /// <summary>
        /// 自定义捕获全局异常
        /// </summary>
        /// <param name="httpContext"></param>
        /// <param name="ex"></param>
        /// <returns></returns>
        private static IHttpResult WebHost_OnException(Http.Model.IHttpContext httpContext, Exception ex)
        {
            Console.WriteLine($"WebHost_OnException:{ex.Message}");
            return new HttpContentResult(ex.Message);
        }

        /// <summary>
        /// 自定义处理
        /// </summary>
        /// <param name="context"></param>
        private static void WebHost_OnRequestDelegate(IHttpContext context)
        {
            var url = context.Request.Url;

            context.Response.Status = HttpStatusCode.OK;

            context.Response.ContentType = "content-type: text/html;charset=utf-8";

            context.Response.Write($"<p>当前请求地址是：{url}</p>");

            context.Response.End();
        }
    }
}
