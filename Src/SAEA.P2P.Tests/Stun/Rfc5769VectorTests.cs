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
*文件名： Rfc5769VectorTests
*版本号： net10.0
*唯一标识：8e3a6738-ec48-48d0-8105-b8b295767517
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 17:00:00
*描述：RFC 5769 四个官方样例向量逐字节验收
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 17:00:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 RFC 5769 向量用例
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Stun;

namespace SAEA.P2P.Tests.Stun
{
    /// <summary>
    /// RFC 5769 官方样例：解码回环、MESSAGE-INTEGRITY 与 FINGERPRINT 校验，以及用本实现重新编码后的逐字节比对。
    /// </summary>
    [TestClass]
    public class Rfc5769VectorTests
    {
        /// <summary>
        /// 短凭据口令，四个样例中前三个共用
        /// </summary>
        private const string ShortTermPassword = "VOkJxbRl1RmTxUk/WvJxBt";

        /// <summary>
        /// RFC 5769 2.1 样例请求
        /// </summary>
        private const string SampleRequestHex = @"
            00 01 00 58 21 12 A4 42 B7 E7 A7 01 BC 34 D6 86 FA 87 DF AE
            80 22 00 10 53 54 55 4E 20 74 65 73 74 20 63 6C 69 65 6E 74
            00 24 00 04 6E 00 01 FF
            80 29 00 08 93 2F F9 B1 51 26 3B 36
            00 06 00 09 65 76 74 6A 3A 68 36 76 59 20 20 20
            00 08 00 14 9A EA A7 0C BF D8 CB 56 78 1E F2 B5 B2 D3 F2 49 C1 B5 71 A2
            80 28 00 04 E5 7A 3B CF";

        /// <summary>
        /// RFC 5769 2.2 IPv4 样例应答
        /// </summary>
        private const string SampleIPv4ResponseHex = @"
            01 01 00 3C 21 12 A4 42 B7 E7 A7 01 BC 34 D6 86 FA 87 DF AE
            80 22 00 0B 74 65 73 74 20 76 65 63 74 6F 72 20
            00 20 00 08 00 01 A1 47 E1 12 A6 43
            00 08 00 14 2B 91 F5 99 FD 9E 90 C3 8C 74 89 F9 2A F9 BA 53 F0 6B E7 D7
            80 28 00 04 C0 7D 4C 96";

        /// <summary>
        /// RFC 5769 2.3 IPv6 样例应答
        /// </summary>
        private const string SampleIPv6ResponseHex = @"
            01 01 00 48 21 12 A4 42 B7 E7 A7 01 BC 34 D6 86 FA 87 DF AE
            80 22 00 0B 74 65 73 74 20 76 65 63 74 6F 72 20
            00 20 00 14 00 02 A1 47 01 13 A9 FA A5 D3 F1 79 BC 25 F4 B5 BE D2 B9 D9
            00 08 00 14 A3 82 95 4E 4B E6 7B F1 17 84 C9 7C 82 92 C2 75 BF E3 ED 41
            80 28 00 04 C8 FB 0B 4C";

        /// <summary>
        /// RFC 5769 2.4 长期凭据样例请求
        /// </summary>
        private const string SampleLongTermRequestHex = @"
            00 01 00 60 21 12 A4 42 78 AD 34 33 C6 AD 72 C0 29 DA 41 2E
            00 06 00 12 E3 83 9E E3 83 88 E3 83 AA E3 83 83 E3 82 AF E3 82 B9 00 00
            00 15 00 1C 66 2F 2F 34 39 39 6B 39 35 34 64 36 4F 4C 33 34 6F 4C 39 46 53 54 76 79 36 34 73 41
            00 14 00 0B 65 78 61 6D 70 6C 65 2E 6F 72 67 00
            00 08 00 14 F6 70 24 65 6D D6 4A 3E 02 B8 E0 71 2E 85 C9 A2 8C A8 96 66";

