using SAEA.Common;
using System;

namespace SAEA.DNSTest
{
    class Program
    {
        public static void Main(string[] args)
        {
            ConsoleHelper.Title = "SAEA.DNS Test";

            ConsoleHelper.WriteLine("SAEA.DNS Test");

            ConsoleHelper.WriteLine("输入s启动DNS Server \t 输入c启动DNS Client \t输入a 启动DNS Server 和DNS Client");

            do
            {
                try
                {
                    var input = ConsoleHelper.ReadLine();

                    switch (input)
                    {
                        case "s":
                            Server.InitAsync().GetAwaiter().GetResult();
                            break;

                        case "c":
                            Client.LookupAsync("baidu.com").GetAwaiter().GetResult();
                            break;

                        default:
                            Server.Init();
                            Client.LookupAsync("baidu.com").GetAwaiter().GetResult();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    ConsoleHelper.WriteLine($"Err:{ex.Message} \t{ex.Source} \t {ex.StackTrace}");
                }


            }
            while (true);

            Console.ReadLine();
        }
    }
}
