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