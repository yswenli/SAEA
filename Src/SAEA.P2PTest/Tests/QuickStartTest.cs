using System;

using SAEA.Common;
using SAEA.P2P.Builder;
using SAEA.P2P.Ice;

namespace SAEA.P2PTest.Tests
{
    /// <summary>
    /// 快速上手示例：演示客户端与服务端构建器的典型链式配置
    /// </summary>
    public static class QuickStartTest
    {
        /// <summary>
        /// 依次演示客户端与服务端构建器的配置方式并打印生成的选项
        /// </summary>
        public static void Run()
        {
            ConsoleHelper.WriteLine("=== QuickStartTest ===");
            ConsoleHelper.WriteLine("");

            DemoClientBuilder();
            ConsoleHelper.WriteLine("");

            DemoServerBuilder();
            ConsoleHelper.WriteLine("");

            ConsoleHelper.WriteLine("=== QuickStartTest Complete ===");
        }

        static void DemoClientBuilder()
        {
            ConsoleHelper.WriteLine("--- DemoClientBuilder ---");

            var client = new P2PClientBuilder()
                .SetServer("p2p.example.com", 39654)
                .SetNodeId("client-001")
                .UseIce(new IceSessionOptions())
                .BuildClient();

            ConsoleHelper.WriteLine("Client configured:");
            ConsoleHelper.WriteLine($"  NodeId: {client.PeerId}");
            ConsoleHelper.WriteLine($"  Registered: {client.IsRegistered}");

            // 构建即创建生命周期令牌，演示结束立即释放，避免未连接实例挂住资源
            client.Dispose();

            ConsoleHelper.WriteLine("DemoClientBuilder: PASSED");
        }

        static void DemoServerBuilder()
        {
            ConsoleHelper.WriteLine("--- DemoServerBuilder ---");

            var options = new P2PServerBuilder()
                .SetPort(39654)
                .SetMaxNodes(100)
                .Build();

            ConsoleHelper.WriteLine("Server options configured:");
            ConsoleHelper.WriteLine($"  Bind: {options.BindIP}:{options.Port}");
            ConsoleHelper.WriteLine($"  MaxNodes: {options.MaxNodes}");
            ConsoleHelper.WriteLine($"  Logging: {options.Logging.Enabled}");

            ConsoleHelper.WriteLine("DemoServerBuilder: PASSED");
        }
    }
}