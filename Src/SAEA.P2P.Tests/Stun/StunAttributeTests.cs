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
*命名空间：SAEA.P2P.Tests.Stun
*文件名： StunAttributeTests
*版本号： net10.0
*唯一标识：301302e2-5682-435d-a51e-6cd94c53f01c
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 15:30:00
*描述：STUN 属性 TLV、未知属性与地址属性编解码验收
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 15:30:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 STUN 属性用例
*
*****************************************************************************/
using System;
using System.Net;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Stun;

namespace SAEA.P2P.Tests.Stun
{
    /// <summary>
    /// STUN 属性：补齐规则、未知必选与可选属性处理、异或地址规范化、重复属性顺序。
    /// </summary>
    [TestClass]
    public class StunAttributeTests
    {
        /// <summary>
        /// RFC 5769 2.1 样例请求使用的事务 ID
        /// </summary>
        private static readonly byte[] Rfc5769TransactionId =
        {
            0xB7, 0xE7, 0xA7, 0x01, 0xBC, 0x34, 0xD6, 0x86, 0xFA, 0x87, 0xDF, 0xAE
        };

        /// <summary>
        /// 值长度不是 4 的倍数时要补零，属性头里的长度仍是未补齐的值长度，消息头长度含补齐
        /// </summary>
        [TestMethod]
        public void StunAttribute_值长度非4倍数_补齐且length含补齐()
        {
            var attribute = new StunAttribute(StunAttributeType.Software, new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 });
            var encoded = StunAttributeCodec.Encode(attribute);

            Assert.AreEqual(12, encoded.Length, "5 字节值的属性应占 4 + 8 字节");
            Assert.AreEqual(5, (encoded[2] << 8) | encoded[3], "属性头长度应为未补齐的值长度");
            Assert.AreEqual(0, encoded[9], "第一个补齐字节应为零");
            Assert.AreEqual(0, encoded[10], "补齐字节应为零");
            Assert.AreEqual(0, encoded[11], "补齐字节应为零");

            var message = new StunMessage(StunMessageType.Binding, StunClass.Request, Rfc5769TransactionId);
            message.SetAttributes(new[] { attribute });
            var bytes = message.ToArray();

            Assert.AreEqual(12, (bytes[2] << 8) | bytes[3], "消息头声明长度应含补齐字节");
            Assert.AreEqual(32, bytes.Length, "整包长度应为 20 + 12");
        }

        /// <summary>
        /// 不认识的必选属性要标记出来，供服务端回 420
        /// </summary>
        [TestMethod]
        public void StunMessage_未知必选属性_返回需420()
        {
            var unknown = new StunAttribute(0x7FFF, new byte[0]);
            var message = new StunMessage(StunMessageType.Binding, StunClass.Request, Rfc5769TransactionId);
            message.SetAttributes(new[] { unknown });

            StunMessage parsed;
            StunParseError error;
            Assert.IsTrue(StunMessage.TryParse(message.ToArray(), out parsed, out error), $"解析自有报文失败：{error}");

            Assert.IsTrue(parsed.HasUnknownComprehensionRequired, "0x7FFF 是未知必选属性，应要求回 420");
            Assert.AreEqual(1, parsed.UnknownComprehensionRequiredTypes.Count, "未知必选属性列表长度应为 1");
            Assert.AreEqual((ushort)0x7FFF, parsed.UnknownComprehensionRequiredTypes[0], "未知必选属性类型应原样保留");
            Assert.AreEqual(1, parsed.Attributes.Count, "未知属性不得在解析阶段被丢弃");
        }

        /// <summary>
        /// 不认识的可选属性应原样保留，且不触发 420
        /// </summary>
        [TestMethod]
        public void StunMessage_未知可选属性_原样保留()
        {
            var unknown = new StunAttribute(0xC001, new byte[] { 0x01, 0x02, 0x03 });
            var message = new StunMessage(StunMessageType.Binding, StunClass.Request, Rfc5769TransactionId);
            message.SetAttributes(new[] { unknown });

            StunMessage parsed;
            StunParseError error;
            Assert.IsTrue(StunMessage.TryParse(message.ToArray(), out parsed, out error), $"解析自有报文失败：{error}");

            Assert.IsFalse(parsed.HasUnknownComprehensionRequired, "可选属性的最高位为 1，不应要求回 420");
            Assert.AreEqual(1, parsed.Attributes.Count, "未知可选属性应保留");
            Assert.AreEqual((ushort)0xC001, parsed.Attributes[0].Type, "属性类型应原样保留");
            CollectionAssert.AreEqual(new byte[] { 0x01, 0x02, 0x03 }, parsed.Attributes[0].Value.ToArray(), "属性值应原样保留");
        }

        /// <summary>
        /// v4-mapped v6 地址按 IPv4 编码，异或结果与 RFC 5769 的样例值一致
        /// </summary>
        [TestMethod]
        public void XorMappedAddress_v4映射v6_编码为v4()
        {
            var endPoint = new IPEndPoint(IPAddress.Parse("::ffff:192.0.2.1"), 32853);
            var attribute = StunAddressCodec.EncodeAddress(StunAttributeType.XorMappedAddress, endPoint, true, Rfc5769TransactionId);

            var expected = new byte[] { 0x00, 0x01, 0xA1, 0x47, 0xE1, 0x12, 0xA6, 0x43 };
            CollectionAssert.AreEqual(expected, attribute.Value.ToArray(), "异或地址编码与 RFC 5769 样例不一致");

            IPEndPoint decoded;
            Assert.IsTrue(StunAddressCodec.TryDecodeAddress(attribute, Rfc5769TransactionId, out decoded), "异或地址应能解码");
            Assert.AreEqual(IPAddress.Parse("192.0.2.1"), decoded.Address, "v4-mapped v6 解码后应为 IPv4");
            Assert.AreEqual(32853, decoded.Port, "端口解码错误");
        }

        /// <summary>
        /// 同一属性重复出现时，GetAll 按报文顺序返回全部，TryGet 返回第一个
        /// </summary>
        [TestMethod]
        public void StunMessage_重复属性_按顺序返回全部()
        {
            var first = new StunAttribute(StunAttributeType.Software, new byte[] { 0x61, 0x62, 0x63 });
            var second = new StunAttribute(StunAttributeType.Software, new byte[] { 0x64, 0x65, 0x66 });

            var message = new StunMessage(StunMessageType.Binding, StunClass.Request, Rfc5769TransactionId);
            message.SetAttributes(new[] { first, second });

            StunMessage parsed;
            StunParseError error;
            Assert.IsTrue(StunMessage.TryParse(message.ToArray(), out parsed, out error), $"解析自有报文失败：{error}");

            var all = parsed.GetAll(StunAttributeType.Software);
            Assert.AreEqual(2, all.Count, "两个 SOFTWARE 属性都应返回");
            CollectionAssert.AreEqual(new byte[] { 0x61, 0x62, 0x63 }, all[0].Value.ToArray(), "第一个属性顺序错误");
            CollectionAssert.AreEqual(new byte[] { 0x64, 0x65, 0x66 }, all[1].Value.ToArray(), "第二个属性顺序错误");

            StunAttribute single;
            Assert.IsTrue(parsed.TryGet(StunAttributeType.Software, out single), "TryGet 应命中 SOFTWARE");
            CollectionAssert.AreEqual(new byte[] { 0x61, 0x62, 0x63 }, single.Value.ToArray(), "TryGet 应返回第一个属性");
        }
    }
}