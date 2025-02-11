/****************************************************************************
*项目名称：SAEA.MQTTTest
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTTTest
*类 名 称：Program
*版本号： v7.0.0.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/14 14:11:15
*描述：
*=====================================================================
*修改时间：2019/1/14 14:11:15
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/

using System;
using System.Threading.Tasks;

using SAEA.Common;

namespace SAEA.MQTTTest
{
    class Program
    {
        static void Main(string[] args)
        {

            while (true)
            {
                Console.Title = $"SAEA.MQTT Test -- {DateTimeHelper.Now}";

                Console.WriteLine("1 = Start client");
                Console.WriteLine("2 = Start server");
                Console.WriteLine("3 = Start performance test");
                Console.WriteLine("4 = Start managed client");
                Console.WriteLine("5 = Start public broker test");
                Console.WriteLine("6 = Start server & client");
                Console.WriteLine("7 = Client flow test");
                Console.WriteLine("8 = Start performance test (client only)");
                Console.WriteLine("9 = Start server (no trace)");

                var pressedKey = Console.ReadLine();

                switch (pressedKey)
                {
                    case "1":
                        Task.Run(ClientTest.RunAsync);
                        break;
                    case "2":
                        Task.Run(ServerTest.RunAsync);
                        break;
                    case "3":
                        Task.Run(PerformanceTest.RunClientAndServer);
                        break;
                    case "4":
                        Task.Run(ManagedClientTest.RunAsync);
                        break;
                    case "5":
                        Task.Run(PublicBrokerTest.RunAsync);
                        break;
                    case "6":
                        Task.Run(ServerAndClientTest.RunAsync);
                        break;
                    case "7":
                        Task.Run(ClientFlowTest.RunAsync);
                        break;
                    case "8":
                        PerformanceTest.RunClientOnly();
                        break;
                    case "9":
                        ServerTest.RunEmptyServer();
                        break;
                }
            }
        }

    }
}
