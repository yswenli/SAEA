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
*命名空间：SAEA.P2P.Turn
*文件名： TurnStreamFramer
*版本号： v26.11.5.1
*唯一标识：33b00df6-2831-47f1-9569-0f3c15c84195
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 01:30:00
*描述：TURN over TCP 的流式解复用与自定界
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 01:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TURN over TCP 的流式解复用与自定界
*
*****************************************************************************/
using System;

using SAEA.P2P.Common;
using SAEA.P2P.Stun;

namespace SAEA.P2P.Turn
{
    /// <summary>
    /// TURN over TCP/TLS 的流式分帧器：不加长度前缀，STUN 报文按自身 20 字节头与 length 自定界，
    /// ChannelData 按 4 字节头自定界；首字节 0x00 到 0x3F 为 STUN，0x40 到 0x4F 为 ChannelData，其余丢一个字节后继续扫描。
    /// 解析状态保留在实例内，因此半包不产出、粘包逐条产出，调用方不能按一次读取的边界解析。
    /// </summary>
    public sealed class TurnStreamFramer
    {
        /// <summary>
        /// 单帧长度上限；UDP 中继最大载荷 65507 加头部也小于该值，超过即视为非法数据
        /// </summary>
        private const int MaxFrameLength = 65536;

        /// <summary>
        /// 初始缓冲长度，按常见 STUN 报文大小预留，避免小报文频繁扩容
        /// </summary>
        private const int InitialBufferSize = 2048;

        /// <summary>
        /// 尚未被取走的字节
        /// </summary>
        private byte[] _buffer = new byte[InitialBufferSize];

        /// <summary>
        /// 缓冲内有效字节数，位于缓冲起始处
        /// </summary>
        private int _count;

        /// <summary>
        /// 缓冲中尚未成帧的字节数，用于诊断半包状态
        /// </summary>
        public int BufferedCount
        {
            get { return _count; }
        }

        /// <summary>
        /// 追加一段刚读到的字节；数据可以只是半条报文，也可以是任意条报文
        /// </summary>
        /// <param name="data">本次读到的字节</param>
        public void Feed(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0)
            {
                return;
            }

            EnsureCapacity(_count + data.Length);
            data.CopyTo(new Span<byte>(_buffer, _count, data.Length));
            _count += data.Length;
        }

