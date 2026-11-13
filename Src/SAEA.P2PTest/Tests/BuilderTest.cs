using System;
using System.Net;

using SAEA.P2P.Builder;
using SAEA.P2P.Common;
using SAEA.P2P.Ice;
using SAEA.P2P.Signaling;
using SAEA.P2P.Turn;

namespace SAEA.P2PTest.Tests
{
    /// <summary>
    /// 客户端与服务端构建器自测：基础链式配置产出客户端、配置整体替换、守卫子句与服务端取值
    /// </summary>
    public static class BuilderTest
    {
        /// <summary>
        /// 依次校验客户端构建器基础链路、整体替换取值、非法配置抛错，以及服务端构建器取值
        /// </summary>
        public static void Run()
        {
            TestHarness.Section("BuilderTest");

            ClientBuilderBasics();
            ClientBuilderOptionReplacement();
            ClientBuilderGuards();
            ServerBuilder();

            TestHarness.WriteSummary("BuilderTest");
        }

        static void ClientBuilderBasics()
        {
            TestHarness.Section("client builder basics");

            var endpoint = new IPEndPoint(IPAddress.Parse("10.0.0.1"), 40000);
            var client = new P2PClientBuilder()
                .SetServer(endpoint)
                .SetNodeId("test-node-001")
                .SetTimeout(15000)
                .EnableTls()
                .BuildClient();

            try
            {
                // 构建器未提供选项回读，只能通过客户端公开面确认节点标识已写入且尚未连接服务端
                TestHarness.Expect(client.PeerId == "test-node-001", "BuildClient keeps node id", client.PeerId);
                TestHarness.Expect(!client.IsRegistered, "freshly built client is not registered");
                TestHarness.Expect(!string.IsNullOrEmpty(client.PeerId), "BuildClient yields a usable client");
            }
            finally
            {
                client.Dispose();
            }
        }

        static void ClientBuilderOptionReplacement()
        {
            TestHarness.Section("client builder option replacement");

            var signaling = new SignalingOptions
            {
                ServerAddress = "10.0.0.9",
                ServerPort = 40000,
                PeerId = "replace-node",
                ConnectTimeoutMs = 12345,
                RegisterTimeoutMs = 23456,
                ReconnectIntervalMs = 3456,
                MaxReconnectAttempts = 7,
                HeartbeatIntervalMs = 4567
            };

            var client = new P2PClientBuilder()
                .SetServer("127.0.0.1", 1)
                .UseSignaling(signaling)
                .EnableTls()
                .BuildClient();

            try
            {
                // 先 SetServer 再 UseSignaling：PeerId 与服务器地址若仍为旧值即说明替换未生效
                TestHarness.Expect(client.PeerId == "replace-node", "UseSignaling replaces earlier settings", client.PeerId);
                TestHarness.Expect(signaling.ServerAddress == "10.0.0.9" && signaling.ServerPort == 40000,
                    "UseSignaling keeps supplied server address");
                TestHarness.Expect(signaling.ConnectTimeoutMs == 12345 && signaling.RegisterTimeoutMs == 23456,
                    "UseSignaling keeps supplied timeouts");
                TestHarness.Expect(signaling.ReconnectIntervalMs == 3456 && signaling.MaxReconnectAttempts == 7
                    && signaling.HeartbeatIntervalMs == 4567, "UseSignaling keeps supplied reconnect and heartbeat");
                TestHarness.Expect(signaling.EnableTls && signaling.TlsTargetHost == "10.0.0.9",
                    "EnableTls fills TlsTargetHost from the replaced server address", signaling.TlsTargetHost);
            }
            finally
            {
                client.Dispose();
            }

            var ice = new IceSessionOptions
            {
                GatheringBudget = TimeSpan.FromSeconds(2),
                UdpDirectBudget = TimeSpan.FromSeconds(4),
                TcpDirectBudget = TimeSpan.FromSeconds(5),
                TurnStageBudget = TimeSpan.FromSeconds(6),
                SessionBudget = TimeSpan.FromSeconds(40)
            };
            var turn = new TurnClientOptions
            {
                ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 3478),
                Username = "turn-user",
                Password = "turn-password"
            };

