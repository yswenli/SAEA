using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;

using SAEA.P2P;
using SAEA.P2P.Builder;
using SAEA.P2P.Channel;
using SAEA.P2P.Common;
using SAEA.P2P.Core;
using SAEA.P2P.Ice;
using SAEA.P2P.Model;
using SAEA.P2P.Signaling;

namespace SAEA.P2PTest.Tests
{
    /// <summary>
    /// 边界与异常输入测试：会话与节点模型、错误码、信令与 ICE 配置校验、构建器守卫。
    /// </summary>
    public static class EdgeCaseTest
    {
        /// <summary>
        /// 依次运行会话与节点模型、错误码、配置校验与构建器守卫的边界断言
        /// </summary>
        public static void Run()
        {
            TestHarness.Section("EdgeCaseTest");

            PeerSessionEdges();
            NodeInfoEdges();
            ErrorCodeEdges();
            SignalingOptionsEdges();
            IceSessionOptionsEdges();
            BuilderGuardEdges();

            TestHarness.WriteSummary("EdgeCaseTest");
        }

        static bool DoesNotThrow(Action action)
        {
            try
            {
                action();
                return true;
            }
            catch
            {
                return false;
            }
        }

        static void PeerSessionEdges()
        {
            TestHarness.Section("PeerSession edges");

            TestHarness.Throws<ArgumentException>(() => new PeerSession(null!, "p"), "PeerSession null sessionId throws");
            TestHarness.Throws<ArgumentException>(() => new PeerSession("", "p"), "PeerSession empty sessionId throws");
            TestHarness.Throws<ArgumentException>(() => new PeerSession("   ", "p"), "PeerSession blank sessionId throws");
            TestHarness.Throws<ArgumentException>(() => new PeerSession("s", "  "), "PeerSession blank peerId throws");

            var session = new PeerSession("s1", "peer1");
            TestHarness.Expect(session.SessionId == "s1" && session.PeerId == "peer1", "PeerSession keeps identifiers");
            TestHarness.Expect(session.Channel == ChannelType.UdpDirect, "default channel is UdpDirect",
                session.Channel.ToString());
            TestHarness.Expect(session.LastActiveTime >= session.CreatedTime, "last active time starts at creation time");

            var before = session.LastActiveTime;
            session.Active();
            TestHarness.Expect(session.LastActiveTime >= before, "Active refreshes last active time");
            TestHarness.Expect(session.ToString().Contains("peer1"), "ToString contains peer id", session.ToString());
        }

        static void NodeInfoEdges()
        {
            TestHarness.Section("NodeInfo edges");

            var before = DateTime.UtcNow;
            var node = new NodeInfo();
            TestHarness.Expect(node.NodeId == null && node.NodeName == null, "NodeInfo identifiers default to null");
            TestHarness.Expect(node.PublicAddress == null && node.PublicPort == 0, "public address defaults to unset");
            TestHarness.Expect(node.LocalAddress == null && node.LocalPort == 0, "local address defaults to unset");
            // 先取到局部变量再判空：直接对属性做 null 检查会让编译器把该属性标记为可能为空，影响后续断言
            var initialServices = node.Services;
            TestHarness.Expect(initialServices != null && initialServices.Count == 0, "Services defaults to an empty dictionary");
            TestHarness.Expect(node.State == NodeState.Init && !node.IsOnline, "default state Init is offline");
            TestHarness.Expect(node.RegisteredTime >= before.AddSeconds(-1) && node.RegisteredTime <= DateTime.UtcNow.AddSeconds(1),
                "RegisteredTime defaults to current UTC time");

            node.State = NodeState.Registered;
            TestHarness.Expect(node.IsOnline, "Registered is online");
            node.State = NodeState.Connected;
            TestHarness.Expect(node.IsOnline, "Connected is online");
            node.State = NodeState.Idle;
            TestHarness.Expect(node.IsOnline, "Idle is online");
            node.State = NodeState.Error;
            TestHarness.Expect(!node.IsOnline, "Error is offline");

            TestHarness.Expect(node.GetPublicEndPoint() == null, "GetPublicEndPoint null when unset");
            TestHarness.Expect(node.GetLocalEndPoint() == null, "GetLocalEndPoint null when unset");

            node.NodeName = "edge-node";
            node.PublicAddress = "10.0.0.1";
            node.PublicPort = 1234;
            node.LocalAddress = "127.0.0.1";
            node.LocalPort = 65535;
            node.State = NodeState.Disconnected;
            node.Services["k"] = "v";

            var clone = node.Clone();
            TestHarness.Expect(clone.NodeId == node.NodeId && clone.NodeName == "edge-node", "Clone copies identifiers");
            TestHarness.Expect(clone.PublicAddress == "10.0.0.1" && clone.PublicPort == 1234, "Clone copies public address");
            TestHarness.Expect(clone.LocalAddress == "127.0.0.1" && clone.LocalPort == 65535, "Clone copies local address");
            TestHarness.Expect(clone.State == NodeState.Disconnected, "Clone copies state");
            TestHarness.Expect(clone.RegisteredTime == node.RegisteredTime, "Clone copies registered time");
            TestHarness.Expect(clone.Services.Count == 0, "Clone does not copy Services dictionary");
            clone.Services["x"] = "y";
            TestHarness.Expect(node.Services.Count == 1, "Clone Services are independent from source");

            TestHarness.Expect(node.GetPublicEndPoint() == "10.0.0.1:1234", "public endpoint formatted", node.GetPublicEndPoint());
            TestHarness.Expect(node.GetLocalEndPoint() == "127.0.0.1:65535", "local endpoint formatted", node.GetLocalEndPoint());
            TestHarness.Expect(node.ToString().Contains("edge-node"), "ToString contains node name", node.ToString());
        }

