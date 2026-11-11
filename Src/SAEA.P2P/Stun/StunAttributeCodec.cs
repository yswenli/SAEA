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
*文件名： StunAttributeCodec
*版本号： v26.11.5.1
*唯一标识：6107172a-ed3d-4e31-9bf7-168aef061a8b
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 15:20:00
*描述：STUN 属性 TLV 编解码与整型属性读写
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 15:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 STUN 属性 TLV 编解码
*
*****************************************************************************/
using System;
using System.Collections.Generic;

namespace SAEA.P2P.Stun
{
    /// <summary>
    /// STUN 属性 TLV 编解码：值区补齐到 4 字节倍数、补齐字节为零且计入头部声明长度。
    /// </summary>
    public static class StunAttributeCodec
    {
        /// <summary>
        /// 本实现已定义的属性类型集合，用于区分未知的必选属性
        /// </summary>
        private static readonly HashSet<ushort> KnownTypes = BuildKnownTypes();

        /// <summary>
        /// 编码单个属性，含 4 字节属性头与补齐字节
        /// </summary>
        /// <param name="attribute">属性</param>
        /// <returns>属性字节</returns>
        public static byte[] Encode(StunAttribute attribute)
        {
            if (attribute == null)
            {
                throw new ArgumentNullException(nameof(attribute));
            }

            var value = attribute.Value.Span;
            var buffer = new byte[attribute.EncodedLength];
            buffer[0] = (byte)(attribute.Type >> 8);
            buffer[1] = (byte)attribute.Type;
            buffer[2] = (byte)(value.Length >> 8);
            buffer[3] = (byte)value.Length;
            for (var i = 0; i < value.Length; i++)
            {
                buffer[StunConstants.AttributeHeaderLength + i] = value[i];
            }
            return buffer;
        }

        /// <summary>
        /// 按给定顺序编码属性列表
        /// </summary>
        /// <param name="attributes">属性列表</param>
        /// <returns>拼接后的属性区字节</returns>
        public static byte[] EncodeAll(IEnumerable<StunAttribute> attributes)
        {
            if (attributes == null)
            {
                throw new ArgumentNullException(nameof(attributes));
            }

            var list = new List<byte>();
            foreach (var attribute in attributes)
            {
                list.AddRange(Encode(attribute));
            }
            return list.ToArray();
        }

        /// <summary>
        /// 解析整段属性区；任一属性被截断即整体失败，不返回半截列表
        /// </summary>
        /// <param name="data">属性区字节，不含消息头</param>
        /// <param name="attributes">解析出的属性，保持报文顺序</param>
        /// <param name="error">失败原因，成功时为 None</param>
        /// <returns>是否解析成功</returns>
        public static bool TryDecodeAll(ReadOnlySpan<byte> data, out IReadOnlyList<StunAttribute> attributes, out StunParseError error)
        {
            var list = new List<StunAttribute>();
            error = StunParseError.None;
            attributes = list;

            var offset = 0;
            while (offset < data.Length)
            {
                if (data.Length - offset < StunConstants.AttributeHeaderLength)
                {
                    error = StunParseError.AttributesTruncated;
                    return false;
                }

                var type = (ushort)((data[offset] << 8) | data[offset + 1]);
                var length = (data[offset + 2] << 8) | data[offset + 3];
                var padded = (length + 3) & ~3;
                if (data.Length - offset - StunConstants.AttributeHeaderLength < padded)
                {
                    error = StunParseError.AttributesTruncated;
                    return false;
                }

                var value = new byte[length];
                for (var i = 0; i < length; i++)
                {
                    value[i] = data[offset + StunConstants.AttributeHeaderLength + i];
                }

                list.Add(new StunAttribute(type, value));
                offset += StunConstants.AttributeHeaderLength + padded;
            }

            return true;
        }

        /// <summary>
        /// 判断属性类型是否在本实现已定义的表中
        /// </summary>
        /// <param name="type">属性类型原值</param>
        /// <returns>已定义返回 true</returns>
        public static bool IsKnownType(ushort type)
        {
            return KnownTypes.Contains(type);
        }

        /// <summary>
        /// 创建无值属性，例如 USE-CANDIDATE
        /// </summary>
        /// <param name="type">属性类型</param>
        /// <returns>属性</returns>
        public static StunAttribute CreateEmpty(StunAttributeType type)
        {
            return new StunAttribute(type, new byte[0]);
        }

        /// <summary>
        /// 创建 32 位无符号整型属性，按大端保存
        /// </summary>
        /// <param name="type">属性类型</param>
        /// <param name="value">整型值</param>
        /// <returns>属性</returns>
        public static StunAttribute CreateUInt32(StunAttributeType type, uint value)
        {
            return new StunAttribute(type, new[]
            {
                (byte)(value >> 24),
                (byte)(value >> 16),
                (byte)(value >> 8),
                (byte)value
            });
        }

        /// <summary>
        /// 创建 64 位无符号整型属性，按大端保存
        /// </summary>
        /// <param name="type">属性类型</param>
        /// <param name="value">整型值</param>
        /// <returns>属性</returns>
        public static StunAttribute CreateUInt64(StunAttributeType type, ulong value)
        {
            return new StunAttribute(type, new[]
            {
                (byte)(value >> 56),
                (byte)(value >> 48),
                (byte)(value >> 40),
                (byte)(value >> 32),
                (byte)(value >> 24),
                (byte)(value >> 16),
                (byte)(value >> 8),
                (byte)value
            });
        }

        /// <summary>
        /// 读取 32 位无符号整型属性；长度不为 4 时返回 false
        /// </summary>
        /// <param name="attribute">属性</param>
        /// <param name="value">整型值</param>
        /// <returns>是否读取成功</returns>
        public static bool TryGetUInt32(StunAttribute attribute, out uint value)
        {
            value = 0;
            if (attribute == null || attribute.Value.Length != 4)
            {
                return false;
            }

            var span = attribute.Value.Span;
            value = ((uint)span[0] << 24) | ((uint)span[1] << 16) | ((uint)span[2] << 8) | span[3];
            return true;
        }

        /// <summary>
        /// 读取 64 位无符号整型属性；长度不为 8 时返回 false
        /// </summary>
        /// <param name="attribute">属性</param>
        /// <param name="value">整型值</param>
        /// <returns>是否读取成功</returns>
        public static bool TryGetUInt64(StunAttribute attribute, out ulong value)
        {
            value = 0;
            if (attribute == null || attribute.Value.Length != 8)
            {
                return false;
            }

            var span = attribute.Value.Span;
            ulong result = 0;
            for (var i = 0; i < 8; i++)
            {
                result = (result << 8) | span[i];
            }
            value = result;
            return true;
        }

        /// <summary>
        /// 由枚举定义生成已知类型集合
        /// </summary>
        /// <returns>已知类型集合</returns>
        private static HashSet<ushort> BuildKnownTypes()
        {
            var set = new HashSet<ushort>();
            foreach (var value in (StunAttributeType[])Enum.GetValues(typeof(StunAttributeType)))
            {
                set.Add((ushort)value);
            }
            return set;
        }
    }
}