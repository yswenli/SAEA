using SAEA.Common;
using SAEA.MVC;

namespace SAEA.MVCTest
{
    class Program
    {
        static void Main(string[] args)
        {
            ConsoleHelper.Title = "SAEA.MVCTest";

            var mvcConfig = SAEAMvcApplicationConfigBuilder.Read();

            mvcConfig.Count = 1;

            mvcConfig.Port = 28080;

            mvcConfig.IsStaticsCached = false;

            SAEAMvcApplicationConfigBuilder.Write(mvcConfig);

            SAEAMvcApplication mvcApplication = new SAEAMvcApplication(mvcConfig);

            mvcApplication.OnException += MvcApplication_OnException;

            //设置默认控制器

            //mvcApplication.SetDefault("home", "index");

            //mvcApplication.SetDefault("index.html");

            //限制

            //mvcApplication.SetForbiddenAccessList("/content/");

            //mvcApplication.SetForbiddenAccessList(".jpg");

            mvcApplication.Start();

            mvcApplication.SetCrossDomainHeaders("token", "auth");

            mvcApplication.Restart();

            ConsoleHelper.WriteLine($"MVC已启动！\t\r\n访问请输入http://127.0.0.1:{mvcConfig.Port}/{{controller}}/{{action}}");

            ConsoleHelper.WriteLine("回车结束！");

            ConsoleHelper.ReadLine();
        }

        private static Http.Model.IHttpResult MvcApplication_OnException(Http.Model.IHttpContext httpContext, System.Exception ex)
        {
            return new ContentResult($"已通过事件捕获发生异常，url：{httpContext.Request.Url},ex:{ex.Message}");
        }
    }
}
