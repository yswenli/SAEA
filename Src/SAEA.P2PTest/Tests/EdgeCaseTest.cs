using System;
using SAEA.P2P.Builder;
using SAEA.P2P.Channel;
using SAEA.P2P.Common;
using SAEA.P2P.Core;
using SAEA.P2P.Model;
using SAEA.P2P.Relay;

namespace SAEA.P2PTest.Tests
{
    /// <summary>
    /// 边界与异常输入测试：模型、选项校验、构建器守卫、中继会话配额。
    /// </summary>
    public static class EdgeCaseTest
    {
        public static void Run()
        {
            TestHarness.Section("EdgeCaseTest");

            PeerSessionEdges();
            NodeInfoEdges();
            ErrorCodeEdges();
            OptionValidationEdges();
            BuilderGuardEdges();
            RelaySessionEdges();

            TestHarness.WriteSummary("EdgeCaseTest");
        }

        static void PeerSessionEdges()
        {
            TestHarness.Section("PeerSession edges");

            TestHarness.Throws<ArgumentException>(() => new PeerSession(null, "p"), "PeerSession null sessionId throws");
            TestHarness.Throws<ArgumentException>(() => new PeerSession("", "p"), "PeerSession empty sessionId throws");
            TestHarness.Throws<ArgumentException>(() => new PeerSession("s", "  "), "PeerSession blank peerId throws");

            var s = new PeerSession("s1", "peer1");
            TestHarness.Expect(s.Channel == ChannelType.Direct, "default channel is Direct");
            TestHarness.Expect(s.State == NodeState.Init, "default state is Init");
            TestHarness.Expect(s.NextSendSeq() == 1 && s.NextSendSeq() == 2 && s.NextSendSeq() == 3, "NextSendSeq increments");
            TestHarness.Expect(s.NextReceiveSeq() == 1 && s.NextReceiveSeq() == 2, "NextReceiveSeq increments");

            TestHarness.Expect(s.GetPublicEndPoint() == null, "GetPublicEndPoint null when unset");
            TestHarness.Expect(s.GetLocalEndPoint() == null, "GetLocalEndPoint null when unset");

            s.PublicAddress = "1.2.3.4";
            s.PublicPort = 80;
            s.LocalAddress = "127.0.0.1";
            s.LocalPort = 65535;
            TestHarness.Expect(s.GetPublicEndPoint() == "1.2.3.4:80", "GetPublicEndPoint formats", s.GetPublicEndPoint());
            TestHarness.Expect(s.GetLocalEndPoint() == "127.0.0.1:65535", "GetLocalEndPoint formats", s.GetLocalEndPoint());

            TestHarness.Expect(!s.IsExpired(60000), "fresh session is not expired");
            TestHarness.Expect(s.IsExpired(-1), "IsExpired(-1) is true");
            s.Active();
            TestHarness.Expect(!s.IsExpired(60000), "Active resets last active time");
            TestHarness.Expect(s.ToString().Contains("peer1"), "ToString contains peer id");
        }

        static void NodeInfoEdges()
        {
            TestHarness.Section("NodeInfo edges");

            var n = new NodeInfo { NodeId = "n1", State = NodeState.Registered };
            TestHarness.Expect(n.IsOnline, "Registered is online");
            n.State = NodeState.Connected;
            TestHarness.Expect(n.IsOnline, "Connected is online");
            n.State = NodeState.Idle;
            TestHarness.Expect(n.IsOnline, "Idle is online");
            n.State = NodeState.Init;
            TestHarness.Expect(!n.IsOnline, "Init is offline");
            n.State = NodeState.Disconnected;
            TestHarness.Expect(!n.IsOnline, "Disconnected is offline");

            n.Services["k"] = "v";
            var clone = n.Clone();
            TestHarness.Expect(clone.NodeId == "n1" && clone.State == NodeState.Disconnected, "Clone copies scalar fields");
            TestHarness.Expect(clone.Services.Count == 0, "Clone does not copy Services dictionary (documented quirk)");
            clone.Services["x"] = "y";
            TestHarness.Expect(n.Services.Count == 1, "Clone Services are independent from source");

            TestHarness.Expect(n.GetPublicEndPoint() == null, "NodeInfo public endpoint null when unset");
            n.PublicAddress = "10.0.0.1";
            n.PublicPort = 1234;
            TestHarness.Expect(n.GetPublicEndPoint() == "10.0.0.1:1234", "NodeInfo public endpoint formatted");
        }

        static void ErrorCodeEdges()
        {
            TestHarness.Section("ErrorCode edges");

            TestHarness.Expect(ErrorCode.PunchFailed == "EO01", "PunchFailed is EO01");
            TestHarness.Expect(ErrorCode.RelaySessionNotFound == "ER03", "RelaySessionNotFound is ER03");
            TestHarness.Expect(ErrorCode.RelayQuotaExceeded == "ER04", "RelayQuotaExceeded is ER04");
            TestHarness.Expect(ErrorCode.EncryptionFailed == "EE01", "EncryptionFailed is EE01");
            TestHarness.Expect(ErrorCode.GetDescription(ErrorCode.RelaySessionNotFound) == "Relay session not found", "known description resolved");
            TestHarness.Expect(ErrorCode.GetDescription("NOPE") == "Unknown error", "unknown description fallback");
            TestHarness.Expect(ErrorCode.GetDescription(null) == "Unknown error", "null description fallback");
        }

