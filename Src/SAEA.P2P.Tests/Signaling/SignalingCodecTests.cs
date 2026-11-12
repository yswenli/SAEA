/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                               
 *Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.P2P.Tests.Signaling
*文件名： SignalingCodecTests
*版本号： net10.0
*唯一标识：80b6b562-d3ba-4d32-ab21-b0527bbd8e68
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 18:15:22
*描述：信令报文编解码用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 18:15:22
*修改人： yswenli
*版本号： net10.0
*描述：信令报文编解码用例
*
*****************************************************************************/
using System;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.P2P.Common;
using SAEA.P2P.Protocol;
using SAEA.P2P.Signaling;

namespace SAEA.P2P.Tests.Signaling
{
    /// <summary>
    /// 信令报文 JSON 编解码：全消息集往返、应答方角色取值、未知字段忽略、版本与空负载的 Error 兜底。
    /// </summary>
    [TestClass]
    public class SignalingCodecTests
    {
        /// <summary>
        /// 待覆盖的信令消息类型全集，含 0x10/0x11 注册、0x60/0x61 鉴权与 0x90 到 0x97 的会话信令
        /// </summary>
        private static readonly P2PMessageType[] AllMessageTypes =
        {
            P2PMessageType.Register,
            P2PMessageType.RegisterAck,
            P2PMessageType.AuthChallenge,
            P2PMessageType.AuthResponse,
            P2PMessageType.ConnectRequest,
            P2PMessageType.SessionOffer,
            P2PMessageType.SessionAnswer,
            P2PMessageType.TrickleCandidate,
            P2PMessageType.EndOfCandidates,
            P2PMessageType.TurnCredential,
            P2PMessageType.SessionClose,
            P2PMessageType.Error
        };

        /// <summary>
        /// 每种消息填各自代表性字段后编码再解码，类型、协议版本与全部已填字段都应原样还原
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void SignalingCodec_全消息集_编码解码回环()
        {
            foreach (var type in AllMessageTypes)
            {
                var typeName = type.ToString();
                var expected = CreateSample(type);
                var actual = SignalingJsonCodec.Decode(SignalingJsonCodec.Encode(expected));

                Assert.AreEqual(type, actual.Type, "消息类型 " + typeName + " 往返后类型不一致");
                Assert.AreEqual(SignalingJsonCodec.CurrentProtocolVersion, actual.Version,
                    "消息类型 " + typeName + " 往返后协议版本不一致");

                // Error 本身是合法报文，只有其余类型被降级为 Error 才说明解码失败
                if (type != P2PMessageType.Error)
                {
                    Assert.AreNotEqual(P2PMessageType.Error, actual.Type,
                        "消息类型 " + typeName + " 被误判为 Error：" + actual.Code + " " + actual.Message);
                }

                AssertStringField(typeName, "PeerId", expected.PeerId, actual.PeerId);
                AssertStringField(typeName, "NodeName", expected.NodeName, actual.NodeName);
                AssertStringField(typeName, "SessionId", expected.SessionId, actual.SessionId);
                AssertStringField(typeName, "TargetPeerId", expected.TargetPeerId, actual.TargetPeerId);
                AssertStringField(typeName, "Ufrag", expected.Ufrag, actual.Ufrag);
                AssertStringField(typeName, "Pwd", expected.Pwd, actual.Pwd);
                AssertStringField(typeName, "Role", expected.Role, actual.Role);
                AssertStringField(typeName, "Nonce", expected.Nonce, actual.Nonce);
                AssertStringField(typeName, "Response", expected.Response, actual.Response);
                AssertStringField(typeName, "Realm", expected.Realm, actual.Realm);
                AssertStringField(typeName, "Username", expected.Username, actual.Username);
                AssertStringField(typeName, "Password", expected.Password, actual.Password);
                AssertStringField(typeName, "Reason", expected.Reason, actual.Reason);
                AssertStringField(typeName, "Code", expected.Code, actual.Code);
                AssertStringField(typeName, "Message", expected.Message, actual.Message);

                Assert.AreEqual(expected.ServerTime, actual.ServerTime,
                    "消息类型 " + typeName + " 的 ServerTime 往返不一致");
                Assert.AreEqual(expected.Tiebreaker, actual.Tiebreaker,
                    "消息类型 " + typeName + " 的 Tiebreaker 往返不一致");
                Assert.AreEqual(expected.Ttl, actual.Ttl,
                    "消息类型 " + typeName + " 的 Ttl 往返不一致");
                Assert.AreEqual(expected.EndOfCandidates, actual.EndOfCandidates,
                    "消息类型 " + typeName + " 的 EndOfCandidates 往返不一致");

                AssertStringArray(typeName, "Capabilities", expected.Capabilities, actual.Capabilities);
                AssertStringArray(typeName, "Urls", expected.Urls, actual.Urls);
                AssertCandidateArray(typeName, expected.Candidates, actual.Candidates);
            }
        }

