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

            SAEAMvcApplicationConfigBuilder.Write(mvcConfig);

            SAEAMvcApplication mvcApplication = new SAEAMvcApplication(mvcConfig);

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
    }
}