        /// <summary>
        /// 样例请求：解码字段正确、两个哈希都能校验、重新编码逐字节一致
        /// </summary>
        [TestMethod]
        public void Rfc5769_样例请求_逐字节一致()
        {
            var sample = Hex(SampleRequestHex);
            var parsed = Parse(sample);

            Assert.AreEqual(StunMessageType.Binding, parsed.Method, "方法应为 Binding");
            Assert.AreEqual(StunClass.Request, parsed.Class, "类别应为请求");
            Assert.AreEqual(6, parsed.Attributes.Count, "样例请求应有 6 个属性");

            StunAttribute software;
            Assert.IsTrue(parsed.TryGet(StunAttributeType.Software, out software), "应解析出 SOFTWARE");
            Assert.AreEqual("STUN test client", Encoding.ASCII.GetString(software.Value.ToArray()), "SOFTWARE 值错误");

            StunAttribute priority;
            Assert.IsTrue(parsed.TryGet(StunAttributeType.Priority, out priority), "应解析出 PRIORITY");
            uint priorityValue;
            Assert.IsTrue(StunAttributeCodec.TryGetUInt32(priority, out priorityValue), "PRIORITY 应为 4 字节");
            Assert.AreEqual(0x6E0001FFu, priorityValue, "PRIORITY 值错误");

            StunAttribute username;
            Assert.IsTrue(parsed.TryGet(StunAttributeType.Username, out username), "应解析出 USERNAME");
            Assert.AreEqual("evtj:h6vY", Encoding.ASCII.GetString(username.Value.ToArray()), "USERNAME 值错误");

            var password = Encoding.ASCII.GetBytes(ShortTermPassword);
            Assert.IsTrue(StunIntegrity.TryVerify(parsed, password), "样例请求的 MESSAGE-INTEGRITY 应校验通过");
            Assert.IsTrue(StunFingerprint.TryVerify(parsed), "样例请求的 FINGERPRINT 应校验通过");

            var rebuilt = RebuildWithHashes(parsed, password);
            CollectionAssert.AreEqual(sample, rebuilt, "重新编码的样例请求与 RFC 5769 不一致");
        }

        /// <summary>
        /// IPv4 应答：XOR-MAPPED-ADDRESS 编码正确、哈希可校验、重新编码逐字节一致
        /// </summary>
        [TestMethod]
        public void Rfc5769_IPv4应答_逐字节一致()
        {
            var sample = Hex(SampleIPv4ResponseHex);
            var parsed = Parse(sample);

            Assert.AreEqual(StunClass.SuccessResponse, parsed.Class, "类别应为成功应答");

            var password = Encoding.ASCII.GetBytes(ShortTermPassword);
            Assert.IsTrue(StunIntegrity.TryVerify(parsed, password), "IPv4 应答的 MESSAGE-INTEGRITY 应校验通过");
            Assert.IsTrue(StunFingerprint.TryVerify(parsed), "IPv4 应答的 FINGERPRINT 应校验通过");

            var expected = StunAddressCodec.EncodeAddress(
                StunAttributeType.XorMappedAddress,
                new IPEndPoint(IPAddress.Parse("192.0.2.1"), 32853),
                true,
                parsed.TransactionId.ToArray());

            StunAttribute xorMapped;
            Assert.IsTrue(parsed.TryGet(StunAttributeType.XorMappedAddress, out xorMapped), "应解析出 XOR-MAPPED-ADDRESS");
            CollectionAssert.AreEqual(expected.Value.ToArray(), xorMapped.Value.ToArray(), "IPv4 异或映射地址编码与样例不一致");

            var rebuilt = RebuildWithHashes(parsed, password);
            CollectionAssert.AreEqual(sample, rebuilt, "重新编码的 IPv4 应答与 RFC 5769 不一致");
        }

        /// <summary>
        /// IPv6 应答：异或掩码用魔术字加事务 ID，哈希可校验、重新编码逐字节一致
        /// </summary>
        [TestMethod]
        public void Rfc5769_IPv6应答_逐字节一致()
        {
            var sample = Hex(SampleIPv6ResponseHex);
            var parsed = Parse(sample);

            var password = Encoding.ASCII.GetBytes(ShortTermPassword);
            Assert.IsTrue(StunIntegrity.TryVerify(parsed, password), "IPv6 应答的 MESSAGE-INTEGRITY 应校验通过");
            Assert.IsTrue(StunFingerprint.TryVerify(parsed), "IPv6 应答的 FINGERPRINT 应校验通过");

            var expected = StunAddressCodec.EncodeAddress(
                StunAttributeType.XorMappedAddress,
                new IPEndPoint(IPAddress.Parse("2001:db8:1234:5678:11:2233:4455:6677"), 32853),
                true,
                parsed.TransactionId.ToArray());

            StunAttribute xorMapped;
            Assert.IsTrue(parsed.TryGet(StunAttributeType.XorMappedAddress, out xorMapped), "应解析出 XOR-MAPPED-ADDRESS");
            CollectionAssert.AreEqual(expected.Value.ToArray(), xorMapped.Value.ToArray(), "IPv6 异或映射地址编码与样例不一致");

            IPEndPoint decoded;
            Assert.IsTrue(StunAddressCodec.TryDecodeAddress(xorMapped, parsed.TransactionId.Span, out decoded), "IPv6 异或映射地址应能解码");
            Assert.AreEqual(32853, decoded.Port, "IPv6 映射端口错误");

            var rebuilt = RebuildWithHashes(parsed, password);
            CollectionAssert.AreEqual(sample, rebuilt, "重新编码的 IPv6 应答与 RFC 5769 不一致");
        }