        /// <summary>
        /// 应答方必须下发自己的 role 与 tiebreaker，不能回显发起方的值
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void SignalingCodec_SessionAnswer_role为应答方自身值()
        {
            // 发起方取值，用于证明应答报文没有把它回显出去
            var offer = SignalingMessage.Create(P2PMessageType.SessionOffer);
            offer.Role = "controlling";
            offer.Tiebreaker = 0x2222222222222222UL;
            offer.Ufrag = "ufrag-a";

            var answer = SignalingMessage.Create(P2PMessageType.SessionAnswer);
            answer.SessionId = "session-1";
            answer.Role = "controlled";
            answer.Tiebreaker = 0x1111111111111111UL;
            answer.Ufrag = "ufrag-b";
            answer.Pwd = "pwd-b";

            var decoded = SignalingJsonCodec.Decode(SignalingJsonCodec.Encode(answer));

            Assert.AreEqual(P2PMessageType.SessionAnswer, decoded.Type, "应答报文类型被改写");
            Assert.AreEqual(answer.Role, decoded.Role, "应答报文的 Role 应为应答方自身取值");
            Assert.AreEqual(answer.Tiebreaker, decoded.Tiebreaker, "应答报文的 Tiebreaker 应为应答方自身取值");
            Assert.AreEqual(answer.Ufrag, decoded.Ufrag, "应答报文的 Ufrag 应为应答方自身取值");
            Assert.AreEqual(answer.Pwd, decoded.Pwd, "应答报文的 Pwd 应为应答方自身取值");

            Assert.AreNotEqual(offer.Role, decoded.Role, "应答报文回声了发起方角色");
            Assert.AreNotEqual(offer.Tiebreaker, decoded.Tiebreaker, "应答报文回声了发起方决胜值");
            Assert.AreNotEqual(offer.Ufrag, decoded.Ufrag, "应答报文回声了发起方 ufrag");
        }

        /// <summary>
        /// 负载中出现模型未定义的字段与嵌套结构时直接忽略，正常报文不退化为 Error
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void SignalingCodec_未知字段_忽略()
        {
            var json = "{\"version\":1,\"type\":144,\"peerId\":\"peer-a\",\"futureField\":{\"x\":1},\"anotherFuture\":[1,2,3]}";

            var decoded = SignalingJsonCodec.Decode(Encoding.UTF8.GetBytes(json));

            Assert.AreEqual(P2PMessageType.ConnectRequest, decoded.Type, "未知字段导致消息类型解析失败");
            Assert.AreEqual("peer-a", decoded.PeerId, "已定义字段未被解析");
            Assert.AreEqual(1, decoded.Version, "协议版本未被解析");
            Assert.IsNull(decoded.Code, "正常报文被当成了 Error 报文");
        }

        /// <summary>
        /// 协议版本与当前版本不符时返回 Error，错误码为版本不匹配且说明里带实际版本号
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void SignalingCodec_版本不匹配_返回Error()
        {
            var json = "{\"version\":99,\"type\":144,\"peerId\":\"peer-a\"}";

            var decoded = SignalingJsonCodec.Decode(Encoding.UTF8.GetBytes(json));

            Assert.AreEqual(P2PMessageType.Error, decoded.Type, "版本不匹配未返回 Error 报文");
            Assert.AreEqual(ErrorCode.ProtocolVersionMismatch, decoded.Code, "错误码不是版本不匹配");
            Assert.IsNotNull(decoded.Message, "版本不匹配缺少错误说明");
            Assert.IsTrue(decoded.Message.Contains("99"), "错误说明未带实际版本号：" + decoded.Message);
        }

