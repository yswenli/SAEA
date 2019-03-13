using SAEA.Common;
using System;
using System.Reflection;

namespace SAEA.Mvc.ServiceTest
{
    static class Program
    {

        private static readonly string Name = "SAEA.Mvc.ServiceTest";
        private static readonly string Display = "SAEA.Mvc.ServiceTest";
        private static readonly string Description = "SAEA.Mvc.ServiceTest";
        private static readonly string FilePath = Assembly.GetExecutingAssembly().Location;


        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        static void Main(string[] args)
        {
            if (args.Length != 0)
            {
                switch (args[0].ToUpper())
                {
                    case "/I":
                        WinServiceHelper.InstallAndStart(FilePath, Name, Display, Description);
                        return;
                    case "/U":
                        WinServiceHelper.Unstall(Name);
                        return;
                    default:
                        Console.WriteLine("args:");
                        Console.WriteLine("\t/i\t\t 安装服务");
                        Console.WriteLine("\t/u\t\t 卸载服务");
                        return;
                }
            }
            else
            {
                Service1.Run();
            }
        }
    }
}
