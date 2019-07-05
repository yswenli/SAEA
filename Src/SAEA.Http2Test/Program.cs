using SAEA.Common;
using SAEA.Http2;
using SAEA.Http2.Net;
using System;

namespace SAEA.Http2Test
{
    class Program
    {
        static void Main(string[] args)
        {
            ConsoleHelper.WriteLine("WebHost http2.0 Test");

            ConsoleHelper.WriteLine("输入c进行自定义测试，其他为webhost测试");

            var input = ConsoleHelper.ReadLine();

            if (input == "c")
            {

            }
            else
            {
                WebHostTest();
            }

            ConsoleHelper.WriteLine("回车结束");

            ConsoleHelper.ReadLine();
        }


        #region custom



        #endregion



        #region webhost test

        static void WebHostTest()
        {
            WebHost webHost = new WebHost(port: 8088);

            webHost.Start();

            ConsoleHelper.WriteLine("WebHost http2.0 已启动");

            var url = "http://127.0.0.1:8088";

            ConsoleHelper.WriteLine($"请在浏览器中输入URL:{url}");

            Http2Client http2Client1 = new Http2Client(new Uri(url));

            var result1 = http2Client1.Get().GetAwaiter().GetResult();
            var result11 = http2Client1.Get().GetAwaiter().GetResult();

            Http2Client http2Client2 = new Http2Client(new Uri(url));

            var result2 = http2Client2.Post("name=yswenli").GetAwaiter().GetResult();
        }

        #endregion


    }
}
