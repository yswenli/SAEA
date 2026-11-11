/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.P2P.Stun
*文件名： StunIntegrity
*版本号： v26.11.5.1
*唯一标识：2e2434e2-694e-4182-8748-e6f37c33aea7
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 16:00:00
*描述：STUN MESSAGE-INTEGRITY 计算校验与密码算法协商
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 16:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 MESSAGE-INTEGRITY 与密码算法协商
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace SAEA.P2P.Stun
{
    /// <summary>
    /// STUN MESSAGE-INTEGRITY：HMAC-SHA1 覆盖到该属性之前，计算前把头部长度改写为到该属性结尾。
    /// 本轮只签发 SHA-1，密码算法协商只接受 MD5。
    /// </summary>
    public static class StunIntegrity
    {
        /// <summary>
        /// SHA-1 MESSAGE-INTEGRITY 的属性值长度
        /// </summary>
        public const int Sha1ValueLength = 20;

        /// <summary>
        /// MESSAGE-INTEGRITY 属性自身在报文中的长度，含属性头
        /// </summary>
        private const int Sha1EncodedLength = StunConstants.AttributeHeaderLength + Sha1ValueLength;

        /// <summary>
        /// 追加或替换 MESSAGE-INTEGRITY 属性；已有 FINGERPRINT 时插到它之前，保持指纹位于末位
        /// </summary>
        /// <param name="message">待处理消息，会被就地修改</param>
        /// <param name="key">HMAC 密钥；短凭据为口令，长期凭据为派生密钥</param>
        public static void Append(StunMessage message, byte[] key)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            var attributes = new List<StunAttribute>();
            var insertIndex = -1;
            for (var i = 0; i < message.Attributes.Count; i++)
            {
                var attribute = message.Attributes[i];
                if (attribute.Type == (ushort)StunAttributeType.MessageIntegrity)
                {
                    continue;
                }
                if (insertIndex < 0 && attribute.Type == (ushort)StunAttributeType.Fingerprint)
                {
                    insertIndex = attributes.Count;
                }
                attributes.Add(attribute);
            }
            if (insertIndex < 0)
            {
                insertIndex = attributes.Count;
            }

            var prefixAttributes = new List<StunAttribute>();
            for (var i = 0; i < insertIndex; i++)
            {
                prefixAttributes.Add(attributes[i]);
            }

            var head = new StunMessage(message.Method, message.Class, message.TransactionId.ToArray());
            head.SetAttributes(prefixAttributes);
            var prefix = head.ToArray();

            // 头部长度必须指向 MESSAGE-INTEGRITY 末尾，否则与对端校验规则不一致
            var adjustedLength = (ushort)((prefix.Length - StunConstants.HeaderLength) + Sha1EncodedLength);
            prefix[2] = (byte)(adjustedLength >> 8);
            prefix[3] = (byte)adjustedLength;

            attributes.Insert(insertIndex, new StunAttribute(StunAttributeType.MessageIntegrity, ComputeSha1(prefix, key)));
            message.SetAttributes(attributes);
        }

        /// <summary>
        /// 校验第一个 MESSAGE-INTEGRITY 属性；HMAC 输入同样按头部长度回填后的前缀计算
        /// </summary>
        /// <param name="message">待校验消息</param>
        /// <param name="key">HMAC 密钥</param>
        /// <returns>校验通过返回 true</returns>
        public static bool TryVerify(StunMessage message, byte[] key)
        {
            if (message == null || key == null)
            {
                return false;
            }

            var offset = StunConstants.HeaderLength;
            StunAttribute integrity = null;
            var integrityOffset = -1;
            for (var i = 0; i < message.Attributes.Count; i++)
            {
                var attribute = message.Attributes[i];
                if (attribute.Type == (ushort)StunAttributeType.MessageIntegrity)
                {
                    integrity = attribute;
                    integrityOffset = offset;
                    break;
                }
                offset += attribute.EncodedLength;
            }

            if (integrity == null || integrity.Value.Length != Sha1ValueLength)
            {
                return false;
            }

            var raw = message.ToArray();
            var prefix = new byte[integrityOffset];
            Buffer.BlockCopy(raw, 0, prefix, 0, integrityOffset);

            var adjustedLength = (ushort)((integrityOffset - StunConstants.HeaderLength) + Sha1EncodedLength);
            prefix[2] = (byte)(adjustedLength >> 8);
            prefix[3] = (byte)adjustedLength;

            var expected = ComputeSha1(prefix, key);
            return FixedTimeEquals(expected, integrity.Value.Span);
        }

        /// <summary>
        /// 读取对端声明的密码算法列表；优先取 PASSWORD-ALGORITHMS，缺失时退回单个 PASSWORD-ALGORITHM
        /// </summary>
        /// <param name="message">携带算法声明的消息，通常是 401 或 438 应答</param>
        /// <param name="algorithms">解析出的算法列表，保持报文顺序</param>
        /// <returns>找到并解析成功返回 true</returns>
        public static bool TryGetPasswordAlgorithms(StunMessage message, out IReadOnlyList<StunPasswordAlgorithm> algorithms)
        {
            algorithms = new List<StunPasswordAlgorithm>();
            if (message == null)
            {
                return false;
            }

            StunAttribute list;
            if (message.TryGet(StunAttributeType.PasswordAlgorithms, out list))
            {
                var span = list.Value.Span;
                if (span.Length == 0 || span.Length % 4 != 0)
                {
                    return false;
                }

                var result = new List<StunPasswordAlgorithm>();
                for (var i = 0; i < span.Length; i += 4)
                {
                    result.Add((StunPasswordAlgorithm)((span[i] << 8) | span[i + 1]));
                }
                algorithms = result;
                return true;
            }

            StunAttribute single;
            if (message.TryGet(StunAttributeType.PasswordAlgorithm, out single))
            {
                var span = single.Value.Span;
                if (span.Length < 2)
                {
                    return false;
                }
                algorithms = new List<StunPasswordAlgorithm> { (StunPasswordAlgorithm)((span[0] << 8) | span[1]) };
                return true;
            }

            return false;
        }

        /// <summary>
        /// 生成 PASSWORD-ALGORITHMS 属性；每个算法占 4 字节，高 2 字节为算法编号，低 2 字节补零
        /// </summary>
        /// <param name="algorithms">算法列表</param>
        /// <returns>算法列表属性</returns>
        public static StunAttribute CreatePasswordAlgorithms(IEnumerable<StunPasswordAlgorithm> algorithms)
        {
            if (algorithms == null)
            {
                throw new ArgumentNullException(nameof(algorithms));
            }

            var value = new List<byte>();
            foreach (var algorithm in algorithms)
            {
                var code = (ushort)algorithm;
                value.Add((byte)(code >> 8));
                value.Add((byte)code);
                value.Add(0);
                value.Add(0);
            }
            return new StunAttribute(StunAttributeType.PasswordAlgorithms, value.ToArray());
        }

        /// <summary>
        /// 从对端算法列表里选择本地支持的算法；空列表按缺省 MD5 处理，只有不支持的算法时返回 false
        /// </summary>
        /// <param name="offered">对端声明的算法列表</param>
        /// <param name="selected">选中的算法，仅在返回 true 时有效</param>
        /// <returns>存在本地支持的算法返回 true</returns>
        public static bool TrySelectPasswordAlgorithm(IReadOnlyList<StunPasswordAlgorithm> offered, out StunPasswordAlgorithm selected)
        {
            selected = StunPasswordAlgorithm.Md5;

            if (offered == null || offered.Count == 0)
            {
                return true;
            }

            for (var i = 0; i < offered.Count; i++)
            {
                if (offered[i] == StunPasswordAlgorithm.Md5)
                {
                    selected = StunPasswordAlgorithm.Md5;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 计算 HMAC-SHA1
        /// </summary>
        /// <param name="data">待认证字节</param>
        /// <param name="key">密钥</param>
        /// <returns>20 字节摘要</returns>
        private static byte[] ComputeSha1(byte[] data, byte[] key)
        {
            using (var hmac = new HMACSHA1(key))
            {
                return hmac.ComputeHash(data);
            }
        }

        /// <summary>
        /// 定长比较摘要，避免按字节短路带来的时序侧信道
        /// </summary>
        /// <param name="left">期望值</param>
        /// <param name="right">实际值</param>
        /// <returns>完全一致返回 true</returns>
        private static bool FixedTimeEquals(byte[] left, ReadOnlySpan<byte> right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            var diff = 0;
            for (var i = 0; i < left.Length; i++)
            {
                diff |= left[i] ^ right[i];
            }
            return diff == 0;
        }
    }
}