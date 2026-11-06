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
*文件名： FixedLengthSplitter
*版本号： v26.11.5.1
*唯一标识：8006c22a-a7e2-4822-8eb9-8b062bc6d5f8
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：定长分帧器：每帧字节数固定，缓冲不足一帧时等待继续读取
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：定长分帧器：每帧字节数固定，缓冲不足一帧时等待继续读取
*
*****************************************************************************/
using System;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 定长分帧器：每帧字节数固定，缓冲不足一帧时等待继续读取。
    /// 无状态、非线程安全，同一实例可按会话复用。
    /// </summary>
    public sealed class FixedLengthSplitter : IFrameSplitter
    {
        private readonly int _frameLength;

        /// <summary>可接受的最大帧长，等于构造时指定的固定帧长。</summary>
        public int MaxFrameLength
        {
            get { return _frameLength; }
        }

        /// <summary>
        /// 创建定长分帧器。
        /// </summary>
        /// <param name="frameLength">每帧字节数，必须大于 0。</param>
        public FixedLengthSplitter(int frameLength)
        {
            if (frameLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(frameLength), "frameLength 必须大于 0。");

            _frameLength = frameLength;
        }

        /// <inheritdoc />
        public bool TrySplit(ReadOnlyMemory<byte> buffered, out int consumed, out ReadOnlyMemory<byte> frame)
        {
            consumed = 0;
            frame = ReadOnlyMemory<byte>.Empty;

            if (buffered.Length < _frameLength) return false;

            consumed = _frameLength;
            frame = buffered.Slice(0, _frameLength);
            return true;
        }

        /// <inheritdoc />
        public void Reset()
        {
        }
    }
}