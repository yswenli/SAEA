using System;
using System.Threading.Tasks;

using SAEA.Common;
using SAEA.P2PTest.Tests;

namespace SAEA.P2PTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args != null && Array.Exists(args, a => a == "--all"))
            {
                await RunAllAsync();
                Environment.ExitCode = TestHarness.HasFailures ? 1 : 0;
                return;
            }

            if (args != null && Array.Exists(args, a => a == "--bench-iocp"))
            {
                TestHarness.Reset();
                await IocpBenchmark.RunAsync();
                Environment.ExitCode = TestHarness.HasFailures ? 1 : 0;
                await Task.Delay(500);
                return;
            }

            while (true)
            {
                ConsoleHelper.Title = "SAEA.P2P Test";

                ConsoleHelper.WriteLine("SAEA.P2P Test");
                ConsoleHelper.WriteLine("1 = BuilderTest");
                ConsoleHelper.WriteLine("2 = ErrorCodeTest");
                ConsoleHelper.WriteLine("3 = ProtocolTest");
                ConsoleHelper.WriteLine("4 = QuickStartTest");
                ConsoleHelper.WriteLine("5 = ServerTest");
                ConsoleHelper.WriteLine("6 = ClientTest");
                ConsoleHelper.WriteLine("7 = HolePunchTest");
                ConsoleHelper.WriteLine("8 = RelayTest");
                ConsoleHelper.WriteLine("9 = LocalDiscoveryTest");
                ConsoleHelper.WriteLine("10 = AuthEncryptionTest");
                ConsoleHelper.WriteLine("11 = EdgeCaseTest");
                ConsoleHelper.WriteLine("12 = ProtocolAdvancedTest");
                ConsoleHelper.WriteLine("13 = SecurityAdvancedTest");
                ConsoleHelper.WriteLine("14 = RelayAdvancedTest");
                ConsoleHelper.WriteLine("15 = ConcurrencyTest");
                ConsoleHelper.WriteLine("16 = LifecycleTest");
                ConsoleHelper.WriteLine("17 = IntegrationTest");
                ConsoleHelper.WriteLine("18 = PerformanceTest");
                ConsoleHelper.WriteLine("19 = Run all advanced tests");
                ConsoleHelper.WriteLine("20 = StreamDecoderTest");
                ConsoleHelper.WriteLine("21 = IocpReceiveBenchmark");
                ConsoleHelper.WriteLine("0 = Exit");

                var pressedKey = ConsoleHelper.ReadLine();

                switch (pressedKey)
                {
                    case "1":
                        BuilderTest.Run();
                        break;
                    case "2":
                        ErrorCodeTest.Run();
                        break;
                    case "3":
                        ProtocolTest.Run();
                        break;
                    case "4":
                        QuickStartTest.Run();
                        break;
                    case "5":
                        await ServerTest.RunAsync();
                        break;
                    case "6":
                        await ClientTest.RunAsync();
                        break;
                    case "7":
                        await HolePunchTest.RunAsync();
                        break;
                    case "8":
                        await RelayTest.RunAsync();
                        break;
                    case "9":
                        await LocalDiscoveryTest.RunAsync();
                        break;
                    case "10":
                        await AuthEncryptionTest.RunAsync();
                        break;
                    case "11":
                        EdgeCaseTest.Run();
                        break;
                    case "12":
                        ProtocolAdvancedTest.Run();
                        break;
                    case "13":
                        SecurityAdvancedTest.Run();
                        break;
                    case "14":
                        RelayAdvancedTest.Run();
                        break;
                    case "15":
                        ConcurrencyTest.Run();
                        break;
                    case "16":
                        await LifecycleTest.RunAsync();
                        break;
                    case "17":
                        await IntegrationTest.RunAsync();
                        break;
                    case "18":
                        PerformanceTest.Run();
                        break;
                    case "19":
                        await RunAllAsync();
                        break;
                    case "20":
                        StreamDecoderTest.Run();
                        break;
                    case "21":
                        await IocpBenchmark.RunAsync();
                        break;
                    case "0":
                        return;
                }
            }
        }

        static async Task RunAllAsync()
        {
            TestHarness.Reset();

            EdgeCaseTest.Run();
            SpanPipelineTest.Run();
            ProtocolAdvancedTest.Run();
            SecurityAdvancedTest.Run();
            RelayAdvancedTest.Run();
            ConcurrencyTest.Run();
            await LifecycleTest.RunAsync();
            await IntegrationTest.RunAsync();
            PerformanceTest.Run();
            StreamDecoderTest.Run();
            await StreamDecoderTest.RunIocpClientAsync();
            await StreamDecoderTest.RunIocpServerAsync();

            TestHarness.WriteSummary("ALL ADVANCED TESTS");

            // ConsoleHelper 输出为异步队列，退出前留出时间刷出。
            await Task.Delay(500);
        }
    }
}