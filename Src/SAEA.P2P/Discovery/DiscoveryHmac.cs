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
*命名空间：SAEA.P2P.Discovery
*文件名： DiscoveryHmac
*版本号： v26.11.5.1
*唯一标识：9829556b-123d-4d2f-9d00-0249ef1458ec
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 16:59:24
*描述：无信令局域网发现报文的 HMAC-SHA256 计算与校验
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 16:59:24
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增无信令局域网发现报文的 HMAC-SHA256 计算与校验
*
*****************************************************************************/
using System;
using System.Security.Cryptography;
using System.Text;

namespace SAEA.P2P.Discovery
{
    /// <summary>
    /// 无信令局域网发现报文的 HMAC-SHA256 计算与校验，密钥与标签都按 UTF-8 与 Base64 处理
    /// </summary>
    public static class DiscoveryHmac
    {
        /// <summary>
        /// 计算报文的 HMAC-SHA256 标签
        /// </summary>
        /// <param name="key">共享密钥；空表示不启用认证</param>
        /// <param name="payload">参与计算的字节，应为 Hmac 置空后的规范 JSON 负载</param>
        /// <returns>Base64 标签；密钥或负载为空时返回 null</returns>
        public static string Compute(string key, byte[] payload)
        {
            if (string.IsNullOrEmpty(key) || payload == null)
            {
                return null;
            }

            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
            {
                return Convert.ToBase64String(hmac.ComputeHash(payload));
            }
        }

        /// <summary>
        /// 校验报文的 HMAC-SHA256 标签
        /// </summary>
        /// <param name="key">共享密钥；空表示不启用认证</param>
        /// <param name="payload">参与计算的字节，应为 Hmac 置空后的规范 JSON 负载</param>
        /// <param name="tag">报文携带的 Base64 标签</param>
        /// <returns>密钥为空时仅当标签也为空才通过；否则按定长比较判定是否一致</returns>
        public static bool Verify(string key, byte[] payload, string tag)
        {
            // 未配置密钥的节点只接受同样未签名的报文，避免双方一边签名一边校验造成静默失配
            if (string.IsNullOrEmpty(key))
            {
                return string.IsNullOrEmpty(tag);
            }

            if (string.IsNullOrEmpty(tag) || payload == null)
            {
                return false;
            }

            var expected = Compute(key, payload);
            if (expected == null)
            {
                return false;
            }

            return FixedTimeEquals(expected, tag);
        }

        /// <summary>
        /// 定长比较 Base64 标签，避免按字节提前返回带来的时序侧信道
        /// </summary>
        /// <param name="expected">期望标签</param>
        /// <param name="actual">实际标签</param>
        /// <returns>完全一致返回 true</returns>
        private static bool FixedTimeEquals(string expected, string actual)
        {
            if (expected == null || actual == null)
            {
                return false;
            }

            var left = Encoding.UTF8.GetBytes(expected);
            var right = Encoding.UTF8.GetBytes(actual);

            // 长度不同时仍遍历较短一段，避免长度差异通过耗时泄露
            var length = Math.Min(left.Length, right.Length);
            var diff = left.Length ^ right.Length;
            for (var i = 0; i < length; i++)
            {
                diff |= left[i] ^ right[i];
            }
            return diff == 0;
        }
    }
}