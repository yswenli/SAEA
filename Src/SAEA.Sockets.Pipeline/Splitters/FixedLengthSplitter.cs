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