using System;
using System.Threading.Tasks;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 入站处理器基类，默认把消息与异常原样传给下游。
    /// 子类只重写需要介入的方法；若某个方法不应透传，必须显式重写并写明语义。
    /// </summary>
    public abstract class InboundHandlerAdapter : ChannelHandlerAdapter, IChannelInboundHandler
    {
        /// <summary>默认透传到下一入站处理器。</summary>
        public virtual Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
        {
            return ctx.FireChannelReadAsync(msg);
        }

        /// <summary>默认不做事；该通知由泵按链顺序逐个调用，不依赖处理器转发。</summary>
        public virtual Task ChannelReadCompleteAsync(IChannelHandlerContext ctx)
        {
            return Task.CompletedTask;
        }

        /// <summary>默认继续向链尾传播。</summary>
        public virtual Task ChannelExceptionCaughtAsync(IChannelHandlerContext ctx, Exception ex)
        {
            return ctx.FireExceptionCaughtAsync(ex);
        }
    }
}