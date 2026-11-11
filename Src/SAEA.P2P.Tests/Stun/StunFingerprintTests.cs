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
*文件名： StunFingerprintTests
*版本号： net10.0
*唯一标识：fb061894-035e-4322-880b-89c9d1199436
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 15:50:00
*描述：CRC-32 标准向量与 FINGERPRINT 计算校验验收
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 15:50:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 FINGERPRINT 用例
*
*****************************************************************************/
using System.Collections.Generic;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Common;
using SAEA.P2P.Stun;

namespace SAEA.P2P.Tests.Stun
{
    /// <summary>
    /// CRC-32 与 FINGERPRINT：标准向量、编码后自校验、篡改值与位置非法。
    /// </summary>
    [TestClass]
    public class StunFingerprintTests
    {
        /// <summary>
        /// 固定事务 ID，便于复现
        /// </summary>
        private static readonly byte[] TransactionId =
        {
            0xB7, 0xE7, 0xA7, 0x01, 0xBC, 0x34, 0xD6, 0x86, 0xFA, 0x87, 0xDF, 0xAE
        };

        /// <summary>
        /// CRC-32 对标准字符串 123456789 的结果应为 0xCBF43926
        /// </summary>
        [TestMethod]
        public void Crc32_标准向量_结果匹配()
        {
            var data = Encoding.ASCII.GetBytes("123456789");
            Assert.AreEqual(0xCBF43926u, Crc32.Compute(data), "CRC-32 标准向量不匹配");
        }

        /// <summary>
        /// 编码后 FINGERPRINT 位于末位，且能通过校验
        /// </summary>
        [TestMethod]
        public void StunFingerprint_编码后校验通过()
        {
            var message = new StunMessage(StunMessageType.Binding, StunClass.Request, TransactionId);
            message.SetAttributes(new[] { new StunAttribute(StunAttributeType.Software, Encoding.ASCII.GetBytes("SAEA")) });

            StunFingerprint.Append(message);

            var bytes = message.ToArray();
            Assert.AreEqual(0x80, bytes[bytes.Length - 8], "末位属性类型高字节应为 0x80");
            Assert.AreEqual(0x28, bytes[bytes.Length - 7], "末位属性类型低字节应为 0x28");
            Assert.AreEqual(0x00, bytes[bytes.Length - 6], "FINGERPRINT 长度高字节应为 0");
            Assert.AreEqual(0x04, bytes[bytes.Length - 5], "FINGERPRINT 长度应为 4");

            StunMessage parsed;
            StunParseError error;
            Assert.IsTrue(StunMessage.TryParse(bytes, out parsed, out error), $"解析自有报文失败：{error}");
            Assert.IsTrue(StunFingerprint.TryVerify(parsed), "编码后的 FINGERPRINT 应校验通过");
        }

        /// <summary>
        /// 只改指纹值不改头部长度时必须校验失败，防止把裸 CRC 当成合法指纹
        /// </summary>
        [TestMethod]
        public void StunFingerprint_只改值不改length_校验失败()
        {
            var message = new StunMessage(StunMessageType.Binding, StunClass.Request, TransactionId);
            message.SetAttributes(new[] { new StunAttribute(StunAttributeType.Software, Encoding.ASCII.GetBytes("SAEA")) });
            StunFingerprint.Append(message);

            var bytes = message.ToArray();
            bytes[bytes.Length - 1] ^= 0xFF;

            StunMessage parsed;
            StunParseError error;
            Assert.IsTrue(StunMessage.TryParse(bytes, out parsed, out error), $"解析被篡改的报文失败：{error}");
            Assert.IsFalse(StunFingerprint.TryVerify(parsed), "指纹值被篡改后不应校验通过");
        }

        /// <summary>
        /// FINGERPRINT 不在末位时判为非法
        /// </summary>
        [TestMethod]
        public void StunFingerprint_非末位_判非法()
        {
            var message = new StunMessage(StunMessageType.Binding, StunClass.Request, TransactionId);
            StunFingerprint.Append(message);

            var attributes = new List<StunAttribute>(message.Attributes);
            attributes.Add(StunAttributeCodec.CreateEmpty(StunAttributeType.UseCandidate));
            message.SetAttributes(attributes);

            Assert.IsFalse(StunFingerprint.TryVerify(message), "FINGERPRINT 不在末位时不应校验通过");
        }
    }
}