using System;
using System.Net;
using System.Threading.Tasks;

using SAEA.P2P.Builder;
using SAEA.P2P.Core;

namespace SAEA.P2PTest.Tests
{
    /// <summary>
    /// P2P 信令服务端自测：服务端创建后的初始状态、事件订阅与退订，以及启动、停止和再次启动
    /// </summary>
    public static class ServerTest
    {
        /// <summary>
        /// 依次校验服务端创建后的端口与节点数、事件订阅与退订，以及启动、停止再启动的完整流程
        /// </summary>
        /// <returns>异步任务，完成后该组场景与断言已全部执行</returns>
        public static async Task RunAsync()
        {
            TestHarness.Section("ServerTest");

            TestServerCreation();
            TestServerEvents();
            await TestServerStartStop();

            TestHarness.WriteSummary("ServerTest");
        }

        /// <summary>
        /// 按指定端口构造监听回环地址的服务端，日志阈值取 Error 以减少输出
        /// </summary>
        /// <param name="port">监听端口</param>
        /// <param name="maxNodes">最大节点数</param>
        /// <returns>尚未启动监听的服务端实例</returns>
        static P2PServer CreateServer(int port, int maxNodes)
        {
            var options = new P2PServerBuilder()
                .SetPort(port)
                .SetIP("127.0.0.1")
                .SetMaxNodes(maxNodes)
                .SetFreeTime(60000)
                .EnableLogging(4)
                .Build();

            return new P2PServer(options);
        }

        static void TestServerCreation()
        {
            TestHarness.Section("server creation");

            int port = TestHarness.GetFreeTcpPort();
            var server = CreateServer(port, 100);

            TestHarness.Expect(server.Port == port, "Port matches the configured value", server.Port.ToString());
            TestHarness.Expect(server.NodeCount == 0, "NodeCount is zero before Start", server.NodeCount.ToString());
            TestHarness.Expect(!server.IsRunning, "IsRunning is false before Start", server.IsRunning.ToString());
        }

        static void TestServerEvents()
        {
            TestHarness.Section("server events");

            int port = TestHarness.GetFreeTcpPort();
            var server = CreateServer(port, 10);

            // 用具名委托才能退订，回调体留空：本用例只验证订阅与退订都不抛异常
            Action<string, IPEndPoint> onRegistered = (nodeId, endpoint) => { };
            Action<string> onUnregistered = nodeId => { };
            Action<string, string> onError = (id, error) => { };

            bool eventHandled;
            try
            {
                server.OnNodeRegistered += onRegistered;
                server.OnNodeUnregistered += onUnregistered;
                server.OnError += onError;

                server.OnNodeRegistered -= onRegistered;
                server.OnNodeUnregistered -= onUnregistered;
                server.OnError -= onError;
                eventHandled = true;
            }
            catch (Exception ex)
            {
                eventHandled = false;
                TestHarness.Expect(false, "server events can be subscribed and unsubscribed", ex.GetType().Name + ": " + ex.Message);
            }

            if (eventHandled)
            {
                TestHarness.Expect(true, "server events can be subscribed and unsubscribed");
            }
        }

        static async Task TestServerStartStop()
        {
            TestHarness.Section("server start and stop");

            int port = TestHarness.GetFreeTcpPort();
            var server = CreateServer(port, 10);

            try
            {
                server.Start();
                TestHarness.Expect(await TestHarness.WaitUntil(() => server.IsRunning, 3000),
                    "IsRunning is true after Start", server.IsRunning.ToString());

                server.Stop();
                TestHarness.Expect(!server.IsRunning, "IsRunning is false after Stop", server.IsRunning.ToString());

                server.Start();
                TestHarness.Expect(await TestHarness.WaitUntil(() => server.IsRunning, 3000),
                    "server can start again after Stop", server.IsRunning.ToString());
            }
            finally
            {
                server.Stop();
            }
        }
    }
}