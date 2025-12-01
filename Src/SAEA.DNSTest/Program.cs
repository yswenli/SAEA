using SAEA.Common;

using System;

namespace SAEA.DNSTest
{
    class Program
    {
        public static void Main(string[] args)
        {
            ConsoleHelper.Title = $"SAEA.DNS Test -- {DateTimeHelper.Now}";

            ConsoleHelper.WriteLine("SAEA.DNS Test");

            do
            {
                Test();
            }
            while (true);
        }

        static async void Test()
        {
            ConsoleHelper.WriteLine("输入s启动DNS Server \t 输入c启动DNS Client \t输入a 启动DNS Server 和DNS Client");

            try
            {
                var input = ConsoleHelper.ReadLine();

                switch (input)
                {
                    case "s":
                        Server.Init();
                        break;

                    case "c":
                        await Client.LookupAsync("baidu.com");
                        await Client.LookupAsync("yswenli.net");
                        break;

                    default:
                        Server.Init();
                        await Client.LookupAsync("baidu.com");
                        await Client.LookupAsync("yswenli.net");
                        break;
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteLine($"Err:{ex.Message} \t{ex.Source} \t {ex.StackTrace}");
            }
        }
    }
}
