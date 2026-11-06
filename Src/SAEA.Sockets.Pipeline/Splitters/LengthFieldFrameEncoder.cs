using System;
using System.Buffers;
using System.Buffers.Binary;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 基于长度字段的出站分帧器：写入 8B 小端 body 长度、1B 类型码，再写入 body。
    /// 与 <see cref="LengthFieldSplitter.LegacyCompatible"/> 描述的线格式互逆。
    /// 无状态、非线程安全。
    /// </summary>
    public sealed class LengthFieldFrameEncoder : IFrameEncoder
    {
        /// <inheritdoc />
        public void Encode(ReadOnlyMemory<byte> body, int typeCode, IBufferWriter<byte> writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (typeCode < 0 || typeCode > 255)
                throw new ArgumentOutOfRangeException(nameof(typeCode), "type code 必须落在 0..255。");

            var head = writer.GetSpan(FrameConstants.P_Head);
            BinaryPrimitives.WriteInt64LittleEndian(head, body.Length);
            head[FrameConstants.P_LEN] = (byte)typeCode;
            writer.Advance(FrameConstants.P_Head);
            writer.Write(body.Span);
        }
    }
}