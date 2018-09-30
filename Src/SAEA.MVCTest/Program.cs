using SAEA.Common;
using SAEA.WebAPI;
using System.Diagnostics;

namespace SAEA.WebAPITest
{
    class Program
    {
        static void Main(string[] args)
        {
            ConsoleHelper.Title = "SAEA.WebAPITest";

            SAEAMvcApplication mvcApplication = new SAEAMvcApplication();

            mvcApplication.SetDefault("home", "index");

            mvcApplication.Start();

            ConsoleHelper.WriteLine("WebApi已启动！\t\r\n访问请输入http://127.0.0.1:39654/controller/action");

            ConsoleHelper.ReadLine();
        }
    }
}
