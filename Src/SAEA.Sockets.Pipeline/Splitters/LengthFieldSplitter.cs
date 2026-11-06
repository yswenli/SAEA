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
*命名空间：SAEA.Sockets.Pipeline
*文件名： LengthFieldSplitter
*版本号： v26.11.5.1
*唯一标识：e82f88f0-3a4d-442f-b167-b95929a67bbd
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：基于长度字段的分帧器：读取头部里的长度值算出整帧长度，再按配置决定剥掉多少头部字节
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:45:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：分帧输入改为 ReadOnlySequence，支持泵直接在 Pipe 缓冲上切帧与跨段帧
*
*****************************************************************************/
using System;
using System.Buffers;
using System.Buffers.Binary;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 基于长度字段的分帧器：读取头部里的长度值算出整帧长度，再按配置决定剥掉多少头部字节。
    /// 长度字段的取值口径由 <c>lengthAdjustment</c> 补齐，默认预设配合 8B body 长度 + 1B 类型的线格式。
    /// 无状态、非线程安全，同一实例可按会话复用。
    /// </summary>
    public sealed class LengthFieldSplitter : IFrameSplitter
    {
        private readonly int _maxFrameLength;
        private readonly int _lengthFieldOffset;
        private readonly int _lengthFieldLength;
        private readonly int _lengthAdjustment;
        private readonly int _initialBytesToStrip;
        private readonly bool _isLittleEndian;

        /// <summary>可接受的最大帧体长度；总帧长为头部长度加上该值。</summary>
        public int MaxFrameLength
        {
            get { return _maxFrameLength; }
        }

        /// <summary>
        /// 创建分帧器。
        /// </summary>
        /// <param name="maxFrameLength">帧体长度上限，非负；0 只允许空 body。</param>
        /// <param name="lengthFieldOffset">长度字段相对帧首的偏移，非负。</param>
        /// <param name="lengthFieldLength">长度字段字节数，取 1、2、3、4 或 8。</param>
        /// <param name="lengthAdjustment">
        /// 长度修正值，允许为负。整帧长度 = 头部长度 + 长度字段值 + 该修正值。
        /// </param>
        /// <param name="initialBytesToStrip">产出帧时从帧首剥掉的字节数，落在 0 到头部长度之间。</param>
        /// <param name="isLittleEndian">长度字段是否按小端解析。</param>
        public LengthFieldSplitter(
            int maxFrameLength,
            int lengthFieldOffset = 0,
            int lengthFieldLength = 8,
            int lengthAdjustment = 1,
            int initialBytesToStrip = 8,
            bool isLittleEndian = true)
        {
            if (maxFrameLength < 0)
                throw new ArgumentOutOfRangeException(nameof(maxFrameLength), "maxFrameLength 不能为负数。");
            if (lengthFieldOffset < 0)
                throw new ArgumentOutOfRangeException(nameof(lengthFieldOffset), "lengthFieldOffset 不能为负数。");
            if (lengthFieldLength != 1 && lengthFieldLength != 2 && lengthFieldLength != 3
                && lengthFieldLength != 4 && lengthFieldLength != 8)
                throw new ArgumentException("lengthFieldLength 只支持 1、2、3、4 或 8 字节。", nameof(lengthFieldLength));

            var head = (long)lengthFieldOffset + lengthFieldLength;
            if (head > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(lengthFieldOffset), "头部长度溢出。");
            if (initialBytesToStrip < 0 || initialBytesToStrip > head)
                throw new ArgumentOutOfRangeException(nameof(initialBytesToStrip), "initialBytesToStrip 必须落在 0 到头部长度之间。");

            _maxFrameLength = maxFrameLength;
            _lengthFieldOffset = lengthFieldOffset;
            _lengthFieldLength = lengthFieldLength;
            _lengthAdjustment = lengthAdjustment;
            _initialBytesToStrip = initialBytesToStrip;
            _isLittleEndian = isLittleEndian;
        }

        /// <summary>
        /// 默认预设：8B 小端 body 长度 + 1B 类型，剥掉 8B 长度字段，产出 <c>[1B type][body]</c>。
        /// 类型字节在解析 Payload 前即可用，是生产默认。
        /// </summary>
        /// <param name="maxFrameLength">可接受的最大帧长（帧体口径），必须大于 0。</param>
        /// <returns>生产用分帧器实例。</returns>
        public static LengthFieldSplitter Default(int maxFrameLength)
        {
            return new LengthFieldSplitter(
                maxFrameLength,
                0,
                FrameConstants.P_LEN,
                FrameConstants.P_Head - FrameConstants.P_LEN,
                FrameConstants.P_LEN,
                true);
        }

        /// <summary>
        /// 兼容预设：不剥任何头部，产出完整 <c>[8B len][1B type][body]</c>，仅用于与既有拆帧逻辑做逐字段比对。
        /// </summary>
        /// <param name="maxFrameLength">可接受的最大帧长（帧体口径），必须大于 0。</param>
        /// <returns>保留完整头部的分帧器实例。</returns>
        public static LengthFieldSplitter LegacyCompatible(int maxFrameLength)
        {
            return new LengthFieldSplitter(
                maxFrameLength,
                0,
                FrameConstants.P_LEN,
                FrameConstants.P_Head - FrameConstants.P_LEN,
                0,
                true);
        }

        /// <inheritdoc />
        public bool TrySplit(ReadOnlySequence<byte> buffer, out long consumed, out ReadOnlySequence<byte> frame)
        {
            consumed = 0;
            frame = default(ReadOnlySequence<byte>);

            var head = _lengthFieldOffset + _lengthFieldLength;
            if (buffer.Length < head) return false;

            var bodyLen = ReadLength(buffer);
            if (bodyLen < 0 || bodyLen > _maxFrameLength)
                throw new FrameProtocolException($"非法的数据帧长度: {bodyLen}");

            var total = (long)head + bodyLen + _lengthAdjustment;
            if (total < head || total > int.MaxValue)
                throw new FrameProtocolException($"帧总长度越界: {total}");
            if (buffer.Length < total) return false;

            consumed = total;
            frame = buffer.Slice(_initialBytesToStrip, total - _initialBytesToStrip);
            return true;
        }

        /// <inheritdoc />
        public void Reset()
        {
        }

        private long ReadLength(ReadOnlySequence<byte> buffer)
        {
            var field = buffer.Slice(_lengthFieldOffset, _lengthFieldLength);
            if (field.IsSingleSegment)
                return ReadLength(field.First.Span);

            // 长度字段跨段时先并入栈上小缓冲；字段最多 8 字节，不产生堆分配。
            Span<byte> scratch = stackalloc byte[8];
            field.CopyTo(scratch);
            return ReadLength(scratch.Slice(0, _lengthFieldLength));
        }

        private long ReadLength(ReadOnlySpan<byte> field)
        {
            switch (_lengthFieldLength)
            {
                case 1:
                    return field[0];
                case 2:
                    return _isLittleEndian
                        ? BinaryPrimitives.ReadUInt16LittleEndian(field)
                        : BinaryPrimitives.ReadUInt16BigEndian(field);
                case 3:
                    return _isLittleEndian
                        ? field[0] | ((long)field[1] << 8) | ((long)field[2] << 16)
                        : ((long)field[0] << 16) | ((long)field[1] << 8) | field[2];
                case 4:
                    return _isLittleEndian
                        ? BinaryPrimitives.ReadUInt32LittleEndian(field)
                        : BinaryPrimitives.ReadUInt32BigEndian(field);
                default:
                    return _isLittleEndian
                        ? BinaryPrimitives.ReadInt64LittleEndian(field)
                        : BinaryPrimitives.ReadInt64BigEndian(field);
            }
        }
    }
}