        static void OptionValidationEdges()
        {
            TestHarness.Section("P2POptions validation edges");

            TestHarness.Throws<P2PException>(() => new P2POptions().Validate(), "missing NodeId throws");

            var localOk = true;
            try { new P2POptions { NodeId = "n" }.Validate(); }
            catch { localOk = false; }
            TestHarness.Expect(localOk, "local-only options valid without server address");

            var badPort = new P2POptions { NodeId = "n", ServerAddress = "127.0.0.1", ServerPort = 0 };
            TestHarness.Throws<P2PException>(() => badPort.Validate(), "invalid server port throws");

            var relayBad = new P2POptions { NodeId = "n" };
            relayBad.Relay.MaxRelayConnections = 0;
            TestHarness.Throws<P2PException>(() => relayBad.Validate(), "MaxRelayConnections <= 0 throws");

            var discBad = new P2POptions { NodeId = "n" };
            discBad.Discovery.LocalDiscoveryPort = 70000;
            TestHarness.Throws<P2PException>(() => discBad.Validate(), "invalid discovery port throws");

            var encNoKey = new P2POptions { NodeId = "n" };
            encNoKey.Encryption.Enabled = true;
            encNoKey.Encryption.Key = null;
            TestHarness.Throws<P2PException>(() => encNoKey.Validate(), "encryption enabled without key throws");

            var encBadSize = new P2POptions { NodeId = "n" };
            encBadSize.Encryption.Enabled = true;
            encBadSize.Encryption.Key = "0123456789abcdef";
            encBadSize.Encryption.KeySize = 64;
            TestHarness.Throws<P2PException>(() => encBadSize.Validate(), "invalid KeySize throws");

            var timeoutBad = new P2POptions { NodeId = "n" };
            timeoutBad.Timeout.ConnectTimeoutMs = 0;
            TestHarness.Throws<P2PException>(() => timeoutBad.Validate(), "ConnectTimeoutMs <= 0 throws");

            var punchBad = new P2POptions { NodeId = "n" };
            punchBad.HolePunch.MaxAttempts = 0;
            TestHarness.Throws<P2PException>(() => punchBad.Validate(), "MaxAttempts <= 0 throws");

            var serverBad = new P2PServerOptions { BindIP = "0.0.0.0", Port = 0 };
            TestHarness.Throws<P2PException>(() => serverBad.Validate(), "server port 0 throws");

            var serverIpBad = new P2PServerOptions { BindIP = "  ", Port = 39654 };
            TestHarness.Throws<P2PException>(() => serverIpBad.Validate(), "blank BindIP throws");
        }

        static void BuilderGuardEdges()
        {
            TestHarness.Section("Builder guard edges");

            TestHarness.Throws<P2PException>(() => new P2PClientBuilder().SetNodeId(""), "SetNodeId empty throws");
            TestHarness.Throws<P2PException>(() => new P2PClientBuilder().SetNodeId(new string('x', 65)), "SetNodeId too long throws");
            TestHarness.Throws<P2PException>(() => new P2PClientBuilder().SetServer(null, 1), "SetServer null address throws");
            TestHarness.Throws<P2PException>(() => new P2PClientBuilder().SetServer("h", 0), "SetServer invalid port throws");
            TestHarness.Throws<P2PException>(() => new P2PClientBuilder().SetServer((System.Net.IPEndPoint)null), "SetServer null endpoint throws");
            TestHarness.Throws<P2PException>(() => new P2PClientBuilder().EnableEncryption("abc"), "EnableEncryption short key throws");
            TestHarness.Throws<P2PException>(() => new P2PClientBuilder().EnableEncryption(null), "EnableEncryption null key throws");
            TestHarness.Throws<P2PException>(() => new P2PClientBuilder().EnableLocalDiscovery(0), "EnableLocalDiscovery invalid port throws");
            TestHarness.Throws<P2PException>(() => new P2PClientBuilder().EnableLocalDiscovery(39655, " "), "EnableLocalDiscovery blank multicast throws");
            TestHarness.Throws<P2PException>(() => new P2PClientBuilder().SetTimeout(0), "SetTimeout 0 throws");

            TestHarness.Throws<P2PException>(
                () => new P2PClientBuilder().SetNodeId("n").EnableEncryption().Build(),
                "Build rejects encryption enabled without key");

            TestHarness.Throws<P2PException>(() => new P2PServerBuilder().SetPort(0), "server SetPort 0 throws");
            TestHarness.Throws<P2PException>(() => new P2PServerBuilder().SetMaxNodes(0), "server SetMaxNodes 0 throws");
            TestHarness.Throws<P2PException>(() => new P2PServerBuilder().EnableTls("", ""), "server EnableTls empty cert throws");
        }

        static void RelaySessionEdges()
        {
            TestHarness.Section("RelaySession edges");

            TestHarness.Expect(new RelaySession().State == RelayState.Pending, "default relay state Pending");

            var rs = new RelaySession { MaxQuota = 100, BytesTransferred = 100 };
            TestHarness.Expect(!rs.IsOverQuota, "quota boundary (==) is not over");

            rs.BytesTransferred = 101;
            TestHarness.Expect(rs.IsOverQuota, "above quota is over");

            rs.MaxQuota = 0;
            TestHarness.Expect(!rs.IsOverQuota, "quota 0 means unlimited");

            rs.MaxQuota = -5;
            TestHarness.Expect(!rs.IsOverQuota, "negative quota means unlimited");

            var rs2 = new RelaySession();
            var before = rs2.LastActiveTime;
            rs2.AddBytes(10);
            TestHarness.Expect(rs2.BytesTransferred == 10 && rs2.LastActiveTime >= before, "AddBytes accumulates and refreshes");

            rs2.Close();
            TestHarness.Expect(rs2.State == RelayState.Closed, "Close sets Closed");
        }
    }
}