using System.Diagnostics;
using System.Threading.Tasks;
using SAEA.Common;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 入站日志：记录本次消息在本节点下游的执行耗时。
    /// <para>
    /// 计时口径是从调用下游到下游返回（含 <c>await</c> 期间）。本处理器不改变消息对象、
    /// 不吞异常；下游异常仍按链契约经 <c>ExceptionCaught</c> 流向链尾。
    /// </para>
    /// </summary>
    public sealed class InboundLoggingHandler : InboundHandlerAdapter
    {
        /// <summary>记录连接标识、消息 CLR 类型名与下游耗时后原样下传。</summary>
        public override async Task ChannelReadAsync(IChannelHandlerContext ctx, object msg)
        {
            var watch = Stopwatch.StartNew();
            try
            {
                await ctx.FireChannelReadAsync(msg).ConfigureAwait(false);
            }
            finally
            {
                watch.Stop();
                LogHelper.Debug(
                    "入站消息:" + ctx.ChannelId + "," +
                    (msg == null ? "null" : msg.GetType().Name) + "," +
                    watch.ElapsedMilliseconds + "ms");
            }
        }
    }
}