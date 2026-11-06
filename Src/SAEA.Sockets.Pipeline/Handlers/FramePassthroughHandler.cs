using System;
using System.Threading.Tasks;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 原始帧字节旁路：<see cref="Retain"/> 队列条目后把同一个 <see cref="PooledMessage"/>
    /// 交给下游分支，由该分支自己的尾部处理器 <see cref="PooledMessage.Release"/> 这一份引用。
    /// <para>
    /// executor 的执行包 <c>finally</c> 释放的是队列条目那一份，与这里 Retain 出来的不是同一个引用，
    /// 两者互不替代。本处理器不得与 <see cref="CodecDecoderHandler"/> /
    /// <see cref="EnvelopeUnwrapHandler"/> 装在同一条链上——两套所有权不能并存。
    /// </para>
    /// </summary>
    public sealed class FramePassthroughHandler : InboundHandlerAdapter
    {
        /// <summary>只接受 <see cref="PooledMessage"/>；Retain 后原样下传。</summary>
        public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
        {
            var pooled = msg as PooledMessage;
            if (pooled == null)
                throw new InvalidOperationException("FramePassthroughHandler 的入参必须是 PooledMessage。");

            pooled.Retain();
            await ctx.FireChannelReadAsync(msg).ConfigureAwait(false);
        }
    }
}