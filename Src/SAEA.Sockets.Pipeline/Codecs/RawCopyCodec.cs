using System;
using System.Buffers;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 调试与基准用 codec：把帧拷贝成信封，不做任何解析。
    /// 与零拷贝透传不同，它返回独立数组，不与池化帧体共享所有权。
    /// </summary>
    public sealed class RawCopyCodec : ICodec
    {
        /// <summary>首字节仍作为类型码，其余字节精确拷贝为 payload。</summary>
        public object Decode(ReadOnlyMemory<byte> frame)
        {
            if (frame.Length < FrameConstants.P_Type)
                throw new FrameProtocolException("帧长度不足以读取 type 字节。");

            var payload = new byte[frame.Length - FrameConstants.P_Type];
            frame.Slice(FrameConstants.P_Type).Span.CopyTo(payload);
            return new Envelope { Type = frame.Span[0], Payload = payload };
        }

        /// <summary>只写 body，不写包头。</summary>
        public void Encode(object message, IBufferWriter<byte> writer)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            var envelope = message as Envelope;
            if (envelope == null)
                throw new InvalidOperationException("RawCopyCodec 只接受 Envelope。");
            if (envelope.Payload != null)
                writer.Write(envelope.Payload);
        }
    }
}