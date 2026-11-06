using System;
using System.Threading.Tasks;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 入站第一站：把队列条目持有的帧字节交给 <see cref="ICodec"/> 解成信封。
    /// 帧字节在解码完成后立即归还，<see cref="Envelope.Payload"/> 已是独立拷贝。
    /// </summary>
    public sealed class CodecDecoderHandler : InboundHandlerAdapter
    {
        private readonly ICodec _codec;

        /// <summary>以第一层编解码器创建入站解码处理器。</summary>
        public CodecDecoderHandler(ICodec codec)
        {
            if (codec == null)
                throw new ArgumentNullException(nameof(codec));

            _codec = codec;
        }

        /// <summary>只接受 <see cref="PooledMessage"/>；解码完成后归还帧字节，再下传解出的信封。</summary>
        public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
        {
            var pooled = msg as PooledMessage;
            if (pooled == null)
                throw new InvalidOperationException("CodecDecoderHandler 的入参必须是 PooledMessage。");

            // 解码失败时不在本 handler 归还，交给执行包的 finally 统一释放，避免异常路径提前归还。
            var decoded = _codec.Decode(pooled.Frame);

            pooled.Release();
            await ctx.FireChannelReadAsync(decoded).ConfigureAwait(false);
        }
    }
}