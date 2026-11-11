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
*文件名： StunMessage
*版本号： v26.11.5.1
*唯一标识：6d6cf22c-a848-439a-8578-763a5dc019d6
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 15:00:00
*描述：STUN 消息头编解码与原始属性区承载
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 18:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：修正类别低位未左移到 4 位导致的编码错误
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace SAEA.P2P.Stun
{
    /// <summary>
    /// STUN 消息：承载方法、类别、事务 ID 与已编码的属性区。
    /// 属性区按 TLV 原样保存，便于 FINGERPRINT 与 MESSAGE-INTEGRITY 直接按字节偏移计算。
    /// </summary>
    public sealed class StunMessage
    {
        /// <summary>
        /// 事务 ID，长度固定为 12 字节
        /// </summary>
        private readonly byte[] _transactionId;

        /// <summary>
        /// 已按 TLV 编码并补齐的属性区，不含 20 字节消息头
        /// </summary>
        private byte[] _attributes;

        /// <summary>
        /// 解析后的属性列表，保持报文顺序
        /// </summary>
        private IReadOnlyList<StunAttribute> _decodedAttributes;

        /// <summary>
        /// 本实现不认识、但类型为 comprehension-required 的属性，用于回 420
        /// </summary>
        private ushort[] _unknownRequiredTypes;

        /// <summary>
        /// 创建消息；不传事务 ID 时用加密随机数生成
        /// </summary>
        /// <param name="method">STUN 方法</param>
        /// <param name="class">消息类别</param>
        /// <param name="transactionId">事务 ID，必须为 12 字节；为 null 时自动生成</param>
        public StunMessage(StunMessageType method, StunClass @class, byte[] transactionId = null)
        {
            Method = method;
            Class = @class;

            if (transactionId == null)
            {
                _transactionId = new byte[StunConstants.TransactionIdLength];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(_transactionId);
                }
            }
            else
            {
                if (transactionId.Length != StunConstants.TransactionIdLength)
                {
                    throw new ArgumentException("事务 ID 必须为 12 字节", nameof(transactionId));
                }
                _transactionId = (byte[])transactionId.Clone();
            }

            _attributes = new byte[0];
            _decodedAttributes = new List<StunAttribute>();
            _unknownRequiredTypes = new ushort[0];
        }

        /// <summary>
        /// STUN 方法
        /// </summary>
        public StunMessageType Method { get; }

        /// <summary>
        /// 消息类别
        /// </summary>
        public StunClass Class { get; }

        /// <summary>
        /// 事务 ID；返回只读视图，底层数组不会被外部改写
        /// </summary>
        public ReadOnlyMemory<byte> TransactionId
        {
            get { return _transactionId; }
        }

        /// <summary>
        /// 属性区原始字节，已按 TLV 编码并包含补齐字节
        /// </summary>
        public ReadOnlyMemory<byte> AttributesRaw
        {
            get { return _attributes; }
        }

        /// <summary>
        /// 解析后的属性列表，保持报文中的先后顺序；同一类型可出现多次
        /// </summary>
        public IReadOnlyList<StunAttribute> Attributes
        {
            get { return _decodedAttributes; }
        }

        /// <summary>
        /// 是否存在本实现不认识且为 comprehension-required 的属性；为 true 时应回 420
        /// </summary>
        public bool HasUnknownComprehensionRequired
        {
            get { return _unknownRequiredTypes.Length > 0; }
        }

        /// <summary>
        /// 本实现不认识的 comprehension-required 属性类型，供 UNKNOWN-ATTRIBUTES 原样回填
        /// </summary>
        public IReadOnlyList<ushort> UnknownComprehensionRequiredTypes
        {
            get { return _unknownRequiredTypes; }
        }

        /// <summary>
        /// 头部声明长度，即属性区字节数，不含 20 字节消息头
        /// </summary>
        public int Length
        {
            get { return _attributes.Length; }
        }

        /// <summary>
        /// 用已编码好的属性区替换当前内容；长度必须为 4 的倍数且不超过头部可表示范围
        /// </summary>
        /// <param name="attributes">属性区字节，含每个属性的补齐字节</param>
        public void SetAttributesRaw(ReadOnlySpan<byte> attributes)
        {
            if (attributes.Length % 4 != 0)
            {
                throw new ArgumentException("属性区长度必须为 4 的倍数", nameof(attributes));
            }
            if (attributes.Length > ushort.MaxValue)
            {
                throw new ArgumentException("属性区长度超出 STUN 头部可表示范围", nameof(attributes));
            }

            IReadOnlyList<StunAttribute> decoded;
            StunParseError error;
            if (!StunAttributeCodec.TryDecodeAll(attributes, out decoded, out error))
            {
                throw new ArgumentException("属性区不是合法的 TLV 序列：" + error, nameof(attributes));
            }

            _attributes = attributes.ToArray();
            ApplyDecoded(decoded);
        }

        /// <summary>
        /// 按属性列表替换当前内容，编码时按列表顺序拼接
        /// </summary>
        /// <param name="attributes">属性列表</param>
        public void SetAttributes(IEnumerable<StunAttribute> attributes)
        {
            if (attributes == null)
            {
                throw new ArgumentNullException(nameof(attributes));
            }

            SetAttributesRaw(StunAttributeCodec.EncodeAll(attributes));
        }

        /// <summary>
        /// 取第一个指定类型的属性
        /// </summary>
        /// <param name="type">属性类型</param>
        /// <param name="attribute">匹配到的属性</param>
        /// <returns>存在返回 true</returns>
        public bool TryGet(StunAttributeType type, out StunAttribute attribute)
        {
            var target = (ushort)type;
            for (var i = 0; i < _decodedAttributes.Count; i++)
            {
                if (_decodedAttributes[i].Type == target)
                {
                    attribute = _decodedAttributes[i];
                    return true;
                }
            }

            attribute = null;
            return false;
        }

        /// <summary>
        /// 取全部指定类型的属性，保持报文顺序
        /// </summary>
        /// <param name="type">属性类型</param>
        /// <returns>属性列表；没有时为空列表</returns>
        public IReadOnlyList<StunAttribute> GetAll(StunAttributeType type)
        {
            var target = (ushort)type;
            var result = new List<StunAttribute>();
            for (var i = 0; i < _decodedAttributes.Count; i++)
            {
                if (_decodedAttributes[i].Type == target)
                {
                    result.Add(_decodedAttributes[i]);
                }
            }
            return result;
        }

        /// <summary>
        /// 编码为完整报文，含 20 字节消息头
        /// </summary>
        /// <returns>报文字节</returns>
        public byte[] ToArray()
        {
            var buffer = new byte[StunConstants.HeaderLength + _attributes.Length];
            WriteUInt16(buffer, 0, EncodeType(Method, Class));
            WriteUInt16(buffer, 2, (ushort)_attributes.Length);
            WriteUInt32(buffer, 4, StunConstants.MagicCookie);
            Buffer.BlockCopy(_transactionId, 0, buffer, 8, StunConstants.TransactionIdLength);
            Buffer.BlockCopy(_attributes, 0, buffer, StunConstants.HeaderLength, _attributes.Length);
            return buffer;
        }

        /// <summary>
        /// 按 RFC 8489 规则把方法与类别编码为消息类型字段
        /// </summary>
        /// <param name="method">STUN 方法，取值 0x000 到 0xFFF</param>
        /// <param name="class">消息类别</param>
        /// <returns>16 位消息类型字段</returns>
        public static ushort EncodeType(StunMessageType method, StunClass @class)
        {
            var m = (int)method;
            var c = (int)@class;
            if (m < 0 || m > 0x0FFF)
            {
                throw new ArgumentOutOfRangeException(nameof(method));
            }
            if (c < 0 || c > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(@class));
            }

            return (ushort)(((m & 0x0F80) << 2) | ((c & 0x2) << 7) | ((m & 0x0070) << 1) | ((c & 0x1) << 4) | (m & 0x000F));
        }

        /// <summary>
        /// 从消息类型字段还原 STUN 方法
        /// </summary>
        /// <param name="type">16 位消息类型字段</param>
        /// <returns>STUN 方法</returns>
        public static StunMessageType DecodeMethod(ushort type)
        {
            var method = ((type & 0x3E00) >> 2) | ((type & 0x00E0) >> 1) | (type & 0x000F);
            return (StunMessageType)method;
        }

        /// <summary>
        /// 从消息类型字段还原消息类别
        /// </summary>
        /// <param name="type">16 位消息类型字段</param>
        /// <returns>消息类别</returns>
        public static StunClass DecodeClass(ushort type)
        {
            return (StunClass)(((type & 0x0100) >> 7) | ((type & 0x0010) >> 4));
        }

        /// <summary>
        /// 解析完整报文；失败时给出结构化原因，不抛异常
        /// </summary>
        /// <param name="data">完整报文字节，长度必须与头部声明一致</param>
        /// <param name="message">解析成功的消息，失败时为 null</param>
        /// <param name="error">失败原因，成功时为 None</param>
        /// <returns>是否解析成功</returns>
        public static bool TryParse(ReadOnlySpan<byte> data, out StunMessage message, out StunParseError error)
        {
            message = null;
            error = StunParseError.None;

            if (data.Length < StunConstants.HeaderLength)
            {
                error = StunParseError.TooShort;
                return false;
            }

            var type = ReadUInt16(data, 0);
            if ((type & 0xC000) != 0)
            {
                error = StunParseError.TopBitsNotZero;
                return false;
            }

            if (ReadUInt32(data, 4) != StunConstants.MagicCookie)
            {
                error = StunParseError.InvalidMagicCookie;
                return false;
            }

            var length = ReadUInt16(data, 2);
            if (data.Length != StunConstants.HeaderLength + length)
            {
                error = StunParseError.LengthMismatch;
                return false;
            }

            if (length % 4 != 0)
            {
                error = StunParseError.LengthNotAligned;
                return false;
            }

            var transactionId = new byte[StunConstants.TransactionIdLength];
            for (var i = 0; i < StunConstants.TransactionIdLength; i++)
            {
                transactionId[i] = data[8 + i];
            }

            var attributesRaw = new byte[length];
            for (var i = 0; i < length; i++)
            {
                attributesRaw[i] = data[StunConstants.HeaderLength + i];
            }

            IReadOnlyList<StunAttribute> decoded;
            StunParseError attributeError;
            if (!StunAttributeCodec.TryDecodeAll(attributesRaw, out decoded, out attributeError))
            {
                error = attributeError;
                return false;
            }

            var parsed = new StunMessage(DecodeMethod(type), DecodeClass(type), transactionId);
            parsed._attributes = attributesRaw;
            parsed.ApplyDecoded(decoded);

            message = parsed;
            return true;
        }

        /// <summary>
        /// 应用解析出的属性列表，并算出未知必选属性
        /// </summary>
        /// <param name="decoded">属性列表</param>
        private void ApplyDecoded(IReadOnlyList<StunAttribute> decoded)
        {
            _decodedAttributes = decoded;

            var unknown = new List<ushort>();
            for (var i = 0; i < decoded.Count; i++)
            {
                if (decoded[i].IsComprehensionRequired && !StunAttributeCodec.IsKnownType(decoded[i].Type))
                {
                    unknown.Add(decoded[i].Type);
                }
            }
            _unknownRequiredTypes = unknown.ToArray();
        }

        /// <summary>
        /// 按大端写入 16 位整数
        /// </summary>
        /// <param name="buffer">目标缓冲区</param>
        /// <param name="offset">写入位置</param>
        /// <param name="value">待写入值</param>
        private static void WriteUInt16(byte[] buffer, int offset, ushort value)
        {
            buffer[offset] = (byte)(value >> 8);
            buffer[offset + 1] = (byte)value;
        }

        /// <summary>
        /// 按大端写入 32 位整数
        /// </summary>
        /// <param name="buffer">目标缓冲区</param>
        /// <param name="offset">写入位置</param>
        /// <param name="value">待写入值</param>
        private static void WriteUInt32(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        /// <summary>
        /// 按大端读取 16 位整数
        /// </summary>
        /// <param name="data">来源数据</param>
        /// <param name="offset">读取位置</param>
        /// <returns>读到的值</returns>
        private static ushort ReadUInt16(ReadOnlySpan<byte> data, int offset)
        {
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }

        /// <summary>
        /// 按大端读取 32 位整数
        /// </summary>
        /// <param name="data">来源数据</param>
        /// <param name="offset">读取位置</param>
        /// <returns>读到的值</returns>
        private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset)
        {
            return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | ((uint)data[offset + 2] << 8) | data[offset + 3];
        }
    }
}