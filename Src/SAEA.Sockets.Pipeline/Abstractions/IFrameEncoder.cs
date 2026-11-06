using System;
using System.Buffers;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 出站分帧：把 body 写成带包头的完整帧，与 <see cref="IFrameSplitter"/> 互逆。
    /// </summary>
    public interface IFrameEncoder
    {
        /// <summary>
        /// 把 body 写成完整帧。只写 body 与协议头，不做序列化。
        /// <paramref name="typeCode"/> 必须落在 0..255，越界抛
        /// <see cref="ArgumentOutOfRangeException"/>，不得静默截断。
        /// </summary>
        void Encode(ReadOnlyMemory<byte> body, int typeCode, IBufferWriter<byte> writer);
    }
}