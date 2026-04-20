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
        static async Task Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "emqx")
            {
                await EmqxBrokerTest.RunAsync();
                return;
            }

            while (true)
            {
                ConsoleHelper.Title = $"SAEA.MQTT Test";

                ConsoleHelper.WriteLine("SAEA.MQTT Test");
                ConsoleHelper.WriteLine("1 = Start client");
                ConsoleHelper.WriteLine("2 = Start server");
                ConsoleHelper.WriteLine("3 = Start performance test");
                ConsoleHelper.WriteLine("4 = Start managed client");
                ConsoleHelper.WriteLine("5 = Start public broker test");
                ConsoleHelper.WriteLine("6 = Start server & client");
                ConsoleHelper.WriteLine("7 = Client flow test");
                ConsoleHelper.WriteLine("8 = Start performance test (client only)");
                ConsoleHelper.WriteLine("9 = Start server (no trace)");
                ConsoleHelper.WriteLine("10 = Test broker.emqx.io MQTT 5.0");

                var pressedKey = ConsoleHelper.ReadLine();

                switch (pressedKey)
                {
                    case "1":
                        await ClientTest.RunAsync();
                        break;
                    case "2":
                        await ServerTest.RunAsync();
                        break;
                    case "3":
                        await PerformanceTest.RunClientAndServer();
                        break;
                    case "4":
                        await ManagedClientTest.RunAsync();
                        break;
                    case "5":
                        await PublicBrokerTest.RunAsync();
                        break;
                    case "6":
                        await ServerAndClientTest.RunAsync();
                        break;
                    case "7":
                        await ClientFlowTest.RunAsync();
                        break;
                    case "8":
                        PerformanceTest.RunClientOnly();
                        break;
                    case "9":
                        ServerTest.RunEmptyServer();
                        break;
                    case "10":
                        await EmqxBrokerTest.RunAsync();
                        break;
                }
            }
        }

    }
}
