using SAEA.Common;
using SAEA.MVC;

namespace SAEA.MVCTest
{
    class Program
    {
        static void Main(string[] args)
        {
            ConsoleHelper.Title = "SAEA.MVCTest";

            SAEAMvcApplication mvcApplication = new SAEAMvcApplication(root: "/html/", count: 2);

            //设置默认控制器
            mvcApplication.SetDefault("home", "index");

            mvcApplication.SetDefault("index.html");


            //限制
            mvcApplication.SetForbiddenAccessList("/content/");

            mvcApplication.SetForbiddenAccessList(".jpg");


            mvcApplication.Start();

            ConsoleHelper.WriteLine("MVC已启动！\t\r\n访问请输入http://127.0.0.1:39654/{controller}/{action}");

            ConsoleHelper.ReadLine();
        }
    }
}
