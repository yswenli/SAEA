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
*文件名： LineSplitter
*版本号： v26.11.5.1
*唯一标识：99ad783b-c6bc-4a41-92c2-2f904f75da3d
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：按行分帧：以 \n 为分隔符，剥掉分隔符后再去掉帧尾的 \r， 因此 \n 与 \r\n 两种换行都能得到干净的行内容
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：按行分帧：以 \n 为分隔符，剥掉分隔符后再去掉帧尾的 \r， 因此 \n 与 \r\n 两种换行都能得到干净的行内容
*
*****************************************************************************/
using System;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 按行分帧：以 <c>\n</c> 为分隔符，剥掉分隔符后再去掉帧尾的 <c>\r</c>，
    /// 因此 <c>\n</c> 与 <c>\r\n</c> 两种换行都能得到干净的行内容。空行产生空帧。
    /// 无状态、非线程安全，同一实例可按会话复用。
    /// </summary>
    public sealed class LineSplitter : IFrameSplitter
    {
        private readonly DelimiterSplitter _inner;

        /// <summary>可接受的最大帧长，按整帧长度（含换行符）解释。</summary>
        public int MaxFrameLength
        {
            get { return _inner.MaxFrameLength; }
        }

        /// <summary>
        /// 创建按行分帧器。
        /// </summary>
        /// <param name="maxFrameLength">整帧长度上限，必须大于 0。</param>
        public LineSplitter(int maxFrameLength)
        {
            _inner = new DelimiterSplitter((byte)'\n', maxFrameLength);
        }

        /// <inheritdoc />
        public bool TrySplit(ReadOnlyMemory<byte> buffered, out int consumed, out ReadOnlyMemory<byte> frame)
        {
            if (!_inner.TrySplit(buffered, out consumed, out frame)) return false;

            if (frame.Length > 0 && frame.Span[frame.Length - 1] == (byte)'\r')
                frame = frame.Slice(0, frame.Length - 1);

            return true;
        }

        /// <inheritdoc />
        public void Reset()
        {
            _inner.Reset();
        }
    }
}