        /// <summary>
        /// null 与零长负载都视为报文不完整，返回格式错误
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void SignalingCodec_空负载_返回ES02()
        {
            var fromNull = SignalingJsonCodec.Decode(null);
            Assert.AreEqual(P2PMessageType.Error, fromNull.Type, "null 负载未返回 Error 报文");
            Assert.AreEqual(ErrorCode.ProtocolMalformed, fromNull.Code, "null 负载的错误码不是格式错误");

            var fromEmpty = SignalingJsonCodec.Decode(new byte[0]);
            Assert.AreEqual(P2PMessageType.Error, fromEmpty.Type, "零长负载未返回 Error 报文");
            Assert.AreEqual(ErrorCode.ProtocolMalformed, fromEmpty.Code, "零长负载的错误码不是格式错误");
        }

        /// <summary>
        /// 按消息类型填一组代表性字段，各类型填不同字段以覆盖报文模型的全部属性
        /// </summary>
        /// <param name="type">消息类型</param>
        /// <returns>填好字段的样例报文</returns>
        private static SignalingMessage CreateSample(P2PMessageType type)
        {
            var message = SignalingMessage.Create(type);
            switch (type)
            {
                case P2PMessageType.Register:
                    message.PeerId = "peer-a";
                    message.NodeName = "node-a";
                    message.Capabilities = new[] { "udp", "tcp", "ipv6", "turn" };
                    break;

                case P2PMessageType.RegisterAck:
                    message.PeerId = "peer-a";
                    message.SessionId = "session-ack";
                    message.ServerTime = new DateTimeOffset(2026, 11, 12, 18, 15, 22, TimeSpan.FromHours(8));
                    break;

                case P2PMessageType.AuthChallenge:
                    message.PeerId = "peer-a";
                    message.Nonce = "nonce-1234";
                    message.SessionId = "session-auth";
                    break;

                case P2PMessageType.AuthResponse:
                    message.PeerId = "peer-a";
                    message.Nonce = "nonce-1234";
                    message.Response = "response-digest";
                    break;

                case P2PMessageType.ConnectRequest:
                    message.PeerId = "peer-a";
                    message.TargetPeerId = "peer-b";
                    message.SessionId = "session-1";
                    break;

                case P2PMessageType.SessionOffer:
                    message.PeerId = "peer-a";
                    message.TargetPeerId = "peer-b";
                    message.SessionId = "session-1";
                    message.Ufrag = "ufrag-a";
                    message.Pwd = "pwd-a";
                    message.Role = "controlling";
                    message.Tiebreaker = 0x1111222233334444UL;
                    message.Candidates = CreateSampleCandidates();
                    message.EndOfCandidates = false;
                    break;

                case P2PMessageType.SessionAnswer:
                    message.PeerId = "peer-b";
                    message.TargetPeerId = "peer-a";
                    message.SessionId = "session-1";
                    message.Ufrag = "ufrag-b";
                    message.Pwd = "pwd-b";
                    message.Role = "controlled";
                    message.Tiebreaker = 0x5555666677778888UL;
                    message.Candidates = new[] { CreateUdpCandidate() };
                    message.EndOfCandidates = true;
                    break;

                case P2PMessageType.TrickleCandidate:
                    message.SessionId = "session-1";
                    message.Ufrag = "ufrag-a";
                    message.Candidates = new[] { CreateTcpCandidate() };
                    message.EndOfCandidates = false;
                    break;

                case P2PMessageType.EndOfCandidates:
                    message.SessionId = "session-1";
                    message.Ufrag = "ufrag-a";
                    message.EndOfCandidates = true;
                    break;

                case P2PMessageType.TurnCredential:
                    message.SessionId = "session-1";
                    message.Realm = "saea.turn";
                    message.Username = "1717171717:peer-a";
                    message.Password = "turn-secret";
                    message.Ttl = 600;
                    message.Urls = new[] { "turn:turn.example.com:3478?transport=udp", "turn:turn.example.com:3478?transport=tcp" };
                    break;

                case P2PMessageType.SessionClose:
                    message.PeerId = "peer-a";
                    message.SessionId = "session-1";
                    message.Reason = "peer hangup";
                    break;

                case P2PMessageType.Error:
                    message.PeerId = "peer-a";
                    message.SessionId = "session-1";
                    message.Code = ErrorCode.ProtocolMalformed;
                    message.Message = "信令负载无法解析";
                    break;

                default:
                    Assert.Fail("未覆盖的消息类型：" + type);
                    break;
            }

            return message;
        }

        /// <summary>
        /// 构造两条候选：一条 TCP passive 候选带关联地址，一条 UDP 候选无关联地址
        /// </summary>
        /// <returns>候选数组</returns>
        private static SignalingCandidate[] CreateSampleCandidates()
        {
            return new[] { CreateTcpCandidate(), CreateUdpCandidate() };
        }