        static void ErrorCodeEdges()
        {
            TestHarness.Section("ErrorCode edges");

            var fields = typeof(ErrorCode).GetFields(BindingFlags.Public | BindingFlags.Static);
            var codes = new HashSet<string>(StringComparer.Ordinal);
            var duplicates = new List<string>();
            var missingDescription = new List<string>();

            foreach (var field in fields)
            {
                if (field.FieldType != typeof(string))
                {
                    continue;
                }

                var code = field.GetValue(null) as string;
                if (string.IsNullOrWhiteSpace(code))
                {
                    missingDescription.Add(field.Name);
                    continue;
                }

                if (!codes.Add(code!))
                {
                    duplicates.Add(field.Name + "=" + code);
                    continue;
                }

                if (ErrorCode.GetDescription(code!) == "Unknown error")
                {
                    missingDescription.Add(field.Name);
                }
            }

            TestHarness.Expect(codes.Count > 0, "ErrorCode exposes codes", codes.Count.ToString());
            TestHarness.Expect(duplicates.Count == 0, "ErrorCode values are unique", string.Join(",", duplicates));
            TestHarness.Expect(missingDescription.Count == 0, "every ErrorCode has a description",
                string.Join(",", missingDescription));

            TestHarness.Expect(ErrorCode.RegisterPeerIdMissing == "EP01", "RegisterPeerIdMissing is EP01");
            TestHarness.Expect(ErrorCode.NoCandidatePair == "EI01", "NoCandidatePair is EI01");
            TestHarness.Expect(ErrorCode.IceTcpSimultaneousOpenUnsupported == "EI04", "IceTcpSimultaneousOpenUnsupported is EI04");
            TestHarness.Expect(ErrorCode.TurnAuthFailed == "ET02", "TurnAuthFailed is ET02");
            TestHarness.Expect(ErrorCode.SessionTimeout == "ES05", "SessionTimeout is ES05");
            TestHarness.Expect(ErrorCode.GetDescription(ErrorCode.RegisterPeerIdMissing) == "Peer ID missing in registration request",
                "known description resolved");
            TestHarness.Expect(ErrorCode.GetDescription("NOPE") == "Unknown error", "unknown description fallback");
            TestHarness.Expect(ErrorCode.GetDescription(null!) == "Unknown error", "null description fallback");
        }

