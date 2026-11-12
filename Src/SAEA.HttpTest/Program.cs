using SAEA.Common;
using SAEA.Http;
using SAEA.Http.Model;
using SAEA.Sockets.Pipeline;

using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
            //webHost.WebConfig.RequestDelegate = WebHost_OnRequestDelegate;

            webHost.WebConfig.ExceptionHandler = WebHost_OnException;

            webHost.WebConfig.ConfigurePipeline = (channelId, pipeline) =>
            {
                // 普通 HTTP 请求由派发节点直接交给 RequestDelegate，不会流到这里的处理器；
                // 此处只做连接级观测，演示自定义节点该挂在哪里。
                pipeline.AddLast(new ConnectionTraceHandler());
            };

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

        /// <summary>
        /// 连接级观测处理器，演示自定义入站处理器挂载位置
        /// </summary>
        private sealed class ConnectionTraceHandler : InboundHandlerAdapter
        {
            private static int _online;

            /// <summary>
            /// 连接建立时登记在线数
            /// </summary>
            /// <param name="ctx">当前处理器在链上的节点上下文</param>
            /// <returns>已完成的任务</returns>
            public override Task ChannelActiveAsync(IChannelHandlerContext ctx)
            {
                var online = Interlocked.Increment(ref _online);

                ConsoleHelper.WriteLine($"连接建立 channelId = {ctx.Channel.Id}，当前在线数 = {online}");

                // 生命周期通知由泵按链顺序逐个调用，不依赖处理器向下游转发。
                return Task.CompletedTask;
            }

            /// <summary>
            /// 连接断开时更新在线数
            /// </summary>
            /// <param name="ctx">当前处理器在链上的节点上下文</param>
            /// <returns>已完成的任务</returns>
            public override Task ChannelInactiveAsync(IChannelHandlerContext ctx)
            {
                var online = Interlocked.Decrement(ref _online);

                ConsoleHelper.WriteLine($"连接断开 channelId = {ctx.Channel.Id}，当前在线数 = {online}");

                return Task.CompletedTask;
            }
        }
    }
}
