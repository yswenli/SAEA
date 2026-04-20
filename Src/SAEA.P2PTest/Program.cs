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
                    case "0":
                        return;
                }
            }
        }
    }
}