        /// <summary>
        /// 尝试取出一条完整帧；缓冲不足一条完整帧时返回 false 且不消费任何字节
        /// </summary>
        /// <param name="frame">取出的帧；没有完整帧时为 null</param>
        /// <returns>取到完整帧返回 true</returns>
        public bool TryReadFrame(out TurnStreamFrame frame)
        {
            frame = null;
            while (_count > 0)
            {
                var span = new ReadOnlySpan<byte>(_buffer, 0, _count);
                var first = span[0];

                FrameReadResult result;
                if (first <= 0x3F)
                {
                    result = TryReadStunFrame(span, out frame);
                }
                else if (first <= 0x4F)
                {
                    result = TryReadChannelDataFrame(span, out frame);
                }
                else
                {
                    // 0x50 到 0xFF 不属于 STUN 与 ChannelData，属于 RFC 7983 的复用区间，丢一个字节重新同步
                    P2PLogHelper.Debug("TurnStreamFramer", "首字节 " + first + " 非法，丢弃一个字节重新同步");
                    Consume(1);
                    result = FrameReadResult.Resynced;
                }

                if (result == FrameReadResult.Taken)
                {
                    return true;
                }

                // 半包时不能继续扫描，否则会在原位置反复尝试
                if (result == FrameReadResult.Incomplete)
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// 按 STUN 头部长度字段取一条报文；长度字段非法时丢弃一个字节
        /// </summary>
        /// <param name="span">当前缓冲视图</param>
        /// <param name="frame">取出的帧</param>
        /// <returns>取到帧、仍需等待或已重新同步</returns>
        private FrameReadResult TryReadStunFrame(ReadOnlySpan<byte> span, out TurnStreamFrame frame)
        {
            frame = null;
            if (span.Length < StunConstants.HeaderLength)
            {
                return FrameReadResult.Incomplete;
            }

            var length = (span[2] << 8) | span[3];
            if ((length & 3) != 0)
            {
                // STUN 属性区必须是 4 字节的倍数，长度错位说明已无法按头部定界
                P2PLogHelper.Debug("TurnStreamFramer", "STUN 长度 " + length + " 不是 4 的倍数，丢弃一个字节重新同步");
                Consume(1);
                return FrameReadResult.Resynced;
            }

            var total = StunConstants.HeaderLength + length;
            if (total > MaxFrameLength)
            {
                P2PLogHelper.Debug("TurnStreamFramer", "STUN 帧长度 " + total + " 超过上限，丢弃一个字节重新同步");
                Consume(1);
                return FrameReadResult.Resynced;
            }

            if (span.Length < total)
            {
                return FrameReadResult.Incomplete;
            }

            frame = new TurnStreamFrame(TurnStreamFrameKind.Stun, Copy(0, total));
            Consume(total);
            return FrameReadResult.Taken;
        }

        /// <summary>
        /// 按 ChannelData 头部取一条报文；头部非法时丢弃一个字节
        /// </summary>
        /// <param name="span">当前缓冲视图</param>
        /// <param name="frame">取出的帧</param>
        /// <returns>取到帧、仍需等待或已重新同步</returns>
        private FrameReadResult TryReadChannelDataFrame(ReadOnlySpan<byte> span, out TurnStreamFrame frame)
        {
            frame = null;
            if (span.Length < ChannelData.HeaderLength)
            {
                return FrameReadResult.Incomplete;
            }

            var channel = (ushort)((span[0] << 8) | span[1]);
            if (!ChannelData.IsValidChannelNumber(channel))
            {
                P2PLogHelper.Debug("TurnStreamFramer", "通道号 " + channel + " 不在允许区间，丢弃一个字节重新同步");
                Consume(1);
                return FrameReadResult.Resynced;
            }

            // 长度字段只计应用数据，补齐字节要额外计入传输长度
            var length = (span[2] << 8) | span[3];
            var required = ChannelData.HeaderLength + length;
            if (required > MaxFrameLength)
            {
                P2PLogHelper.Debug("TurnStreamFramer", "ChannelData 帧长度 " + required + " 超过上限，丢弃一个字节重新同步");
                Consume(1);
                return FrameReadResult.Resynced;
            }

            if (span.Length < required)
            {
                return FrameReadResult.Incomplete;
            }

            var take = (required + 3) & ~3;
            if (span.Length < take)
            {
                // 发送方未补齐：只消费声明长度；已到达的零字节按补齐吞掉，非零视为下一帧开头
                take = required;
                while (take < _count && _buffer[take] == 0)
                {
                    take++;
                }
            }

            frame = new TurnStreamFrame(TurnStreamFrameKind.ChannelData, Copy(0, take));
            Consume(take);
            return FrameReadResult.Taken;
        }

        /// <summary>
        /// 复制缓冲区前段字节
        /// </summary>
        /// <param name="offset">起始偏移</param>
        /// <param name="count">复制长度</param>
        /// <returns>字节副本</returns>
        private byte[] Copy(int offset, int count)
        {
            var result = new byte[count];
            Buffer.BlockCopy(_buffer, offset, result, 0, count);
            return result;
        }

        /// <summary>
        /// 消费缓冲区前段字节并把剩余字节前移
        /// </summary>
        /// <param name="count">消费字节数</param>
        private void Consume(int count)
        {
            if (count >= _count)
            {
                _count = 0;
                return;
            }

            Buffer.BlockCopy(_buffer, count, _buffer, 0, _count - count);
            _count -= count;
        }

        /// <summary>
        /// 保证缓冲容量足够；不足时按 2 倍扩容
        /// </summary>
        /// <param name="required">需要的容量</param>
        private void EnsureCapacity(int required)
        {
            if (_buffer.Length >= required)
            {
                return;
            }

            var size = _buffer.Length;
            while (size < required)
            {
                size *= 2;
            }

            var grown = new byte[size];
            Buffer.BlockCopy(_buffer, 0, grown, 0, _count);
            _buffer = grown;
        }

        /// <summary>
        /// 单步解析结果：决定是继续扫描、等待更多字节还是直接产出
        /// </summary>
        private enum FrameReadResult
        {
            /// <summary>
            /// 缓冲不足一条完整帧
            /// </summary>
            Incomplete = 0,

            /// <summary>
            /// 已取出一条完整帧
            /// </summary>
            Taken = 1,

            /// <summary>
            /// 已丢弃非法前导字节，需要继续扫描
            /// </summary>
            Resynced = 2
        }
    }
}