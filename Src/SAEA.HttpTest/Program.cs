using SAEA.Common;
using SAEA.Http;
using System;

namespace SAEA.HttpTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("WebHost Test");

            WebHost webHost = new WebHost(port: 8088);

            webHost.Start();

            ConsoleHelper.WriteLine("WebHost 已启动");

            ConsoleHelper.WriteLine("请在浏览器中输入URL:http://127.0.0.1:8088");

            ConsoleHelper.WriteLine("回车结束");

            Console.ReadLine();

        }
    }
}
