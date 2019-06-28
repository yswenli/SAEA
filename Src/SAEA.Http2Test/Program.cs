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
            Console.WriteLine("WebHost http2.0 Test");

            WebHost webHost = new WebHost(port: 8088);

            webHost.Start();

            ConsoleHelper.WriteLine("WebHost http2.0 已启动");

            var url = "http://127.0.0.1:8088";

            ConsoleHelper.WriteLine($"请在浏览器中输入URL:{url}");


            Http2Client http2Client1 = new Http2Client(new Uri(url));

            var result1 = http2Client1.Get().GetAwaiter().GetResult();


            Http2Client http2Client2 = new Http2Client(new Uri(url), true);

            var result2 = http2Client2.Get().GetAwaiter().GetResult();


            ConsoleHelper.WriteLine("回车结束");

            Console.ReadLine();
        }
    }
}
