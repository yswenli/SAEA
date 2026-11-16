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
*文件名： StunIntegrityTests
*版本号： net10.0
*唯一标识：36d6dd18-ba7a-41ae-8d61-3cc9a41566e7
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 16:05:00
*描述：MESSAGE-INTEGRITY 编码校验与密码算法协商验收
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 16:05:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 MESSAGE-INTEGRITY 用例
*
*****************************************************************************/
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Stun;

namespace SAEA.P2P.Tests.Stun
{
    /// <summary>
    /// MESSAGE-INTEGRITY：SHA-1 编码自校验、长度未回填的失败路径、尾随指纹与算法协商。
    /// </summary>
    [TestCategory(P2PTestSuite.Unit)]
    [TestClass]
    public class StunIntegrityTests
    {
        /// <summary>
        /// 固定事务 ID，便于复现
        /// </summary>
        private static readonly byte[] TransactionId =
        {
            0xB7, 0xE7, 0xA7, 0x01, 0xBC, 0x34, 0xD6, 0x86, 0xFA, 0x87, 0xDF, 0xAE
        };

        /// <summary>
        /// 测试用短凭据密钥
        /// </summary>
        private static readonly byte[] Key = Encoding.ASCII.GetBytes("VOkJxbRl1RmTxUk/WvJxBt");

        /// <summary>
        /// SHA-1 编码后应能通过校验，篡改属性值后校验失败
        /// </summary>
        [TestMethod]
        public void StunIntegrity_SHA1_编码后校验通过()
        {
            var message = new StunMessage(StunMessageType.Binding, StunClass.Request, TransactionId);
            message.SetAttributes(new[]
            {
                new StunAttribute(StunAttributeType.Software, Encoding.ASCII.GetBytes("SAEA")),
                StunAttributeCodec.CreateUInt32(StunAttributeType.Priority, 0x6E0000FF)
            });

            StunIntegrity.Append(message, Key);

            Assert.AreEqual(3, message.Attributes.Count, "编码后应追加一个 MESSAGE-INTEGRITY 属性");
            Assert.AreEqual((ushort)StunAttributeType.MessageIntegrity, message.Attributes[2].Type, "追加的属性应为 MESSAGE-INTEGRITY");
            Assert.AreEqual(StunIntegrity.Sha1ValueLength, message.Attributes[2].Value.Length, "SHA-1 摘要应为 20 字节");
            Assert.IsTrue(StunIntegrity.TryVerify(message, Key), "编码后的 MESSAGE-INTEGRITY 应校验通过");

            var tampered = new List<StunAttribute>(message.Attributes);
            var value = tampered[2].Value.ToArray();
            value[0] ^= 0xFF;
            tampered[2] = new StunAttribute(StunAttributeType.MessageIntegrity, value);
            message.SetAttributes(tampered);

            Assert.IsFalse(StunIntegrity.TryVerify(message, Key), "摘要被篡改后不应校验通过");
        }

        /// <summary>
        /// 头部长度不回填时，按未回填长度算出的摘要必须被拒
        /// </summary>
        [TestMethod]
        public void StunIntegrity_length未回填_校验失败()
        {
            var message = new StunMessage(StunMessageType.Binding, StunClass.Request, TransactionId);
            message.SetAttributes(new[] { new StunAttribute(StunAttributeType.Software, Encoding.ASCII.GetBytes("SAEA")) });

            // 模拟错误的编码方式：直接把不含 MESSAGE-INTEGRITY 的报文拿去算 HMAC，不回填头部长度
            byte[] buggyDigest;
            using (var hmac = new HMACSHA1(Key))
            {
                buggyDigest = hmac.ComputeHash(message.ToArray());
            }

            var attributes = new List<StunAttribute>(message.Attributes)
            {
                new StunAttribute(StunAttributeType.MessageIntegrity, buggyDigest)
            };
            message.SetAttributes(attributes);

            Assert.IsFalse(StunIntegrity.TryVerify(message, Key), "长度未回填的摘要不应校验通过");
        }

        /// <summary>
        /// MESSAGE-INTEGRITY 之后还有 FINGERPRINT 时，完整性校验仍应按该属性之前的前缀计算
        /// </summary>
        [TestMethod]
        public void StunIntegrity_尾部有指纹_仍可校验()
        {
            var message = new StunMessage(StunMessageType.Binding, StunClass.Request, TransactionId);
            message.SetAttributes(new[] { new StunAttribute(StunAttributeType.Software, Encoding.ASCII.GetBytes("SAEA")) });

            StunIntegrity.Append(message, Key);
            StunFingerprint.Append(message);

            Assert.AreEqual((ushort)StunAttributeType.Fingerprint, message.Attributes[message.Attributes.Count - 1].Type, "FINGERPRINT 应位于末位");
            Assert.IsTrue(StunIntegrity.TryVerify(message, Key), "尾部带指纹时 MESSAGE-INTEGRITY 仍应校验通过");
            Assert.IsTrue(StunFingerprint.TryVerify(message), "同时编码的 FINGERPRINT 应校验通过");
        }

        /// <summary>
        /// 对端只声明 SHA-256 时必须明确表示不支持，不能静默按 MD5 继续
        /// </summary>
        [TestMethod]
        public void StunIntegrity_仅支持SHA256的对端_返回明确不支持()
        {
            var message = new StunMessage(StunMessageType.Binding, StunClass.ErrorResponse, TransactionId);
            message.SetAttributes(new[]
            {
                StunIntegrity.CreatePasswordAlgorithms(new[] { StunPasswordAlgorithm.Sha256 })
            });

            IReadOnlyList<StunPasswordAlgorithm> offered;
            Assert.IsTrue(StunIntegrity.TryGetPasswordAlgorithms(message, out offered), "应能解析 PASSWORD-ALGORITHMS");
            Assert.AreEqual(1, offered.Count, "算法列表应有一项");
            Assert.AreEqual(StunPasswordAlgorithm.Sha256, offered[0], "解析出的算法应为 SHA-256");

            StunPasswordAlgorithm selected;
            Assert.IsFalse(StunIntegrity.TrySelectPasswordAlgorithm(offered, out selected), "只有 SHA-256 时应返回不支持");
        }
    }
}