        static void SignalingOptionsEdges()
        {
            TestHarness.Section("SignalingOptions validation edges");

            TestHarness.Expect(DoesNotThrow(() => new SignalingOptions { PeerId = "n" }.Validate()),
                "default signaling options with PeerId pass validation");

            TestHarness.Throws<P2PException>(() => new SignalingOptions().Validate(), "missing PeerId throws");
            TestHarness.Throws<P2PException>(() => new SignalingOptions { PeerId = "  " }.Validate(), "blank PeerId throws");
            TestHarness.Throws<P2PException>(() => new SignalingOptions { PeerId = "n", ServerAddress = "  " }.Validate(),
                "blank ServerAddress throws");

            var badPort = TestHarness.Throws<P2PException>(
                () => new SignalingOptions { PeerId = "n", ServerPort = 0 }.Validate(),
                "ServerPort 0 throws");
            TestHarness.Expect(badPort != null && badPort.ErrorCode == ErrorCode.RegisterServerUnavailable,
                "invalid port reports RegisterServerUnavailable", badPort?.ErrorCode);
            TestHarness.Throws<P2PException>(() => new SignalingOptions { PeerId = "n", ServerPort = 70000 }.Validate(),
                "ServerPort above 65535 throws");

            TestHarness.Throws<P2PException>(() => new SignalingOptions { PeerId = "n", ConnectTimeoutMs = 0 }.Validate(),
                "ConnectTimeoutMs 0 throws");
            TestHarness.Throws<P2PException>(() => new SignalingOptions { PeerId = "n", RegisterTimeoutMs = 0 }.Validate(),
                "RegisterTimeoutMs 0 throws");
            TestHarness.Throws<P2PException>(() => new SignalingOptions { PeerId = "n", ReconnectIntervalMs = 0 }.Validate(),
                "ReconnectIntervalMs 0 throws");
            TestHarness.Throws<P2PException>(() => new SignalingOptions { PeerId = "n", HeartbeatIntervalMs = 0 }.Validate(),
                "HeartbeatIntervalMs 0 throws");
            TestHarness.Throws<P2PException>(
                () => new SignalingOptions { PeerId = "n", MaxReconnectAttempts = -1 }.Validate(),
                "negative MaxReconnectAttempts throws");
        }

        static void IceSessionOptionsEdges()
        {
            TestHarness.Section("IceSessionOptions validation edges");

            TestHarness.Expect(DoesNotThrow(() => new IceSessionOptions().Validate()), "default ICE options pass validation");

            var noIce = new IceSessionOptions { Ice = null! };
            TestHarness.Throws<ArgumentException>(() => noIce.Validate(), "null Ice options throw");

            var negativeGathering = new IceSessionOptions { GatheringBudget = TimeSpan.FromSeconds(-1) };
            TestHarness.Throws<ArgumentOutOfRangeException>(() => negativeGathering.Validate(),
                "negative gathering budget throws");

            var zeroUdp = new IceSessionOptions { UdpDirectBudget = TimeSpan.Zero };
            TestHarness.Throws<ArgumentOutOfRangeException>(() => zeroUdp.Validate(), "zero UDP direct budget throws");

            var zeroTcp = new IceSessionOptions { TcpDirectBudget = TimeSpan.Zero };
            TestHarness.Throws<ArgumentOutOfRangeException>(() => zeroTcp.Validate(), "zero TCP direct budget throws");

            var zeroTurn = new IceSessionOptions { TurnStageBudget = TimeSpan.Zero };
            TestHarness.Throws<ArgumentOutOfRangeException>(() => zeroTurn.Validate(), "zero TURN stage budget throws");

            var zeroSession = new IceSessionOptions { SessionBudget = TimeSpan.Zero };
            TestHarness.Throws<ArgumentOutOfRangeException>(() => zeroSession.Validate(), "zero session budget throws");

            var shortSession = new IceSessionOptions { SessionBudget = TimeSpan.FromSeconds(1) };
            var shortError = TestHarness.Throws<ArgumentException>(() => shortSession.Validate(),
                "session budget below stage total throws");
            TestHarness.Expect(shortError != null && shortError.ParamName == nameof(IceSessionOptions.SessionBudget),
                "short session budget points at SessionBudget", shortError?.ParamName);

            var blankTlsHost = new IceSessionOptions { TurnTlsTargetHost = "  " };
            TestHarness.Throws<ArgumentException>(() => blankTlsHost.Validate(), "blank TURN TLS target host throws");
        }

