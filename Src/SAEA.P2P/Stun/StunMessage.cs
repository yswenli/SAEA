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
*修改时间：2026/11/11 15:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 STUN 消息头编解码
*
*****************************************************************************/
using System;
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

            _attributes = attributes.ToArray();
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

            return (ushort)(((m & 0x0F80) << 2) | ((c & 0x2) << 7) | ((m & 0x0070) << 1) | (c & 0x1) | (m & 0x000F));
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
            Buffer.BlockCopy(data.Slice(8, StunConstants.TransactionIdLength).ToArray(), 0, transactionId, 0, StunConstants.TransactionIdLength);

            var parsed = new StunMessage(DecodeMethod(type), DecodeClass(type), transactionId);
            if (length > 0)
            {
                parsed._attributes = data.Slice(StunConstants.HeaderLength, length).ToArray();
            }

            message = parsed;
            return true;
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