        /// <summary>
        /// 长期凭据请求：密钥必须先按 MD5(username:realm:password) 派生，重新编码逐字节一致
        /// </summary>
        [TestMethod]
        public void Rfc5769_长期凭据请求_逐字节一致()
        {
            var sample = Hex(SampleLongTermRequestHex);
            var parsed = Parse(sample);

            // SASLprep 处理后的用户名与口令，样例给的正是处理后的结果
            const string usernameText = "\u30DE\u30C8\u30EA\u30C3\u30AF\u30B9";
            const string realmText = "example.org";
            const string passwordText = "TheMatrIX";
            var key = DeriveLongTermKey(usernameText, realmText, passwordText);

            Assert.IsTrue(StunIntegrity.TryVerify(parsed, key), "长期凭据样例的 MESSAGE-INTEGRITY 应校验通过");
            Assert.IsFalse(StunFingerprint.TryVerify(parsed), "长期凭据样例不带 FINGERPRINT，校验应返回 false");

            StunAttribute nonce;
            Assert.IsTrue(parsed.TryGet(StunAttributeType.Nonce, out nonce), "应解析出 NONCE");
            Assert.AreEqual("f//499k954d6OL34oL9FSTvy64sA", Encoding.ASCII.GetString(nonce.Value.ToArray()), "NONCE 值错误");

            var request = new StunMessage(StunMessageType.Binding, StunClass.Request, parsed.TransactionId.ToArray());
            request.SetAttributes(new[]
            {
                new StunAttribute(StunAttributeType.Username, Encoding.UTF8.GetBytes(usernameText)),
                new StunAttribute(StunAttributeType.Nonce, Encoding.ASCII.GetBytes("f//499k954d6OL34oL9FSTvy64sA")),
                new StunAttribute(StunAttributeType.Realm, Encoding.ASCII.GetBytes(realmText))
            });
            StunIntegrity.Append(request, key);

            CollectionAssert.AreEqual(sample, request.ToArray(), "重新编码的长期凭据请求与 RFC 5769 不一致");
        }

        /// <summary>
        /// 去掉样例末尾的两个哈希属性后，用本实现的追加逻辑重新生成
        /// </summary>
        /// <param name="parsed">官方样例解析结果</param>
        /// <param name="integrityKey">MESSAGE-INTEGRITY 密钥</param>
        /// <returns>重新编码后的完整报文</returns>
        private static byte[] RebuildWithHashes(StunMessage parsed, byte[] integrityKey)
        {
            var attributes = new List<StunAttribute>();
            for (var i = 0; i < parsed.Attributes.Count; i++)
            {
                var attribute = parsed.Attributes[i];
                if (attribute.Type != (ushort)StunAttributeType.MessageIntegrity
                    && attribute.Type != (ushort)StunAttributeType.Fingerprint)
                {
                    attributes.Add(attribute);
                }
            }

            var rebuilt = new StunMessage(parsed.Method, parsed.Class, parsed.TransactionId.ToArray());
            rebuilt.SetAttributes(attributes);
            StunIntegrity.Append(rebuilt, integrityKey);
            StunFingerprint.Append(rebuilt);
            return rebuilt.ToArray();
        }

        /// <summary>
        /// 按长期凭据规则派生 HMAC 密钥
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="realm">认证域</param>
        /// <param name="password">口令</param>
        /// <returns>16 字节密钥</returns>
        private static byte[] DeriveLongTermKey(string username, string realm, string password)
        {
            using (var md5 = MD5.Create())
            {
                return md5.ComputeHash(Encoding.UTF8.GetBytes(username + ":" + realm + ":" + password));
            }
        }

        /// <summary>
        /// 解析样例，失败时直接断言失败
        /// </summary>
        /// <param name="data">样例字节</param>
        /// <returns>解析出的消息</returns>
        private static StunMessage Parse(byte[] data)
        {
            StunMessage message;
            StunParseError error;
            Assert.IsTrue(StunMessage.TryParse(data, out message, out error), $"官方样例解析失败：{error}");
            return message;
        }

        /// <summary>
        /// 把带空格与换行的十六进制文本转成字节
        /// </summary>
        /// <param name="text">十六进制文本</param>
        /// <returns>字节数组</returns>
        private static byte[] Hex(string text)
        {
            var builder = new StringBuilder(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (!char.IsWhiteSpace(c))
                {
                    builder.Append(c);
                }
            }
            return Convert.FromHexString(builder.ToString());
        }
    }
}