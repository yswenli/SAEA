using SAEA.Common;
using SAEA.MVC;

namespace SAEA.MVCTest
{
    class Program
    {
        static void Main(string[] args)
        {
            int num1 = GetNum1();

            ref int num2 = ref GetNum2();            

            while (true)
            {
                ThreadHelper.Sleep(1000);
                ConsoleHelper.WriteLine($"num1:{num1},num2:{num2}");
            }



            ConsoleHelper.Title = "SAEA.MVCTest";

            SAEAMvcApplication mvcApplication = new SAEAMvcApplication(root: "/html/", count: 10000);

            //设置默认控制器
            mvcApplication.SetDefault("home", "index");

            mvcApplication.SetDefault("index.html");


            //限制
            mvcApplication.SetForbiddenAccessList("/content/");

            mvcApplication.SetForbiddenAccessList(".jpg");

            mvcApplication.Start();

            ConsoleHelper.WriteLine("MVC已启动！\t\r\n访问请输入http://127.0.0.1:39654/{controller}/{action}");

            ConsoleHelper.WriteLine("回车结束！");

            ConsoleHelper.ReadLine();
        }


        static int testNum1 = 0;
        static int GetNum1()
        {
            TaskHelper.Start(() =>
            {
                while (true)
                {
                    ThreadHelper.Sleep(100);
                    testNum1++;
                }
            });
            return testNum1;
        }


        static int testNum2 = 0;
        static ref int GetNum2()
        {
            TaskHelper.Start(() =>
            {
                while (true)
                {
                    ThreadHelper.Sleep(100);
                    testNum2++;
                }
            });
            return ref testNum2;
        }

        

    }
}
