/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.Sockets.Pipeline
*文件名： OutboundLoggingHandler
*版本号： v26.11.5.1
*唯一标识：e368eb70-4013-4f61-98fe-80364c9efdc8
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：出站日志：位于 FrameEncoderHandler 的 tail 一侧， 记录本次出站在本节点下游的耗时与已编码 body 的类型码、长度
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：出站日志：位于 FrameEncoderHandler 的 tail 一侧， 记录本次出站在本节点下游的耗时与已编码 body 的类型码、长度
*
*****************************************************************************/
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
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="msg">待记录的出站消息，可为 null。</param>
        /// <returns>链首接收完成后结束的任务。</returns>
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