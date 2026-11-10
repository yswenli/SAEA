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
*修改时间：2026/11/9 11:16:22
*修改人： yswenli
*版本号： v26.11.5.1
*描述：无边界分帧器：把当前可用缓冲整体作为一帧，消息边界等于一次读取边界
*
*****************************************************************************/
using System;
using System.Buffers;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 无边界分帧器：把当前可用缓冲整体作为一帧返回，空缓冲返回 false。
    /// 用于 FTP 数据通道、Socket5 隧道这类「读多少算多少」的协议；消息边界等于一次读取边界，
    /// 使用方必须在协议文档中写明这一点。它仍然累积残帧于「一次读取」之内，
    /// 因此 <see cref="ChannelPipeline.Create"/> 的缓冲下限校验不豁免。
    /// </summary>
    public sealed class TakeAvailableSplitter : IFrameSplitter
    {
        private readonly int _maxFrameLength;

        /// <summary>
        /// 创建无边界分帧器。
        /// </summary>
        /// <param name="maxFrameLength">单次读取可接受的最大块大小，必须大于 0；超过时抛 <see cref="FrameProtocolException"/>。</param>
        public TakeAvailableSplitter(int maxFrameLength)
        {
            if (maxFrameLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxFrameLength), "maxFrameLength 必须大于 0。");

            _maxFrameLength = maxFrameLength;
        }

        /// <summary>单次读取上限，也是本分帧器的最大帧长。</summary>
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

            if (buffer.Length > _maxFrameLength)
            {
                throw new FrameProtocolException(
                    "无边界分帧的可用缓冲长度 " + buffer.Length + " 超过单次读取上限 " + _maxFrameLength + "。");
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