        /// <summary>
        /// 构造带关联地址的 TCP passive 候选
        /// </summary>
        /// <returns>候选</returns>
        private static SignalingCandidate CreateTcpCandidate()
        {
            return new SignalingCandidate
            {
                Foundation = "2",
                Component = 1,
                Transport = "tcp",
                Priority = 1677729535,
                Address = "192.0.2.20",
                Port = 9,
                Type = "host",
                RelatedAddress = "192.0.2.10",
                RelatedPort = 50000,
                TcpType = "passive"
            };
        }

        /// <summary>
        /// 构造无关联地址的 UDP 候选
        /// </summary>
        /// <returns>候选</returns>
        private static SignalingCandidate CreateUdpCandidate()
        {
            return new SignalingCandidate
            {
                Foundation = "1",
                Component = 1,
                Transport = "udp",
                Priority = 2130706431,
                Address = "192.0.2.10",
                Port = 50000,
                Type = "host",
                RelatedAddress = null,
                RelatedPort = 0,
                TcpType = null
            };
        }

        /// <summary>
        /// 断言字符串字段往返一致，未填写的字段两侧都应为 null
        /// </summary>
        /// <param name="typeName">消息类型名，用于失败文案</param>
        /// <param name="fieldName">字段名</param>
        /// <param name="expected">编码前取值</param>
        /// <param name="actual">解码后取值</param>
        private static void AssertStringField(string typeName, string fieldName, string expected, string actual)
        {
            Assert.AreEqual(expected, actual, "消息类型 " + typeName + " 的 " + fieldName + " 往返不一致");
        }

        /// <summary>
        /// 断言字符串数组字段往返一致，未填写的字段两侧都应为 null
        /// </summary>
        /// <param name="typeName">消息类型名，用于失败文案</param>
        /// <param name="fieldName">字段名</param>
        /// <param name="expected">编码前取值</param>
        /// <param name="actual">解码后取值</param>
        private static void AssertStringArray(string typeName, string fieldName, string[] expected, string[] actual)
        {
            if (expected == null)
            {
                Assert.IsNull(actual, "消息类型 " + typeName + " 的 " + fieldName + " 应为 null");
                return;
            }

            CollectionAssert.AreEqual(expected, actual, "消息类型 " + typeName + " 的 " + fieldName + " 往返不一致");
        }

        /// <summary>
        /// 断言候选数组逐项往返一致，未填写候选的两个报文都应为 null
        /// </summary>
        /// <param name="typeName">消息类型名，用于失败文案</param>
        /// <param name="expected">编码前候选</param>
        /// <param name="actual">解码后候选</param>
        private static void AssertCandidateArray(string typeName, SignalingCandidate[] expected, SignalingCandidate[] actual)
        {
            if (expected == null)
            {
                Assert.IsNull(actual, "消息类型 " + typeName + " 的 Candidates 应为 null");
                return;
            }

            Assert.AreEqual(expected.Length, actual.Length, "消息类型 " + typeName + " 的候选数量往返不一致");
            for (var i = 0; i < expected.Length; i++)
            {
                var prefix = "消息类型 " + typeName + " 的第 " + i + " 条候选";
                Assert.AreEqual(expected[i].Foundation, actual[i].Foundation, prefix + " Foundation 往返不一致");
                Assert.AreEqual(expected[i].Component, actual[i].Component, prefix + " Component 往返不一致");
                Assert.AreEqual(expected[i].Transport, actual[i].Transport, prefix + " Transport 往返不一致");
                Assert.AreEqual(expected[i].Priority, actual[i].Priority, prefix + " Priority 往返不一致");
                Assert.AreEqual(expected[i].Address, actual[i].Address, prefix + " Address 往返不一致");
                Assert.AreEqual(expected[i].Port, actual[i].Port, prefix + " Port 往返不一致");
                Assert.AreEqual(expected[i].Type, actual[i].Type, prefix + " Type 往返不一致");
                Assert.AreEqual(expected[i].RelatedAddress, actual[i].RelatedAddress, prefix + " RelatedAddress 往返不一致");
                Assert.AreEqual(expected[i].RelatedPort, actual[i].RelatedPort, prefix + " RelatedPort 往返不一致");
                Assert.AreEqual(expected[i].TcpType, actual[i].TcpType, prefix + " TcpType 往返不一致");
            }
        }
    }
}