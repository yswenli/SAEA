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
*文件名： StunHeaderTests
*版本号： net10.0
*唯一标识：9eeec4cd-bbe8-4968-a8dc-4c19437443f1
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 15:05:00
*描述：STUN 消息头编解码与解析失败原因验收
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 18:20:00
*修改人： yswenli
*版本号： net10.0
*描述：补充四种类别编码解码互逆的回归用例
*
*****************************************************************************/
using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Stun;

namespace SAEA.P2P.Tests.Stun
{
    /// <summary>
    /// STUN 消息头：类型字段编码、解析前校验与失败原因。
    /// </summary>
    [TestClass]
    public class StunHeaderTests
    {
        /// <summary>
        /// RFC 5769 2.1 样例请求使用的事务 ID
        /// </summary>
        private static readonly byte[] Rfc5769TransactionId =
        {
            0xB7, 0xE7, 0xA7, 0x01, 0xBC, 0x34, 0xD6, 0x86, 0xFA, 0x87, 0xDF, 0xAE
        };

        /// <summary>
        /// 方法与类别按公式编码后，头部应与 RFC 5769 样例请求逐字节一致
        /// </summary>
        [TestMethod]
        public void StunMessage_方法类组合编码_与RFC5769请求头一致()
        {
            var message = new StunMessage(StunMessageType.Binding, StunClass.Request, Rfc5769TransactionId);
            var encoded = message.ToArray();

            var expected = new byte[]
            {
                0x00, 0x01, 0x00, 0x00, 0x21, 0x12, 0xA4, 0x42,
                0xB7, 0xE7, 0xA7, 0x01, 0xBC, 0x34, 0xD6, 0x86, 0xFA, 0x87, 0xDF, 0xAE
            };
            CollectionAssert.AreEqual(expected, encoded, "Binding Request 头部编码与 RFC 5769 不一致");

            // 解析回环：方法、类别与事务 ID 都必须还原
            StunMessage parsed;
            StunParseError error;
            Assert.IsTrue(StunMessage.TryParse(encoded, out parsed, out error), $"解析自有报文失败：{error}");
            Assert.AreEqual(StunMessageType.Binding, parsed.Method);
            Assert.AreEqual(StunClass.Request, parsed.Class);
            CollectionAssert.AreEqual(Rfc5769TransactionId, parsed.TransactionId.ToArray());

            // 0x0101 是绑定成功应答、0x0111 是绑定错误应答，用于校验类别位的拆分
            Assert.AreEqual(StunMessageType.Binding, StunMessage.DecodeMethod(0x0101));
            Assert.AreEqual(StunClass.SuccessResponse, StunMessage.DecodeClass(0x0101));
            Assert.AreEqual(StunMessageType.Binding, StunMessage.DecodeMethod(0x0111));
            Assert.AreEqual(StunClass.ErrorResponse, StunMessage.DecodeClass(0x0111));
            Assert.AreEqual(0x0101, StunMessage.EncodeType(StunMessageType.Binding, StunClass.SuccessResponse));
        }

        /// <summary>
        /// 四种方法与类别组合都必须编码解码互逆；错误应答的类别低位要落在第 4 位
        /// </summary>
        [TestMethod]
        public void StunMessage_四种类别_编码解码互逆()
        {
            var cases = new[]
            {
                new { Class = StunClass.Request, Type = (ushort)0x0001 },
                new { Class = StunClass.Indication, Type = (ushort)0x0011 },
                new { Class = StunClass.SuccessResponse, Type = (ushort)0x0101 },
                new { Class = StunClass.ErrorResponse, Type = (ushort)0x0111 }
            };

            foreach (var item in cases)
            {
                var encoded = StunMessage.EncodeType(StunMessageType.Binding, item.Class);
                Assert.AreEqual(item.Type, encoded, item.Class + " 的类型字段编码错误");
                Assert.AreEqual(StunMessageType.Binding, StunMessage.DecodeMethod(encoded), item.Class + " 的方法解码错误");
                Assert.AreEqual(item.Class, StunMessage.DecodeClass(encoded), item.Class + " 的类别解码错误");
            }
        }

        /// <summary>
        /// 头部声明长度与实际字节数不符时返回失败原因，不抛异常
        /// </summary>
        [TestMethod]
        public void StunMessage_解析长度不匹配_返回失败不抛异常()
        {
            // 头部声明还有 4 字节属性，但报文里没有
            var header = BuildHeader(0x0001, 4);

            StunMessage message;
            StunParseError error;
            var ok = StunMessage.TryParse(header, out message, out error);

            Assert.IsFalse(ok, "长度不匹配的报文不应解析成功");
            Assert.IsNull(message, "解析失败时不应产出消息对象");
            Assert.AreEqual(StunParseError.LengthMismatch, error, "失败原因应为长度不匹配");
        }

        /// <summary>
        /// 消息类型最高两位非零的报文必须拒绝
        /// </summary>
        [TestMethod]
        public void StunMessage_最高位非零_拒绝解析()
        {
            var header = BuildHeader(0x8001, 0);

            StunMessage message;
            StunParseError error;
            var ok = StunMessage.TryParse(header, out message, out error);

            Assert.IsFalse(ok, "最高位非零的报文不应解析成功");
            Assert.AreEqual(StunParseError.TopBitsNotZero, error, "失败原因应为最高位非零");
        }

        /// <summary>
        /// 按给定类型与长度拼装一个 20 字节合法头部
        /// </summary>
        /// <param name="type">消息类型字段</param>
        /// <param name="length">头部声明长度</param>
        /// <returns>20 字节头部</returns>
        private static byte[] BuildHeader(ushort type, ushort length)
        {
            var header = new byte[StunConstants.HeaderLength];
            header[0] = (byte)(type >> 8);
            header[1] = (byte)type;
            header[2] = (byte)(length >> 8);
            header[3] = (byte)length;
            header[4] = 0x21;
            header[5] = 0x12;
            header[6] = 0xA4;
            header[7] = 0x42;
            Buffer.BlockCopy(Rfc5769TransactionId, 0, header, 8, StunConstants.TransactionIdLength);
            return header;
        }
    }
}