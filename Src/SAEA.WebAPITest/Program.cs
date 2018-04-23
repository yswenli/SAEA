using SAEA.Commom;
using SAEA.WebAPI;

namespace SAEA.WebAPITest
{
    class Program
    {
        static void Main(string[] args)
        {
            ConsoleHelper.Title = "SAEA.WebAPITest";

            MvcApplication mvcApplication = new MvcApplication();

            mvcApplication.Start();

            ConsoleHelper.WriteLine("WebApi已启动！访问请输入http://127.0.0.1:39654/controller/action");

            ConsoleHelper.ReadLine();
        }
    }
}
