using System.Threading.Tasks;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 出站处理器基类，默认把消息原样交给下一出站处理器（向链首方向）。
    /// </summary>
    public abstract class OutboundHandlerAdapter : ChannelHandlerAdapter, IChannelOutboundHandler
    {
        /// <summary>默认透传；出站异常必须原样冒泡，不得记录后返回成功。</summary>
        public virtual Task WriteAsync(IChannelHandlerContext ctx, object msg)
        {
            return ctx.WriteAsync(msg);
        }
    }
}