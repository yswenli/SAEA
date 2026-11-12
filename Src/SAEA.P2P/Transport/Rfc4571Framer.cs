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
*命名空间：SAEA.P2P.Transport
*文件名： Rfc4571Framer
*版本号： v26.11.5.1
*唯一标识：d6d0e2e8-5a9c-4eef-a875-074d9fe63684
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 13:08:57
*描述：RFC 4571 两字节长度前缀分帧器
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 13:08:57
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 RFC 4571 两字节长度前缀分帧器
*
*****************************************************************************/
using System;
using System.IO;

namespace SAEA.P2P.Transport
{
    /// <summary>
    /// RFC 4571 分帧：在 TCP 字节流上给每帧加 2 字节大端长度前缀，零长度帧合法。
    /// 只依赖 System 命名空间，不引用 SAEA.Sockets 类型。
    /// </summary>
    public sealed class Rfc4571Framer
    {
        /// <summary>
        /// 长度前缀字节数
        /// </summary>
        public const int HeaderLength = 2;

        /// <summary>
        /// 两字节前缀能表达的最大帧长
        /// </summary>
        private const int MaxAllowedFrameLength = 65535;

        /// <summary>
        /// 首次分配时的缓冲容量，避免只有一帧就按 2 字节起分配
        /// </summary>
        private const int InitialCapacity = 256;

        private byte[] _buffer;

        /// <summary>
        /// 未消费数据在缓冲中的起始下标
        /// </summary>
        private int _start;

        /// <summary>
        /// 未消费数据在缓冲中的结束下标，不含
        /// </summary>
        private int _end;

        /// <summary>
        /// 创建分帧器
        /// </summary>
        /// <param name="maxFrameLength">允许的单帧载荷上限，取值 0 到 65535，0 表示只接受零长度帧</param>
        /// <exception cref="ArgumentOutOfRangeException">maxFrameLength 超出 0 到 65535 时抛出</exception>
        public Rfc4571Framer(int maxFrameLength = 65535)
        {
            if (maxFrameLength < 0 || maxFrameLength > MaxAllowedFrameLength)
            {
                throw new ArgumentOutOfRangeException(nameof(maxFrameLength), maxFrameLength, "单帧长度上限必须位于 0 到 65535 之间");
            }

            MaxFrameLength = maxFrameLength;
            _buffer = new byte[InitialCapacity];
        }

        /// <summary>
        /// 允许的单帧载荷上限，受两字节前缀限制不会超过 65535
        /// </summary>
        public int MaxFrameLength { get; }

        /// <summary>
        /// 已缓存但未取走的字节数，含未凑齐的前缀与未读完的帧体
        /// </summary>
        public int BufferedLength
        {
            get { return _end - _start; }
        }

        /// <summary>
        /// 追加收到的字节流片段；数据不要求对齐帧边界，半包与粘包都由内部缓冲承接
        /// </summary>
        /// <param name="data">本次读到的字节，仅在调用期间有效，内部会复制到自己的缓冲</param>
        /// <exception cref="InvalidDataException">缓冲中某个前缀声明的帧长超过 <see cref="MaxFrameLength"/> 时抛出；抛出前缓冲已复位，实例可继续使用</exception>
        public void Append(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0)
            {
                return;
            }

            EnsureCapacity(data.Length);
            data.CopyTo(_buffer.AsSpan(_end));
            _end += data.Length;

            // 一次追加可能带进多帧，逐个校验能读到前缀的帧长；超限说明流边界已丢失，不能继续按帧切分
            var probe = _start;
            while (_end - probe >= HeaderLength)
            {
                var declared = ReadFrameLength(_buffer, probe);
                if (declared > MaxFrameLength)
                {
                    // 先复位再抛，避免坏长度一直留在缓冲里导致后续每次 Append 都失败
                    Reset();
                    throw new InvalidDataException("帧长 " + declared + " 超过上限 " + MaxFrameLength);
                }

                if (_end - probe - HeaderLength < declared)
                {
                    // 帧体没到齐，前缀本身合法，等后续字节补齐
                    break;
                }

                probe += HeaderLength + declared;
            }
        }