        static void BuilderGuardEdges()
        {
            TestHarness.Section("builder guard edges");

            TestHarness.Throws<P2PException>(() => new P2PClientBuilder().SetNodeId(""), "SetNodeId empty throws");
            TestHarness.Throws<P2PException>(() => new P2PClientBuilder().SetNodeId(new string('x', 65)),
                "SetNodeId longer than 64 chars throws");
            TestHarness.Throws<P2PException>(() => new P2PClientBuilder().SetServer(null!, 1), "SetServer null address throws");
            TestHarness.Throws<P2PException>(() => new P2PClientBuilder().SetServer("h", 0), "SetServer invalid port throws");
            TestHarness.Throws<P2PException>(() => new P2PClientBuilder().SetServer((IPEndPoint)null!),
                "SetServer null endpoint throws");
            TestHarness.Throws<P2PException>(() => new P2PClientBuilder().SetTimeout(0), "SetTimeout 0 throws");
            TestHarness.Throws<ArgumentNullException>(() => new P2PClientBuilder().UseSignaling(null!),
                "UseSignaling null throws");
            TestHarness.Throws<ArgumentNullException>(() => new P2PClientBuilder().UseIce(null!), "UseIce null throws");
            TestHarness.Throws<ArgumentNullException>(() => new P2PClientBuilder().UseTurn(null!), "UseTurn null throws");

            var missingPeer = TestHarness.Throws<P2PException>(() => new P2PClientBuilder().BuildClient(),
                "BuildClient without PeerId throws");
            TestHarness.Expect(missingPeer != null && missingPeer.ErrorCode == ErrorCode.RegisterPeerIdMissing,
                "BuildClient reports RegisterPeerIdMissing", missingPeer?.ErrorCode);

            var badPort = TestHarness.Throws<P2PException>(
                () => new P2PClientBuilder()
                    .UseSignaling(new SignalingOptions { PeerId = "n", ServerPort = 0 })
                    .BuildClient(),
                "BuildClient with ServerPort 0 throws");
            TestHarness.Expect(badPort != null && badPort.ErrorCode == ErrorCode.RegisterServerUnavailable,
                "BuildClient reports RegisterServerUnavailable", badPort?.ErrorCode);

            TestHarness.Throws<ArgumentException>(
                () => new P2PClientBuilder()
                    .UseIce(new IceSessionOptions { SessionBudget = TimeSpan.FromSeconds(1) })
                    .SetNodeId("n")
                    .BuildClient(),
                "BuildClient with invalid ICE budget throws");

            TestHarness.Throws<P2PException>(() => new P2PServerBuilder().SetPort(0), "server SetPort 0 throws");
            TestHarness.Throws<P2PException>(() => new P2PServerBuilder().SetIP("  "), "server SetIP blank throws");
            TestHarness.Throws<P2PException>(() => new P2PServerBuilder().SetMaxNodes(0), "server SetMaxNodes 0 throws");
            TestHarness.Throws<P2PException>(() => new P2PServerBuilder().SetFreeTime(0), "server SetFreeTime 0 throws");
            TestHarness.Throws<P2PException>(() => new P2PServerBuilder().EnableTls("", ""), "server EnableTls empty cert throws");
            TestHarness.Throws<P2PException>(() => new P2PServerBuilder().EnableLogging(2, "  "),
                "server EnableLogging blank path throws");

            TestHarness.Section("P2PQuick edges");

            // 无信令入口恢复后构造即启动发现，这里只校验返回实例与标识，Dispose 负责停止发现
            var localNet = P2PQuick.LocalNet("edge-local");
            try
            {
                TestHarness.Expect(localNet != null && localNet.PeerId == "edge-local",
                    "P2PQuick.LocalNet(nodeId) returns a client with the node id",
                    localNet == null ? "null" : localNet.PeerId);
            }
            finally
            {
                localNet.Dispose();
            }

            var localNetWithDiscovery = P2PQuick.LocalNet("edge-local", 39655, "224.0.0.250");
            try
            {
                TestHarness.Expect(localNetWithDiscovery != null && localNetWithDiscovery.PeerId == "edge-local",
                    "P2PQuick.LocalNet(nodeId, port, multicast) returns a client with the node id",
                    localNetWithDiscovery == null ? "null" : localNetWithDiscovery.PeerId);
            }
            finally
            {
                localNetWithDiscovery.Dispose();
            }
        }
    }
}