            var iceClient = new P2PClientBuilder()
                .UseIce(ice)
                .UseTurn(turn)
                .SetNodeId("ice-node")
                .BuildClient();

            try
            {
                // UseTurn 写入的是 UseIce 替换进来的那个实例，引用相同即证明两次替换都落到了同一份配置
                TestHarness.Expect(ReferenceEquals(ice.Turn, turn), "UseTurn writes into the replaced ICE options");
                TestHarness.Expect(ice.SessionBudget == TimeSpan.FromSeconds(40), "UseIce keeps supplied session budget",
                    ice.SessionBudget.ToString());
            }
            finally
            {
                iceClient.Dispose();
            }

            // 默认 ICE 预算合法，只有替换进来的非法预算才会让 BuildClient 抛错
            var brokenIce = new IceSessionOptions { SessionBudget = TimeSpan.FromSeconds(1) };
            var iceError = TestHarness.Throws<ArgumentException>(
                () => new P2PClientBuilder().UseIce(brokenIce).SetNodeId("broken-ice").BuildClient(),
                "BuildClient validates the replaced ICE options");
            TestHarness.Expect(iceError != null && iceError.ParamName == nameof(IceSessionOptions.SessionBudget),
                "ICE validation failure points at SessionBudget", iceError?.ParamName);
        }

        static void ClientBuilderGuards()
        {
            TestHarness.Section("client builder guards");

            var missingPeer = TestHarness.Throws<P2PException>(
                () => new P2PClientBuilder().SetServer("127.0.0.1", 39654).BuildClient(),
                "BuildClient without PeerId throws");
            TestHarness.Expect(missingPeer != null && missingPeer.ErrorCode == ErrorCode.RegisterPeerIdMissing,
                "missing PeerId reports RegisterPeerIdMissing", missingPeer?.ErrorCode);

            // SetServer 拒绝 0 端口，这里借 UseSignaling 直接把非法端口交给 BuildClient 校验
            var badPort = TestHarness.Throws<P2PException>(
                () => new P2PClientBuilder()
                    .UseSignaling(new SignalingOptions { PeerId = "guard-node", ServerPort = 0 })
                    .BuildClient(),
                "BuildClient with ServerPort 0 throws");
            TestHarness.Expect(badPort != null && badPort.ErrorCode == ErrorCode.RegisterServerUnavailable,
                "ServerPort 0 reports RegisterServerUnavailable", badPort?.ErrorCode);
        }

        static void ServerBuilder()
        {
            TestHarness.Section("server builder");

            var plain = new P2PServerBuilder()
                .SetPort(39654)
                .SetIP("0.0.0.0")
                .SetMaxNodes(5000)
                .SetFreeTime(90000)
                .EnableLogging()
                .Build();

            TestHarness.Expect(plain.BindIP == "0.0.0.0" && plain.Port == 39654,
                "server builder keeps bind address and port");
            TestHarness.Expect(plain.MaxNodes == 5000, "server builder keeps max nodes", plain.MaxNodes.ToString());
            TestHarness.Expect(plain.Logging.Enabled && plain.Logging.Level == 2,
                "EnableLogging keeps default level 2", plain.Logging.Level.ToString());
            TestHarness.Expect(!plain.Tls.Enabled, "TLS stays disabled by default");
            TestHarness.Expect(plain.Timeout.FreeTimeMs == 90000, "SetFreeTime writes free time",
                plain.Timeout.FreeTimeMs.ToString());

            var tls = new P2PServerBuilder()
                .SetPort(39655)
                .SetMaxNodes(10)
                .EnableLogging(3)
                .EnableTls("server.pfx", "cert-password")
                .Build();

            TestHarness.Expect(tls.Tls.Enabled && tls.Tls.CertificatePath == "server.pfx",
                "EnableTls turns TLS on with a certificate path");
            TestHarness.Expect(tls.Logging.Enabled && tls.Logging.Level == 3,
                "EnableLogging keeps the explicit level", tls.Logging.Level.ToString());
        }
    }
}