        /// <summary>
        /// 尝试取出一帧完整载荷；不足一帧时返回 false 且不改动缓冲
        /// </summary>
        /// <param name="frame">成功时为内部缓冲的一段切片，不复制；只在<b>下一次 <see cref="Append"/>、<see cref="TryReadFrame"/> 或 <see cref="Reset"/></b> 之前有效，跨调用保存或异步投递必须先复制</param>
        /// <returns>缓冲中存在完整帧时返回 true，零长度帧也返回 true</returns>
        public bool TryReadFrame(out ReadOnlyMemory<byte> frame)
        {
            var buffered = BufferedLength;
            if (buffered < HeaderLength)
            {
                frame = default(ReadOnlyMemory<byte>);
                return false;
            }

            var declared = ReadFrameLength(_buffer, _start);
            if (buffered - HeaderLength < declared)
            {
                // 帧体未收齐，保留缓冲与读偏移，等下次 Append 续读
                frame = default(ReadOnlyMemory<byte>);
                return false;
            }

            frame = new ReadOnlyMemory<byte>(_buffer, _start + HeaderLength, declared);
            _start += HeaderLength + declared;
            if (_start == _end)
            {
                // 排空后把偏移归零，下一帧重新从缓冲头部写入，避免偏移无限右移
                _start = 0;
                _end = 0;
            }

            return true;
        }

        /// <summary>
        /// 清空缓冲并复位读偏移，用于丢弃一条已不可信的流
        /// </summary>
        public void Reset()
        {
            _start = 0;
            _end = 0;
        }

        /// <summary>
        /// 把载荷写成 RFC 4571 帧放进调用方提供的目标缓冲，不产生额外分配
        /// </summary>
        /// <param name="payload">帧体</param>
        /// <param name="destination">目标缓冲，长度至少为 payload.Length + 2</param>
        /// <returns>写入的总字节数，即 payload.Length + 2</returns>
        /// <exception cref="ArgumentException">载荷超过 65535 字节或目标缓冲不足时抛出</exception>
        public static int WriteFrame(ReadOnlySpan<byte> payload, Span<byte> destination)
        {
            if (payload.Length > MaxAllowedFrameLength)
            {
                throw new ArgumentException("载荷长度 " + payload.Length + " 超过 RFC 4571 上限 " + MaxAllowedFrameLength, nameof(payload));
            }

            var total = payload.Length + HeaderLength;
            if (destination.Length < total)
            {
                throw new ArgumentException("目标缓冲不足，需要 " + total + " 字节，实际 " + destination.Length + " 字节", nameof(destination));
            }

            // 前缀为大端序，两字节最多表达 65535
            destination[0] = (byte)(payload.Length >> 8);
            destination[1] = (byte)payload.Length;
            payload.CopyTo(destination.Slice(HeaderLength));
            return total;
        }

        /// <summary>
        /// 分配新数组并按 RFC 4571 写入一帧；热路径应优先用 <see cref="WriteFrame"/> 配池化缓冲，避免额外分配
        /// </summary>
        /// <param name="payload">帧体</param>
        /// <returns>新数组，前两字节为大端长度前缀，其后为载荷副本</returns>
        /// <exception cref="ArgumentException">载荷超过 65535 字节时抛出</exception>
        public static byte[] Encode(ReadOnlySpan<byte> payload)
        {
            var frame = new byte[payload.Length + HeaderLength];
            WriteFrame(payload, frame);
            return frame;
        }

        /// <summary>
        /// 读出两字节大端前缀
        /// </summary>
        /// <param name="buffer">内部缓冲</param>
        /// <param name="offset">前缀起始下标</param>
        /// <returns>声明的帧体长度</returns>
        private static int ReadFrameLength(byte[] buffer, int offset)
        {
            return (buffer[offset] << 8) | buffer[offset + 1];
        }

        /// <summary>
        /// 保证缓冲尾部至少还能写入 extra 字节，空间不够时先压缩再按需扩容
        /// </summary>
        /// <param name="extra">本次要追加的字节数</param>
        private void EnsureCapacity(int extra)
        {
            if (_buffer.Length - _end >= extra)
            {
                return;
            }

            var live = _end - _start;
            if (_start > 0 && _buffer.Length - live >= extra)
            {
                // 已消费的前缀把空间空出来了，前移未消费字节即可，不必扩容
                Buffer.BlockCopy(_buffer, _start, _buffer, 0, live);
                _start = 0;
                _end = live;
                return;
            }

            var required = live + extra;
            var capacity = _buffer.Length;
            while (capacity < required)
            {
                var doubled = capacity * 2;
                // 翻倍溢出时直接退到实际所需容量
                capacity = doubled < capacity ? required : doubled;
            }

            var resized = new byte[capacity];
            if (live > 0)
            {
                Buffer.BlockCopy(_buffer, _start, resized, 0, live);
            }

            _buffer = resized;
            _start = 0;
            _end = live;
        }
    }
}