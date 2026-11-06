using System;
using System.Threading.Tasks;
using SAEA.Common.Caching;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 出站编码：把信封的 payload 写进池化缓冲，产出只含 body 的中间体。
    /// 中间体在本次出站传递返回（或抛异常）后立即释放，下游只能同步读取 body。
    /// </summary>
    public sealed class CodecEncoderHandler : OutboundHandlerAdapter
    {
        private readonly ICodec _codec;

        /// <summary>以第一层编解码器创建出站编码处理器。</summary>
        public CodecEncoderHandler(ICodec codec)
        {
            if (codec == null)
                throw new ArgumentNullException(nameof(codec));

            _codec = codec;
        }

        /// <summary>只接受 <see cref="Envelope"/>；编码结果交给下游补帧头。</summary>
        public override async Task WriteAsync(IChannelHandlerContext ctx, object msg)
        {
            var envelope = msg as Envelope;
            if (envelope == null)
                throw new InvalidOperationException("CodecEncoderHandler 只接受 Envelope。");

            var writer = new PooledBufferWriter(Math.Max(256, envelope.Payload == null ? 0 : envelope.Payload.Length));
            EncodedBody encoded = null;
            try
            {
                _codec.Encode(envelope, writer);
                encoded = new EncodedBody(envelope.Type, writer.WrittenMemory, writer);
                await ctx.WriteAsync(encoded).ConfigureAwait(false);
            }
            finally
            {
                // 编码器抛异常时 encoded 仍为 null，此时由这里归还刚借出的缓冲。
                if (encoded != null)
                    encoded.Dispose();
                else
                    writer.Dispose();
            }
        }
    }
}