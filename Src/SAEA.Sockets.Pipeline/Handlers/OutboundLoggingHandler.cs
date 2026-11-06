using System.Diagnostics;
using System.Threading.Tasks;
using SAEA.Common;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 出站日志：位于 <see cref="FrameEncoderHandler"/> 的 tail 一侧，
    /// 记录本次出站在本节点下游的耗时与已编码 body 的类型码、长度。
    /// <para>
    /// 本处理器不改变消息对象、不吞异常；出站失败必须原样冒泡给发起方。
    /// 出站方向先于帧编码器执行，因此看不到最终帧字节；需要抓包级原始字节时
    /// 使用 <see cref="FramePassthroughHandler"/> 分支。
    /// </para>
    /// </summary>
    public sealed class OutboundLoggingHandler : OutboundHandlerAdapter
    {
        /// <summary>记录连接标识、类型码/body 长度与下游耗时后原样向链首传递。</summary>
        public override async Task WriteAsync(IChannelHandlerContext ctx, object msg)
        {
            var watch = Stopwatch.StartNew();
            try
            {
                await ctx.WriteAsync(msg).ConfigureAwait(false);
            }
            finally
            {
                watch.Stop();

                var encoded = msg as EncodedBody;
                if (encoded != null)
                {
                    LogHelper.Debug(
                        "出站消息:" + ctx.ChannelId + "," + encoded.TypeCode + "," +
                        encoded.Body.Length + "," + watch.ElapsedMilliseconds + "ms");
                }
                else
                {
                    LogHelper.Debug(
                        "出站消息:" + ctx.ChannelId + "," +
                        (msg == null ? "null" : msg.GetType().Name) + "," +
                        watch.ElapsedMilliseconds + "ms");
                }
            }
        }
    }
}