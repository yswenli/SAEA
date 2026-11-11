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
*文件名： TakeAvailableSplitter
*版本号： v26.11.5.1
*唯一标识：98efa7d9-e867-4787-b70f-bdd00b84175a
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/9 11:16:22
*描述：无边界分帧器：把当前可用缓冲整体作为一帧，消息边界等于一次读取边界
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 18:26:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：无边界分帧器超过单帧上限时按上限切块，不再抛 FrameProtocolException
*
*****************************************************************************/
using System;
using System.Buffers;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 无边界分帧器：把当前可用缓冲整体作为一帧返回，空缓冲返回 false；
    /// 可用缓冲超过单帧上限时按上限切块，剩余部分留给后续调用继续切。
    /// 用于 FTP 数据通道、Socket5 隧道这类「读多少算多少」的协议；消息边界通常等于一次读取边界，
    /// 只有在一次读取交出多段合并缓冲时才按上限切成多帧，使用方必须在协议文档中写明这一点。
    /// 它仍然在同一帧内消费「一次读取」的字节，因此 <see cref="ChannelPipeline.Create"/> 的缓冲下限校验不豁免。
    /// </summary>
    public sealed class TakeAvailableSplitter : IFrameSplitter
    {
        private readonly int _maxFrameLength;

        /// <summary>
        /// 创建无边界分帧器。
        /// </summary>
        /// <param name="maxFrameLength">单帧硬上界，必须大于 0；可用缓冲超过该值时按该长度切块，不再抛异常。</param>
        public TakeAvailableSplitter(int maxFrameLength)
        {
            if (maxFrameLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxFrameLength), "maxFrameLength 必须大于 0。");

            _maxFrameLength = maxFrameLength;
        }

        /// <summary>单帧硬上界：单次产出的帧长度不会超过该值；缓冲超出时按此切块。</summary>
        public int MaxFrameLength
        {
            get { return _maxFrameLength; }
        }

        /// <inheritdoc />
        public bool TrySplit(ReadOnlySequence<byte> buffer, out long consumed, out ReadOnlySequence<byte> frame)
        {
            consumed = 0;
            frame = default(ReadOnlySequence<byte>);

            if (buffer.IsEmpty)
                return false;

            // 泵落后于下游时 Pipe 会把多次读取合并成一段交出，一次 ReadResult 可能远大于单帧上限；
            // 这里按上限切块，保证帧大小有界且不丢字节，剩余字节由下一次 TrySplit 继续切。
            if (buffer.Length > _maxFrameLength)
            {
                consumed = _maxFrameLength;
                frame = buffer.Slice(0, _maxFrameLength);
                return true;
            }

            consumed = buffer.Length;
            frame = buffer;
            return true;
        }

        /// <inheritdoc />
        public void Reset()
        {
        }
    }
}