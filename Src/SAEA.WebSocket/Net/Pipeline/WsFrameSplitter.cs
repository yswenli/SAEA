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
*命名空间：SAEA.WebSocket
*文件名： WsFrameSplitter
*版本号： v26.11.5.1
*唯一标识：78217f43-b469-46c0-b995-5aabcf88f9ca
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 13:30:00
*描述：WebSocket 帧阶段分帧器，按 RFC 6455 帧头切出完整 wire frame
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 13:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增，按帧头长度切分 WebSocket wire frame
*
*****************************************************************************/
using System;
using System.Buffers;
using SAEA.Sockets.Pipeline;

namespace SAEA.WebSocket
{
    /// <summary>
    /// WebSocket 帧阶段分帧器：按 RFC 6455 帧头里的长度码切出完整 wire frame。
    /// <para>
    /// 产出的是<b>完整帧</b>（帧头 + 帧体），帧头解析、掩码还原与分片重组都由解码器负责，
    /// 本类只判断「这一帧的字节是否到齐」，不解释 opcode 语义。
    /// </para>
    /// <para>
    /// 单帧帧体超过 <see cref="MaxFrameLength"/> 直接按协议错误抛出，由链尾收口；
    /// 分片重组后的消息总长由解码器按 <c>MaxMessageSize</c> 校验。
    /// 实例只持有上限配置，无跨调用状态，每连接新建一个。
    /// </para>
    /// </summary>
    public sealed class WsFrameSplitter : IFrameSplitter
    {
        /// <summary>帧头最大字节数：2 字节基础头 + 8 字节扩展长度 + 4 字节掩码。</summary>
        private const int MaxHeaderBytes = 14;

        private readonly int _maxFrameLength;

        /// <summary>
        /// 创建帧阶段分帧器。
        /// </summary>
        /// <param name="maxFrameLength">单帧帧体上限（字节），非负；0 只允许空帧体</param>
        public WsFrameSplitter(int maxFrameLength)
        {
            if (maxFrameLength < 0)
                throw new ArgumentOutOfRangeException(nameof(maxFrameLength), "maxFrameLength 不能为负数。");

            _maxFrameLength = maxFrameLength;
        }

        /// <summary>可接受的单帧帧体长度上限；帧头不计入。</summary>
        public int MaxFrameLength
        {
            get { return _maxFrameLength; }
        }

        /// <inheritdoc />
        public bool TrySplit(ReadOnlySequence<byte> buffer, out long consumed, out ReadOnlySequence<byte> frame)
        {
            consumed = 0;
            frame = default(ReadOnlySequence<byte>);

            if (buffer.Length < 2)
                return false;

            // 帧头最多 14 字节：先并进栈上小缓冲，跨段时也不用分配，且不依赖 buffer.First 的长度。
            var probe = (int)Math.Min(buffer.Length, MaxHeaderBytes);
            Span<byte> head = stackalloc byte[MaxHeaderBytes];
            buffer.Slice(0, probe).CopyTo(head);

            var b1 = head[1];
            var len7 = b1 & 0x7f;
            var masked = (b1 & 0x80) != 0;
            var extLen = len7 == 126 ? 2 : (len7 == 127 ? 8 : 0);
            var headerLen = 2 + extLen + (masked ? 4 : 0);

            if (buffer.Length < headerLen)
                return false;

            // probe 至少等于 headerLen：上面已保证 buffer.Length >= headerLen，probe 取两者较小值。
            long payloadLength;
            if (len7 < 126)
            {
                payloadLength = len7;
            }
            else if (len7 == 126)
            {
                payloadLength = (head[2] << 8) | head[3];
            }
            else
            {
                // 最高位为 1 表示长度超过 long 可表示范围，RFC 6455 禁止使用。
                if ((head[2] & 0x80) != 0)
                    throw new FrameProtocolException(WebSocketProtocolErrors.InvalidLength);

                ulong value = 0;
                for (var i = 0; i < 8; i++)
                    value = (value << 8) | head[2 + i];

                if (value > (ulong)int.MaxValue)
                    throw new FrameProtocolException(WebSocketProtocolErrors.InvalidLength);

                payloadLength = (long)value;
            }

            if (payloadLength > _maxFrameLength)
                throw new FrameProtocolException(WebSocketProtocolErrors.MessageTooLarge);

            var total = headerLen + payloadLength;
            if (total > int.MaxValue)
                throw new FrameProtocolException(WebSocketProtocolErrors.InvalidLength);

            if (buffer.Length < total)
                return false;

            consumed = total;
            frame = buffer.Slice(0, total);
            return true;
        }

        /// <inheritdoc />
        public void Reset()
        {
            // 半帧留在接收泵的 Pipe 里，本实例不保存跨调用状态，空实现且可重复调用。
        }
    }
}