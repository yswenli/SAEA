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
*文件名： StunAddressCodec
*版本号： v26.11.5.1
*唯一标识：6e1c659c-d65e-4918-a557-29ff8131d0a6
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 15:20:00
*描述：STUN 地址属性编解码
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 15:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 STUN 地址属性编解码
*
*****************************************************************************/
using System;
using System.Net;
using System.Net.Sockets;

namespace SAEA.P2P.Stun
{
    /// <summary>
    /// STUN 地址属性编解码：家族字节加端口加地址，异或类属性用魔术字与事务 ID 做掩码。
    /// </summary>
    public static class StunAddressCodec
    {
        /// <summary>
        /// 编码地址属性；v4-mapped v6 地址按 v4 编码，避免对端把它当成 IPv6 候选
        /// </summary>
        /// <param name="type">属性类型</param>
        /// <param name="endPoint">地址端点</param>
        /// <param name="xor">是否按异或规则编码</param>
        /// <param name="transactionId">事务 ID，异或编码时必须是 12 字节</param>
        /// <returns>地址属性</returns>
        public static StunAttribute EncodeAddress(StunAttributeType type, IPEndPoint endPoint, bool xor, byte[] transactionId = null)
        {
            if (endPoint == null)
            {
                throw new ArgumentNullException(nameof(endPoint));
            }

            var address = endPoint.Address;
            if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }

            var addressBytes = address.GetAddressBytes();
            if (addressBytes.Length != 4 && addressBytes.Length != 16)
            {
                throw new ArgumentException("只支持 IPv4 与 IPv6 地址", nameof(endPoint));
            }

            var value = new byte[4 + addressBytes.Length];
            value[1] = (byte)(addressBytes.Length == 4 ? 0x01 : 0x02);
            var port = endPoint.Port;
            if (xor)
            {
                port ^= (int)(StunConstants.MagicCookie >> 16);
            }
            value[2] = (byte)(port >> 8);
            value[3] = (byte)port;

            Buffer.BlockCopy(addressBytes, 0, value, 4, addressBytes.Length);

            if (xor)
            {
                var mask = BuildXorMask(addressBytes.Length, transactionId);
                for (var i = 0; i < addressBytes.Length; i++)
                {
                    value[4 + i] = (byte)(addressBytes[i] ^ mask[i]);
                }
            }

            return new StunAttribute(type, value);
        }

        /// <summary>
        /// 解码地址属性；类型属于异或类时必须提供 12 字节事务 ID
        /// </summary>
        /// <param name="attribute">地址属性</param>
        /// <param name="transactionId">事务 ID</param>
        /// <param name="endPoint">解码出的端点</param>
        /// <returns>是否解码成功</returns>
        public static bool TryDecodeAddress(StunAttribute attribute, ReadOnlySpan<byte> transactionId, out IPEndPoint endPoint)
        {
            endPoint = null;
            if (attribute == null)
            {
                return false;
            }

            var span = attribute.Value.Span;
            if (span.Length < 4 || span[0] != 0)
            {
                return false;
            }

            int addressLength;
            if (span[1] == 0x01)
            {
                addressLength = 4;
            }
            else if (span[1] == 0x02)
            {
                addressLength = 16;
            }
            else
            {
                return false;
            }

            if (span.Length != 4 + addressLength)
            {
                return false;
            }

            var xor = IsXorAddressType(attribute.Type);
            if (xor && transactionId.Length != StunConstants.TransactionIdLength)
            {
                return false;
            }

            var addressBytes = new byte[addressLength];
            for (var i = 0; i < addressLength; i++)
            {
                addressBytes[i] = span[4 + i];
            }

            if (xor)
            {
                var mask = BuildXorMask(addressLength, transactionId.ToArray());
                for (var i = 0; i < addressLength; i++)
                {
                    addressBytes[i] = (byte)(addressBytes[i] ^ mask[i]);
                }
            }

            var port = (span[2] << 8) | span[3];
            if (xor)
            {
                port ^= (int)(StunConstants.MagicCookie >> 16);
            }

            endPoint = new IPEndPoint(new IPAddress(addressBytes), port);
            return true;
        }

        /// <summary>
        /// 判断属性类型是否按异或规则处理地址
        /// </summary>
        /// <param name="type">属性类型原值</param>
        /// <returns>异或类地址属性返回 true</returns>
        public static bool IsXorAddressType(ushort type)
        {
            return type == (ushort)StunAttributeType.XorMappedAddress
                || type == (ushort)StunAttributeType.XorPeerAddress
                || type == (ushort)StunAttributeType.XorRelayedAddress;
        }

        /// <summary>
        /// 构造异或掩码：前 4 字节取魔术字，其后取事务 ID；IPv4 只用前 4 字节
        /// </summary>
        /// <param name="addressLength">地址长度</param>
        /// <param name="transactionId">事务 ID</param>
        /// <returns>与地址等长的掩码</returns>
        private static byte[] BuildXorMask(int addressLength, byte[] transactionId)
        {
            if (transactionId == null || transactionId.Length != StunConstants.TransactionIdLength)
            {
                throw new ArgumentException("事务 ID 必须为 12 字节", nameof(transactionId));
            }

            var mask = new byte[16];
            mask[0] = (byte)((StunConstants.MagicCookie >> 24) & 0xFF);
            mask[1] = (byte)((StunConstants.MagicCookie >> 16) & 0xFF);
            mask[2] = (byte)((StunConstants.MagicCookie >> 8) & 0xFF);
            mask[3] = (byte)(StunConstants.MagicCookie & 0xFF);
            Buffer.BlockCopy(transactionId, 0, mask, 4, StunConstants.TransactionIdLength);
            return mask;
        }
    }
}