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
*文件名： StunFingerprint
*版本号： v26.11.5.1
*唯一标识：795c79fc-ec63-442b-a7b0-a329c6db54aa
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 15:45:00
*描述：STUN FINGERPRINT 属性计算与校验
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 15:45:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 STUN FINGERPRINT 计算与校验
*
*****************************************************************************/
using System;
using System.Collections.Generic;

using SAEA.P2P.Common;

namespace SAEA.P2P.Stun
{
    /// <summary>
    /// STUN FINGERPRINT：整包 CRC-32 再与固定常量异或，且必须是最后一个属性。
    /// </summary>
    public static class StunFingerprint
    {
        /// <summary>
        /// 追加或替换 FINGERPRINT 属性，并把它放到属性列表末尾。
        /// 计算前先把头部长度改为含 FINGERPRINT 的 8 字节，保证与对端校验规则一致。
        /// </summary>
        /// <param name="message">待处理消息，会被就地修改</param>
        public static void Append(StunMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            // 旧的 FINGERPRINT 必须先摘掉，否则会出现在非末位导致对端判非法
            var attributes = new List<StunAttribute>();
            for (var i = 0; i < message.Attributes.Count; i++)
            {
                var attribute = message.Attributes[i];
                if (attribute.Type != (ushort)StunAttributeType.Fingerprint)
                {
                    attributes.Add(attribute);
                }
            }

            // 借一个临时消息生成“不含指纹”的报文，再手工把长度改成含指纹的值
            var head = new StunMessage(message.Method, message.Class, message.TransactionId.ToArray());
            head.SetAttributes(attributes);
            var prefix = head.ToArray();

            var lengthWithFingerprint = (ushort)(prefix.Length - StunConstants.HeaderLength + 8);
            prefix[2] = (byte)(lengthWithFingerprint >> 8);
            prefix[3] = (byte)lengthWithFingerprint;

            var crc = Crc32.Compute(prefix);
            attributes.Add(StunAttributeCodec.CreateUInt32(StunAttributeType.Fingerprint, crc ^ StunConstants.FingerprintXor));

            message.SetAttributes(attributes);
        }

        /// <summary>
        /// 校验 FINGERPRINT：必须存在、长度为 4 且位于属性列表末尾，CRC 覆盖该属性之前的全部字节
        /// </summary>
        /// <param name="message">待校验消息</param>
        /// <returns>校验通过返回 true</returns>
        public static bool TryVerify(StunMessage message)
        {
            if (message == null || message.Attributes.Count == 0)
            {
                return false;
            }

            var last = message.Attributes[message.Attributes.Count - 1];
            if (last.Type != (ushort)StunAttributeType.Fingerprint || last.Value.Length != 4)
            {
                return false;
            }

            var raw = message.ToArray();
            var expected = ((uint)raw[raw.Length - 4] << 24)
                | ((uint)raw[raw.Length - 3] << 16)
                | ((uint)raw[raw.Length - 2] << 8)
                | raw[raw.Length - 1];
            var actual = Crc32.Compute(new ReadOnlySpan<byte>(raw, 0, raw.Length - 8)) ^ StunConstants.FingerprintXor;

            return expected == actual;
        }
    }
}