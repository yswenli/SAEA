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
*命名空间：SAEA.P2P.Tests.Discovery
*文件名： DiscoveryPacketTests
*版本号： net10.0
*唯一标识：f77168af-41e4-4833-b592-1f3b898eb408
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 16:59:24
*描述：无信令局域网发现报文编解码与 HMAC 用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 16:59:24
*修改人： yswenli
*版本号： net10.0
*描述：新增无信令局域网发现报文编解码与 HMAC 用例
*
*****************************************************************************/
using System;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.P2P.Discovery;
using SAEA.P2P.Signaling;
using SAEA.P2P.Transport;

namespace SAEA.P2P.Tests.Discovery
{
    /// <summary>
    /// 无信令局域网发现报文：签名后的编码解码回环、HMAC 不匹配拒收与超长负载丢弃。
    /// </summary>
    [TestCategory(P2PTestSuite.Unit)]
    [TestClass]
    public class DiscoveryPacketTests
    {
        /// <summary>
        /// 测试用共享密钥
        /// </summary>
        private const string SharedKey = "discovery-test-key";

        /// <summary>
        /// 全字段报文签名后编码再解码，版本、节点信息、ICE 参数与候选都应原样还原，且 HMAC 通过校验
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void DiscoveryPacket_编码解码回环_字段完整()
        {
            var expected = CreateSample();

            var payload = DiscoveryJsonCodec.Encode(expected, SharedKey);

            DiscoveryPacket actual;
            string error;
            var ok = DiscoveryJsonCodec.TryDecode(payload, SharedKey, out actual, out error);

            Assert.IsTrue(ok, "签名报文解码失败：" + error);
            Assert.IsNotNull(actual, "解码结果不应为 null");
            Assert.AreEqual(DiscoveryJsonCodec.CurrentProtocolVersion, actual.Version, "协议版本不一致");
            Assert.AreEqual(expected.PeerId, actual.PeerId, "节点标识不一致");
            Assert.AreEqual(expected.NodeName, actual.NodeName, "节点名称不一致");
            Assert.AreEqual(expected.Ufrag, actual.Ufrag, "ICE 用户名片段不一致");
            Assert.AreEqual(expected.Pwd, actual.Pwd, "ICE 口令不一致");
            Assert.AreEqual(expected.Role, actual.Role, "ICE 角色不一致");
            Assert.AreEqual(expected.Tiebreaker, actual.Tiebreaker, "角色裁决值不一致");
            Assert.AreEqual(expected.TimestampUnixMs, actual.TimestampUnixMs, "时间戳不一致");
            Assert.AreEqual(expected.Hmac, actual.Hmac, "HMAC 标签不一致");
            Assert.IsNotNull(actual.Candidates, "候选列表不应丢失");

            // 候选是报文里唯一的多值结构，逐项核对，避免只验长度漏掉字段错位
            Assert.AreEqual(expected.Candidates.Length, actual.Candidates.Length, "候选数量不一致");
            for (var i = 0; i < expected.Candidates.Length; i++)
            {
                var left = expected.Candidates[i];
                var right = actual.Candidates[i];
                Assert.AreEqual(left.Foundation, right.Foundation, "候选 " + i + " 基础标识不一致");
                Assert.AreEqual(left.Component, right.Component, "候选 " + i + " 分量不一致");
                Assert.AreEqual(left.Transport, right.Transport, "候选 " + i + " 传输协议不一致");
                Assert.AreEqual(left.Priority, right.Priority, "候选 " + i + " 优先级不一致");
                Assert.AreEqual(left.Address, right.Address, "候选 " + i + " 地址不一致");
                Assert.AreEqual(left.Port, right.Port, "候选 " + i + " 端口不一致");
                Assert.AreEqual(left.Type, right.Type, "候选 " + i + " 类型不一致");
                Assert.AreEqual(left.RelatedAddress, right.RelatedAddress, "候选 " + i + " 关联地址不一致");
                Assert.AreEqual(left.RelatedPort, right.RelatedPort, "候选 " + i + " 关联端口不一致");
                Assert.AreEqual(left.TcpType, right.TcpType, "候选 " + i + " TCP 类型不一致");
            }
        }

        /// <summary>
        /// 用另一把密钥校验，或把签名后的负载改掉一个字段，都应因 HMAC 不匹配被拒收
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void DiscoveryPacket_HMAC不匹配_拒绝()
        {
            var payload = DiscoveryJsonCodec.Encode(CreateSample(), SharedKey);

            DiscoveryPacket packet;
            string error;
            var wrongKeyAccepted = DiscoveryJsonCodec.TryDecode(payload, "another-key", out packet, out error);

            Assert.IsFalse(wrongKeyAccepted, "错误的密钥不应通过校验");
            Assert.IsNull(packet, "校验失败时不应返回报文");
            Assert.IsTrue(error != null && error.Contains("HMAC"), "失败原因应说明 HMAC 校验失败，实际：" + error);

            // 签名覆盖的是 Hmac 置空后的规范负载，改任一业务字段都必须导致重算结果不同
            var text = Encoding.UTF8.GetString(payload);
            var tamperedText = text.Replace("\"peer-a\"", "\"peer-z\"");
            Assert.AreNotEqual(text, tamperedText, "篡改未生效，用例前提不成立");

            var tamperedAccepted = DiscoveryJsonCodec.TryDecode(
                Encoding.UTF8.GetBytes(tamperedText), SharedKey, out packet, out error);

            Assert.IsFalse(tamperedAccepted, "内容被篡改的负载不应通过校验");
            Assert.IsNull(packet, "篡改负载校验失败时不应返回报文");
        }

        /// <summary>
        /// 超过 UDP 单包上限的负载在解析前就被丢弃，且不抛异常
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void DiscoveryPacket_超长_丢弃()
        {
            var oversized = new byte[DataPlaneCodec.MaxUdpDatagramLength + 1];

            DiscoveryPacket packet;
            string error;
            var accepted = DiscoveryJsonCodec.TryDecode(oversized, out packet, out error);

            Assert.IsFalse(accepted, "超长负载不应被接受");
            Assert.IsNull(packet, "超长负载不应产出报文");
            Assert.IsTrue(error != null && error.Contains("单包上限"), "失败原因应说明超长，实际：" + error);
        }

        /// <summary>
        /// 构造覆盖全部字段的发现报文，候选同时包含 UDP 与 TCP 两类
        /// </summary>
        /// <returns>样例报文</returns>
        private static DiscoveryPacket CreateSample()
        {
            return new DiscoveryPacket
            {
                Version = DiscoveryJsonCodec.CurrentProtocolVersion,
                PeerId = "peer-a",
                NodeName = "节点甲",
                Ufrag = "ufrag-a",
                Pwd = "pwd-a",
                Role = "controlling",
                Tiebreaker = 18446744073709551615UL,
                TimestampUnixMs = 1763024364000,
                Candidates = new[]
                {
                    new SignalingCandidate
                    {
                        Foundation = "1",
                        Component = 1,
                        Transport = "udp",
                        Priority = 2130706431,
                        Address = "192.168.1.10",
                        Port = 50000,
                        Type = "host",
                        RelatedAddress = null,
                        RelatedPort = 0,
                        TcpType = null
                    },
                    new SignalingCandidate
                    {
                        Foundation = "2",
                        Component = 1,
                        Transport = "tcp",
                        Priority = 16777215,
                        Address = "192.168.1.10",
                        Port = 0,
                        Type = "host",
                        RelatedAddress = "192.168.1.10",
                        RelatedPort = 50001,
                        TcpType = "active"
                    }
                }
            };
        }
    }
}