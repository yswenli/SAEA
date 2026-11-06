using System;
using System.Threading.Tasks;
using SAEA.Common.Caching;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 出站最后一站：把 body 与类型码写成完整帧，并把最终缓冲的所有权包进 <see cref="TransportFrame"/>。
    /// 成功返回即表示所有权已交给链头/传输层；失败时由本处理器归还。
    /// </summary>
    public sealed class FrameEncoderHandler : OutboundHandlerAdapter
    {
        private readonly IFrameEncoder _encoder;

        /// <summary>以出站分帧器创建帧编码处理器。</summary>
        public FrameEncoderHandler(IFrameEncoder encoder)
        {
            if (encoder == null)
                throw new ArgumentNullException(nameof(encoder));

            _encoder = encoder;
        }

        /// <summary>只接受 <see cref="EncodedBody"/>；补完包头后只向链头交 <see cref="TransportFrame"/>。</summary>
        public override async Task WriteAsync(IChannelHandlerContext ctx, object msg)
        {
            var encoded = msg as EncodedBody;
            if (encoded == null)
                throw new InvalidOperationException("FrameEncoderHandler 之前必须经过 CodecEncoderHandler。");

            var writer = new PooledBufferWriter(encoded.Body.Length + FrameConstants.P_Head);
            TransportFrame frame = null;
            try
            {
                _encoder.Encode(encoded.Body, encoded.TypeCode, writer);
                frame = new TransportFrame(writer.WrittenMemory, writer);
                await ctx.WriteAsync(frame).ConfigureAwait(false);
            }
            catch
            {
                // 正常返回表示所有权已经交出，只在这里归还尚未交付的缓冲。
                if (frame != null)
                    frame.Dispose();
                else
                    writer.Dispose();
                throw;
            }
        }
    }
}