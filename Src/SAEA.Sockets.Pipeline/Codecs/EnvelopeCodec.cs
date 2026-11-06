using System;
using System.Buffers;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// v1 生产默认 codec：把 <c>[1B type][payload]</c> 帧切成信封，不做任何序列化。
    /// 解码出的 payload 是独立数组，调用方可以安全地释放原帧背衬。
    /// </summary>
    public sealed class EnvelopeCodec : ICodec
    {
        /// <summary>切出首字节作为类型码，其余字节精确拷贝为 payload。</summary>
        public object Decode(ReadOnlyMemory<byte> frame)
        {
            if (frame.Length < FrameConstants.P_Type)
                throw new FrameProtocolException("帧长度不足以读取 type 字节。");

            var payload = new byte[frame.Length - FrameConstants.P_Type];
            frame.Slice(FrameConstants.P_Type).Span.CopyTo(payload);
            return new Envelope { Type = frame.Span[0], Payload = payload };
        }

        /// <summary>只写 body，不写包头；类型码由帧编码器负责写入。</summary>
        public void Encode(object message, IBufferWriter<byte> writer)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            var envelope = message as Envelope;
            if (envelope == null)
                throw new InvalidOperationException("EnvelopeCodec 只接受 Envelope。");
            if (envelope.Payload != null)
                writer.Write(envelope.Payload);
        }
    }
}