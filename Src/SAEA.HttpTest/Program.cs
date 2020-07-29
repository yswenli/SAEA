using SAEA.Common;
using SAEA.Http;
using SAEA.Http.Model;
using System;

namespace SAEA.HttpTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("WebHost Test");

            WebHost webHost = new WebHost(port: 18080);

            webHost.Start();

            webHost.SetCrossDomainHeaders("token", "auth");

            ConsoleHelper.WriteLine("WebHost 已启动");

            ConsoleHelper.WriteLine("请在浏览器中输入URL:http://127.0.0.1:18080");

            ConsoleHelper.WriteLine("回车结束");

            Console.ReadLine();
            
        }
    }
}
